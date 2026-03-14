using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace JFM.RoslynNavigator.Analyzers;

/// <summary>
/// GR-CFGAWAIT: Detects await expressions without .ConfigureAwait(false) in library code.
/// Library code must use ConfigureAwait(false) to avoid deadlocks when called from synchronization contexts.
/// Skips projects with names ending in .Tests.
/// </summary>
public sealed class MissingConfigureAwaitDetector : IAntiPatternDetector
{
    public bool RequiresSemanticModel => true;

    public IEnumerable<AntiPatternViolation> Detect(SyntaxTree tree, SemanticModel? model, CancellationToken ct)
    {
        if (model is null)
            yield break;

        // Skip test projects
        var assemblyName = model.Compilation.AssemblyName ?? string.Empty;
        if (assemblyName.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase) ||
            assemblyName.EndsWith(".Test", StringComparison.OrdinalIgnoreCase) ||
            assemblyName.Contains(".Tests.", StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        var root = tree.GetRoot(ct);
        var filePath = tree.FilePath;

        foreach (var awaitExpr in root.DescendantNodes().OfType<AwaitExpressionSyntax>())
        {
            ct.ThrowIfCancellationRequested();

            // Check if the awaited expression ends with .ConfigureAwait(...)
            if (HasConfigureAwait(awaitExpr.Expression))
                continue;

            var line = awaitExpr.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            yield return new AntiPatternViolation(
                "GR-CFGAWAIT",
                AntiPatternSeverity.Info,
                "await without ConfigureAwait(false) in library code",
                filePath,
                line,
                "Add .ConfigureAwait(false) to avoid synchronization context deadlocks");
        }
    }

    private static bool HasConfigureAwait(ExpressionSyntax expression)
    {
        // The expression should be an invocation of .ConfigureAwait(...)
        if (expression is InvocationExpressionSyntax invocation &&
            invocation.Expression is MemberAccessExpressionSyntax access &&
            access.Name.Identifier.Text == "ConfigureAwait")
        {
            return true;
        }

        return false;
    }
}
