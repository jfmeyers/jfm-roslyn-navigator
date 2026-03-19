using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RoslynLens.Analyzers;

/// <summary>
/// GR-GUID: Detects Guid.NewGuid() calls.
/// Granit uses IGuidGenerator.Create() for sequential GUIDs optimized for clustered indexes.
/// </summary>
public sealed class GuidNewGuidDetector : IAntiPatternDetector
{
    public bool RequiresSemanticModel => false;

    public IEnumerable<AntiPatternViolation> Detect(SyntaxTree tree, SemanticModel? model, CancellationToken ct)
    {
        var root = tree.GetRoot(ct);
        var filePath = tree.FilePath;

        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            ct.ThrowIfCancellationRequested();

            var text = invocation.Expression.ToString();
            if (text is "Guid.NewGuid" or "System.Guid.NewGuid")
            {
                var line = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                yield return new AntiPatternViolation(
                    "GR-GUID",
                    AntiPatternSeverity.Warning,
                    "Guid.NewGuid() produces random GUIDs — causes index fragmentation",
                    filePath,
                    line,
                    "Use IGuidGenerator.Create() for sequential GUIDs");
            }
        }
    }
}
