using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RoslynLens;

/// <summary>
/// Computes code complexity metrics from Roslyn syntax nodes.
/// </summary>
public static class ComplexityAnalyzer
{
    public static int CalculateCyclomaticComplexity(SyntaxNode node)
    {
        var complexity = 1; // base path

        foreach (var descendant in node.DescendantNodes())
        {
            complexity += descendant switch
            {
                IfStatementSyntax => 1,
                WhileStatementSyntax => 1,
                ForStatementSyntax => 1,
                ForEachStatementSyntax => 1,
                CaseSwitchLabelSyntax => 1,
                CasePatternSwitchLabelSyntax => 1,
                ConditionalExpressionSyntax => 1,
                CatchClauseSyntax => 1,
                BinaryExpressionSyntax bin when bin.IsKind(SyntaxKind.LogicalAndExpression)
                    || bin.IsKind(SyntaxKind.LogicalOrExpression)
                    || bin.IsKind(SyntaxKind.CoalesceExpression) => 1,
                SwitchExpressionArmSyntax => 1,
                _ => 0
            };
        }

        return complexity;
    }

    public static int CalculateCognitiveComplexity(SyntaxNode node)
    {
        return CalculateCognitiveRecursive(node, 0);
    }

    private static int CalculateCognitiveRecursive(SyntaxNode node, int nesting)
    {
        var total = 0;

        foreach (var child in node.ChildNodes())
        {
            switch (child)
            {
                case IfStatementSyntax ifStmt:
                    total += ScoreIfStatement(ifStmt, nesting);
                    break;

                case WhileStatementSyntax:
                case ForStatementSyntax:
                case ForEachStatementSyntax:
                case DoStatementSyntax:
                case SwitchStatementSyntax:
                case SwitchExpressionSyntax:
                case CatchClauseSyntax:
                case ConditionalExpressionSyntax:
                    total += 1 + nesting;
                    total += CalculateCognitiveRecursive(child, nesting + 1);
                    break;

                case BinaryExpressionSyntax bin when bin.IsKind(SyntaxKind.LogicalAndExpression)
                    || bin.IsKind(SyntaxKind.LogicalOrExpression):
                    total += 1;
                    total += CalculateCognitiveRecursive(child, nesting);
                    break;

                case LambdaExpressionSyntax:
                    total += CalculateCognitiveRecursive(child, nesting + 1);
                    break;

                default:
                    total += CalculateCognitiveRecursive(child, nesting);
                    break;
            }
        }

        return total;
    }

    private static int ScoreIfStatement(IfStatementSyntax ifStmt, int nesting)
    {
        var score = 1 + nesting;
        if (ifStmt.Statement is not null)
            score += CalculateCognitiveRecursive(ifStmt.Statement, nesting + 1);
        if (ifStmt.Else is null) return score;

        score += 1;
        score += ifStmt.Else.Statement is IfStatementSyntax
            ? CalculateCognitiveRecursive(ifStmt.Else, nesting)
            : CalculateCognitiveRecursive(ifStmt.Else, nesting + 1);
        return score;
    }

    public static int CalculateMaxNestingDepth(SyntaxNode node)
    {
        return CalculateNestingRecursive(node, 0);
    }

    private static int CalculateNestingRecursive(SyntaxNode node, int currentDepth)
    {
        var maxDepth = currentDepth;

        foreach (var child in node.ChildNodes())
        {
            var isNesting = child is BlockSyntax && child.Parent is (
                IfStatementSyntax or ElseClauseSyntax or
                WhileStatementSyntax or ForStatementSyntax or
                ForEachStatementSyntax or DoStatementSyntax or
                TryStatementSyntax or CatchClauseSyntax or FinallyClauseSyntax or
                SwitchStatementSyntax or LockStatementSyntax or UsingStatementSyntax);

            var childDepth = isNesting
                ? CalculateNestingRecursive(child, currentDepth + 1)
                : CalculateNestingRecursive(child, currentDepth);

            maxDepth = Math.Max(maxDepth, childDepth);
        }

        return maxDepth;
    }

    public static int CalculateLogicalLoc(SyntaxNode node)
    {
        var text = node.GetText();
        var count = 0;

        foreach (var line in text.Lines)
        {
            var lineText = line.ToString().Trim();
            if (lineText.Length == 0) continue;
            if (lineText.StartsWith("//")) continue;
            count++;
        }

        return count;
    }

    public static Responses.ComplexityMetrics Analyze(SyntaxNode node) =>
        new(
            CalculateCyclomaticComplexity(node),
            CalculateCognitiveComplexity(node),
            CalculateMaxNestingDepth(node),
            CalculateLogicalLoc(node));
}
