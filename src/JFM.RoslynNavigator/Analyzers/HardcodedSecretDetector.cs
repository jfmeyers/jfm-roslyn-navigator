using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace JFM.RoslynNavigator.Analyzers;

/// <summary>
/// GR-SECRET: Detects hardcoded secrets in string literal assignments.
/// Variables/properties with names containing password, secret, apiKey, connectionString, or token
/// should never contain literal values.
/// </summary>
public sealed class HardcodedSecretDetector : IAntiPatternDetector
{
    private static readonly string[] SensitiveNames =
    [
        "password", "secret", "apikey", "connectionstring", "token"
    ];

    public bool RequiresSemanticModel => false;

    public IEnumerable<AntiPatternViolation> Detect(SyntaxTree tree, SemanticModel? model, CancellationToken ct)
    {
        var root = tree.GetRoot(ct);
        var filePath = tree.FilePath;

        // Check variable declarations: var password = "literal";
        foreach (var declarator in root.DescendantNodes().OfType<VariableDeclaratorSyntax>())
        {
            ct.ThrowIfCancellationRequested();

            if (declarator.Initializer?.Value is not LiteralExpressionSyntax literal)
                continue;

            if (!literal.IsKind(SyntaxKind.StringLiteralExpression))
                continue;

            var name = declarator.Identifier.Text;
            if (!IsSensitiveName(name))
                continue;

            var value = literal.Token.ValueText;
            if (IsPlaceholderOrEmpty(value))
                continue;

            var line = declarator.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            yield return CreateViolation(name, filePath, line);
        }

        // Check property assignments: Password = "literal"
        foreach (var assignment in root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            ct.ThrowIfCancellationRequested();

            if (assignment.Right is not LiteralExpressionSyntax literal)
                continue;

            if (!literal.IsKind(SyntaxKind.StringLiteralExpression))
                continue;

            var name = assignment.Left switch
            {
                IdentifierNameSyntax id => id.Identifier.Text,
                MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text,
                _ => null
            };

            if (name is null || !IsSensitiveName(name))
                continue;

            var value = literal.Token.ValueText;
            if (IsPlaceholderOrEmpty(value))
                continue;

            var line = assignment.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            yield return CreateViolation(name, filePath, line);
        }

        // Check property initializers in object creation
        foreach (var initializer in root.DescendantNodes().OfType<InitializerExpressionSyntax>())
        {
            ct.ThrowIfCancellationRequested();

            foreach (var expr in initializer.Expressions)
            {
                if (expr is not AssignmentExpressionSyntax propAssign)
                    continue;

                if (propAssign.Right is not LiteralExpressionSyntax literal)
                    continue;

                if (!literal.IsKind(SyntaxKind.StringLiteralExpression))
                    continue;

                var name = propAssign.Left switch
                {
                    IdentifierNameSyntax id => id.Identifier.Text,
                    _ => null
                };

                if (name is null || !IsSensitiveName(name))
                    continue;

                var value = literal.Token.ValueText;
                if (IsPlaceholderOrEmpty(value))
                    continue;

                var line = propAssign.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                yield return CreateViolation(name, filePath, line);
            }
        }
    }

    private static bool IsSensitiveName(string name)
    {
        var lower = name.ToLowerInvariant();
        foreach (var sensitive in SensitiveNames)
        {
            if (lower.Contains(sensitive, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool IsPlaceholderOrEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ||
        value.StartsWith("${", StringComparison.Ordinal) ||
        value.StartsWith("{", StringComparison.Ordinal) ||
        value.Equals("CHANGE_ME", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("TODO", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("PLACEHOLDER", StringComparison.OrdinalIgnoreCase);

    private static AntiPatternViolation CreateViolation(string name, string? filePath, int line) =>
        new(
            "GR-SECRET",
            AntiPatternSeverity.Error,
            $"Hardcoded secret in '{name}' — secrets must not be stored in source code",
            filePath,
            line,
            "Use IConfiguration, Vault, or environment variables for secrets");
}
