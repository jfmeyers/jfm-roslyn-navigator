using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RoslynLens.Analyzers;

/// <summary>
/// GR-SYNC-EF: Detects synchronous SaveChanges() calls on DbContext-derived types.
/// Always use SaveChangesAsync() with a CancellationToken.
/// </summary>
public sealed class SynchronousSaveChangesDetector : IAntiPatternDetector
{
    public bool RequiresSemanticModel => true;

    public IEnumerable<AntiPatternViolation> Detect(SyntaxTree tree, SemanticModel? model, CancellationToken ct)
    {
        if (model is null)
            yield break;

        var root = tree.GetRoot(ct);
        var filePath = tree.FilePath;

        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            ct.ThrowIfCancellationRequested();

            if (invocation.Expression is not MemberAccessExpressionSyntax access)
                continue;

            if (access.Name.Identifier.Text != "SaveChanges")
                continue;

            // Try to resolve the receiver type
            var receiverType = model.GetTypeInfo(access.Expression, ct).Type;

            if (receiverType is null)
            {
                // Fallback heuristic: check if the receiver name suggests a DbContext
                var receiverText = access.Expression.ToString();
                if (receiverText.Contains("Context", StringComparison.OrdinalIgnoreCase) ||
                    receiverText.Contains("Db", StringComparison.OrdinalIgnoreCase) ||
                    receiverText == "_context" ||
                    receiverText == "_dbContext" ||
                    receiverText == "db")
                {
                    var line = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    yield return CreateViolation(filePath, line);
                }

                continue;
            }

            if (IsDbContextDerived(receiverType))
            {
                var line = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                yield return CreateViolation(filePath, line);
            }
        }
    }

    private static bool IsDbContextDerived(ITypeSymbol type)
    {
        var current = type;
        while (current is not null)
        {
            var fullName = current.ToDisplayString();
            if (fullName == "Microsoft.EntityFrameworkCore.DbContext")
                return true;

            current = current.BaseType;
        }

        return false;
    }

    private static AntiPatternViolation CreateViolation(string? filePath, int line) =>
        new(
            "GR-SYNC-EF",
            AntiPatternSeverity.Warning,
            "Synchronous SaveChanges() — blocks the thread on database I/O",
            filePath,
            line,
            "Use SaveChangesAsync(cancellationToken) instead");
}
