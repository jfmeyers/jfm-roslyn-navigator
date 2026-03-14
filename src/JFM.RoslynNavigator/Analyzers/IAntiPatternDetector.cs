using Microsoft.CodeAnalysis;

namespace JFM.RoslynNavigator.Analyzers;

public interface IAntiPatternDetector
{
    bool RequiresSemanticModel { get; }

    IEnumerable<AntiPatternViolation> Detect(SyntaxTree tree, SemanticModel? model, CancellationToken ct);
}
