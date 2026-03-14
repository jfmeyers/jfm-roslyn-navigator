using System.ComponentModel;
using System.Text.Json;
using JFM.RoslynNavigator.Analyzers;
using JFM.RoslynNavigator.Responses;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModelContextProtocol.Server;

namespace JFM.RoslynNavigator.Tools;

[McpServerToolType]
public static class ValidateGranitConventionsTool
{
    private static readonly IAntiPatternDetector[] NamingDetectors =
    [
        new DtoSuffixDetector()
    ];

    private static readonly IAntiPatternDetector[] SecurityDetectors =
    [
        new HardcodedSecretDetector()
    ];

    private static readonly IAntiPatternDetector[] EfCoreDetectors =
    [
        new SynchronousSaveChangesDetector(),
        new EfCoreNoTrackingDetector()
    ];

    private static readonly IAntiPatternDetector[] AllGranitDetectors =
    [
        new GuidNewGuidDetector(),
        new HardcodedSecretDetector(),
        new SynchronousSaveChangesDetector(),
        new TypedResultsBadRequestDetector(),
        new NewRegexDetector(),
        new ThreadSleepDetector(),
        new ConsoleWriteDetector(),
        new MissingConfigureAwaitDetector(),
        new DtoSuffixDetector()
    ];

    [McpServerTool(Name = "validate_granit_conventions")]
    [Description("Validate Granit framework conventions across the solution. Checks naming, security, EF Core patterns, and module dependency conventions. Returns violations grouped by category.")]
    public static async Task<string> ExecuteAsync(
        WorkspaceManager workspace,
        [Description("Optional project name filter (only check matching projects)")] string? projectFilter = null,
        [Description("Optional file path filter (only check this specific file)")] string? file = null,
        [Description("Check category: 'all', 'naming', 'security', 'efcore', 'dependencies'")] string checkCategory = "all",
        CancellationToken ct = default)
    {
        var status = workspace.EnsureReadyOrStatus(ct);
        if (status is not null) return status;

        var violations = new List<ConventionViolation>();
        var compilations = await workspace.GetAllCompilationsAsync(ct);

        foreach (var (project, compilation) in compilations)
        {
            ct.ThrowIfCancellationRequested();

            if (projectFilter is not null &&
                !project.Name.Contains(projectFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var syntaxTree in compilation.SyntaxTrees)
            {
                ct.ThrowIfCancellationRequested();

                if (file is not null &&
                    !syntaxTree.FilePath.Equals(file, StringComparison.OrdinalIgnoreCase) &&
                    !syntaxTree.FilePath.EndsWith(file, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                SemanticModel? model = null;

                var detectors = GetDetectorsForCategory(checkCategory);
                foreach (var detector in detectors)
                {
                    if (detector.RequiresSemanticModel)
                    {
                        model ??= compilation.GetSemanticModel(syntaxTree);
                    }

                    foreach (var violation in detector.Detect(syntaxTree, model, ct))
                    {
                        var category = CategorizeViolation(violation.Id);
                        violations.Add(new ConventionViolation(
                            category,
                            violation.Id,
                            violation.Severity.ToString(),
                            violation.Message,
                            violation.File,
                            violation.Line,
                            violation.Suggestion));
                    }
                }

                // Category-specific structural checks
                if (checkCategory is "all" or "naming")
                {
                    violations.AddRange(CheckEndpointPrefixConventions(syntaxTree, ct));
                }

                if (checkCategory is "all" or "efcore")
                {
                    violations.AddRange(CheckApplyGranitConventions(syntaxTree, ct));
                }

                if (checkCategory is "all" or "dependencies")
                {
                    violations.AddRange(await CheckDependsOnConventions(syntaxTree, project, compilation, ct));
                }
            }
        }

        var byCategory = violations
            .GroupBy(v => v.Category)
            .ToDictionary(g => g.Key, g => g.Count());

        var result = new GranitConventionsResult(violations, violations.Count, byCategory);
        return JsonSerializer.Serialize(result);
    }

    private static IEnumerable<IAntiPatternDetector> GetDetectorsForCategory(string category) =>
        category.ToLowerInvariant() switch
        {
            "naming" => NamingDetectors,
            "security" => SecurityDetectors,
            "efcore" => EfCoreDetectors,
            "dependencies" => [], // Handled by structural checks
            _ => AllGranitDetectors
        };

    private static string CategorizeViolation(string id) =>
        id switch
        {
            "GR-DTO" => "naming",
            "GR-SECRET" => "security",
            "GR-SYNC-EF" or "AP009" => "efcore",
            "GR-CFGAWAIT" => "async",
            "GR-GUID" or "GR-BADREQ" or "GR-REGEX" or "GR-SLEEP" or "GR-CONSOLE" => "conventions",
            _ => "other"
        };

    private static IEnumerable<ConventionViolation> CheckEndpointPrefixConventions(
        SyntaxTree tree, CancellationToken ct)
    {
        var root = tree.GetRoot(ct);
        var filePath = tree.FilePath;

        // Check for endpoint DTO types that lack a module-context prefix
        // Generic names like "CreateRequest", "UpdateRequest", "DeleteRequest" without prefix
        var genericPrefixes = new[] { "Create", "Update", "Delete", "Get", "List", "Search" };

        foreach (var typeDecl in root.DescendantNodes())
        {
            ct.ThrowIfCancellationRequested();

            string? name = typeDecl switch
            {
                ClassDeclarationSyntax cls => cls.Identifier.Text,
                RecordDeclarationSyntax rec => rec.Identifier.Text,
                _ => null
            };

            if (name is null)
                continue;

            if (!name.EndsWith("Request", StringComparison.Ordinal) &&
                !name.EndsWith("Response", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var prefix in genericPrefixes)
            {
                if (name == prefix + "Request" || name == prefix + "Response")
                {
                    var line = typeDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    yield return new ConventionViolation(
                        "naming",
                        "GR-ENDPOINT-PREFIX",
                        "Warning",
                        $"Endpoint DTO '{name}' uses a generic name — OpenAPI flattens namespaces causing schema conflicts",
                        filePath,
                        line,
                        $"Prefix with module context (e.g. 'Workflow{name}' instead of '{name}')");
                }
            }
        }
    }

    private static IEnumerable<ConventionViolation> CheckApplyGranitConventions(
        SyntaxTree tree, CancellationToken ct)
    {
        var root = tree.GetRoot(ct);
        var filePath = tree.FilePath;

        // Find DbContext-derived classes and check for ApplyGranitConventions in OnModelCreating
        foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            ct.ThrowIfCancellationRequested();

            if (classDecl.BaseList is null)
                continue;

            var isDbContext = classDecl.BaseList.Types.Any(t =>
            {
                var typeName = t.Type.ToString();
                return typeName.Contains("DbContext", StringComparison.Ordinal);
            });

            if (!isDbContext)
                continue;

            // Find OnModelCreating method
            var onModelCreating = classDecl.Members
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.Text == "OnModelCreating");

            if (onModelCreating is null)
                continue;

            // Check if ApplyGranitConventions is called
            var methodBody = onModelCreating.Body?.ToString() ??
                             onModelCreating.ExpressionBody?.ToString() ??
                             string.Empty;

            if (!methodBody.Contains("ApplyGranitConventions", StringComparison.Ordinal))
            {
                var line = onModelCreating.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                yield return new ConventionViolation(
                    "efcore",
                    "GR-CONVENTIONS-MISSING",
                    "Error",
                    $"DbContext '{classDecl.Identifier.Text}' OnModelCreating does not call ApplyGranitConventions()",
                    filePath,
                    line,
                    "Add modelBuilder.ApplyGranitConventions(currentTenant, dataFilter) at the end of OnModelCreating");
            }

            // Check for manual HasQueryFilter — conflicts with ApplyGranitConventions
            if (methodBody.Contains("HasQueryFilter", StringComparison.Ordinal))
            {
                var line = onModelCreating.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                yield return new ConventionViolation(
                    "efcore",
                    "GR-MANUAL-QUERYFILTER",
                    "Warning",
                    $"DbContext '{classDecl.Identifier.Text}' uses manual HasQueryFilter — conflicts with ApplyGranitConventions",
                    filePath,
                    line,
                    "Remove manual HasQueryFilter calls; ApplyGranitConventions handles all standard filters");
            }
        }
    }

    private static async Task<IEnumerable<ConventionViolation>> CheckDependsOnConventions(
        SyntaxTree tree, Project project, Compilation compilation, CancellationToken ct)
    {
        var root = await tree.GetRootAsync(ct);
        var filePath = tree.FilePath;
        var violations = new List<ConventionViolation>();

        foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            ct.ThrowIfCancellationRequested();

            var className = classDecl.Identifier.Text;
            if (!className.EndsWith("Module", StringComparison.Ordinal))
                continue;

            if (classDecl.BaseList is null)
                continue;

            var isModule = classDecl.BaseList.Types.Any(t =>
                t.Type.ToString().Contains("Module", StringComparison.Ordinal));

            if (!isModule)
                continue;

            // Extract [DependsOn] types
            var dependsOnTypes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var attrList in classDecl.AttributeLists)
            {
                foreach (var attr in attrList.Attributes)
                {
                    var attrName = attr.Name.ToString();
                    if (attrName is not ("DependsOn" or "DependsOnAttribute"))
                        continue;

                    if (attr.ArgumentList is null)
                        continue;

                    foreach (var arg in attr.ArgumentList.Arguments)
                    {
                        if (arg.Expression is TypeOfExpressionSyntax typeOf)
                        {
                            dependsOnTypes.Add(typeOf.Type.ToString());
                        }
                    }
                }
            }

            // Get project references
            var projectRefs = project.ProjectReferences
                .Select(r =>
                {
                    var solution = compilation.References
                        .OfType<CompilationReference>()
                        .FirstOrDefault(c => c.Compilation.AssemblyName?.Contains(
                            r.ProjectId.Id.ToString(), StringComparison.Ordinal) == true);
                    return solution?.Compilation.AssemblyName;
                })
                .Where(n => n is not null)
                .ToList();

            // Check: Granit.Core should never be in DependsOn (it is implicit)
            if (dependsOnTypes.Any(t => t.Contains("GranitCoreModule", StringComparison.Ordinal)))
            {
                var line = classDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                violations.Add(new ConventionViolation(
                    "dependencies",
                    "GR-DEPS-CORE",
                    "Warning",
                    $"Module '{className}' declares DependsOn(GranitCoreModule) — Granit.Core is implicit",
                    filePath,
                    line,
                    "Remove DependsOn(typeof(GranitCoreModule)); it is always available"));
            }

            // Check alphabetical order of DependsOn entries
            var dependsOnList = dependsOnTypes.ToList();
            var sorted = dependsOnList.OrderBy(d => d, StringComparer.Ordinal).ToList();
            if (!dependsOnList.SequenceEqual(sorted))
            {
                var line = classDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                violations.Add(new ConventionViolation(
                    "dependencies",
                    "GR-DEPS-ORDER",
                    "Info",
                    $"Module '{className}' DependsOn entries are not in alphabetical order",
                    filePath,
                    line,
                    "Sort DependsOn entries alphabetically by module name"));
            }
        }

        return violations;
    }
}
