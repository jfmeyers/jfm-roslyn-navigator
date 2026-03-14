using System.ComponentModel;
using System.Text.Json;
using JFM.RoslynNavigator.Responses;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using ModelContextProtocol.Server;

namespace JFM.RoslynNavigator;

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

    [McpServerTool(Name = "find_dead_code")]
    [Description("Finds potentially unreferenced types, methods, and properties that may be dead code.")]
    public static async Task<string> ExecuteAsync(
        WorkspaceManager workspace,
        [Description("Scope: 'file', 'project', or 'solution' (default 'solution')")] string scope = "solution",
        [Description("File or project path (required for 'file' and 'project' scopes)")] string? path = null,
        [Description("Kind filter: 'all', 'types', 'methods', or 'properties' (default 'all')")] string kind = "all",
        [Description("Maximum number of results to return (default 50)")] int maxResults = 50,
        CancellationToken ct = default)
    {
        var status = workspace.EnsureReadyOrStatus(ct);
        if (status is not null) return status;

        var solution = workspace.GetSolution();
        if (solution is null)
            return JsonSerializer.Serialize(new { error = "No solution loaded" });

        var projects = GetProjectsInScope(solution, scope, path);
        if (projects.Count == 0)
            return JsonSerializer.Serialize(new { error = $"No projects found for scope '{scope}' with path '{path}'" });

        var entries = new List<DeadCodeEntry>();

        foreach (var project in projects)
        {
            ct.ThrowIfCancellationRequested();

            var compilation = await workspace.GetCompilationAsync(project, ct);
            if (compilation is null) continue;

            await AnalyzeProjectForDeadCodeAsync(compilation, solution, project, kind, maxResults, entries, ct);
        }

        var result = new DeadCodeResult(entries, entries.Count);
        return JsonSerializer.Serialize(result);
    }

    private static async Task AnalyzeProjectForDeadCodeAsync(
        Compilation compilation,
        Solution solution,
        Project project,
        string kind,
        int maxResults,
        List<DeadCodeEntry> entries,
        CancellationToken ct)
    {
        foreach (var tree in compilation.SyntaxTrees)
        {
            ct.ThrowIfCancellationRequested();
            if (entries.Count >= maxResults) break;

            var model = compilation.GetSemanticModel(tree);
            var root = await tree.GetRootAsync(ct);

            var declarations = root.DescendantNodes()
                .Where(n => n is TypeDeclarationSyntax
                         or MethodDeclarationSyntax
                         or PropertyDeclarationSyntax);

            foreach (var decl in declarations)
            {
                if (entries.Count >= maxResults) break;

                await CheckDeclarationAsync(decl, model, solution, project, kind, entries, ct);
            }
        }
    }

    private static async Task CheckDeclarationAsync(
        SyntaxNode decl,
        SemanticModel model,
        Solution solution,
        Project project,
        string kind,
        List<DeadCodeEntry> entries,
        CancellationToken ct)
    {
        var symbol = model.GetDeclaredSymbol(decl, ct);
        if (symbol is null) return;

        if (!MatchesKindFilter(symbol, kind)) return;
        if (ShouldSkip(symbol)) return;

        var references = await SymbolFinder.FindReferencesAsync(symbol, solution, ct);
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

    private static bool ShouldSkip(ISymbol symbol)
    {
        if (symbol is IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.PropertyGet or MethodKind.PropertySet or MethodKind.EventAdd or MethodKind.EventRemove })
            return true;

        if (IsInterfaceImplementation(symbol))
            return true;

        if (symbol is IMethodSymbol { IsOverride: true } or IPropertySymbol { IsOverride: true })
            return true;

        if (symbol.DeclaredAccessibility == Accessibility.Public &&
            symbol.ContainingType?.DeclaredAccessibility == Accessibility.Public)
            return true;

        if (symbol is IMethodSymbol && EntryPointNames.Contains(symbol.Name))
            return true;

        if (symbol is IMethodSymbol methodSymbol && HasTestAttribute(methodSymbol))
            return true;

        return false;
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
        foreach (var attr in methodSymbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name is not null &&
                TestAttributes.Contains(attr.AttributeClass.Name.Replace("Attribute", "")))
                return true;
        }

        return false;
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
