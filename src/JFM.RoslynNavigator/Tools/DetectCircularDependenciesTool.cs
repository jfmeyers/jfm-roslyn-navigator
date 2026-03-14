using System.ComponentModel;
using System.Text.Json;
using JFM.RoslynNavigator.Responses;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModelContextProtocol.Server;

namespace JFM.RoslynNavigator;

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
            return JsonSerializer.Serialize(new { error = "No solution loaded" });

        var cycles = scope.ToLowerInvariant() switch
        {
            "types" => await DetectTypeCyclesAsync(workspace, solution, projectFilter, ct),
            _ => DetectProjectCycles(solution, projectFilter)
        };

        var result = new CircularDependenciesResult(scope, cycles, cycles.Count);
        return JsonSerializer.Serialize(result);
    }

    private static List<CycleEntry> DetectProjectCycles(Solution solution, string? projectFilter)
    {
        // Build adjacency list
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

                var typeDecls = root.DescendantNodes().OfType<TypeDeclarationSyntax>();

                foreach (var typeDecl in typeDecls)
                {
                    var symbol = model.GetDeclaredSymbol(typeDecl, ct);
                    if (symbol is null) continue;

                    if (symbol is not INamedTypeSymbol namedSymbol) continue;

                    var typeName = namedSymbol.ToDisplayString();
                    if (!graph.ContainsKey(typeName))
                        graph[typeName] = [];

                    // Collect dependencies from fields, properties, and base types
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

                    // Base type
                    if (namedSymbol.BaseType is not null && !IsSystemType(namedSymbol.BaseType))
                        graph[typeName].Add(namedSymbol.BaseType.ToDisplayString());

                    // Interfaces
                    foreach (var iface in namedSymbol.Interfaces)
                    {
                        if (!IsSystemType(iface))
                            graph[typeName].Add(iface.ToDisplayString());
                    }
                }
            }
        }

        return FindCyclesDfs(graph);
    }

    private static List<CycleEntry> FindCyclesDfs(Dictionary<string, List<string>> graph)
    {
        var cycles = new List<CycleEntry>();
        var visited = new HashSet<string>();
        var inStack = new HashSet<string>();
        var path = new List<string>();

        foreach (var node in graph.Keys)
        {
            if (!visited.Contains(node))
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
                if (inStack.Contains(neighbor))
                {
                    // Found a cycle — extract it
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
        }

        path.RemoveAt(path.Count - 1);
        inStack.Remove(node);
    }

    private static bool IsSystemType(INamedTypeSymbol type)
    {
        var ns = type.ContainingNamespace?.ToDisplayString();
        return ns is not null &&
               (ns.StartsWith("System", StringComparison.Ordinal) ||
                ns.StartsWith("Microsoft", StringComparison.Ordinal));
    }
}
