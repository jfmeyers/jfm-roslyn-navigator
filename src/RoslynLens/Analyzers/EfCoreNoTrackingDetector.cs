using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RoslynLens.Analyzers;

/// <summary>
/// AP009: Detects EF Core LINQ queries that materialize without AsNoTracking().
/// Read-only queries should use AsNoTracking() for better performance.
/// This is a heuristic detector — it looks for terminal LINQ methods without a prior AsNoTracking() in the chain.
/// </summary>
public sealed class EfCoreNoTrackingDetector : IAntiPatternDetector
{
    private static readonly HashSet<string> TerminalMethods = new(StringComparer.Ordinal)
    {
        "ToListAsync",
        "ToArrayAsync",
        "FirstAsync",
        "FirstOrDefaultAsync",
        "SingleAsync",
        "SingleOrDefaultAsync",
        "CountAsync",
        "AnyAsync",
        "AllAsync",
        "ToList",
        "ToArray",
        "First",
        "FirstOrDefault",
        "Single",
        "SingleOrDefault"
    };

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

            var methodName = access.Name.Identifier.Text;
            if (!TerminalMethods.Contains(methodName))
                continue;

            // Walk up the invocation chain to check for AsNoTracking
            if (HasAsNoTrackingInChain(access.Expression))
                continue;

            // Check if the chain originates from a DbSet-like access
            if (!LooksLikeEfCoreQuery(access.Expression, model, ct))
                continue;

            var line = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            yield return new AntiPatternViolation(
                "AP009",
                AntiPatternSeverity.Info,
                $"EF Core query materialized via {methodName}() without AsNoTracking()",
                filePath,
                line,
                "Add .AsNoTracking() for read-only queries to improve performance");
        }
    }

    private static bool HasAsNoTrackingInChain(ExpressionSyntax expression)
    {
        var current = expression;
        while (current is InvocationExpressionSyntax innerInvocation)
        {
            if (innerInvocation.Expression is MemberAccessExpressionSyntax innerAccess)
            {
                if (innerAccess.Name.Identifier.Text is "AsNoTracking" or "AsNoTrackingWithIdentityResolution")
                    return true;

                current = innerAccess.Expression;
            }
            else
            {
                break;
            }
        }

        // Also check direct member access without invocation
        if (current is MemberAccessExpressionSyntax ma &&
            ma.Name.Identifier.Text is "AsNoTracking" or "AsNoTrackingWithIdentityResolution")
        {
            return true;
        }

        return false;
    }

    private static bool LooksLikeEfCoreQuery(ExpressionSyntax expression, SemanticModel model, CancellationToken ct)
    {
        // Walk to the root of the chain
        var current = expression;
        while (current is InvocationExpressionSyntax inv)
        {
            if (inv.Expression is MemberAccessExpressionSyntax ma)
                current = ma.Expression;
            else
                break;
        }

        if (current is MemberAccessExpressionSyntax rootAccess)
            current = rootAccess;

        // Try to resolve the type of the root expression
        var typeInfo = model.GetTypeInfo(current, ct);
        var typeName = typeInfo.Type?.ToDisplayString() ?? string.Empty;

        // Heuristic: DbSet<T>, IQueryable<T> from EF context
        return typeName.Contains("DbSet", StringComparison.Ordinal) ||
               typeName.Contains("IQueryable", StringComparison.Ordinal);
    }
}
