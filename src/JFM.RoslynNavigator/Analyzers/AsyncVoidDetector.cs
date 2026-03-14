using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace JFM.RoslynNavigator.Analyzers;

/// <summary>
/// AP001: Detects async void methods (except event handlers).
/// async void methods swallow exceptions and cannot be awaited.
/// </summary>
public sealed class AsyncVoidDetector : IAntiPatternDetector
{
    public bool RequiresSemanticModel => false;

    public IEnumerable<AntiPatternViolation> Detect(SyntaxTree tree, SemanticModel? model, CancellationToken ct)
    {
        var root = tree.GetRoot(ct);
        var filePath = tree.FilePath;

        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            ct.ThrowIfCancellationRequested();

            if (!method.Modifiers.Any(SyntaxKind.AsyncKeyword))
                continue;

            if (method.ReturnType is not PredefinedTypeSyntax predefined)
                continue;

            if (!predefined.Keyword.IsKind(SyntaxKind.VoidKeyword))
                continue;

            // Skip event handlers: methods with a parameter whose type name contains "EventHandler"
            if (IsEventHandler(method))
                continue;

            var line = method.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

            yield return new AntiPatternViolation(
                "AP001",
                AntiPatternSeverity.Error,
                $"async void method '{method.Identifier.Text}' — exceptions will be unobserved and the call cannot be awaited",
                filePath,
                line,
                "Return Task instead of void");
        }
    }

    private static bool IsEventHandler(MethodDeclarationSyntax method)
    {
        foreach (var param in method.ParameterList.Parameters)
        {
            var typeName = param.Type?.ToString() ?? string.Empty;
            if (typeName.Contains("EventHandler", StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
