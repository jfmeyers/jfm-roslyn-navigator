using System.ComponentModel;
using System.Text.Json;
using RoslynLens.Responses;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Server;

namespace RoslynLens;

[McpServerToolType]
public static class GetPublicApiBatchTool
{
    [McpServerTool(Name = "get_public_api_batch")]
    [Description("Get the public API surface of multiple types in a single call. Accepts comma-separated type names.")]
    public static async Task<string> ExecuteAsync(
        WorkspaceManager workspace,
        [Description("Comma-separated type names")] string typeNames,
        CancellationToken ct = default)
    {
        var status = workspace.EnsureReadyOrStatus(ct);
        if (status is not null) return status;

        var queries = typeNames.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var items = new List<ApiBatchItem>();
        var succeeded = 0;
        var failed = 0;

        foreach (var typeName in queries)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var symbol = await SymbolResolver.ResolveSymbolAsync(workspace, typeName, ct: ct);
                if (symbol is not INamedTypeSymbol typeSymbol)
                {
                    items.Add(new ApiBatchItem(typeName, null, $"Type '{typeName}' not found"));
                    failed++;
                    continue;
                }

                var members = typeSymbol.GetMembers()
                    .Where(m => m.DeclaredAccessibility == Accessibility.Public)
                    .Where(m => !m.IsImplicitlyDeclared)
                    .Where(m => m is not IMethodSymbol { MethodKind: MethodKind.PropertyGet or MethodKind.PropertySet or MethodKind.EventAdd or MethodKind.EventRemove })
                    .Select(m =>
                    {
                        var kind = m switch
                        {
                            IMethodSymbol { MethodKind: MethodKind.Constructor } => "constructor",
                            IMethodSymbol => "method",
                            IPropertySymbol => "property",
                            IFieldSymbol => "field",
                            IEventSymbol => "event",
                            _ => m.Kind.ToString().ToLowerInvariant()
                        };
                        var returnType = m switch
                        {
                            IMethodSymbol ms => ms.ReturnType.ToDisplayString(),
                            IPropertySymbol ps => ps.Type.ToDisplayString(),
                            IFieldSymbol fs => fs.Type.ToDisplayString(),
                            _ => null
                        };
                        return new ApiMember(m.Name, kind, m.ToDisplayString(), returnType);
                    }).ToList();

                items.Add(new ApiBatchItem(typeName, new PublicApiResult(typeSymbol.ToDisplayString(), members, members.Count), null));
                succeeded++;
            }
            catch (Exception ex)
            {
                items.Add(new ApiBatchItem(typeName, null, ex.Message));
                failed++;
            }
        }

        return JsonSerializer.Serialize(new ApiBatchResult(items, items.Count, succeeded, failed));
    }
}
