using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RoslynLens.Analyzers;

/// <summary>
/// AP008: Detects public async methods that do not accept a CancellationToken parameter.
/// Public async APIs should always support cancellation.
/// </summary>
public sealed class MissingCancellationTokenDetector : IAntiPatternDetector
{
    public bool RequiresSemanticModel => true;

    public IEnumerable<AntiPatternViolation> Detect(SyntaxTree tree, SemanticModel? model, CancellationToken ct)
    {
        if (model is null)
            yield break;

        var root = tree.GetRoot(ct);
        var filePath = tree.FilePath;

        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            ct.ThrowIfCancellationRequested();

            if (!method.Modifiers.Any(SyntaxKind.AsyncKeyword))
                continue;

            if (!method.Modifiers.Any(SyntaxKind.PublicKeyword))
                continue;

            if (!HasCancellationTokenParameter(method, model, ct))
            {
                var line = method.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                yield return new AntiPatternViolation(
                    "AP008",
                    AntiPatternSeverity.Warning,
                    $"Public async method '{method.Identifier.Text}' missing CancellationToken parameter",
                    filePath,
                    line,
                    "Add CancellationToken as the last parameter");
            }
        }
    }

    private static bool HasCancellationTokenParameter(
        MethodDeclarationSyntax method, SemanticModel model, CancellationToken ct)
    {
        foreach (var param in method.ParameterList.Parameters)
        {
            var paramSymbol = model.GetDeclaredSymbol(param, ct);
            if (paramSymbol?.Type is INamedTypeSymbol paramType &&
                paramType.Name == "CancellationToken" &&
                paramType.ContainingNamespace?.ToDisplayString() == "System.Threading")
            {
                return true;
            }

            var typeName = param.Type?.ToString() ?? string.Empty;
            if (typeName is "CancellationToken" or "System.Threading.CancellationToken")
            {
                return true;
            }
        }

        return false;
    }
}
