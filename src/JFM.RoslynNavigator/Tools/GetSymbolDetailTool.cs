using System.ComponentModel;
using System.Text.Json;
using JFM.RoslynNavigator.Responses;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Server;

namespace JFM.RoslynNavigator;

[McpServerToolType]
public static class GetSymbolDetailTool
{
    [McpServerTool(Name = "get_symbol_detail")]
    [Description("Returns full signature, parameters, return type, and XML documentation for a symbol.")]
    public static async Task<string> ExecuteAsync(
        WorkspaceManager workspace,
        [Description("Name of the symbol to look up")] string symbolName,
        [Description("Optional containing type to disambiguate overloaded names")] string? containingType = null,
        CancellationToken ct = default)
    {
        var status = workspace.EnsureReadyOrStatus(ct);
        if (status is not null) return status;

        var candidates = await SymbolResolver.FindSymbolsByNameAsync(workspace, symbolName, ct: ct);

        if (containingType is not null)
        {
            candidates = candidates
                .Where(s => s.ContainingType?.Name.Equals(containingType, StringComparison.OrdinalIgnoreCase) == true)
                .ToList();
        }

        if (candidates.Count == 0)
            return JsonSerializer.Serialize(new { error = $"Symbol '{symbolName}' not found" });

        var symbol = candidates[0];
        var location = SymbolResolver.GetLocation(symbol);

        var parameters = symbol is IMethodSymbol method
            ? method.Parameters.Select(p => new ParameterDetail(
                p.Name,
                p.Type.ToDisplayString(),
                p.HasExplicitDefaultValue,
                p.HasExplicitDefaultValue ? p.ExplicitDefaultValue?.ToString() : null)).ToList()
            : null;

        var returnType = symbol switch
        {
            IMethodSymbol m => m.ReturnType.ToDisplayString(),
            IPropertySymbol p => p.Type.ToDisplayString(),
            IFieldSymbol f => f.Type.ToDisplayString(),
            IEventSymbol e => e.Type.ToDisplayString(),
            _ => null
        };

        var xmlDoc = symbol.GetDocumentationCommentXml(cancellationToken: ct);
        if (string.IsNullOrWhiteSpace(xmlDoc))
            xmlDoc = null;

        var result = new SymbolDetail(
            symbol.Name,
            symbol.Kind.ToString(),
            symbol.ToDisplayString(),
            returnType,
            location.FilePath,
            location.Line,
            parameters,
            xmlDoc);

        return JsonSerializer.Serialize(result);
    }
}
