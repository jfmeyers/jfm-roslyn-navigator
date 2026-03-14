namespace JFM.RoslynNavigator;

/// <summary>
/// Discovers .sln/.slnx files using BFS from a starting directory.
/// Resolution order: explicit --solution arg > working directory BFS.
/// </summary>
public static class SolutionDiscovery
{
    private static readonly HashSet<string> SkipDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".vs", ".idea", "node_modules", "bin", "obj",
        "packages", "artifacts", "TestResults", ".claude", "nupkgs"
    };

    private const int MaxBfsDepth = 3;

    public static string? FindSolutionPath(string[] args)
    {
        var explicitPath = ParseExplicitPath(args);
        if (explicitPath is not null)
            return explicitPath;

        return BfsDiscovery(Directory.GetCurrentDirectory());
    }

    private static string? ParseExplicitPath(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] is "--solution" or "-s")
            {
                var path = args[i + 1];

                if (File.Exists(path) && IsSolutionFile(path))
                    return Path.GetFullPath(path);

                if (Directory.Exists(path))
                    return BfsDiscovery(path);
            }
        }

        return null;
    }

    public static string? BfsDiscovery(string startDirectory)
    {
        var queue = new Queue<(string Path, int Depth)>();
        queue.Enqueue((startDirectory, 0));

        string? bestMatch = null;
        var bestDepth = int.MaxValue;

        while (queue.Count > 0)
        {
            var (dirPath, depth) = queue.Dequeue();

            if (depth > MaxBfsDepth)
                continue;

            try
            {
                foreach (var file in Directory.EnumerateFiles(dirPath))
                {
                    if (!IsSolutionFile(file))
                        continue;

                    if (depth < bestDepth ||
                        (depth == bestDepth && string.Compare(file, bestMatch, StringComparison.OrdinalIgnoreCase) < 0))
                    {
                        bestMatch = Path.GetFullPath(file);
                        bestDepth = depth;
                    }
                }

                // Prefer .slnx over .sln at same depth
                if (bestMatch is not null && bestMatch.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase) && depth == bestDepth)
                    return bestMatch;

                foreach (var subDir in Directory.EnumerateDirectories(dirPath))
                {
                    var dirName = Path.GetFileName(subDir);
                    if (!SkipDirectories.Contains(dirName))
                        queue.Enqueue((subDir, depth + 1));
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip directories we can't access
            }
        }

        return bestMatch;
    }

    private static bool IsSolutionFile(string path) =>
        path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase);
}
