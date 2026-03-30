using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RoslynLens.Analyzers;

/// <summary>
/// AP003: Detects direct instantiation of HttpClient via new HttpClient().
/// HttpClient should be managed by IHttpClientFactory to avoid socket exhaustion.
/// </summary>
public sealed class HttpClientInstantiationDetector : ObjectCreationDetectorBase
{
    protected override IReadOnlyList<string> TargetTypeNames { get; } =
        ["HttpClient", "System.Net.Http.HttpClient"];

    protected override string Id => "AP003";
    protected override string Message => "Direct HttpClient instantiation — causes socket exhaustion under load";
    protected override string Suggestion => "Use IHttpClientFactory instead";

    public override IEnumerable<AntiPatternViolation> Detect(SyntaxTree tree, SemanticModel? model, CancellationToken ct)
    {
        foreach (var violation in base.Detect(tree, model, ct))
            yield return violation;

        // Also check implicit new: HttpClient client = new(...)
        var root = tree.GetRoot(ct);
        var filePath = tree.FilePath;

        foreach (var implicitNew in root.DescendantNodes().OfType<ImplicitObjectCreationExpressionSyntax>())
        {
            ct.ThrowIfCancellationRequested();

            if (implicitNew.Parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax declaration } }
                && IsTargetType(declaration.Type.ToString()))
            {
                var line = implicitNew.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                yield return new AntiPatternViolation(
                    Id, AntiPatternSeverity.Warning, Message, filePath, line, Suggestion);
            }
        }
    }
}
