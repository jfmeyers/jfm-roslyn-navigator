using System.ComponentModel;
using RoslynLens.Responses;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using ModelContextProtocol.Server;

namespace RoslynLens;

[McpServerToolType]
public static class FindDeadCodeTool
{
    private static readonly HashSet<string> TestAttributes =
    [
        "Fact", "Theory", "Test", "TestMethod",
        "TestCase", "TestFixture", "TestClass",
        "SetUp", "TearDown", "OneTimeSetUp", "OneTimeTearDown",
        "ClassInitialize", "ClassCleanup",
        "AssemblyInitialize", "AssemblyCleanup",
        "GlobalSetup", "GlobalCleanup", "Benchmark"
    ];

    private static readonly HashSet<string> EntryPointNames =
    [
        "Main", "ConfigureServices", "Configure",
        "CreateHostBuilder", "CreateWebHostBuilder"
    ];

    private sealed record AnalysisContext(
        Solution Solution, string Kind, int MaxResults,
        bool IncludePublicMembers, bool IncludeEntryPoints, string[]? FileFilters);

    [McpServerTool(Name = "find_dead_code")]
    [Description("Finds potentially unreferenced types, methods, and properties that may be dead code.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S107:Methods should not have too many parameters", Justification = "MCP tool parameters are protocol-mandated")]
    public static async Task<string> ExecuteAsync(
        WorkspaceManager workspace,
        [Description("Scope: 'file', 'project', or 'solution' (default 'solution')")] string scope = "solution",
        [Description("File or project path (required for 'file' and 'project' scopes)")] string? path = null,
        [Description("Kind filter: 'all', 'types', 'methods', or 'properties' (default 'all')")] string kind = "all",
        [Description("Maximum number of results to return (default 50)")] int maxResults = 50,
        [Description("Include public members in results (default false)")] bool includePublicMembers = false,
        [Description("Include entry points and test methods in results (default false)")] bool includeEntryPoints = false,
        [Description("Comma-separated project names to scope analysis")] string? projectFilter = null,
        [Description("Comma-separated file path suffixes to scope analysis")] string? fileFilter = null,
        CancellationToken ct = default)
    {
        var status = workspace.EnsureReadyOrStatus(ct);
        if (status is not null) return status;

        var solution = workspace.GetSolution();
        if (solution is null)
            return Json.Serialize(new { error = "No solution loaded" });

        var projects = GetProjectsInScope(solution, scope, path);
        if (projects.Count == 0)
            return Json.Serialize(new { error = $"No projects found for scope '{scope}' with path '{path}'" });

        if (projectFilter is not null)
        {
            var names = projectFilter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            projects = projects.Where(p => names.Any(n => p.Name.Equals(n, StringComparison.OrdinalIgnoreCase))).ToList();
        }

        var ctx = new AnalysisContext(solution, kind, maxResults, includePublicMembers, includeEntryPoints,
            fileFilter?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        var entries = new List<DeadCodeEntry>();

        foreach (var project in projects)
        {
            ct.ThrowIfCancellationRequested();
            var compilation = await workspace.GetCompilationAsync(project, ct);
            if (compilation is null) continue;

            await AnalyzeProjectForDeadCodeAsync(compilation, project, ctx, entries, ct);
        }

        var result = new DeadCodeResult(entries, entries.Count);
        return Json.Serialize(result);
    }

    private static async Task AnalyzeProjectForDeadCodeAsync(
        Compilation compilation, Project project, AnalysisContext ctx,
        List<DeadCodeEntry> entries, CancellationToken ct)
    {
        foreach (var tree in compilation.SyntaxTrees)
        {
            ct.ThrowIfCancellationRequested();
            if (entries.Count >= ctx.MaxResults) break;

            if (!MatchesFileFilter(tree.FilePath, ctx.FileFilters))
                continue;

            var model = compilation.GetSemanticModel(tree);
            var root = await tree.GetRootAsync(ct);

            var declarations = root.DescendantNodes()
                .Where(n => n is TypeDeclarationSyntax
                         or MethodDeclarationSyntax
                         or PropertyDeclarationSyntax);

            foreach (var decl in declarations)
            {
                if (entries.Count >= ctx.MaxResults) break;
                await CheckDeclarationAsync(decl, model, project, ctx, entries, ct);
            }
        }
    }

    private static bool MatchesFileFilter(string? treePath, string[]? fileFilters)
    {
        if (fileFilters is not { Length: > 0 }) return true;
        var filePath = treePath?.Replace('\\', '/') ?? "";
        return fileFilters.Any(f => filePath.EndsWith(f.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase));
    }

    private static async Task CheckDeclarationAsync(
        SyntaxNode decl, SemanticModel model, Project project,
        AnalysisContext ctx, List<DeadCodeEntry> entries, CancellationToken ct)
    {
        var symbol = model.GetDeclaredSymbol(decl, ct);
        if (symbol is null) return;

        if (!MatchesKindFilter(symbol, ctx.Kind)) return;
        if (ShouldSkip(symbol, ctx.IncludePublicMembers, ctx.IncludeEntryPoints)) return;

        var references = await SymbolFinder.FindReferencesAsync(symbol, ctx.Solution, ct);
        var refCount = references.Sum(r => r.Locations.Count());

        if (refCount == 0)
        {
            var location = SymbolResolver.GetLocation(symbol);
            entries.Add(new DeadCodeEntry(
                symbol.ToDisplayString(),
                symbol.Kind.ToString(),
                location.FilePath,
                location.Line,
                project.Name));
        }
    }

    private static bool MatchesKindFilter(ISymbol symbol, string kind) =>
        kind.ToLowerInvariant() switch
        {
            "types" => symbol is INamedTypeSymbol,
            "methods" => symbol is IMethodSymbol,
            "properties" => symbol is IPropertySymbol,
            _ => true
        };

    private static bool ShouldSkip(ISymbol symbol, bool includePublicMembers, bool includeEntryPoints)
    {
        if (symbol is IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.PropertyGet or MethodKind.PropertySet or MethodKind.EventAdd or MethodKind.EventRemove })
            return true;

        if (IsInterfaceImplementation(symbol))
            return true;

        if (symbol is IMethodSymbol { IsOverride: true } or IPropertySymbol { IsOverride: true })
            return true;

        if (!includePublicMembers && IsPublicOnPublicType(symbol))
            return true;

        return !includeEntryPoints && IsEntryPointOrTest(symbol);
    }

