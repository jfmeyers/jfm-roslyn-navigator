# JFM.RoslynNavigator

A token-efficient MCP (Model Context Protocol) server for .NET codebase navigation, powered by Roslyn semantic analysis. Designed for use with [Claude Code](https://docs.anthropic.com/en/docs/claude-code).

Instead of reading entire `.cs` files (500-2000+ tokens each), Claude Code queries this MCP server and receives focused, semantic results (30-150 tokens). This dramatically reduces token consumption when working with large .NET solutions.

## Features

### 17 Navigation Tools

| Tool | Purpose |
| ---- | ------- |
| `find_symbol` | Locate type/method definitions by name |
| `find_references` | Find all usages of a symbol |
| `find_implementations` | Find interface implementors and derived classes |
| `find_callers` | Find direct callers of a method |
| `find_overrides` | Find overrides of virtual/abstract methods |
| `find_dead_code` | Detect unused types, methods, and properties |
| `get_type_hierarchy` | Show inheritance chain, interfaces, and derived types |
| `get_public_api` | Get public surface without reading the full file |
| `get_symbol_detail` | Full signature, parameters, return type, and XML docs |
| `get_project_graph` | Solution dependency tree |
| `get_dependency_graph` | Method call chain visualization |
| `get_diagnostics` | Compiler warnings and errors |
| `get_test_coverage_map` | Heuristic test coverage mapping |
| `detect_antipatterns` | 19 anti-pattern detectors |
| `detect_circular_dependencies` | Project and type-level cycle detection |
| `get_module_depends_on` | `[DependsOn]` attribute graph (modular monoliths) |
| `validate_granit_conventions` | Convention violation checker |

### 19 Anti-Pattern Detectors

**General .NET detectors:**

| ID | Detector | Description |
| -- | -------- | ----------- |
| AP001 | AsyncVoidDetector | `async void` methods (except event handlers) |
| AP002 | SyncOverAsyncDetector | `.Result`, `.Wait()`, `.GetAwaiter().GetResult()` |
| AP003 | HttpClientInstantiationDetector | `new HttpClient()` instead of `IHttpClientFactory` |
| AP004 | DateTimeDirectUseDetector | `DateTime.Now`/`UtcNow` instead of `TimeProvider` |
| AP005 | BroadCatchDetector | `catch (Exception)` without re-throw |
| AP006 | LoggingInterpolationDetector | String interpolation in log calls |
| AP007 | PragmaWithoutRestoreDetector | `#pragma warning disable` without matching `restore` |
| AP008 | MissingCancellationTokenDetector | Async methods missing `CancellationToken` parameter |
| AP009 | EfCoreNoTrackingDetector | EF Core queries without `.AsNoTracking()` |

**Domain-specific detectors:**

| ID | Detector | Description |
| -- | -------- | ----------- |
| GR-GUID | GuidNewGuidDetector | `Guid.NewGuid()` instead of `IGuidGenerator` |
| GR-SECRET | HardcodedSecretDetector | Hardcoded passwords, connection strings, API keys |
| GR-SYNC-EF | SynchronousSaveChangesDetector | `SaveChanges()` instead of `SaveChangesAsync()` |
| GR-BADREQ | TypedResultsBadRequestDetector | `TypedResults.BadRequest<string>()` instead of `Problem()` |
| GR-REGEX | NewRegexDetector | `new Regex()` instead of `[GeneratedRegex]` |
| GR-SLEEP | ThreadSleepDetector | `Thread.Sleep()` in production code |
| GR-CONSOLE | ConsoleWriteDetector | `Console.Write`/`WriteLine` instead of `ILogger` |
| GR-CFGAWAIT | MissingConfigureAwaitDetector | Missing `ConfigureAwait(false)` in library code |
| GR-DTO | DtoSuffixDetector | Classes/records with `*Dto` suffix |

## Requirements

- .NET 10 SDK or later
- A .NET solution (`.sln` or `.slnx`)

## Installation

```bash
dotnet tool install --global JFM.RoslynNavigator
```

## Usage

### With Claude Code (recommended)

Add to your Claude Code MCP configuration:

```bash
# User-scoped (available in all projects)
claude mcp add --scope user --transport stdio roslyn-navigator -- jfm-roslyn-navigator

# Project-scoped
claude mcp add --transport stdio roslyn-navigator -- jfm-roslyn-navigator
```

Or via `.mcp.json` in your project root:

```json
{
  "mcpServers": {
    "roslyn-navigator": {
      "type": "stdio",
      "command": "jfm-roslyn-navigator",
      "args": []
    }
  }
}
```

### Solution Discovery

The server automatically finds the nearest `.sln` or `.slnx` file using BFS from the current directory (max 3 levels up). You can also specify a solution explicitly:

```bash
jfm-roslyn-navigator --solution /path/to/MySolution.slnx
```

### Standalone

```bash
jfm-roslyn-navigator
```

The server communicates over stdio using the [MCP protocol](https://modelcontextprotocol.io/).

## Building from Source

```bash
git clone https://github.com/jfmeyers/jfm-roslyn-navigator.git
cd jfm-roslyn-navigator
dotnet build
dotnet test
```

### Pack and install locally

```bash
dotnet pack -c Release -o ./nupkgs
dotnet tool install --global --add-source ./nupkgs JFM.RoslynNavigator
```

## Architecture

```
src/JFM.RoslynNavigator/
├── Program.cs                  # Host + MCP stdio transport
├── SolutionDiscovery.cs        # BFS .sln/.slnx auto-discovery
├── WorkspaceManager.cs         # MSBuildWorkspace, LRU compilation cache
├── WorkspaceInitializer.cs     # Background solution loading
├── SymbolResolver.cs           # Cross-project symbol resolution
├── Tools/                      # 17 MCP tool implementations
├── Analyzers/                  # 19 anti-pattern detectors
└── Responses/                  # Token-optimized DTOs
```

Key design decisions:

- **Lazy compilation**: Solutions with 50+ projects only compile on-demand (LRU cache of 50 compilations)
- **File watcher**: `.cs` changes trigger incremental text updates; `.csproj` changes trigger full reload
- **Background loading**: Solution loads asynchronously; tools return "loading" status until ready
- **Logs to stderr**: All logging goes to stderr to keep stdout clean for JSON-RPC

## Acknowledgments

Inspired by [CWM.RoslynNavigator](https://github.com/codewithmukesh/dotnet-claude-kit/tree/main/mcp/CWM.RoslynNavigator) by Mukesh Murugan (MIT License). Adapted with additional detectors, auto-discovery, and global tool distribution.

## License

[Apache License 2.0](LICENSE)
