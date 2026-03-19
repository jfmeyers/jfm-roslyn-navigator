using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RoslynLens.Analyzers;

/// <summary>
/// AP005: Detects broad catch clauses (catch (Exception) or bare catch) without specific exception types.
/// Broad catches hide bugs and make debugging difficult.
/// </summary>
public sealed class BroadCatchDetector : IAntiPatternDetector
{
    public bool RequiresSemanticModel => false;

    public IEnumerable<AntiPatternViolation> Detect(SyntaxTree tree, SemanticModel? model, CancellationToken ct)
    {
        var root = tree.GetRoot(ct);
        var filePath = tree.FilePath;

        foreach (var catchClause in root.DescendantNodes().OfType<CatchClauseSyntax>())
        {
            ct.ThrowIfCancellationRequested();

            var declaration = catchClause.Declaration;
            var line = catchClause.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

            if (declaration is null)
            {
                // Bare catch { } without any type
                yield return new AntiPatternViolation(
                    "AP005",
                    AntiPatternSeverity.Warning,
                    "Bare catch clause without exception type — hides all errors",
                    filePath,
                    line,
                    "Catch specific exception types");
                continue;
            }

            var typeName = declaration.Type.ToString();
            if (typeName is "Exception" or "System.Exception")
            {
                // Check if there is a when clause — that makes it more targeted
                if (catchClause.Filter is not null)
                    continue;

                yield return new AntiPatternViolation(
                    "AP005",
                    AntiPatternSeverity.Warning,
                    "Broad catch (Exception) without filter — catches all exceptions indiscriminately",
                    filePath,
                    line,
                    "Catch specific exception types or add a when filter");
            }
        }
    }
}