    private static bool IsPublicOnPublicType(ISymbol symbol) =>
        symbol.DeclaredAccessibility == Accessibility.Public &&
        symbol.ContainingType?.DeclaredAccessibility == Accessibility.Public;

    private static bool IsEntryPointOrTest(ISymbol symbol)
    {
        if (symbol is IMethodSymbol && EntryPointNames.Contains(symbol.Name))
            return true;

        if (symbol is IMethodSymbol methodSymbol && HasTestAttribute(methodSymbol))
            return true;

        return HasControllerAttribute(symbol);
    }

    private static bool HasControllerAttribute(ISymbol symbol)
    {
        var type = symbol as INamedTypeSymbol ?? symbol.ContainingType;
        if (type is null) return false;

        return type.GetAttributes()
            .Any(a => a.AttributeClass?.Name is "ApiControllerAttribute" or "ControllerAttribute"
                    or "McpServerToolTypeAttribute");
    }

    private static bool IsInterfaceImplementation(ISymbol symbol)
    {
        if (symbol.ContainingType is null)
            return false;

        foreach (var iface in symbol.ContainingType.AllInterfaces)
        {
            foreach (var member in iface.GetMembers())
            {
                var impl = symbol.ContainingType.FindImplementationForInterfaceMember(member);
                if (SymbolEqualityComparer.Default.Equals(impl, symbol))
                    return true;
            }
        }

        return false;
    }

    private static bool HasTestAttribute(IMethodSymbol methodSymbol)
    {
        return methodSymbol.GetAttributes()
            .Where(attr => attr.AttributeClass?.Name is not null)
            .Any(attr => TestAttributes.Contains(attr.AttributeClass!.Name.Replace("Attribute", "")));
    }

    private static List<Project> GetProjectsInScope(Solution solution, string scope, string? path)
    {
        switch (scope.ToLowerInvariant())
        {
            case "file":
            {
                if (path is null) return [];
                var normalized = path.Replace('\\', '/');
                var project = solution.Projects
                    .FirstOrDefault(p => p.Documents
                        .Any(d => d.FilePath?.Replace('\\', '/').EndsWith(normalized, StringComparison.OrdinalIgnoreCase) == true));
                return project is not null ? [project] : [];
            }
            case "project":
            {
                if (path is null) return [];
                var project = solution.Projects
                    .FirstOrDefault(p => p.Name.Equals(path, StringComparison.OrdinalIgnoreCase)
                        || (p.FilePath?.Replace('\\', '/').EndsWith(path.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase) == true));
                return project is not null ? [project] : [];
            }
            default:
                return solution.Projects.ToList();
        }
    }
}
