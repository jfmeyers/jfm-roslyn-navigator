using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace JFM.RoslynNavigator;

/// <summary>
/// Cross-project symbol resolution with disambiguation by file path and line number.
/// </summary>
public static class SymbolResolver
{
    public static async Task<IReadOnlyList<ISymbol>> FindSymbolsByNameAsync(
        WorkspaceManager workspace,
        string name,
        string? kind = null,
        CancellationToken ct = default)
    {
        var solution = workspace.GetSolution();
        if (solution is null) return [];

        var results = new List<ISymbol>();
        var seen = new HashSet<string>();

        foreach (var project in solution.Projects)
        {
            var compilation = await workspace.GetCompilationAsync(project, ct);
            if (compilation is null) continue;

            var symbols = compilation.GetSymbolsWithName(
                n => n.Equals(name, StringComparison.Ordinal) ||
                     n.Equals(name, StringComparison.OrdinalIgnoreCase),
                SymbolFilter.All, ct);

            foreach (var symbol in symbols)
            {
                if (kind is not null && !MatchesKind(symbol, kind))
                    continue;

                var displayString = symbol.ToDisplayString();
                if (seen.Add(displayString))
                    results.Add(symbol);
            }
        }

        return results;
    }

    public static async Task<ISymbol?> ResolveSymbolAsync(
        WorkspaceManager workspace,
        string name,
        string? filePath = null,
        int? line = null,
        string? kind = null,
        CancellationToken ct = default)
    {
        var candidates = await FindSymbolsByNameAsync(workspace, name, kind, ct);

        if (candidates.Count == 0) return null;
        if (candidates.Count == 1) return candidates[0];

        // Disambiguate by file path suffix match
        if (filePath is not null)
        {
            var normalized = filePath.Replace('\\', '/');
            var match = candidates.FirstOrDefault(s =>
            {
                var loc = GetLocation(s);
                return loc.FilePath is not null &&
                       loc.FilePath.Replace('\\', '/').EndsWith(normalized, StringComparison.OrdinalIgnoreCase);
            });
            if (match is not null) return match;
        }

        // Disambiguate by line number
        if (line is not null)
        {
            var match = candidates.FirstOrDefault(s =>
            {
                var loc = GetLocation(s);
                return loc.Line == line;
            });
            if (match is not null) return match;
        }

        // Return first match
        return candidates[0];
    }

    public static (string? FilePath, int? Line) GetLocation(ISymbol symbol)
    {
        var syntaxRef = symbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef is null) return (null, null);

        var span = syntaxRef.SyntaxTree.GetLineSpan(syntaxRef.Span);
        return (syntaxRef.SyntaxTree.FilePath, span.StartLinePosition.Line + 1);
    }

    public static bool MatchesKind(ISymbol symbol, string kind) =>
        kind.ToLowerInvariant() switch
        {
            "class" => symbol is INamedTypeSymbol { TypeKind: TypeKind.Class },
            "interface" => symbol is INamedTypeSymbol { TypeKind: TypeKind.Interface },
            "struct" => symbol is INamedTypeSymbol { TypeKind: TypeKind.Struct },
            "enum" => symbol is INamedTypeSymbol { TypeKind: TypeKind.Enum },
            "record" => symbol is INamedTypeSymbol { IsRecord: true },
            "method" => symbol is IMethodSymbol,
            "property" => symbol is IPropertySymbol,
            "field" => symbol is IFieldSymbol,
            "event" => symbol is IEventSymbol,
            "namespace" => symbol is INamespaceSymbol,
            _ => true
        };

    public static async Task<IReadOnlyList<ReferencedSymbol>> FindReferencesAsync(
        WorkspaceManager workspace,
        ISymbol symbol,
        CancellationToken ct)
    {
        var solution = workspace.GetSolution();
        if (solution is null) return [];

        var references = await SymbolFinder.FindReferencesAsync(symbol, solution, ct);
        return references.ToList();
    }
}
