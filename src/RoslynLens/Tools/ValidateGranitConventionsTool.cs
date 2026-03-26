using System.ComponentModel;
using RoslynLens.Analyzers;
using RoslynLens.Responses;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModelContextProtocol.Server;

namespace RoslynLens.Tools;

[McpServerToolType]
public static class ValidateGranitConventionsTool
{
    private const string CategoryNaming = "naming";
    private const string CategorySecurity = "security";
    private const string CategoryEfcore = "efcore";
    private const string CategoryDependencies = "dependencies";

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

            await AnalyzeCompilationAsync(compilation, file, checkCategory, violations, ct);
        }

        var byCategory = violations
            .GroupBy(v => v.Category)
            .ToDictionary(g => g.Key, g => g.Count());

        var result = new GranitConventionsResult(violations, violations.Count, byCategory);
        return Json.Serialize(result);
    }

    private static async Task AnalyzeCompilationAsync(
        Compilation compilation,
        string? file,
        string checkCategory,
        List<ConventionViolation> violations,
        CancellationToken ct)
    {
        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            ct.ThrowIfCancellationRequested();

            if (!MatchesFileFilter(syntaxTree, file))
                continue;

            AnalyzeSyntaxTree(syntaxTree, compilation, checkCategory, violations, ct);
            await AnalyzeStructuralConventionsAsync(syntaxTree, checkCategory, violations, ct);
        }
    }

    private static bool MatchesFileFilter(SyntaxTree syntaxTree, string? file)
    {
        if (file is null) return true;

        return syntaxTree.FilePath.Equals(file, StringComparison.OrdinalIgnoreCase) ||
               syntaxTree.FilePath.EndsWith(file, StringComparison.OrdinalIgnoreCase);
    }

    private static void AnalyzeSyntaxTree(
        SyntaxTree syntaxTree,
        Compilation compilation,
        string checkCategory,
        List<ConventionViolation> violations,
        CancellationToken ct)
    {
        SemanticModel? model = null;

        foreach (var detector in GetDetectorsForCategory(checkCategory))
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
    }

    private static async Task AnalyzeStructuralConventionsAsync(
        SyntaxTree syntaxTree,
        string checkCategory,
        List<ConventionViolation> violations,
        CancellationToken ct)
    {
        if (checkCategory is "all" or CategoryNaming)
        {
            violations.AddRange(CheckEndpointPrefixConventions(syntaxTree, ct));
        }

        if (checkCategory is "all" or CategoryEfcore)
        {
            violations.AddRange(CheckApplyGranitConventions(syntaxTree, ct));
        }

        if (checkCategory is "all" or CategoryDependencies)
        {
            violations.AddRange(await CheckDependsOnConventions(syntaxTree, ct));
        }
    }

    private static IAntiPatternDetector[] GetDetectorsForCategory(string category) =>
        category.ToLowerInvariant() switch
        {
            CategoryNaming => NamingDetectors,
            CategorySecurity => SecurityDetectors,
            CategoryEfcore => EfCoreDetectors,
            CategoryDependencies => [], // Handled by structural checks
            _ => AllGranitDetectors
        };

    private static string CategorizeViolation(string id) =>
        id switch
        {
            "GR-DTO" => CategoryNaming,
            "GR-SECRET" => CategorySecurity,
            "GR-SYNC-EF" or "AP009" => CategoryEfcore,
            "GR-CFGAWAIT" => "async",
            "GR-GUID" or "GR-BADREQ" or "GR-REGEX" or "GR-SLEEP" or "GR-CONSOLE" => "conventions",
            _ => "other"
        };

    private static IEnumerable<ConventionViolation> CheckEndpointPrefixConventions(
        SyntaxTree tree, CancellationToken ct)
    {
        var root = tree.GetRoot(ct);
        var filePath = tree.FilePath;

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

            var matchedPrefix = genericPrefixes.FirstOrDefault(p => name == p + "Request" || name == p + "Response");
            if (matchedPrefix is not null)
            {
                var line = typeDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                yield return new ConventionViolation(
                    CategoryNaming,
                    "GR-ENDPOINT-PREFIX",
                    "Warning",
                    $"Endpoint DTO '{name}' uses a generic name — OpenAPI flattens namespaces causing schema conflicts",
                    filePath,
                    line,
                    $"Prefix with module context (e.g. 'Workflow{name}' instead of '{name}')");
            }
        }
    }

    private static IEnumerable<ConventionViolation> CheckApplyGranitConventions(
        SyntaxTree tree, CancellationToken ct)
    {
        var root = tree.GetRoot(ct);
        var filePath = tree.FilePath;

        foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            ct.ThrowIfCancellationRequested();

            if (!IsDbContextClass(classDecl))
                continue;

            var onModelCreating = classDecl.Members
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.Text == "OnModelCreating");

            if (onModelCreating is null)
                continue;

            var methodBody = onModelCreating.Body?.ToString() ??
                             onModelCreating.ExpressionBody?.ToString() ??
                             string.Empty;

            if (!methodBody.Contains("ApplyGranitConventions", StringComparison.Ordinal))
            {
                var line = onModelCreating.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                yield return new ConventionViolation(
                    CategoryEfcore,
                    "GR-CONVENTIONS-MISSING",
                    "Error",
                    $"DbContext '{classDecl.Identifier.Text}' OnModelCreating does not call ApplyGranitConventions()",
                    filePath,
                    line,
                    "Add modelBuilder.ApplyGranitConventions(currentTenant, dataFilter) at the end of OnModelCreating");
            }

            if (methodBody.Contains("HasQueryFilter", StringComparison.Ordinal))
            {
                var line = onModelCreating.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                yield return new ConventionViolation(
                    CategoryEfcore,
                    "GR-MANUAL-QUERYFILTER",
                    "Warning",
                    $"DbContext '{classDecl.Identifier.Text}' uses manual HasQueryFilter — conflicts with ApplyGranitConventions",
                    filePath,
                    line,
                    "Remove manual HasQueryFilter calls; ApplyGranitConventions handles all standard filters");
            }
        }
    }

    private static bool IsDbContextClass(ClassDeclarationSyntax classDecl)
    {
        if (classDecl.BaseList is null) return false;

        return classDecl.BaseList.Types.Any(t =>
            t.Type.ToString().Contains("DbContext", StringComparison.Ordinal));
    }

    private static async Task<IEnumerable<ConventionViolation>> CheckDependsOnConventions(
        SyntaxTree tree, CancellationToken ct)
    {
        var root = await tree.GetRootAsync(ct);
        var filePath = tree.FilePath;
        var violations = new List<ConventionViolation>();

        foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            ct.ThrowIfCancellationRequested();

            if (!IsModuleClass(classDecl))
                continue;

            ValidateModuleDependsOn(classDecl, filePath, violations);
        }

        return violations;
    }

    private static bool IsModuleClass(ClassDeclarationSyntax classDecl)
    {
        if (!classDecl.Identifier.Text.EndsWith("Module", StringComparison.Ordinal))
            return false;

        if (classDecl.BaseList is null)
            return false;

        return classDecl.BaseList.Types.Any(t =>
            t.Type.ToString().Contains("Module", StringComparison.Ordinal));
    }

    private static void ValidateModuleDependsOn(
        ClassDeclarationSyntax classDecl, string? filePath, List<ConventionViolation> violations)
    {
        var className = classDecl.Identifier.Text;
        var dependsOnTypes = ExtractDependsOnTypes(classDecl);

        if (dependsOnTypes.Any(t => t.Contains("GranitCoreModule", StringComparison.Ordinal)))
        {
            var line = classDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            violations.Add(new ConventionViolation(
                CategoryDependencies,
                "GR-DEPS-CORE",
                "Warning",
                $"Module '{className}' declares DependsOn(GranitCoreModule) — Granit.Core is implicit",
                filePath,
                line,
                "Remove DependsOn(typeof(GranitCoreModule)); it is always available"));
        }

        var dependsOnList = dependsOnTypes.ToList();
        var sorted = dependsOnList.OrderBy(d => d, StringComparer.Ordinal).ToList();
        if (!dependsOnList.SequenceEqual(sorted))
        {
            var line = classDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            violations.Add(new ConventionViolation(
                CategoryDependencies,
                "GR-DEPS-ORDER",
                "Info",
                $"Module '{className}' DependsOn entries are not in alphabetical order",
                filePath,
                line,
                "Sort DependsOn entries alphabetically by module name"));
        }
    }

    private static HashSet<string> ExtractDependsOnTypes(ClassDeclarationSyntax classDecl)
    {
        var types = new HashSet<string>(StringComparer.Ordinal);

        var typeOfArgs = classDecl.AttributeLists
            .SelectMany(al => al.Attributes)
            .Where(a => a.Name.ToString() is "DependsOn" or "DependsOnAttribute")
            .Where(a => a.ArgumentList is not null)
            .SelectMany(a => a.ArgumentList!.Arguments)
            .Select(arg => arg.Expression)
            .OfType<TypeOfExpressionSyntax>();

        foreach (var typeOf in typeOfArgs)
        {
            types.Add(typeOf.Type.ToString());
        }

        return types;
    }
}
