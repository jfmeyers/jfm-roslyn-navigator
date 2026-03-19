using System.ComponentModel;
using System.Text.Json;
using RoslynLens.Responses;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using ModelContextProtocol.Server;

namespace RoslynLens;

[McpServerToolType]
public static class FindImplementationsTool
{
    [McpServerTool(Name = "find_implementations")]
    [Description("Find all implementations of an interface or all derived classes of a base class across the solution.")]
    public static async Task<string> ExecuteAsync(
        WorkspaceManager workspace,
        [Description("Name of the interface or base class to find implementations for")] string interfaceName,
        CancellationToken ct = default)
    {
        var status = workspace.EnsureReadyOrStatus(ct);
        if (status is not null) return status;

        var symbol = await SymbolResolver.ResolveSymbolAsync(workspace, interfaceName, kind: "interface", ct: ct);

        // If not found as interface, try as class
        symbol ??= await SymbolResolver.ResolveSymbolAsync(workspace, interfaceName, kind: "class", ct: ct);

        if (symbol is not INamedTypeSymbol typeSymbol)
            return JsonSerializer.Serialize(new ImplementationsResult(interfaceName, [], 0));

        var solution = workspace.GetSolution()!;

        IEnumerable<ISymbol> implementingSymbols;
        if (typeSymbol.TypeKind == TypeKind.Interface)
        {
            implementingSymbols = await SymbolFinder.FindImplementationsAsync(typeSymbol, solution, cancellationToken: ct);
        }
        else
        {
            var derivedClasses = await SymbolFinder.FindDerivedClassesAsync(typeSymbol, solution, cancellationToken: ct);
            implementingSymbols = derivedClasses;
        }

        var implementations = implementingSymbols.Select(s =>
        {
            var (file, line) = SymbolResolver.GetLocation(s);
            var project = s.ContainingAssembly?.Name;
            var kind = s switch
            {
                INamedTypeSymbol nts => nts.TypeKind.ToString().ToLowerInvariant(),
                _ => s.Kind.ToString().ToLowerInvariant()
            };

            return new ImplementationMatch(s.ToDisplayString(), kind, file, line, project);
        }).ToList();

        var result = new ImplementationsResult(typeSymbol.ToDisplayString(), implementations, implementations.Count);
        return JsonSerializer.Serialize(result);
    }
}
