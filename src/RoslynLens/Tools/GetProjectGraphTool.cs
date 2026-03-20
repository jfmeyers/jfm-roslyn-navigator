using System.ComponentModel;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using RoslynLens.Responses;
using ModelContextProtocol.Server;

namespace RoslynLens;

[McpServerToolType]
public static class GetProjectGraphTool
{
    [McpServerTool(Name = "get_project_graph")]
    [Description("Returns the project dependency graph for the loaded solution, including target frameworks. Use projectFilter for large solutions.")]
    public static Task<string> ExecuteAsync(
        WorkspaceManager workspace,
        [Description("Optional project name filter (comma-separated, supports substring match e.g. 'Granit.AI,Granit.Core')")] string? projectFilter = null,
        [Description("Include transitive dependencies of filtered projects (default false)")] bool includeTransitive = false,
        [Description("Maximum number of projects to return (default 50)")] int maxResults = 50,
        CancellationToken ct = default)
    {
        var status = workspace.EnsureReadyOrStatus(ct);
        if (status is not null) return Task.FromResult(status);

        var solution = workspace.GetSolution();
        if (solution is null)
            return Task.FromResult(JsonSerializer.Serialize(new { error = "No solution loaded" }));

        var filters = projectFilter?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var allProjects = solution.Projects.ToList();
        var matchingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (filters is not null)
        {
            foreach (var project in allProjects)
            {
                if (filters.Any(f => project.Name.Contains(f, StringComparison.OrdinalIgnoreCase)))
                    matchingNames.Add(project.Name);
            }

            if (includeTransitive)
                ExpandTransitiveDependencies(solution, allProjects, matchingNames);
        }

        var nodes = new List<ProjectNode>();

        foreach (var project in allProjects)
        {
            ct.ThrowIfCancellationRequested();

            if (filters is not null && !matchingNames.Contains(project.Name))
                continue;

            var references = project.ProjectReferences
                .Select(r => solution.GetProject(r.ProjectId)?.Name)
                .Where(n => n is not null)
                .Cast<string>()
                .ToList();

            var framework = ReadTargetFramework(project.FilePath);

            nodes.Add(new ProjectNode(project.Name, framework, references));

            if (nodes.Count >= maxResults)
                break;
        }

        var totalMatching = filters is not null ? matchingNames.Count : allProjects.Count;
        var result = new ProjectGraphResult(nodes, totalMatching);
        return Task.FromResult(JsonSerializer.Serialize(result));
    }

    private static void ExpandTransitiveDependencies(
        Solution solution,
        List<Project> allProjects,
        HashSet<string> names)
    {
        var projectByName = allProjects.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>(names);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!projectByName.TryGetValue(current, out var project))
                continue;

            foreach (var refId in project.ProjectReferences)
            {
                var refProject = solution.GetProject(refId.ProjectId);
                if (refProject is not null && names.Add(refProject.Name))
                    queue.Enqueue(refProject.Name);
            }
        }
    }

    private static string? ReadTargetFramework(string? projectFilePath)
    {
        if (projectFilePath is null || !File.Exists(projectFilePath))
            return null;

        try
        {
            var doc = XDocument.Load(projectFilePath);
            return doc.Descendants("TargetFramework").FirstOrDefault()?.Value
                ?? doc.Descendants("TargetFrameworks").FirstOrDefault()?.Value;
        }
        catch
        {
            return null;
        }
    }
}
