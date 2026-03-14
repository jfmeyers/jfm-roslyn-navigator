using System.ComponentModel;
using System.Text.Json;
using JFM.RoslynNavigator.Responses;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using ModelContextProtocol.Server;

namespace JFM.RoslynNavigator;

[McpServerToolType]
public static class GetTypeHierarchyTool
{
    [McpServerTool(Name = "get_type_hierarchy")]
    [Description("Get the full type hierarchy for a class or interface: base types, implemented interfaces, and derived types.")]
    public static async Task<string> ExecuteAsync(
        WorkspaceManager workspace,
        [Description("Name of the type to get hierarchy for")] string typeName,
        CancellationToken ct = default)
    {
        var status = workspace.EnsureReadyOrStatus(ct);
        if (status is not null) return status;

        var symbol = await SymbolResolver.ResolveSymbolAsync(workspace, typeName, ct: ct);

        if (symbol is not INamedTypeSymbol typeSymbol)
        {
            var empty = new TypeHierarchyResult(
                new TypeHierarchyNode(typeName, "unknown", null, null), [], [], []);
            return JsonSerializer.Serialize(empty);
        }

        var (file, line) = SymbolResolver.GetLocation(typeSymbol);
        var typeNode = new TypeHierarchyNode(
            typeSymbol.ToDisplayString(),
            typeSymbol.TypeKind.ToString().ToLowerInvariant(),
            file, line);

        // Walk base type chain
        var baseTypes = new List<TypeHierarchyNode>();
        var current = typeSymbol.BaseType;
        while (current is not null && current.SpecialType != SpecialType.System_Object)
        {
            var (baseFile, baseLine) = SymbolResolver.GetLocation(current);
            baseTypes.Add(new TypeHierarchyNode(
                current.ToDisplayString(),
                current.TypeKind.ToString().ToLowerInvariant(),
                baseFile, baseLine));
            current = current.BaseType;
        }

        // Collect all interfaces
        var interfaces = typeSymbol.AllInterfaces.Select(i =>
        {
            var (iFile, iLine) = SymbolResolver.GetLocation(i);
            return new TypeHierarchyNode(
                i.ToDisplayString(),
                "interface",
                iFile, iLine);
        }).ToList();

        // Find derived types
        var solution = workspace.GetSolution()!;
        var derivedSymbols = await SymbolFinder.FindDerivedClassesAsync(typeSymbol, solution, cancellationToken: ct);
        var derivedTypes = derivedSymbols.Select(d =>
        {
            var (dFile, dLine) = SymbolResolver.GetLocation(d);
            return new TypeHierarchyNode(
                d.ToDisplayString(),
                d.TypeKind.ToString().ToLowerInvariant(),
                dFile, dLine);
        }).ToList();

        var result = new TypeHierarchyResult(typeNode, baseTypes, interfaces, derivedTypes);
        return JsonSerializer.Serialize(result);
    }
}
