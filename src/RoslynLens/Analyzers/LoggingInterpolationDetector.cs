using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RoslynLens.Analyzers;

/// <summary>
/// AP006: Detects string interpolation used as argument to logging methods.
/// Interpolated strings bypass structured logging and are always evaluated, even if the log level is disabled.
/// </summary>
public sealed class LoggingInterpolationDetector : IAntiPatternDetector
{
    private static readonly HashSet<string> LogMethodNames = new(StringComparer.Ordinal)
    {
        "Log",
        "LogTrace",
        "LogDebug",
        "LogInformation",
        "LogWarning",
        "LogError",
        "LogCritical"
    };

    public bool RequiresSemanticModel => false;

    public IEnumerable<AntiPatternViolation> Detect(SyntaxTree tree, SemanticModel? model, CancellationToken ct)
    {
        var root = tree.GetRoot(ct);
        var filePath = tree.FilePath;

        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            ct.ThrowIfCancellationRequested();

            var methodName = invocation.Expression switch
            {
                MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text,
                IdentifierNameSyntax id => id.Identifier.Text,
                _ => null
            };

            if (methodName is null || !LogMethodNames.Contains(methodName))
                continue;

            var interpolatedArg = invocation.ArgumentList.Arguments
                .FirstOrDefault(a => a.Expression is InterpolatedStringExpressionSyntax);

            if (interpolatedArg is not null)
            {
                var line = interpolatedArg.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                yield return new AntiPatternViolation(
                    "AP006",
                    AntiPatternSeverity.Warning,
                    $"String interpolation in {methodName}() — bypasses structured logging",
                    filePath,
                    line,
                    "Use message template with named placeholders: Log(\"User {UserId} logged in\", userId)");
            }
        }
    }
}
