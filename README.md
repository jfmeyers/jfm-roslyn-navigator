# RoslynLens

[![GitHub](https://img.shields.io/badge/github-repo-blue?logo=github)](https://github.com/jfmeyers/roslyn-lens)
[![NuGet](https://img.shields.io/nuget/v/RoslynLens)](https://www.nuget.org/packages/RoslynLens/)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=jfmeyers_roslyn-lens&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=jfmeyers_roslyn-lens)
[![License: Apache-2.0](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)

A token-efficient MCP (Model Context Protocol) server for .NET codebase
navigation, powered by Roslyn semantic analysis. Designed for use with
[Claude Code](https://docs.anthropic.com/en/docs/claude-code).

Instead of reading entire `.cs` files (500-2000+ tokens each), Claude Code
queries this MCP server and receives focused, semantic results (30-150
tokens). This dramatically reduces token consumption when working with
large .NET solutions.

## Features

### 28 Navigation & Analysis Tools

| Tool | Purpose |
| ---- | ------- |
| `find_symbol` | Locate type/method definitions by name (supports glob patterns) |
| `find_references` | Find all usages of a symbol |
| `find_implementations` | Find interface implementors and derived classes |
| `find_callers` | Find direct callers of a method |
| `find_overrides` | Find overrides of virtual/abstract methods |
| `find_dead_code` | Detect unused types, methods, and properties (with filters) |
| `get_type_hierarchy` | Show inheritance chain, interfaces, and derived types |
| `get_public_api` | Get public surface without reading the full file |
| `get_symbol_detail` | Full signature, parameters, return type, and XML docs |
| `get_project_graph` | Solution dependency tree (with filtering for large solutions) |
| `get_dependency_graph` | Method call chain visualization |
| `get_diagnostics` | Compiler warnings and errors |
| `get_test_coverage_map` | Heuristic test coverage mapping |
| `get_complexity_metrics` | Cyclomatic, cognitive complexity, nesting depth, LOC |
| `detect_antipatterns` | 18 anti-pattern detectors |
| `detect_circular_dependencies` | Project and type-level cycle detection |
| `detect_duplicates` | Structurally similar code detection via AST fingerprinting |
| `get_module_depends_on` | `[DependsOn]` attribute graph (modular monoliths) |
| `validate_conventions` | Convention violation checker |
| `analyze_method` | Compound: signature + callers + dependencies + complexity |
| `get_type_overview` | Compound: public API + hierarchy + implementations + diagnostics |
| `get_file_overview` | Compound: types + diagnostics + anti-patterns for a file |
| `analyze_data_flow` | Variable assignments, reads, writes, captured variables |
| `analyze_control_flow` | Reachability, return points, exit points |
| `find_symbols_batch` | Resolve multiple symbol names in one call |
| `get_public_api_batch` | Get public API of multiple types in one call |
| `get_symbol_detail_batch` | Get details of multiple symbols in one call |
| `resolve_external_source` | Resolve NuGet/framework source via SourceLink or decompilation |

### 18 Anti-Pattern Detectors

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
dotnet tool install --global RoslynLens
```

## Usage

### With Claude Code (recommended)

Add to your Claude Code MCP configuration:

```bash
# User-scoped (available in all projects)
claude mcp add --scope user --transport stdio roslyn-lens -- roslyn-lens

# Project-scoped
claude mcp add --transport stdio roslyn-lens -- roslyn-lens
```

Or via `.mcp.json` in your project root:

```json
{
  "mcpServers": {
    "roslyn-lens": {
      "type": "stdio",
      "command": "roslyn-lens",
      "args": []
    }
  }
}
```

### Solution Discovery

The server automatically finds the nearest `.sln` or `.slnx` file using
BFS from the current directory (max 3 levels up). You can also specify a
solution explicitly:

```bash
roslyn-lens --solution /path/to/MySolution.slnx
```

### Standalone

```bash
roslyn-lens
```

The server communicates over stdio using the [MCP protocol](https://modelcontextprotocol.io/).

## Building from Source

```bash
git clone https://github.com/jfmeyers/roslyn-lens.git
cd roslyn-lens
dotnet build
dotnet test
```

### Pack and install locally

```bash
dotnet pack -c Release -o ./nupkgs
dotnet tool install --global --add-source ./nupkgs RoslynLens
```

## Configuration

Environment variables for runtime tuning:

| Variable | Purpose | Default |
| -------- | ------- | ------- |
| `ROSLYN_LENS_TIMEOUT_SECONDS` | Operation timeout | 30 |
| `ROSLYN_LENS_MAX_RESULTS` | Maximum results per query | 100 |
| `ROSLYN_LENS_CACHE_SIZE` | LRU compilation cache size | 20 |
| `ROSLYN_LENS_LOG_LEVEL` | Log verbosity (Trace/Debug/Information/Warning/Error) | Information |

## Architecture

```text
src/RoslynLens/
├── Program.cs                  # Host + MCP stdio transport
├── SolutionDiscovery.cs        # BFS .sln/.slnx auto-discovery
├── WorkspaceManager.cs         # MSBuildWorkspace, LRU compilation cache
├── WorkspaceInitializer.cs     # Background solution loading
├── SymbolResolver.cs           # Cross-project symbol resolution + fuzzy FQN
├── RoslynLensConfig.cs          # Environment variable configuration
├── WorkspaceState.cs           # Workspace loading state enum
├── ComplexityAnalyzer.cs       # Cyclomatic/cognitive complexity metrics
├── DuplicateCodeDetector.cs    # AST fingerprinting for duplicate detection
├── ExternalSourceResolver.cs   # SourceLink + decompilation for NuGet deps
├── Tools/                      # 28 MCP tool implementations
├── Analyzers/                  # 18 anti-pattern detectors
└── Responses/                  # Token-optimized DTOs
```

Key design decisions:

- **Lazy compilation**: Solutions with 50+ projects only compile
  on-demand (LRU cache of 50 compilations)
- **File watcher**: `.cs` changes trigger incremental text updates;
  `.csproj` changes trigger full reload
- **Background loading**: Solution loads asynchronously; tools return
  "loading" status until ready
- **Logs to stderr**: All logging goes to stderr to keep stdout clean
  for JSON-RPC

## Acknowledgments

Inspired by
[CWM.RoslynNavigator](https://github.com/codewithmukesh/dotnet-claude-kit/tree/main/mcp/CWM.RoslynNavigator)
by Mukesh Murugan (MIT License). Adapted with additional detectors,
auto-discovery, and global tool distribution.

## Documentation

Full documentation is available in the [docs/](docs/README.md) directory.

## Author

**JF Meyers** — [GitHub](https://github.com/jfmeyers)

## License

[Apache License 2.0](LICENSE)
