using System.ComponentModel;
using System.Text.Json;
using RoslynLens.Responses;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModelContextProtocol.Server;

namespace RoslynLens;

[McpServerToolType]
public static class GetComplexityMetricsTool
{
    [McpServerTool(Name = "get_complexity_metrics")]
    [Description("Compute cyclomatic complexity, cognitive complexity, nesting depth, and logical LOC for methods. Can target a single method or scan a type/project for hotspots.")]
    public static async Task<string> ExecuteAsync(
        WorkspaceManager workspace,
        [Description("Method or type name to analyze")] string name,
        [Description("Scope: 'method' (single method), 'type' (all methods in a type), or 'project' (all methods in a project). Default 'method'")] string scope = "method",
        [Description("Containing class name (for method scope disambiguation)")] string? className = null,
        [Description("Minimum cyclomatic complexity threshold to include in results (default 0 = all)")] int threshold = 0,
        [Description("Maximum number of results (default 50)")] int maxResults = 50,
        CancellationToken ct = default)
    {
        var status = workspace.EnsureReadyOrStatus(ct);
        if (status is not null) return status;

        return scope.ToLowerInvariant() switch
        {
            "type" => await AnalyzeTypeAsync(workspace, name, threshold, maxResults, ct),
            "project" => await AnalyzeProjectAsync(workspace, name, threshold, maxResults, ct),
            _ => await AnalyzeMethodAsync(workspace, name, className, ct)
        };
    }

    private static async Task<string> AnalyzeMethodAsync(
        WorkspaceManager workspace, string name, string? className, CancellationToken ct)
    {
        var (_, body, methodSyntax) = await SymbolResolver.ResolveMethodBodyAsync(workspace, name, className, ct);
        if (body is null || methodSyntax is null)
            return JsonSerializer.Serialize(new { error = $"Method '{name}' not found or has no body" });

        var metrics = ComplexityAnalyzer.Analyze(body);
        var location = SymbolResolver.GetLocation(
            (await SymbolResolver.ResolveMethodByNameAsync(workspace, name, className, ct))!);

        var entry = new MethodComplexity(name, className, location.FilePath, location.Line, metrics);
        return JsonSerializer.Serialize(new ComplexityResult([entry], 1));
    }

    private static async Task<string> AnalyzeTypeAsync(
        WorkspaceManager workspace, string typeName, int threshold, int maxResults, CancellationToken ct)
    {
        var symbol = await SymbolResolver.ResolveSymbolAsync(workspace, typeName, ct: ct);
        if (symbol is not INamedTypeSymbol typeSymbol)
            return JsonSerializer.Serialize(new { error = $"Type '{typeName}' not found" });

        var methods = new List<MethodComplexity>();

        foreach (var member in typeSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            ct.ThrowIfCancellationRequested();
            if (methods.Count >= maxResults) break;
            if (member.IsImplicitlyDeclared) continue;

            var syntaxRef = member.DeclaringSyntaxReferences.FirstOrDefault();
            if (syntaxRef is null) continue;

            var syntax = await syntaxRef.GetSyntaxAsync(ct);
            var body = syntax switch
            {
                MethodDeclarationSyntax m => (SyntaxNode?)m.Body ?? m.ExpressionBody,
                _ => null
            };
            if (body is null) continue;

            var metrics = ComplexityAnalyzer.Analyze(body);
            if (metrics.Cyclomatic < threshold) continue;

            var location = SymbolResolver.GetLocation(member);
            methods.Add(new MethodComplexity(member.Name, typeName, location.FilePath, location.Line, metrics));
        }

        var sorted = methods.OrderByDescending(m => m.Metrics.Cyclomatic).ToList();
        return JsonSerializer.Serialize(new ComplexityResult(sorted, sorted.Count));
    }

    private static async Task<string> AnalyzeProjectAsync(
        WorkspaceManager workspace, string projectName, int threshold, int maxResults, CancellationToken ct)
    {
        var solution = workspace.GetSolution();
        if (solution is null)
            return JsonSerializer.Serialize(new { error = "No solution loaded" });

        var project = solution.Projects
            .FirstOrDefault(p => p.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase));
        if (project is null)
            return JsonSerializer.Serialize(new { error = $"Project '{projectName}' not found" });

        var compilation = await workspace.GetCompilationAsync(project, ct);
        if (compilation is null)
            return JsonSerializer.Serialize(new { error = $"Could not compile project '{projectName}'" });

        var methods = new List<MethodComplexity>();

        foreach (var tree in compilation.SyntaxTrees)
        {
            ct.ThrowIfCancellationRequested();
            if (methods.Count >= maxResults) break;

            await CollectMethodMetricsAsync(tree, threshold, maxResults, methods, ct);
        }

        var sorted = methods.OrderByDescending(m => m.Metrics.Cyclomatic).ToList();
        return JsonSerializer.Serialize(new ComplexityResult(sorted, sorted.Count));
    }

    private static async Task CollectMethodMetricsAsync(
        SyntaxTree tree, int threshold, int maxResults,
        List<MethodComplexity> methods, CancellationToken ct)
    {
        var root = await tree.GetRootAsync(ct);
        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            if (methods.Count >= maxResults) break;

            var body = (SyntaxNode?)method.Body ?? method.ExpressionBody;
            if (body is null) continue;

            var metrics = ComplexityAnalyzer.Analyze(body);
            if (metrics.Cyclomatic < threshold) continue;

            var line = method.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            var containingType = method.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault()?.Identifier.Text;

            methods.Add(new MethodComplexity(
                method.Identifier.Text, containingType, tree.FilePath, line, metrics));
        }
    }
}
