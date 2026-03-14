using System.ComponentModel;
using System.Text.Json;
using JFM.RoslynNavigator.Responses;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModelContextProtocol.Server;

namespace JFM.RoslynNavigator;

[McpServerToolType]
public static class GetTestCoverageMapTool
{
    [McpServerTool(Name = "get_test_coverage_map")]
    [Description("Maps production types to their test classes using naming conventions (e.g., Foo -> FooTests). Identifies types without test coverage.")]
    public static async Task<string> ExecuteAsync(
        WorkspaceManager workspace,
        [Description("Optional project name filter (matches production project name)")] string? projectFilter = null,
        [Description("Maximum number of results to return (default 100)")] int maxResults = 100,
        CancellationToken ct = default)
    {
        var status = workspace.EnsureReadyOrStatus(ct);
        if (status is not null) return status;

        var solution = workspace.GetSolution();
        if (solution is null)
            return JsonSerializer.Serialize(new { error = "No solution loaded" });

        var testProjects = solution.Projects
            .Where(p => IsTestProject(p.Name))
            .ToList();

        var productionProjects = solution.Projects
            .Where(p => !IsTestProject(p.Name))
            .Where(p => projectFilter is null || p.Name.Equals(projectFilter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Build a lookup of test class names -> (file, project)
        var testClassMap = new Dictionary<string, (string? File, string Project)>(StringComparer.OrdinalIgnoreCase);

        foreach (var testProject in testProjects)
        {
            ct.ThrowIfCancellationRequested();

            var compilation = await workspace.GetCompilationAsync(testProject, ct);
            if (compilation is null) continue;

            foreach (var tree in compilation.SyntaxTrees)
            {
                var root = await tree.GetRootAsync(ct);
                var typeDecls = root.DescendantNodes().OfType<TypeDeclarationSyntax>();

                foreach (var typeDecl in typeDecls)
                {
                    var typeName = typeDecl.Identifier.Text;
                    testClassMap.TryAdd(typeName, (tree.FilePath, testProject.Name));
                }
            }
        }

        // Match production types to test classes
        var entries = new List<CoverageEntry>();
        var covered = 0;
        var uncovered = 0;

        foreach (var project in productionProjects)
        {
            ct.ThrowIfCancellationRequested();
            if (entries.Count >= maxResults) break;

            var compilation = await workspace.GetCompilationAsync(project, ct);
            if (compilation is null) continue;

            foreach (var tree in compilation.SyntaxTrees)
            {
                if (entries.Count >= maxResults) break;

                var root = await tree.GetRootAsync(ct);
                var typeDecls = root.DescendantNodes().OfType<TypeDeclarationSyntax>();

                foreach (var typeDecl in typeDecls)
                {
                    if (entries.Count >= maxResults) break;

                    var typeName = typeDecl.Identifier.Text;
                    var matchedTest = FindTestClass(typeName, testClassMap);

                    if (matchedTest is not null)
                    {
                        entries.Add(new CoverageEntry(
                            typeName,
                            matchedTest.Value.TestName,
                            matchedTest.Value.File,
                            "covered"));
                        covered++;
                    }
                    else
                    {
                        entries.Add(new CoverageEntry(typeName, null, null, "uncovered"));
                        uncovered++;
                    }
                }
            }
        }

        var result = new TestCoverageMapResult(entries, entries.Count, covered, uncovered);
        return JsonSerializer.Serialize(result);
    }

    private static bool IsTestProject(string projectName) =>
        projectName.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase)
        || projectName.EndsWith(".Test", StringComparison.OrdinalIgnoreCase)
        || projectName.EndsWith(".UnitTests", StringComparison.OrdinalIgnoreCase)
        || projectName.EndsWith(".IntegrationTests", StringComparison.OrdinalIgnoreCase);

    private static (string TestName, string? File)? FindTestClass(
        string productionTypeName,
        Dictionary<string, (string? File, string Project)> testClassMap)
    {
        // Try common naming patterns
        string[] candidates =
        [
            $"{productionTypeName}Tests",
            $"{productionTypeName}Test",
            $"{productionTypeName}_Tests",
            $"{productionTypeName}_Test",
            $"{productionTypeName}Specs",
            $"{productionTypeName}Spec"
        ];

        foreach (var candidate in candidates)
        {
            if (testClassMap.TryGetValue(candidate, out var match))
                return (candidate, match.File);
        }

        return null;
    }
}
