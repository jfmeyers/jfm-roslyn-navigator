using System.ComponentModel;
using System.Text.Json;
using JFM.RoslynNavigator.Responses;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModelContextProtocol.Server;

namespace JFM.RoslynNavigator;

[McpServerToolType]
public static class GetDependencyGraphTool
{
    private static readonly HashSet<string> SkippedNamespacePrefixes =
        ["System", "Microsoft"];

    [McpServerTool(Name = "get_dependency_graph")]
    [Description("Builds a call dependency graph starting from a symbol, recursively walking invocations up to the specified depth.")]
    public static async Task<string> ExecuteAsync(
        WorkspaceManager workspace,
        [Description("Name of the root symbol to start from")] string symbolName,
        [Description("Optional file path to disambiguate the symbol")] string? file = null,
        [Description("Optional line number to disambiguate the symbol")] int? line = null,
        [Description("Maximum recursion depth (default 3)")] int depth = 3,
        CancellationToken ct = default)
    {
        var status = workspace.EnsureReadyOrStatus(ct);
        if (status is not null) return status;

        var symbol = await SymbolResolver.ResolveSymbolAsync(workspace, symbolName, file, line, ct: ct);
        if (symbol is null)
            return JsonSerializer.Serialize(new { error = $"Symbol '{symbolName}' not found" });

        var visited = new HashSet<string>();
        var rootNode = await BuildGraphAsync(workspace, symbol, depth, visited, ct);

        var result = new DependencyGraphResult(rootNode, depth);
        return JsonSerializer.Serialize(result);
    }

    private static async Task<DependencyNode> BuildGraphAsync(
        WorkspaceManager workspace,
        ISymbol symbol,
        int remainingDepth,
        HashSet<string> visited,
        CancellationToken ct)
    {
        var location = SymbolResolver.GetLocation(symbol);
        var key = symbol.ToDisplayString();

        if (remainingDepth <= 0 || !visited.Add(key))
            return new DependencyNode(key, location.FilePath, location.Line, null);

        var calls = new List<DependencyNode>();

        if (symbol is IMethodSymbol)
        {
            foreach (var syntaxRef in symbol.DeclaringSyntaxReferences)
            {
                var syntax = await syntaxRef.GetSyntaxAsync(ct);
                var tree = syntax.SyntaxTree;

                var solution = workspace.GetSolution();
                if (solution is null) continue;

                var project = solution.Projects
                    .SelectMany(p => p.Documents)
                    .FirstOrDefault(d => d.FilePath == tree.FilePath)?
                    .Project;

                if (project is null) continue;

                var compilation = await workspace.GetCompilationAsync(project, ct);
                if (compilation is null) continue;

                var model = compilation.GetSemanticModel(tree);

                var invocations = syntax.DescendantNodes()
                    .OfType<InvocationExpressionSyntax>();

                foreach (var invocation in invocations)
                {
                    ct.ThrowIfCancellationRequested();

                    var symbolInfo = model.GetSymbolInfo(invocation, ct);
                    var calledSymbol = symbolInfo.Symbol;

                    if (calledSymbol is null) continue;
                    if (ShouldSkip(calledSymbol)) continue;

                    var childNode = await BuildGraphAsync(workspace, calledSymbol, remainingDepth - 1, visited, ct);
                    calls.Add(childNode);
                }
            }
        }

        return new DependencyNode(key, location.FilePath, location.Line, calls.Count > 0 ? calls : null);
    }

    private static bool ShouldSkip(ISymbol symbol)
    {
        var ns = symbol.ContainingNamespace?.ToDisplayString();
        if (ns is null) return true;

        foreach (var prefix in SkippedNamespacePrefixes)
        {
            if (ns.StartsWith(prefix, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
