using Microsoft.Extensions.Logging;

namespace RoslynLens;

/// <summary>
/// Runtime configuration read from environment variables.
/// </summary>
public sealed record RoslynLensConfig(
    int TimeoutSeconds,
    int MaxResults,
    int CacheSize,
    LogLevel LogLevel)
{
    private const string Prefix = "ROSLYN_LENS_";

    public static RoslynLensConfig FromEnvironment()
    {
        var timeout = ReadInt($"{Prefix}TIMEOUT_SECONDS", 30);
        var maxResults = ReadInt($"{Prefix}MAX_RESULTS", 100);
        var cacheSize = ReadInt($"{Prefix}CACHE_SIZE", 20);
        var logLevel = ReadLogLevel($"{Prefix}LOG_LEVEL", LogLevel.Information);

        return new RoslynLensConfig(timeout, maxResults, cacheSize, logLevel);
    }

    private static int ReadInt(string name, int defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : defaultValue;
    }

    private static LogLevel ReadLogLevel(string name, LogLevel defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return Enum.TryParse<LogLevel>(value, ignoreCase: true, out var parsed) ? parsed : defaultValue;
    }
}
