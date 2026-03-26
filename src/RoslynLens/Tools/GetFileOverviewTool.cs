using System.ComponentModel;
using RoslynLens.Analyzers;
using RoslynLens.Responses;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModelContextProtocol.Server;

namespace RoslynLens;

[McpServerToolType]
public static class GetFileOverviewTool
{
    [McpServerTool(Name = "get_file_overview")]
    [Description("Compound tool: returns all types in a file, compiler diagnostics, and anti-pattern violations in a single call.")]
    public static async Task<string> ExecuteAsync(
        WorkspaceManager workspace,
        IEnumerable<IAntiPatternDetector> detectors,
        [Description("File path (or suffix) to analyze")] string filePath,
        CancellationToken ct = default)
    {
        var status = workspace.EnsureReadyOrStatus(ct);
        if (status is not null) return status;

        var solution = workspace.GetSolution();
        if (solution is null)
            return Json.Serialize(new { error = "No solution loaded" });

        var normalizedPath = filePath.Replace('\\', '/');

        // Find the document
        Document? document = null;
        foreach (var project in solution.Projects)
        {
            document = project.Documents.FirstOrDefault(d =>
                d.FilePath?.Replace('\\', '/').EndsWith(normalizedPath, StringComparison.OrdinalIgnoreCase) == true);
            if (document is not null) break;
        }

        if (document is null)
            return Json.Serialize(new { error = $"File '{filePath}' not found in solution" });

        var project2 = document.Project;
        var compilation = await workspace.GetCompilationAsync(project2, ct);
        if (compilation is null)
            return Json.Serialize(new { error = "Could not compile project" });

        var tree = await document.GetSyntaxTreeAsync(ct);
        if (tree is null)
            return Json.Serialize(new { error = "Could not get syntax tree" });

        var root = await tree.GetRootAsync(ct);

        // Types in file
        var types = root.DescendantNodes()
            .OfType<BaseTypeDeclarationSyntax>()
            .Select(t =>
            {
                var kind = t switch
                {
                    ClassDeclarationSyntax => "class",
                    InterfaceDeclarationSyntax => "interface",
                    StructDeclarationSyntax => "struct",
                    RecordDeclarationSyntax r => r.ClassOrStructKeyword.Text == "struct" ? "record struct" : "record",
                    EnumDeclarationSyntax => "enum",
                    _ => "type"
                };
                var line = t.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                return new FileTypeEntry(t.Identifier.Text, kind, line);
            }).ToList();

        // Diagnostics
        var diags = compilation.GetDiagnostics(ct)
            .Where(d => d.Location.SourceTree?.FilePath == tree.FilePath)
            .Where(d => d.Severity >= DiagnosticSeverity.Warning)
            .Select(d => new DiagnosticInfo(
                d.Id, d.Severity.ToString(), d.GetMessage(),
                d.Location.SourceTree?.FilePath,
                d.Location.GetLineSpan().StartLinePosition.Line + 1))
            .Take(50)
            .ToList();
        var diagnostics = new DiagnosticsResult(diags, diags.Count, "file");

        // Anti-patterns
        var violations = new List<AntiPatternEntry>();
        var detectorList = detectors.ToList();
        DetectAntiPatternsTool.AnalyzeCompilation(
            compilation, normalizedPath, detectorList, "info", 100, violations, ct);
        var antiPatterns = new AntiPatternsResult(violations, violations.Count);

        var result = new FileOverview(document.FilePath ?? filePath, types, diagnostics, antiPatterns);
        return Json.Serialize(result);
    }
}
