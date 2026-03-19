using System.ComponentModel;
using System.Text.Json;
using RoslynLens.Responses;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using ModelContextProtocol.Server;

namespace RoslynLens;

[McpServerToolType]
public static class AnalyzeMethodTool
{
    [McpServerTool(Name = "analyze_method")]
    [Description("Compound tool: returns a method's signature, callers, dependency graph, and complexity metrics in a single call.")]
    public static async Task<string> ExecuteAsync(
        WorkspaceManager workspace,
        [Description("Name of the method to analyze")] string methodName,
        [Description("Optional containing class name")] string? className = null,
        [Description("Dependency graph depth (default 2)")] int depth = 2,
        CancellationToken ct = default)
    {
        var status = workspace.EnsureReadyOrStatus(ct);
        if (status is not null) return status;

        var symbol = await SymbolResolver.ResolveMethodByNameAsync(workspace, methodName, className, ct);
        if (symbol is null)
            return JsonSerializer.Serialize(new { error = $"Method '{methodName}' not found" });

        // Symbol detail
        var location = SymbolResolver.GetLocation(symbol);
        var parameters = symbol is IMethodSymbol method
            ? method.Parameters.Select(p => new ParameterDetail(
                p.Name, p.Type.ToDisplayString(),
                p.HasExplicitDefaultValue,
                p.HasExplicitDefaultValue ? p.ExplicitDefaultValue?.ToString() : null)).ToList()
            : null;
        var returnType = symbol is IMethodSymbol m ? m.ReturnType.ToDisplayString() : null;
        var xmlDoc = symbol.GetDocumentationCommentXml(cancellationToken: ct);
        if (string.IsNullOrWhiteSpace(xmlDoc)) xmlDoc = null;

        var detail = new SymbolDetail(
            symbol.Name, symbol.Kind.ToString(), symbol.ToDisplayString(),
            returnType, location.FilePath, location.Line, parameters, xmlDoc);

        // Callers
        var solution = workspace.GetSolution()!;
        var callerSymbols = await SymbolFinder.FindCallersAsync(symbol, solution, ct);
        var callerInfos = callerSymbols
            .Where(c => c.IsDirect)
            .Select(c =>
            {
                var (f, l) = SymbolResolver.GetLocation(c.CallingSymbol);
                return new CallerInfo(c.CallingSymbol.Name, c.CallingSymbol.ContainingType?.ToDisplayString(), f, l);
            }).ToList();
        var callers = new CallersResult(symbol.ToDisplayString(), callerInfos, callerInfos.Count);

        // Dependency graph
        var visited = new HashSet<string>();
        var graphRoot = await GetDependencyGraphTool.BuildGraphAsync(workspace, symbol, depth, visited, ct);
        var dependencies = new DependencyGraphResult(graphRoot, depth);

        // Complexity
        ComplexityMetrics? complexity = null;
        var syntaxRef = symbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef is not null)
        {
            var syntax = await syntaxRef.GetSyntaxAsync(ct);
            var body = syntax switch
            {
                MethodDeclarationSyntax md => (SyntaxNode?)md.Body ?? md.ExpressionBody,
                _ => null
            };
            if (body is not null)
                complexity = ComplexityAnalyzer.Analyze(body);
        }

        var result = new MethodAnalysis(detail, callers, dependencies, complexity);
        return JsonSerializer.Serialize(result);
    }
}
