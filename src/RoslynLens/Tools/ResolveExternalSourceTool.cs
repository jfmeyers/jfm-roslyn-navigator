using System.ComponentModel;
using System.Text.Json;
using RoslynLens.Responses;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Server;

namespace RoslynLens;

[McpServerToolType]
public static class ResolveExternalSourceTool
{
    [McpServerTool(Name = "resolve_external_source")]
    [Description("Resolve source code for external (NuGet/framework) symbols via SourceLink or decompilation. Use when a symbol is not in the local solution.")]
    public static async Task<string> ExecuteAsync(
        WorkspaceManager workspace,
        [Description("Name of the external symbol to resolve")] string symbolName,
        [Description("Optional assembly name to narrow search")] string? assemblyName = null,
        CancellationToken ct = default)
    {
        var status = workspace.EnsureReadyOrStatus(ct);
        if (status is not null) return status;

        // Find the symbol — could be a metadata symbol from referenced assemblies
        var candidates = await SymbolResolver.FindSymbolsByNameAsync(workspace, symbolName, ct: ct);

        ISymbol? symbol = null;
        if (assemblyName is not null)
        {
            symbol = candidates.FirstOrDefault(s =>
                s.ContainingAssembly?.Name.Equals(assemblyName, StringComparison.OrdinalIgnoreCase) == true);
        }

        symbol ??= candidates.Count > 0 ? candidates[0] : null;

        if (symbol is null)
            return JsonSerializer.Serialize(new { error = $"Symbol '{symbolName}' not found" });

        var (source, method) = await ExternalSourceResolver.ResolveAsync(symbol, ct);

        string? sourceLink = null;
        string? decompiledSource = null;

        if (method == "sourcelink")
            sourceLink = source;
        else if (method == "decompilation")
            decompiledSource = source;

        var result = new ExternalSourceResult(
            symbol.ToDisplayString(),
            symbol.ContainingAssembly?.Name,
            sourceLink,
            decompiledSource,
            method);

        return JsonSerializer.Serialize(result);
    }
}
