using System.Security.Cryptography;
using System.Text;
using RoslynLens.Responses;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RoslynLens;

/// <summary>
/// Detects structurally similar code by normalizing ASTs and comparing fingerprints.
/// </summary>
public static class DuplicateCodeDetector
{
    public static async Task<List<DuplicateGroup>> FindDuplicatesAsync(
        WorkspaceManager workspace,
        string? projectFilter,
        int minStatements,
        int maxResults,
        CancellationToken ct)
    {
        var solution = workspace.GetSolution();
        if (solution is null) return [];

        var projects = solution.Projects;
        if (projectFilter is not null)
        {
            projects = projects.Where(p =>
                p.Name.Equals(projectFilter, StringComparison.OrdinalIgnoreCase));
        }

        // Collect all method fingerprints
        var methods = new List<(string Fingerprint, DuplicateEntry Entry, int StatementCount)>();

        foreach (var project in projects)
        {
            ct.ThrowIfCancellationRequested();

            var compilation = await workspace.GetCompilationAsync(project, ct);
            if (compilation is null) continue;

            foreach (var tree in compilation.SyntaxTrees)
            {
                ct.ThrowIfCancellationRequested();
                var root = await tree.GetRootAsync(ct);

                foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    var body = method.Body;
                    if (body is null || body.Statements.Count < minStatements) continue;

                    var normalized = NormalizeBody(body);
                    var fingerprint = ComputeFingerprint(normalized);
                    var containingType = method.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault()?.Identifier.Text;
                    var line = method.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

                    methods.Add((fingerprint, new DuplicateEntry(
                        method.Identifier.Text, containingType, tree.FilePath, line),
                        body.Statements.Count));
                }
            }
        }

        // Group by fingerprint
        var groups = methods
            .GroupBy(m => m.Fingerprint)
            .Where(g => g.Count() > 1)
            .Take(maxResults)
            .Select(g => new DuplicateGroup(
                g.Select(m => m.Entry).ToList(),
                1.0f, // exact structural match
                g.First().StatementCount))
            .ToList();

        return groups;
    }

    internal static string NormalizeBody(BlockSyntax body)
    {
        var sb = new StringBuilder();
        NormalizeNode(body, sb);
        return sb.ToString();
    }

    private static void NormalizeNode(SyntaxNode node, StringBuilder sb)
    {
        switch (node)
        {
            case IdentifierNameSyntax:
                sb.Append("ID ");
                break;
            case LiteralExpressionSyntax:
                sb.Append("LIT ");
                break;
            case PredefinedTypeSyntax:
                sb.Append("TYPE ");
                break;
            default:
                sb.Append(node.Kind().ToString());
                sb.Append(' ');
                foreach (var child in node.ChildNodes())
                {
                    NormalizeNode(child, sb);
                }
                break;
        }
    }

    private static string ComputeFingerprint(string normalized)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexStringLower(bytes);
    }
}
