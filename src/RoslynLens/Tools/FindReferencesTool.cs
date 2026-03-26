using System.ComponentModel;
using RoslynLens.Responses;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModelContextProtocol.Server;

namespace RoslynLens;

[McpServerToolType]
public static class FindReferencesTool
{
    [McpServerTool(Name = "find_references")]
    [Description("Find all references to a symbol across the solution. Classifies each reference by kind (inheritance, invocation, assignment, type-argument, parameter, instantiation).")]
    public static async Task<string> ExecuteAsync(
        WorkspaceManager workspace,
        [Description("Name of the symbol to find references for")] string symbolName,
        [Description("Optional file path to disambiguate symbols with the same name")] string? file = null,
        [Description("Optional line number to disambiguate symbols with the same name")] int? line = null,
        CancellationToken ct = default)
    {
        var status = workspace.EnsureReadyOrStatus(ct);
        if (status is not null) return status;

        var symbol = await SymbolResolver.ResolveSymbolAsync(workspace, symbolName, file, line, ct: ct);
        if (symbol is null)
            return Json.Serialize(new ReferencesResult(symbolName, [], 0));

        var referencedSymbols = await SymbolResolver.FindReferencesAsync(workspace, symbol, ct);

        var locations = new List<ReferenceLocation>();

        foreach (var referencedSymbol in referencedSymbols)
        {
            foreach (var location in referencedSymbol.Locations)
            {
                var lineSpan = location.Location.GetLineSpan();
                var filePath = lineSpan.Path;
                var lineNumber = lineSpan.StartLinePosition.Line + 1;

                var kind = await ClassifyReferenceKindAsync(location, ct);
                var context = await GetContextLineAsync(location, ct);

                locations.Add(new ReferenceLocation(filePath, lineNumber, kind, context));
            }
        }

        var result = new ReferencesResult(symbol.ToDisplayString(), locations, locations.Count);
        return Json.Serialize(result);
    }

    private static async Task<string?> ClassifyReferenceKindAsync(
        Microsoft.CodeAnalysis.FindSymbols.ReferenceLocation location,
        CancellationToken ct)
    {
        var syntaxTree = location.Location.SourceTree;
        if (syntaxTree is null) return null;

        var root = await syntaxTree.GetRootAsync(ct);
        var node = root.FindNode(location.Location.SourceSpan);

        return node.Parent switch
        {
            BaseListSyntax => "inheritance",
            SimpleBaseTypeSyntax => "inheritance",
            InvocationExpressionSyntax => "invocation",
            ObjectCreationExpressionSyntax => "instantiation",
            ImplicitObjectCreationExpressionSyntax => "instantiation",
            AssignmentExpressionSyntax => "assignment",
            TypeArgumentListSyntax => "type-argument",
            ParameterSyntax => "parameter",
            MemberAccessExpressionSyntax parent => ClassifyMemberAccess(parent),
            _ => null
        };
    }

    private static string? ClassifyMemberAccess(MemberAccessExpressionSyntax parent) =>
        parent.Parent switch
        {
            InvocationExpressionSyntax => "invocation",
            AssignmentExpressionSyntax => "assignment",
            _ => null
        };

    private static async Task<string?> GetContextLineAsync(
        Microsoft.CodeAnalysis.FindSymbols.ReferenceLocation location,
        CancellationToken ct)
    {
        var syntaxTree = location.Location.SourceTree;
        if (syntaxTree is null) return null;

        var text = await syntaxTree.GetTextAsync(ct);
        var lineSpan = location.Location.GetLineSpan();
        var lineNumber = lineSpan.StartLinePosition.Line;

        if (lineNumber >= text.Lines.Count) return null;

        return text.Lines[lineNumber].ToString().Trim();
    }
}
