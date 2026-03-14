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

        return DetectInVariableDeclarations(root, filePath, ct)
            .Concat(DetectInAssignments(root, filePath, ct))
            .Concat(DetectInObjectInitializers(root, filePath, ct));
    }

    private static IEnumerable<AntiPatternViolation> DetectInVariableDeclarations(
        SyntaxNode root, string? filePath, CancellationToken ct)
    {
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

            if (IsPlaceholderOrEmpty(literal.Token.ValueText))
                continue;

            var line = declarator.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            yield return CreateViolation(name, filePath, line);
        }
    }

    private static IEnumerable<AntiPatternViolation> DetectInAssignments(
        SyntaxNode root, string? filePath, CancellationToken ct)
    {
        foreach (var assignment in root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            ct.ThrowIfCancellationRequested();

            if (assignment.Right is not LiteralExpressionSyntax literal)
                continue;

            if (!literal.IsKind(SyntaxKind.StringLiteralExpression))
                continue;

            var name = GetAssignmentTargetName(assignment);
            if (name is null || !IsSensitiveName(name))
                continue;

            if (IsPlaceholderOrEmpty(literal.Token.ValueText))
                continue;

            var line = assignment.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            yield return CreateViolation(name, filePath, line);
        }
    }

    private static IEnumerable<AntiPatternViolation> DetectInObjectInitializers(
        SyntaxNode root, string? filePath, CancellationToken ct)
    {
        foreach (var initializer in root.DescendantNodes().OfType<InitializerExpressionSyntax>())
        {
            ct.ThrowIfCancellationRequested();

            foreach (var propAssign in initializer.Expressions.OfType<AssignmentExpressionSyntax>())
            {
                var violation = CheckInitializerAssignment(propAssign, filePath);
                if (violation is not null)
                    yield return violation;
            }
        }
    }

    private static AntiPatternViolation? CheckInitializerAssignment(
        AssignmentExpressionSyntax propAssign, string? filePath)
    {
        if (propAssign.Right is not LiteralExpressionSyntax literal)
            return null;

        if (!literal.IsKind(SyntaxKind.StringLiteralExpression))
            return null;

        var name = propAssign.Left is IdentifierNameSyntax id ? id.Identifier.Text : null;
        if (name is null || !IsSensitiveName(name))
            return null;

        if (IsPlaceholderOrEmpty(literal.Token.ValueText))
            return null;

        var line = propAssign.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        return CreateViolation(name, filePath, line);
    }

    private static string? GetAssignmentTargetName(AssignmentExpressionSyntax assignment) =>
        assignment.Left switch
        {
            IdentifierNameSyntax id => id.Identifier.Text,
            MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text,
            _ => null
        };

    private static bool IsSensitiveName(string name)
    {
        var lower = name.ToLowerInvariant();
        return SensitiveNames.Any(sensitive => lower.Contains(sensitive, StringComparison.Ordinal));
    }

    private static bool IsPlaceholderOrEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ||
        value.StartsWith("${", StringComparison.Ordinal) ||
        value.StartsWith('{') ||
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
