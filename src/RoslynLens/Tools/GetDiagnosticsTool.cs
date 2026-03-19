using System.ComponentModel;
using System.Text.Json;
using RoslynLens.Responses;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Server;

namespace RoslynLens;

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

        return scope.ToLowerInvariant() switch
        {
            "file" => await AnalyzeFileScopeAsync(workspace, solution, path, minSeverity, ct),
            "project" => await AnalyzeProjectScopeAsync(workspace, solution, path, minSeverity, ct),
            _ => await AnalyzeSolutionScopeAsync(workspace, solution, minSeverity, ct)
        };
    }

    private static async Task<string> AnalyzeFileScopeAsync(
        WorkspaceManager workspace, Solution solution, string? path,
        DiagnosticSeverity minSeverity, CancellationToken ct)
    {
        if (path is null)
            return JsonSerializer.Serialize(new { error = "Path is required for file scope" });

        var diagnostics = new List<DiagnosticInfo>();
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

        return SerializeResult(diagnostics, "file");
    }

    private static async Task<string> AnalyzeProjectScopeAsync(
        WorkspaceManager workspace, Solution solution, string? path,
        DiagnosticSeverity minSeverity, CancellationToken ct)
    {
        if (path is null)
            return JsonSerializer.Serialize(new { error = "Path is required for project scope" });

        var project = solution.Projects
            .FirstOrDefault(p => p.Name.Equals(path, StringComparison.OrdinalIgnoreCase)
                || (p.FilePath?.Replace('\\', '/').EndsWith(path.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase) == true));

        if (project is null)
            return JsonSerializer.Serialize(new { error = $"Project '{path}' not found" });

        var diagnostics = new List<DiagnosticInfo>();
        var compilation = await workspace.GetCompilationAsync(project, ct);
        if (compilation is not null)
            CollectDiagnostics(compilation.GetDiagnostics(ct), minSeverity, diagnostics);

        return SerializeResult(diagnostics, "project");
    }

    private static async Task<string> AnalyzeSolutionScopeAsync(
        WorkspaceManager workspace, Solution solution,
        DiagnosticSeverity minSeverity, CancellationToken ct)
    {
        var diagnostics = new List<DiagnosticInfo>();

        foreach (var project in solution.Projects)
        {
            ct.ThrowIfCancellationRequested();
            var compilation = await workspace.GetCompilationAsync(project, ct);
            if (compilation is not null)
                CollectDiagnostics(compilation.GetDiagnostics(ct), minSeverity, diagnostics);
        }

        return SerializeResult(diagnostics, "solution");
    }

    private static string SerializeResult(List<DiagnosticInfo> diagnostics, string scope)
    {
        var result = new DiagnosticsResult(diagnostics, diagnostics.Count, scope);
        return JsonSerializer.Serialize(result);
    }

    private static void CollectDiagnostics(
        IEnumerable<Diagnostic> source,
        DiagnosticSeverity minSeverity,
        List<DiagnosticInfo> target)
    {
        foreach (var diag in source.Where(d => d.Severity >= minSeverity))
        {
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
