using System.ComponentModel;
using System.Text.Json;
using JFM.RoslynNavigator.Responses;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using ModelContextProtocol.Server;

namespace JFM.RoslynNavigator;

[McpServerToolType]
public static class FindOverridesTool
{
    [McpServerTool(Name = "find_overrides")]
    [Description("Find all overrides of a virtual or abstract method across the solution.")]
    public static async Task<string> ExecuteAsync(
        WorkspaceManager workspace,
        [Description("Name of the method to find overrides for")] string methodName,
        [Description("Optional containing class name to disambiguate overloaded method names")] string? className = null,
        CancellationToken ct = default)
    {
        var status = workspace.EnsureReadyOrStatus(ct);
        if (status is not null) return status;

        var symbol = await SymbolResolver.ResolveMethodByNameAsync(workspace, methodName, className, ct);
        if (symbol is null)
            return JsonSerializer.Serialize(new OverridesResult(methodName, [], 0));

        var solution = workspace.GetSolution()!;
        var overrides = await SymbolFinder.FindOverridesAsync(symbol, solution, cancellationToken: ct);

        var overrideInfos = overrides.Select(o =>
        {
            var (file, line) = SymbolResolver.GetLocation(o);
            var containingType = o.ContainingType?.ToDisplayString() ?? "unknown";

            return new OverrideInfo(o.Name, containingType, file, line);
        }).ToList();

        var result = new OverridesResult(symbol.ToDisplayString(), overrideInfos, overrideInfos.Count);
        return JsonSerializer.Serialize(result);
    }
}
