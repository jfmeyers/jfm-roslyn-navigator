using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace JFM.RoslynNavigator.Analyzers;

/// <summary>
/// GR-CONSOLE: Detects Console.Write() and Console.WriteLine() calls.
/// Production code should use structured logging via ILogger.
/// </summary>
public sealed class ConsoleWriteDetector : IAntiPatternDetector
{
    private static readonly HashSet<string> ForbiddenCalls = new(StringComparer.Ordinal)
    {
        "Console.Write",
        "Console.WriteLine",
        "System.Console.Write",
        "System.Console.WriteLine"
    };

    public bool RequiresSemanticModel => false;

    public IEnumerable<AntiPatternViolation> Detect(SyntaxTree tree, SemanticModel? model, CancellationToken ct)
    {
        var root = tree.GetRoot(ct);
        var filePath = tree.FilePath;

        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            ct.ThrowIfCancellationRequested();

            var text = invocation.Expression.ToString();
            if (ForbiddenCalls.Contains(text))
            {
                var line = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                yield return new AntiPatternViolation(
                    "GR-CONSOLE",
                    AntiPatternSeverity.Warning,
                    $"{text}() in production code — output is unstructured and not captured by observability",
                    filePath,
                    line,
                    "Use ILogger instead");
            }
        }
    }
}
