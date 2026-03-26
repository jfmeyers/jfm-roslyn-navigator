using RoslynLens;
using Microsoft.Build.Locator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

// MSBuildLocator MUST be called before any Roslyn types are loaded.
MSBuildLocator.RegisterDefaults();

var solutionPath = SolutionDiscovery.FindSolutionPath(args);
WorkspaceInitializer.SolutionPath = solutionPath;

var config = RoslynLensConfig.FromEnvironment();

var builder = Host.CreateApplicationBuilder(args);

// MCP stdio uses stdout for JSON-RPC — all logs must go to stderr.
builder.Logging.ClearProviders();
builder.Logging.SetMinimumLevel(config.LogLevel);
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton(config);
builder.Services.AddSingleton<WorkspaceManager>();
builder.Services.AddHostedService<WorkspaceInitializer>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

var app = builder.Build();
await app.RunAsync();
