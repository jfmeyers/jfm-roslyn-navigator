namespace RoslynLens;

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
                bestMatch = ScanDirectoryForSolution(dirPath, depth, bestMatch, ref bestDepth);

                if (bestMatch is not null && bestMatch.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase) && depth == bestDepth)
                    return bestMatch;

                EnqueueSubDirectories(queue, dirPath, depth);
            }
            catch (UnauthorizedAccessException)
            {
                // Skip directories we can't access
            }
        }

        return bestMatch;
    }

    private static string? ScanDirectoryForSolution(string dirPath, int depth, string? currentBest, ref int bestDepth)
    {
        foreach (var file in Directory.EnumerateFiles(dirPath).Where(IsSolutionFile))
        {
            if (depth < bestDepth ||
                (depth == bestDepth && string.Compare(file, currentBest, StringComparison.OrdinalIgnoreCase) < 0))
            {
                currentBest = Path.GetFullPath(file);
                bestDepth = depth;
            }
        }

        return currentBest;
    }

    private static void EnqueueSubDirectories(Queue<(string Path, int Depth)> queue, string dirPath, int depth)
    {
        foreach (var subDir in Directory.EnumerateDirectories(dirPath))
        {
            var dirName = Path.GetFileName(subDir);
            if (!SkipDirectories.Contains(dirName))
                queue.Enqueue((subDir, depth + 1));
        }
    }

    private static bool IsSolutionFile(string path) =>
        path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase);
}
