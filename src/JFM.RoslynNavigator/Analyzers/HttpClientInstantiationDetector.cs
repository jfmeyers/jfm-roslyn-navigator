using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace JFM.RoslynNavigator.Analyzers;

/// <summary>
/// AP003: Detects direct instantiation of HttpClient via new HttpClient().
/// HttpClient should be managed by IHttpClientFactory to avoid socket exhaustion.
/// </summary>
public sealed class HttpClientInstantiationDetector : IAntiPatternDetector
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
            if (typeName is "HttpClient" or "System.Net.Http.HttpClient")
            {
                var line = creation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                yield return new AntiPatternViolation(
                    "AP003",
                    AntiPatternSeverity.Warning,
                    "Direct HttpClient instantiation — causes socket exhaustion under load",
                    filePath,
                    line,
                    "Use IHttpClientFactory instead");
            }
        }

        // Also check implicit new: HttpClient client = new(...)
        foreach (var implicitNew in root.DescendantNodes().OfType<ImplicitObjectCreationExpressionSyntax>())
        {
            ct.ThrowIfCancellationRequested();

            if (implicitNew.Parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax declaration } })
            {
                var typeName = declaration.Type.ToString();
                if (typeName is "HttpClient" or "System.Net.Http.HttpClient")
                {
                    var line = implicitNew.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    yield return new AntiPatternViolation(
                        "AP003",
                        AntiPatternSeverity.Warning,
                        "Direct HttpClient instantiation — causes socket exhaustion under load",
                        filePath,
                        line,
                        "Use IHttpClientFactory instead");
                }
            }
        }
    }
}
