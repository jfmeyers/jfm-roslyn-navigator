using System.ComponentModel;
using RoslynLens.Responses;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Server;

namespace RoslynLens;

[McpServerToolType]
public static class FindSymbolsBatchTool
{
    [McpServerTool(Name = "find_symbols_batch")]
    [Description("Find multiple symbols by name in a single call. Accepts comma-separated names. Each query is resolved independently.")]
    public static async Task<string> ExecuteAsync(
        WorkspaceManager workspace,
        [Description("Comma-separated symbol names to find")] string names,
        [Description("Optional kind filter: class, interface, method, etc.")] string? kind = null,
        CancellationToken ct = default)
    {
        var status = workspace.EnsureReadyOrStatus(ct);
        if (status is not null) return status;

        var queries = names.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var items = new List<SymbolBatchItem>();
        var succeeded = 0;
        var failed = 0;

        foreach (var query in queries)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var symbols = await SymbolResolver.FindSymbolsByNameAsync(workspace, query, kind, ct);
                var matches = symbols.Select(s =>
                {
                    var (file, line) = SymbolResolver.GetLocation(s);
                    var symbolKind = s switch
                    {
                        INamedTypeSymbol nts => nts.TypeKind.ToString().ToLowerInvariant(),
                        IMethodSymbol => "method",
                        IPropertySymbol => "property",
                        IFieldSymbol => "field",
                        _ => s.Kind.ToString().ToLowerInvariant()
                    };
                    return new SymbolMatch(s.ToDisplayString(), symbolKind, file, line, s.ContainingAssembly?.Name);
                }).ToList();

                items.Add(new SymbolBatchItem(query, new SymbolSearchResult(matches, matches.Count), null));
                succeeded++;
            }
            catch (Exception ex)
            {
                items.Add(new SymbolBatchItem(query, null, ex.Message));
                failed++;
            }
        }

        return Json.Serialize(new SymbolBatchResult(items, items.Count, succeeded, failed));
    }
}
