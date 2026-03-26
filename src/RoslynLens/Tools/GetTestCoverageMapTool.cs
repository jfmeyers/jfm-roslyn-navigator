using System.ComponentModel;
using RoslynLens.Responses;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModelContextProtocol.Server;

namespace RoslynLens;

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
            return Json.Serialize(new { error = "No solution loaded" });

        var testClassMap = await BuildTestClassMapAsync(workspace, solution, ct);

        var productionProjects = solution.Projects
            .Where(p => !IsTestProject(p.Name))
            .Where(p => projectFilter is null || p.Name.Equals(projectFilter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var (entries, covered, uncovered) = await MatchProductionTypesAsync(
            workspace, productionProjects, testClassMap, maxResults, ct);

        var result = new TestCoverageMapResult(entries, entries.Count, covered, uncovered);
        return Json.Serialize(result);
    }

    private static async Task<Dictionary<string, (string? File, string Project)>> BuildTestClassMapAsync(
        WorkspaceManager workspace, Solution solution, CancellationToken ct)
    {
        var testClassMap = new Dictionary<string, (string? File, string Project)>(StringComparer.OrdinalIgnoreCase);

        foreach (var testProject in solution.Projects.Where(p => IsTestProject(p.Name)))
        {
            ct.ThrowIfCancellationRequested();

            var compilation = await workspace.GetCompilationAsync(testProject, ct);
            if (compilation is null) continue;

            foreach (var tree in compilation.SyntaxTrees)
            {
                var root = await tree.GetRootAsync(ct);

                foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
                {
                    testClassMap.TryAdd(typeDecl.Identifier.Text, (tree.FilePath, testProject.Name));
                }
            }
        }

        return testClassMap;
    }

    private static async Task<(List<CoverageEntry> Entries, int Covered, int Uncovered)> MatchProductionTypesAsync(
        WorkspaceManager workspace,
        List<Project> productionProjects,
        Dictionary<string, (string? File, string Project)> testClassMap,
        int maxResults,
        CancellationToken ct)
    {
        var entries = new List<CoverageEntry>();
        var covered = 0;
        var uncovered = 0;

        foreach (var project in productionProjects)
        {
            ct.ThrowIfCancellationRequested();
            if (entries.Count >= maxResults) break;

            var compilation = await workspace.GetCompilationAsync(project, ct);
            if (compilation is null) continue;

            MatchTypesInCompilation(compilation, testClassMap, maxResults, entries, ref covered, ref uncovered, ct);
        }

        return (entries, covered, uncovered);
    }

    private static void MatchTypesInCompilation(
        Compilation compilation,
        Dictionary<string, (string? File, string Project)> testClassMap,
        int maxResults,
        List<CoverageEntry> entries,
        ref int covered,
        ref int uncovered,
        CancellationToken ct)
    {
        foreach (var tree in compilation.SyntaxTrees)
        {
            if (entries.Count >= maxResults) break;

            var root = tree.GetRoot(ct);

            foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (entries.Count >= maxResults) break;

                var typeName = typeDecl.Identifier.Text;
                var matchedTest = FindTestClass(typeName, testClassMap);

                if (matchedTest is not null)
                {
                    entries.Add(new CoverageEntry(typeName, matchedTest.Value.TestName, matchedTest.Value.File, "covered"));
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

    private static bool IsTestProject(string projectName) =>
        projectName.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase)
        || projectName.EndsWith(".Test", StringComparison.OrdinalIgnoreCase)
        || projectName.EndsWith(".UnitTests", StringComparison.OrdinalIgnoreCase)
        || projectName.EndsWith(".IntegrationTests", StringComparison.OrdinalIgnoreCase);

    private static (string TestName, string? File)? FindTestClass(
        string productionTypeName,
        Dictionary<string, (string? File, string Project)> testClassMap)
    {
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
