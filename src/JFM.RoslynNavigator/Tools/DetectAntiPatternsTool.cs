using System.ComponentModel;
using System.Text.Json;
using JFM.RoslynNavigator.Analyzers;
using JFM.RoslynNavigator.Responses;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Server;

namespace JFM.RoslynNavigator;

[McpServerToolType]
public static class DetectAntiPatternsTool
{
    [McpServerTool(Name = "detect_antipatterns")]
    [Description("Runs registered anti-pattern detectors on syntax trees and returns violations with suggestions.")]
    public static async Task<string> ExecuteAsync(
        WorkspaceManager workspace,
        IEnumerable<IAntiPatternDetector> detectors,
        [Description("Optional file path to limit analysis to a single file")] string? file = null,
        [Description("Optional project name filter")] string? projectFilter = null,
        [Description("Minimum severity filter: 'error', 'warning', or 'info' (default 'warning')")] string severity = "warning",
        [Description("Maximum number of results to return (default 100)")] int maxResults = 100,
        CancellationToken ct = default)
    {
        var status = workspace.EnsureReadyOrStatus(ct);
        if (status is not null) return status;

        var solution = workspace.GetSolution();
        if (solution is null)
            return JsonSerializer.Serialize(new { error = "No solution loaded" });

        var detectorList = detectors.ToList();
        if (detectorList.Count == 0)
            return JsonSerializer.Serialize(new AntiPatternsResult([], 0));

        var violations = new List<AntiPatternEntry>();

        var projects = solution.Projects
            .Where(p => projectFilter is null || p.Name.Contains(projectFilter, StringComparison.OrdinalIgnoreCase));

        foreach (var project in projects)
        {
            ct.ThrowIfCancellationRequested();
            if (violations.Count >= maxResults) break;

            var compilation = await workspace.GetCompilationAsync(project, ct);
            if (compilation is null) continue;

            foreach (var tree in compilation.SyntaxTrees)
            {
                ct.ThrowIfCancellationRequested();
                if (violations.Count >= maxResults) break;

                // File filter
                if (file is not null)
                {
                    var normalizedFile = file.Replace('\\', '/');
                    var treePath = tree.FilePath?.Replace('\\', '/');
                    if (treePath is null || !treePath.EndsWith(normalizedFile, StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                var model = compilation.GetSemanticModel(tree);

                foreach (var detector in detectorList)
                {
                    if (violations.Count >= maxResults) break;

                    var results = detector.Detect(tree, model, ct);

                    foreach (var violation in results)
                    {
                        if (violations.Count >= maxResults) break;

                        var severityStr = violation.Severity.ToString();
                        if (!PassesSeverityFilter(severityStr, severity))
                            continue;

                        violations.Add(new AntiPatternEntry(
                            violation.Id,
                            severityStr,
                            violation.Message,
                            violation.File ?? tree.FilePath,
                            violation.Line,
                            violation.Suggestion));
                    }
                }
            }
        }

        var result = new AntiPatternsResult(violations, violations.Count);
        return JsonSerializer.Serialize(result);
    }

    private static bool PassesSeverityFilter(string violationSeverity, string minSeverity)
    {
        var violationLevel = SeverityLevel(violationSeverity);
        var minLevel = SeverityLevel(minSeverity);
        return violationLevel >= minLevel;
    }

    private static int SeverityLevel(string severity) =>
        severity.ToLowerInvariant() switch
        {
            "error" => 3,
            "warning" => 2,
            "info" => 1,
            _ => 0
        };
}
