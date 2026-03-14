using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace JFM.RoslynNavigator.Analyzers;

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

        var disables = new List<(string Code, int Line, SyntaxTrivia Trivia)>();
        var restores = new HashSet<string>(StringComparer.Ordinal);

        foreach (var trivia in root.DescendantTrivia())
        {
            ct.ThrowIfCancellationRequested();

            if (trivia.IsKind(SyntaxKind.PragmaWarningDirectiveTrivia))
            {
                var directive = (PragmaWarningDirectiveTriviaSyntax)trivia.GetStructure()!;
                var codes = directive.ErrorCodes.Select(e => e.ToString().Trim()).ToList();

                if (directive.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword))
                {
                    var line = trivia.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    foreach (var code in codes)
                    {
                        disables.Add((code, line, trivia));
                    }

                    // Handle bare #pragma warning disable (no specific codes)
                    if (codes.Count == 0)
                    {
                        disables.Add(("*", line, trivia));
                    }
                }
                else if (directive.DisableOrRestoreKeyword.IsKind(SyntaxKind.RestoreKeyword))
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
        }

        foreach (var (code, line, _) in disables)
        {
            if (!restores.Contains(code))
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
    }
}
