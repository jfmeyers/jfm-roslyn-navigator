using Microsoft.CodeAnalysis;

namespace RoslynLens.Analyzers;

public interface IAntiPatternDetector
{
    bool RequiresSemanticModel { get; }

    IEnumerable<AntiPatternViolation> Detect(SyntaxTree tree, SemanticModel? model, CancellationToken ct);
}
