using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RoslynLens;

/// <summary>
/// Background service that loads the solution asynchronously at startup.
/// The MCP server accepts connections immediately; tools return "loading" status until ready.
/// </summary>
public sealed class WorkspaceInitializer(
    WorkspaceManager workspaceManager,
    ILogger<WorkspaceInitializer> logger) : BackgroundService
{
    /// <summary>
    /// Set by Program.cs before host starts. Static handoff from CLI args to DI.
    /// </summary>
    public static string? SolutionPath { get; set; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (SolutionPath is null)
        {
            logger.LogWarning("No solution file found. Use --solution <path> or run from a directory containing a .sln/.slnx file.");
            return;
        }

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Loading solution: {SolutionPath}", SolutionPath);

        try
        {
            await workspaceManager.LoadSolutionAsync(SolutionPath, stoppingToken);

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("Solution loaded: {ProjectCount} projects", workspaceManager.ProjectCount);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Failed to load solution: {SolutionPath}", SolutionPath);
        }
    }
}
