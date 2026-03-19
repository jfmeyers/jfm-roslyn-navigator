using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.TypeSystem;

namespace RoslynLens;

/// <summary>
/// Resolves source code for external (NuGet/framework) symbols via SourceLink or decompilation.
/// </summary>
public static class ExternalSourceResolver
{
    private const int MaxDecompiledLines = 60;

    public static async Task<(string? Source, string Method)> ResolveAsync(
        Microsoft.CodeAnalysis.ISymbol symbol, CancellationToken ct)
    {
        // Try SourceLink first (check if symbol has source in the solution)
        foreach (var location in symbol.Locations)
        {
            if (location.IsInSource)
                return (location.SourceTree?.FilePath, "sourcelink");
        }

        // Fallback: decompile
        var decompiled = await Task.Run(() => TryDecompile(symbol, ct), ct);
        if (decompiled is not null)
            return (decompiled, "decompilation");

        return (null, "none");
    }

    private static string? TryDecompile(Microsoft.CodeAnalysis.ISymbol symbol, CancellationToken ct)
    {
        try
        {
            var assemblyPath = GetAssemblyPath(symbol);
            if (assemblyPath is null || !File.Exists(assemblyPath))
                return null;

            ct.ThrowIfCancellationRequested();

            var decompiler = new CSharpDecompiler(assemblyPath, new DecompilerSettings
            {
                ThrowOnAssemblyResolveErrors = false
            });

            var fullTypeName = symbol.ContainingType?.ToDisplayString()
                ?? (symbol is Microsoft.CodeAnalysis.INamedTypeSymbol ? symbol.ToDisplayString() : null);

            if (fullTypeName is null) return null;

            var typeHandle = new FullTypeName(fullTypeName);
            var source = decompiler.DecompileTypeAsString(typeHandle);

            // Truncate for token efficiency
            var lines = source.Split('\n');
            if (lines.Length > MaxDecompiledLines)
            {
                source = string.Join('\n', lines.Take(MaxDecompiledLines))
                    + $"\n// ... truncated ({lines.Length - MaxDecompiledLines} more lines)";
            }

            return source;
        }
        catch
        {
            return null;
        }
    }

    private static string? GetAssemblyPath(Microsoft.CodeAnalysis.ISymbol symbol)
    {
        var assembly = symbol.ContainingAssembly;
        if (assembly is null) return null;

        var assemblyName = assembly.Identity.Name;
        var possiblePaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, $"{assemblyName}.dll"),
        };

        return possiblePaths.FirstOrDefault(File.Exists);
    }
}
