using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace JFM.RoslynNavigator.Analyzers;

/// <summary>
/// GR-REGEX: Detects new Regex() instantiation.
/// Granit convention requires [GeneratedRegex] for compile-time source generation.
/// </summary>
public sealed class NewRegexDetector : IAntiPatternDetector
{
    public bool RequiresSemanticModel => false;

    public IEnumerable<AntiPatternViolation> Detect(SyntaxTree tree, SemanticModel? model, CancellationToken ct)
    {
        var root = tree.GetRoot(ct);
        var filePath = tree.FilePath;

        foreach (var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
        {
            ct.ThrowIfCancellationRequested();

            var typeName = creation.Type.ToString();
            if (typeName is "Regex" or "System.Text.RegularExpressions.Regex")
            {
                var line = creation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                yield return new AntiPatternViolation(
                    "GR-REGEX",
                    AntiPatternSeverity.Warning,
                    "new Regex() uses runtime compilation — slower and not AOT-friendly",
                    filePath,
                    line,
                    "Use [GeneratedRegex] attribute instead");
            }
        }
    }
}
