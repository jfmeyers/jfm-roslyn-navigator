using System.ComponentModel;
using System.Text.Json;
using JFM.RoslynNavigator.Responses;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModelContextProtocol.Server;

namespace JFM.RoslynNavigator.Tools;

[McpServerToolType]
public static class GetModuleDependsOnTool
{
    [McpServerTool(Name = "get_module_depends_on")]
    [Description("Analyze Granit module dependency tree. Finds classes inheriting from GranitModule, reads [DependsOn] attributes, and builds a dependency graph to the specified depth.")]
    public static async Task<string> ExecuteAsync(
        WorkspaceManager workspace,
        [Description("Name of the module to analyze (e.g. 'GranitPersistenceModule' or 'Persistence')")] string moduleName,
        [Description("Maximum depth to traverse (default: 3)")] int depth = 3,
        [Description("Direction: 'dependencies' (what this module depends on) or 'dependents' (what depends on this module)")] string direction = "dependencies",
        CancellationToken ct = default)
    {
        var status = workspace.EnsureReadyOrStatus(ct);
        if (status is not null) return status;

        var compilations = await workspace.GetAllCompilationsAsync(ct);

        // Build a map of all modules: module name -> (project, file, line, dependsOn list)
        var moduleMap = new Dictionary<string, ModuleInfo>(StringComparer.Ordinal);

        foreach (var (project, compilation) in compilations)
        {
            ct.ThrowIfCancellationRequested();

            foreach (var syntaxTree in compilation.SyntaxTrees)
            {
                var root = await syntaxTree.GetRootAsync(ct);

                foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
                {
                    var className = classDecl.Identifier.Text;

                    // Match modules by name pattern or base class
                    if (!IsModuleClass(classDecl))
                        continue;

                    var dependsOnModules = ExtractDependsOn(classDecl);
                    var line = classDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

                    moduleMap[className] = new ModuleInfo(
                        className,
                        project.Name,
                        syntaxTree.FilePath,
                        line,
                        dependsOnModules);
                }
            }
        }

        // Normalize module name: allow short names like "Persistence" -> "GranitPersistenceModule"
        var resolvedName = ResolveModuleName(moduleName, moduleMap);
        if (resolvedName is null || !moduleMap.ContainsKey(resolvedName))
        {
            var empty = new ModuleDependsOnResult(
                new ModuleDependency(moduleName, "unknown", null, null, null),
                0,
                direction);
            return JsonSerializer.Serialize(empty);
        }

        ModuleDependency rootDep;
        int totalModules;

        if (direction.Equals("dependents", StringComparison.OrdinalIgnoreCase))
        {
            // Reverse graph: find all modules that depend on the target
            var reverseMap = BuildReverseMap(moduleMap);
            rootDep = BuildTree(resolvedName, moduleMap, reverseMap, depth, []);
            totalModules = CountNodes(rootDep);
        }
        else
        {
            // Forward graph: what this module depends on
            rootDep = BuildTree(resolvedName, moduleMap, null, depth, []);
            totalModules = CountNodes(rootDep);
        }

        var result = new ModuleDependsOnResult(rootDep, totalModules, direction);
        return JsonSerializer.Serialize(result);
    }

    private static bool IsModuleClass(ClassDeclarationSyntax classDecl)
    {
        var name = classDecl.Identifier.Text;

        // Name ends with "Module"
        if (name.EndsWith("Module", StringComparison.Ordinal))
            return true;

        // Has a base class containing "Module"
        if (classDecl.BaseList is not null)
        {
            foreach (var baseType in classDecl.BaseList.Types)
            {
                var baseTypeName = baseType.Type.ToString();
                if (baseTypeName.Contains("Module", StringComparison.Ordinal))
                    return true;
            }
        }

        return false;
    }

    private static List<string> ExtractDependsOn(ClassDeclarationSyntax classDecl)
    {
        var result = new List<string>();

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
                    // typeof(SomeModule)
                    if (arg.Expression is TypeOfExpressionSyntax typeOf)
                    {
                        result.Add(typeOf.Type.ToString());
                    }
                }
            }
        }

        return result;
    }

    private static string? ResolveModuleName(string input, Dictionary<string, ModuleInfo> map)
    {
        // Exact match
        if (map.ContainsKey(input))
            return input;

        // Try with "Module" suffix
        var withSuffix = input + "Module";
        if (map.ContainsKey(withSuffix))
            return withSuffix;

        // Try with "Granit" prefix
        var withPrefix = "Granit" + input + "Module";
        if (map.ContainsKey(withPrefix))
            return withPrefix;

        // Case-insensitive fallback
        foreach (var key in map.Keys)
        {
            if (key.Equals(input, StringComparison.OrdinalIgnoreCase) ||
                key.Equals(withSuffix, StringComparison.OrdinalIgnoreCase) ||
                key.Equals(withPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return key;
            }
        }

        return null;
    }

    private static Dictionary<string, List<string>> BuildReverseMap(Dictionary<string, ModuleInfo> moduleMap)
    {
        var reverse = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var (moduleName, info) in moduleMap)
        {
            foreach (var dep in info.DependsOn)
            {
                var resolvedDep = ResolveModuleName(dep, moduleMap) ?? dep;
                if (!reverse.TryGetValue(resolvedDep, out var list))
                {
                    list = [];
                    reverse[resolvedDep] = list;
                }

                list.Add(moduleName);
            }
        }

        return reverse;
    }

    private static ModuleDependency BuildTree(
        string moduleName,
        Dictionary<string, ModuleInfo> moduleMap,
        Dictionary<string, List<string>>? reverseMap,
        int remainingDepth,
        HashSet<string> visited)
    {
        var info = moduleMap.GetValueOrDefault(moduleName);
        var project = info?.Project ?? "unknown";
        var file = info?.File;
        var line = info?.Line;

        if (remainingDepth <= 0 || !visited.Add(moduleName))
        {
            return new ModuleDependency(moduleName, project, file, line, null);
        }

        List<string> children;
        if (reverseMap is not null)
        {
            // Dependents direction
            children = reverseMap.GetValueOrDefault(moduleName) ?? [];
        }
        else
        {
            // Dependencies direction
            children = info?.DependsOn
                .Select(d => ResolveModuleName(d, moduleMap) ?? d)
                .ToList() ?? [];
        }

        var childDeps = children
            .Select(c => BuildTree(c, moduleMap, reverseMap, remainingDepth - 1, visited))
            .ToList();

        visited.Remove(moduleName); // Allow the same module to appear in different branches

        return new ModuleDependency(
            moduleName,
            project,
            file,
            line,
            childDeps.Count > 0 ? childDeps : null);
    }

    private static int CountNodes(ModuleDependency node)
    {
        var count = 1;
        if (node.Dependencies is not null)
        {
            foreach (var child in node.Dependencies)
            {
                count += CountNodes(child);
            }
        }

        return count;
    }

    private sealed record ModuleInfo(
        string Name,
        string Project,
        string? File,
        int? Line,
        List<string> DependsOn);
}
