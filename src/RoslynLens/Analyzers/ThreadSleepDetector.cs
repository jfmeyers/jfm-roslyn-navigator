using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RoslynLens.Analyzers;

/// <summary>
/// GR-SLEEP: Detects Thread.Sleep() calls.
/// Thread.Sleep blocks the thread; prefer async alternatives.
/// </summary>
public sealed class ThreadSleepDetector : IAntiPatternDetector
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
            if (text is "Thread.Sleep" or "System.Threading.Thread.Sleep")
            {
                var line = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                yield return new AntiPatternViolation(
                    "GR-SLEEP",
                    AntiPatternSeverity.Warning,
                    "Thread.Sleep() blocks the current thread",
                    filePath,
                    line,
                    "Use Task.Delay() or TimeProvider");
            }
        }
    }
}
