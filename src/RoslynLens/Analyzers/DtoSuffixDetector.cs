using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RoslynLens.Analyzers;

/// <summary>
/// GR-DTO: Detects class/record declarations ending with "Dto" suffix.
/// Granit convention uses *Request for input and *Response for output DTOs.
/// </summary>
public sealed class DtoSuffixDetector : IAntiPatternDetector
{
    public bool RequiresSemanticModel => false;

    public IEnumerable<AntiPatternViolation> Detect(SyntaxTree tree, SemanticModel? model, CancellationToken ct)
    {
        var root = tree.GetRoot(ct);
        var filePath = tree.FilePath;

        foreach (var typeDecl in root.DescendantNodes())
        {
            ct.ThrowIfCancellationRequested();

            string? name = typeDecl switch
            {
                ClassDeclarationSyntax cls => cls.Identifier.Text,
                RecordDeclarationSyntax rec => rec.Identifier.Text,
                _ => null
            };

            if (name is null)
                continue;

            if (!name.EndsWith("Dto", StringComparison.Ordinal) &&
                !name.EndsWith("DTO", StringComparison.Ordinal))
                continue;

            var line = typeDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            var kind = typeDecl is RecordDeclarationSyntax ? "record" : "class";

            yield return new AntiPatternViolation(
                "GR-DTO",
                AntiPatternSeverity.Warning,
                $"{kind} '{name}' uses Dto suffix — violates Granit naming convention",
                filePath,
                line,
                "Use *Request or *Response suffix instead");
        }
    }
}
