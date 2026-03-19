using System.ComponentModel;
using System.Text.Json;
using RoslynLens.Responses;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using ModelContextProtocol.Server;

namespace RoslynLens;

[McpServerToolType]
public static class FindCallersTool
{
    [McpServerTool(Name = "find_callers")]
    [Description("Find all callers (call sites) of a method across the solution.")]
    public static async Task<string> ExecuteAsync(
        WorkspaceManager workspace,
        [Description("Name of the method to find callers for")] string methodName,
        [Description("Optional containing class name to disambiguate overloaded method names")] string? className = null,
        CancellationToken ct = default)
    {
        var status = workspace.EnsureReadyOrStatus(ct);
        if (status is not null) return status;

        var symbol = await SymbolResolver.ResolveMethodByNameAsync(workspace, methodName, className, ct);
        if (symbol is null)
            return JsonSerializer.Serialize(new CallersResult(methodName, [], 0));

        var solution = workspace.GetSolution()!;
        var callers = await SymbolFinder.FindCallersAsync(symbol, solution, ct);

        var callerInfos = callers
            .Where(c => c.IsDirect)
            .Select(c =>
            {
                var (file, line) = SymbolResolver.GetLocation(c.CallingSymbol);
                var containingType = c.CallingSymbol.ContainingType?.ToDisplayString();
                var methodDisplay = c.CallingSymbol.Name;

                return new CallerInfo(methodDisplay, containingType, file, line);
            })
            .ToList();

        var result = new CallersResult(symbol.ToDisplayString(), callerInfos, callerInfos.Count);
        return JsonSerializer.Serialize(result);
    }
}
