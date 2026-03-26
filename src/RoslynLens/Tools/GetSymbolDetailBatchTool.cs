using System.ComponentModel;
using RoslynLens.Responses;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Server;

namespace RoslynLens;

[McpServerToolType]
public static class GetSymbolDetailBatchTool
{
    [McpServerTool(Name = "get_symbol_detail_batch")]
    [Description("Get detailed information about multiple symbols in a single call. Accepts comma-separated symbol names.")]
    public static async Task<string> ExecuteAsync(
        WorkspaceManager workspace,
        [Description("Comma-separated symbol names")] string symbolNames,
        CancellationToken ct = default)
    {
        var status = workspace.EnsureReadyOrStatus(ct);
        if (status is not null) return status;

        var queries = symbolNames.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var items = new List<DetailBatchItem>();
        var succeeded = 0;
        var failed = 0;

        foreach (var symbolName in queries)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var candidates = await SymbolResolver.FindSymbolsByNameAsync(workspace, symbolName, ct: ct);
                if (candidates.Count == 0)
                {
                    items.Add(new DetailBatchItem(symbolName, null, $"Symbol '{symbolName}' not found"));
                    failed++;
                    continue;
                }

                var detail = BuildSymbolDetail(candidates[0], ct);
                items.Add(new DetailBatchItem(symbolName, detail, null));
                succeeded++;
            }
            catch (Exception ex)
            {
                items.Add(new DetailBatchItem(symbolName, null, ex.Message));
                failed++;
            }
        }

        return Json.Serialize(new DetailBatchResult(items, items.Count, succeeded, failed));
    }

    private static SymbolDetail BuildSymbolDetail(ISymbol symbol, CancellationToken ct)
    {
        var location = SymbolResolver.GetLocation(symbol);

        var parameters = symbol is IMethodSymbol method
            ? method.Parameters.Select(p => new ParameterDetail(
                p.Name, p.Type.ToDisplayString(),
                p.HasExplicitDefaultValue,
                p.HasExplicitDefaultValue ? p.ExplicitDefaultValue?.ToString() : null)).ToList()
            : null;

        var returnType = symbol switch
        {
            IMethodSymbol m => m.ReturnType.ToDisplayString(),
            IPropertySymbol p => p.Type.ToDisplayString(),
            IFieldSymbol f => f.Type.ToDisplayString(),
            _ => null
        };

        var xmlDoc = symbol.GetDocumentationCommentXml(cancellationToken: ct);
        if (string.IsNullOrWhiteSpace(xmlDoc)) xmlDoc = null;

        return new SymbolDetail(
            symbol.Name, symbol.Kind.ToString(), symbol.ToDisplayString(),
            returnType, location.FilePath, location.Line, parameters, xmlDoc);
    }
}
