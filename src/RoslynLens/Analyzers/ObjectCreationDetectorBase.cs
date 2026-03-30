using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RoslynLens.Analyzers;

/// <summary>
/// Base class for detectors that match object creation expressions
/// (e.g. new Regex(), new HttpClient()).
/// </summary>
public abstract class ObjectCreationDetectorBase : IAntiPatternDetector
{
    public bool RequiresSemanticModel => false;

    protected abstract IReadOnlyList<string> TargetTypeNames { get; }
    protected abstract string Id { get; }
    protected abstract string Message { get; }
    protected abstract string Suggestion { get; }

    public virtual IEnumerable<AntiPatternViolation> Detect(SyntaxTree tree, SemanticModel? model, CancellationToken ct)
    {
        var root = tree.GetRoot(ct);
        var filePath = tree.FilePath;

        foreach (var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
        {
            ct.ThrowIfCancellationRequested();

            var typeName = creation.Type.ToString();
            if (IsTargetType(typeName))
            {
                var line = creation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                yield return new AntiPatternViolation(
                    Id, AntiPatternSeverity.Warning, Message, filePath, line, Suggestion);
            }
        }
    }

    protected bool IsTargetType(string typeName)
    {
        foreach (var target in TargetTypeNames)
        {
            if (string.Equals(typeName, target, StringComparison.Ordinal))
                return true;
        }
        return false;
    }
}
