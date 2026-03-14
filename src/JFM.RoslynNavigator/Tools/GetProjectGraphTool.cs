using System.ComponentModel;
using System.Text.Json;
using System.Xml.Linq;
using JFM.RoslynNavigator.Responses;
using ModelContextProtocol.Server;

namespace JFM.RoslynNavigator;

[McpServerToolType]
public static class GetProjectGraphTool
{
    [McpServerTool(Name = "get_project_graph")]
    [Description("Returns the project dependency graph for the loaded solution, including target frameworks.")]
    public static Task<string> ExecuteAsync(
        WorkspaceManager workspace,
        CancellationToken ct = default)
    {
        var status = workspace.EnsureReadyOrStatus(ct);
        if (status is not null) return Task.FromResult(status);

        var solution = workspace.GetSolution();
        if (solution is null)
            return Task.FromResult(JsonSerializer.Serialize(new { error = "No solution loaded" }));

        var nodes = new List<ProjectNode>();

        foreach (var project in solution.Projects)
        {
            ct.ThrowIfCancellationRequested();

            var references = project.ProjectReferences
                .Select(r => solution.GetProject(r.ProjectId)?.Name)
                .Where(n => n is not null)
                .Cast<string>()
                .ToList();

            var framework = ReadTargetFramework(project.FilePath);

            nodes.Add(new ProjectNode(project.Name, framework, references));
        }

        var result = new ProjectGraphResult(nodes, nodes.Count);
        return Task.FromResult(JsonSerializer.Serialize(result));
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
