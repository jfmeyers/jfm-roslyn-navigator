using System.ComponentModel;
using System.Text.Json;
using JFM.RoslynNavigator.Responses;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Server;

namespace JFM.RoslynNavigator;

[McpServerToolType]
public static class GetDiagnosticsTool
{
    [McpServerTool(Name = "get_diagnostics")]
    [Description("Returns compiler and analyzer diagnostics for a file, project, or the entire solution.")]
    public static async Task<string> ExecuteAsync(
        WorkspaceManager workspace,
        [Description("Scope: 'file', 'project', or 'solution' (default 'solution')")] string scope = "solution",
        [Description("File or project path (required for 'file' and 'project' scopes)")] string? path = null,
        [Description("Minimum severity filter: 'error', 'warning', 'info', or 'hidden' (default 'warning')")] string severityFilter = "warning",
        CancellationToken ct = default)
    {
        var status = workspace.EnsureReadyOrStatus(ct);
        if (status is not null) return status;

        var solution = workspace.GetSolution();
        if (solution is null)
            return JsonSerializer.Serialize(new { error = "No solution loaded" });

        var minSeverity = ParseSeverity(severityFilter);
        var diagnostics = new List<DiagnosticInfo>();

        switch (scope.ToLowerInvariant())
        {
            case "file":
            {
                if (path is null)
                    return JsonSerializer.Serialize(new { error = "Path is required for file scope" });

                var normalizedPath = path.Replace('\\', '/');
                foreach (var project in solution.Projects)
                {
                    var doc = project.Documents
                        .FirstOrDefault(d => d.FilePath?.Replace('\\', '/').EndsWith(normalizedPath, StringComparison.OrdinalIgnoreCase) == true);

                    if (doc is null) continue;

                    var compilation = await workspace.GetCompilationAsync(project, ct);
                    if (compilation is null) continue;

                    var tree = await doc.GetSyntaxTreeAsync(ct);
                    if (tree is null) continue;

                    var model = compilation.GetSemanticModel(tree);
                    CollectDiagnostics(model.GetDiagnostics(cancellationToken: ct), minSeverity, diagnostics);
                    break;
                }

                break;
            }
            case "project":
            {
                if (path is null)
                    return JsonSerializer.Serialize(new { error = "Path is required for project scope" });

                var project = solution.Projects
                    .FirstOrDefault(p => p.Name.Equals(path, StringComparison.OrdinalIgnoreCase)
                        || (p.FilePath?.Replace('\\', '/').EndsWith(path.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase) == true));

                if (project is null)
                    return JsonSerializer.Serialize(new { error = $"Project '{path}' not found" });

                var compilation = await workspace.GetCompilationAsync(project, ct);
                if (compilation is not null)
                    CollectDiagnostics(compilation.GetDiagnostics(ct), minSeverity, diagnostics);

                break;
            }
            default: // solution
            {
                foreach (var project in solution.Projects)
                {
                    ct.ThrowIfCancellationRequested();
                    var compilation = await workspace.GetCompilationAsync(project, ct);
                    if (compilation is not null)
                        CollectDiagnostics(compilation.GetDiagnostics(ct), minSeverity, diagnostics);
                }

                break;
            }
        }

        var result = new DiagnosticsResult(diagnostics, diagnostics.Count, scope);
        return JsonSerializer.Serialize(result);
    }

    private static void CollectDiagnostics(
        IEnumerable<Diagnostic> source,
        DiagnosticSeverity minSeverity,
        List<DiagnosticInfo> target)
    {
        foreach (var diag in source)
        {
            if (diag.Severity < minSeverity) continue;

            var lineSpan = diag.Location.GetMappedLineSpan();
            target.Add(new DiagnosticInfo(
                diag.Id,
                diag.Severity.ToString(),
                diag.GetMessage(),
                lineSpan.Path,
                lineSpan.IsValid ? lineSpan.StartLinePosition.Line + 1 : null));
        }
    }

    private static DiagnosticSeverity ParseSeverity(string filter) =>
        filter.ToLowerInvariant() switch
        {
            "error" => DiagnosticSeverity.Error,
            "warning" => DiagnosticSeverity.Warning,
            "info" => DiagnosticSeverity.Info,
            "hidden" => DiagnosticSeverity.Hidden,
            _ => DiagnosticSeverity.Warning
        };
}
