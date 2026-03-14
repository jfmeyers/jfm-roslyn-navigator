using System.ComponentModel;
using System.Text.Json;
using JFM.RoslynNavigator.Responses;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Server;

namespace JFM.RoslynNavigator;

[McpServerToolType]
public static class GetPublicApiTool
{
    [McpServerTool(Name = "get_public_api")]
    [Description("Get the public API surface of a type: all public methods, properties, fields, and events with their signatures.")]
    public static async Task<string> ExecuteAsync(
        WorkspaceManager workspace,
        [Description("Name of the type to get public API for")] string typeName,
        CancellationToken ct = default)
    {
        var status = workspace.EnsureReadyOrStatus(ct);
        if (status is not null) return status;

        var symbol = await SymbolResolver.ResolveSymbolAsync(workspace, typeName, ct: ct);

        if (symbol is not INamedTypeSymbol typeSymbol)
            return JsonSerializer.Serialize(new PublicApiResult(typeName, [], 0));

        var members = typeSymbol.GetMembers()
            .Where(m => m.DeclaredAccessibility == Accessibility.Public)
            .Where(m => !m.IsImplicitlyDeclared)
            .Where(m => m is not IMethodSymbol { MethodKind: MethodKind.PropertyGet or MethodKind.PropertySet or MethodKind.EventAdd or MethodKind.EventRemove })
            .Select(m =>
            {
                var kind = m switch
                {
                    IMethodSymbol ms => ms.MethodKind == MethodKind.Constructor ? "constructor" : "method",
                    IPropertySymbol => "property",
                    IFieldSymbol => "field",
                    IEventSymbol => "event",
                    INamedTypeSymbol nts => nts.TypeKind.ToString().ToLowerInvariant(),
                    _ => m.Kind.ToString().ToLowerInvariant()
                };

                var returnType = m switch
                {
                    IMethodSymbol ms => ms.ReturnsVoid ? "void" : ms.ReturnType.ToDisplayString(),
                    IPropertySymbol ps => ps.Type.ToDisplayString(),
                    IFieldSymbol fs => fs.Type.ToDisplayString(),
                    IEventSymbol es => es.Type.ToDisplayString(),
                    _ => null
                };

                return new ApiMember(m.Name, kind, m.ToDisplayString(), returnType);
            })
            .ToList();

        var result = new PublicApiResult(typeSymbol.ToDisplayString(), members, members.Count);
        return JsonSerializer.Serialize(result);
    }
}
