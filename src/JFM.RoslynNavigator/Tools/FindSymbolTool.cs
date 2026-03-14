using System.ComponentModel;
using System.Text.Json;
using JFM.RoslynNavigator.Responses;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Server;

namespace JFM.RoslynNavigator;

[McpServerToolType]
public static class FindSymbolTool
{
    [McpServerTool(Name = "find_symbol")]
    [Description("Find symbols (types, methods, properties, fields) by name across the solution. Optionally filter by kind (class, interface, struct, enum, record, method, property, field, event, namespace).")]
    public static async Task<string> ExecuteAsync(
        WorkspaceManager workspace,
        [Description("Symbol name to search for (exact or case-insensitive match)")] string name,
        [Description("Optional kind filter: class, interface, struct, enum, record, method, property, field, event, namespace")] string? kind = null,
        CancellationToken ct = default)
    {
        var status = workspace.EnsureReadyOrStatus(ct);
        if (status is not null) return status;

        var symbols = await SymbolResolver.FindSymbolsByNameAsync(workspace, name, kind, ct);

        var matches = symbols.Select(s =>
        {
            var (file, line) = SymbolResolver.GetLocation(s);
            var project = s.ContainingAssembly?.Name;
            var symbolKind = s switch
            {
                INamedTypeSymbol nts => nts.TypeKind.ToString().ToLowerInvariant(),
                IMethodSymbol => "method",
                IPropertySymbol => "property",
                IFieldSymbol => "field",
                IEventSymbol => "event",
                INamespaceSymbol => "namespace",
                _ => s.Kind.ToString().ToLowerInvariant()
            };

            return new SymbolMatch(s.ToDisplayString(), symbolKind, file, line, project);
        }).ToList();

        var result = new SymbolSearchResult(matches, matches.Count);
        return JsonSerializer.Serialize(result);
    }
}
