using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RoslynLens.Analyzers;

/// <summary>
/// GR-BADREQ: Detects TypedResults.BadRequest() invocations.
/// Granit convention requires RFC 7807 Problem Details via TypedResults.Problem().
/// </summary>
public sealed class TypedResultsBadRequestDetector : IAntiPatternDetector
{
    public bool RequiresSemanticModel => false;

    public IEnumerable<AntiPatternViolation> Detect(SyntaxTree tree, SemanticModel? model, CancellationToken ct)
    {
        var root = tree.GetRoot(ct);
        var filePath = tree.FilePath;

        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            ct.ThrowIfCancellationRequested();

            if (invocation.Expression is not MemberAccessExpressionSyntax access)
                continue;

            if (access.Name.Identifier.Text != "BadRequest")
                continue;

            var receiverText = access.Expression.ToString();
            if (receiverText is "TypedResults" or "Results")
            {
                var line = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                yield return new AntiPatternViolation(
                    "GR-BADREQ",
                    AntiPatternSeverity.Warning,
                    $"{receiverText}.BadRequest() does not produce RFC 7807 Problem Details",
                    filePath,
                    line,
                    "Use TypedResults.Problem(detail, statusCode: 400) for RFC 7807 compliance");
            }
        }
    }
}
