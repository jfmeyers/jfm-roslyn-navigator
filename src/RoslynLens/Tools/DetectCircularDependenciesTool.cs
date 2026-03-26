using System.ComponentModel;
using RoslynLens.Responses;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModelContextProtocol.Server;

namespace RoslynLens;

[McpServerToolType]
public static class DetectCircularDependenciesTool
{
    [McpServerTool(Name = "detect_circular_dependencies")]
    [Description("Detects circular dependencies in the project reference graph or type dependency graph using DFS cycle detection.")]
    public static async Task<string> ExecuteAsync(
        WorkspaceManager workspace,
        [Description("Scope: 'projects' for project-level or 'types' for type-level cycle detection (default 'projects')")] string scope = "projects",
        [Description("Optional project name filter")] string? projectFilter = null,
        CancellationToken ct = default)
    {
        var status = workspace.EnsureReadyOrStatus(ct);
        if (status is not null) return status;

        var solution = workspace.GetSolution();
        if (solution is null)
            return Json.Serialize(new { error = "No solution loaded" });

        var cycles = scope.ToLowerInvariant() switch
        {
            "types" => await DetectTypeCyclesAsync(workspace, solution, projectFilter, ct),
            _ => DetectProjectCycles(solution, projectFilter)
        };

        var result = new CircularDependenciesResult(scope, cycles, cycles.Count);
        return Json.Serialize(result);
    }

    private static List<CycleEntry> DetectProjectCycles(Solution solution, string? projectFilter)
    {
        var graph = new Dictionary<string, List<string>>();

        foreach (var project in solution.Projects)
        {
            if (projectFilter is not null &&
                !project.Name.Contains(projectFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            var refs = project.ProjectReferences
                .Select(r => solution.GetProject(r.ProjectId)?.Name)
                .Where(n => n is not null)
                .Cast<string>()
                .ToList();

            graph[project.Name] = refs;
        }

        return FindCyclesDfs(graph);
    }

    private static async Task<List<CycleEntry>> DetectTypeCyclesAsync(
        WorkspaceManager workspace,
        Solution solution,
        string? projectFilter,
        CancellationToken ct)
    {
        var graph = new Dictionary<string, List<string>>();

        var projects = solution.Projects
            .Where(p => projectFilter is null || p.Name.Contains(projectFilter, StringComparison.OrdinalIgnoreCase));

        foreach (var project in projects)
        {
            ct.ThrowIfCancellationRequested();

            var compilation = await workspace.GetCompilationAsync(project, ct);
            if (compilation is null) continue;

            foreach (var tree in compilation.SyntaxTrees)
            {
                var model = compilation.GetSemanticModel(tree);
                var root = await tree.GetRootAsync(ct);

                foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
                {
                    CollectTypeDependencies(typeDecl, model, graph, ct);
                }
            }
        }

        return FindCyclesDfs(graph);
    }

    private static void CollectTypeDependencies(
        TypeDeclarationSyntax typeDecl,
        SemanticModel model,
        Dictionary<string, List<string>> graph,
        CancellationToken ct)
    {
        if (model.GetDeclaredSymbol(typeDecl, ct) is not INamedTypeSymbol namedSymbol)
            return;

        var typeName = namedSymbol.ToDisplayString();
        if (!graph.ContainsKey(typeName))
            graph[typeName] = [];

        CollectMemberDependencies(namedSymbol, typeName, graph);
        CollectInheritanceDependencies(namedSymbol, typeName, graph);
    }

    private static void CollectMemberDependencies(
        INamedTypeSymbol namedSymbol, string typeName, Dictionary<string, List<string>> graph)
    {
        foreach (var member in namedSymbol.GetMembers())
        {
            var depType = member switch
            {
                IFieldSymbol f => f.Type,
                IPropertySymbol p => p.Type,
                _ => null
            };

            if (depType is INamedTypeSymbol namedDepType &&
                !IsSystemType(namedDepType) &&
                namedDepType.ToDisplayString() != typeName)
            {
                graph[typeName].Add(namedDepType.ToDisplayString());
            }
        }
    }

    private static void CollectInheritanceDependencies(
        INamedTypeSymbol namedSymbol, string typeName, Dictionary<string, List<string>> graph)
    {
        if (namedSymbol.BaseType is not null && !IsSystemType(namedSymbol.BaseType))
            graph[typeName].Add(namedSymbol.BaseType.ToDisplayString());

        foreach (var iface in namedSymbol.Interfaces.Where(i => !IsSystemType(i)))
        {
            graph[typeName].Add(iface.ToDisplayString());
        }
    }

    private static List<CycleEntry> FindCyclesDfs(Dictionary<string, List<string>> graph)
    {
        var cycles = new List<CycleEntry>();
        var visited = new HashSet<string>();
        var inStack = new HashSet<string>();
        var path = new List<string>();

        foreach (var node in graph.Keys.Where(n => !visited.Contains(n)))
        {
            Dfs(node, graph, visited, inStack, path, cycles);
        }

        return cycles;
    }

    private static void Dfs(
        string node,
        Dictionary<string, List<string>> graph,
        HashSet<string> visited,
        HashSet<string> inStack,
        List<string> path,
        List<CycleEntry> cycles)
    {
        visited.Add(node);
        inStack.Add(node);
        path.Add(node);

        if (graph.TryGetValue(node, out var neighbors))
        {
            foreach (var neighbor in neighbors)
            {
                ProcessNeighbor(neighbor, graph, visited, inStack, path, cycles);
            }
        }

        path.RemoveAt(path.Count - 1);
        inStack.Remove(node);
    }

    private static void ProcessNeighbor(
        string neighbor,
        Dictionary<string, List<string>> graph,
        HashSet<string> visited,
        HashSet<string> inStack,
        List<string> path,
        List<CycleEntry> cycles)
    {
        if (inStack.Contains(neighbor))
        {
            var cycleStart = path.IndexOf(neighbor);
            if (cycleStart >= 0)
            {
                var cycleNodes = path.Skip(cycleStart).Append(neighbor).ToList();
                cycles.Add(new CycleEntry(cycleNodes));
            }
        }
        else if (!visited.Contains(neighbor))
        {
            Dfs(neighbor, graph, visited, inStack, path, cycles);
        }
    }

    private static bool IsSystemType(INamedTypeSymbol type)
    {
        var ns = type.ContainingNamespace?.ToDisplayString();
        return ns is not null &&
               (ns.StartsWith("System", StringComparison.Ordinal) ||
                ns.StartsWith("Microsoft", StringComparison.Ordinal));
    }
}
