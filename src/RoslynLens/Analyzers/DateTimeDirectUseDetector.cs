using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RoslynLens.Analyzers;

/// <summary>
/// AP004: Detects direct use of DateTime.Now, DateTime.UtcNow, DateTimeOffset.Now, DateTimeOffset.UtcNow.
/// These static calls make code untestable and violate the Granit timing abstraction.
/// </summary>
public sealed class DateTimeDirectUseDetector : IAntiPatternDetector
{
    private static readonly HashSet<string> ForbiddenAccesses = new(StringComparer.Ordinal)
    {
        "DateTime.Now",
        "DateTime.UtcNow",
        "DateTimeOffset.Now",
        "DateTimeOffset.UtcNow",
        "System.DateTime.Now",
        "System.DateTime.UtcNow",
        "System.DateTimeOffset.Now",
        "System.DateTimeOffset.UtcNow"
    };

    public bool RequiresSemanticModel => false;

    public IEnumerable<AntiPatternViolation> Detect(SyntaxTree tree, SemanticModel? model, CancellationToken ct)
    {
        var root = tree.GetRoot(ct);
        var filePath = tree.FilePath;

        foreach (var memberAccess in root.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
        {
            ct.ThrowIfCancellationRequested();

            var fullText = memberAccess.ToString();
            if (ForbiddenAccesses.Contains(fullText))
            {
                var line = memberAccess.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                yield return new AntiPatternViolation(
                    "AP004",
                    AntiPatternSeverity.Warning,
                    $"Direct use of {fullText} — makes code untestable",
                    filePath,
                    line,
                    "Inject TimeProvider or IClock instead");
            }
        }
    }
}
