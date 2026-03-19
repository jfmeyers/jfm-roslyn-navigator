using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RoslynLens.Analyzers;

/// <summary>
/// AP007: Detects #pragma warning disable without a matching #pragma warning restore.
/// Unrestored pragmas silently suppress warnings for the rest of the file.
/// </summary>
public sealed class PragmaWithoutRestoreDetector : IAntiPatternDetector
{
    public bool RequiresSemanticModel => false;

    public IEnumerable<AntiPatternViolation> Detect(SyntaxTree tree, SemanticModel? model, CancellationToken ct)
    {
        var root = tree.GetRoot(ct);
        var filePath = tree.FilePath;

        var (disables, restores) = CollectPragmaDirectives(root, ct);

        foreach (var (code, line, _) in disables.Where(d => !restores.Contains(d.Code)))
        {
            var codeDisplay = code == "*" ? "(all warnings)" : code;
            yield return new AntiPatternViolation(
                "AP007",
                AntiPatternSeverity.Warning,
                $"#pragma warning disable {codeDisplay} without matching restore",
                filePath,
                line,
                "Add a matching #pragma warning restore after the affected code");
        }
    }

    private static (List<(string Code, int Line, SyntaxTrivia Trivia)> Disables, HashSet<string> Restores)
        CollectPragmaDirectives(SyntaxNode root, CancellationToken ct)
    {
        var disables = new List<(string Code, int Line, SyntaxTrivia Trivia)>();
        var restores = new HashSet<string>(StringComparer.Ordinal);

        foreach (var trivia in root.DescendantTrivia()
                     .Where(t => t.IsKind(SyntaxKind.PragmaWarningDirectiveTrivia)))
        {
            ct.ThrowIfCancellationRequested();

            var directive = (PragmaWarningDirectiveTriviaSyntax)trivia.GetStructure()!;
            var codes = directive.ErrorCodes.Select(e => e.ToString().Trim()).ToList();

            if (directive.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword))
            {
                CollectDisableCodes(disables, codes, trivia);
            }
            else if (directive.DisableOrRestoreKeyword.IsKind(SyntaxKind.RestoreKeyword))
            {
                CollectRestoreCodes(restores, codes);
            }
        }

        return (disables, restores);
    }

    private static void CollectDisableCodes(
        List<(string Code, int Line, SyntaxTrivia Trivia)> disables,
        List<string> codes, SyntaxTrivia trivia)
    {
        var line = trivia.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

        foreach (var code in codes)
        {
            disables.Add((code, line, trivia));
        }

        if (codes.Count == 0)
        {
            disables.Add(("*", line, trivia));
        }
    }

    private static void CollectRestoreCodes(HashSet<string> restores, List<string> codes)
    {
        foreach (var code in codes)
        {
            restores.Add(code);
        }

        if (codes.Count == 0)
        {
            restores.Add("*");
        }
    }
}
