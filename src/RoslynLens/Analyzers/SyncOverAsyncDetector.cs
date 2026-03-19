using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RoslynLens.Analyzers;

/// <summary>
/// AP002: Detects synchronous blocking over async code (.Result, .Wait(), .GetAwaiter().GetResult()).
/// These calls can cause deadlocks in async contexts.
/// </summary>
public sealed class SyncOverAsyncDetector : IAntiPatternDetector
{
    public bool RequiresSemanticModel => false;

    public IEnumerable<AntiPatternViolation> Detect(SyntaxTree tree, SemanticModel? model, CancellationToken ct)
    {
        var root = tree.GetRoot(ct);
        var filePath = tree.FilePath;

        // Detect .Result access
        foreach (var memberAccess in root.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
        {
            ct.ThrowIfCancellationRequested();

            var memberName = memberAccess.Name.Identifier.Text;

            if (memberName == "Result" && memberAccess.Parent is not InvocationExpressionSyntax)
            {
                var line = memberAccess.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                yield return new AntiPatternViolation(
                    "AP002",
                    AntiPatternSeverity.Error,
                    "Synchronous .Result access on a Task — potential deadlock",
                    filePath,
                    line,
                    "Use await instead of .Result");
            }
        }

        // Detect .Wait() and .GetAwaiter().GetResult()
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            ct.ThrowIfCancellationRequested();

            if (invocation.Expression is not MemberAccessExpressionSyntax access)
                continue;

            var methodName = access.Name.Identifier.Text;

            if (methodName == "Wait" && invocation.ArgumentList.Arguments.Count == 0)
            {
                var line = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                yield return new AntiPatternViolation(
                    "AP002",
                    AntiPatternSeverity.Error,
                    "Synchronous .Wait() call on a Task — potential deadlock",
                    filePath,
                    line,
                    "Use await instead of .Wait()");
            }

            if (methodName == "GetResult" &&
                access.Expression is InvocationExpressionSyntax innerInvocation &&
                innerInvocation.Expression is MemberAccessExpressionSyntax innerAccess &&
                innerAccess.Name.Identifier.Text == "GetAwaiter")
            {
                var line = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                yield return new AntiPatternViolation(
                    "AP002",
                    AntiPatternSeverity.Error,
                    "Synchronous .GetAwaiter().GetResult() — potential deadlock",
                    filePath,
                    line,
                    "Use await instead of .GetAwaiter().GetResult()");
            }
        }
    }
}
