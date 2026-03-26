using System.ComponentModel;
using RoslynLens.Responses;
using ModelContextProtocol.Server;

namespace RoslynLens;

[McpServerToolType]
public static class DetectDuplicatesTool
{
    [McpServerTool(Name = "detect_duplicates")]
    [Description("Detect structurally similar code blocks across the solution using AST fingerprinting. Returns groups of duplicate methods.")]
    public static async Task<string> ExecuteAsync(
        WorkspaceManager workspace,
        [Description("Optional project name to scope analysis")] string? projectFilter = null,
        [Description("Minimum number of statements in a method to consider (default 5)")] int minStatements = 5,
        [Description("Maximum number of duplicate groups to return (default 20)")] int maxResults = 20,
        CancellationToken ct = default)
    {
        var status = workspace.EnsureReadyOrStatus(ct);
        if (status is not null) return status;

        var groups = await DuplicateCodeDetector.FindDuplicatesAsync(
            workspace, projectFilter, minStatements, maxResults, ct);

        var totalDuplicates = groups.Sum(g => g.Occurrences.Count);
        var result = new DuplicatesResult(groups, groups.Count, totalDuplicates);
        return Json.Serialize(result);
    }
}
