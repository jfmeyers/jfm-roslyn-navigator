# How It Works

## Overview

RoslynLens is an MCP (Model Context Protocol) server that uses Roslyn to
provide semantic code navigation. Instead of Claude Code reading entire `.cs` files
(500-2000+ tokens each), it queries this server and receives focused results
(30-150 tokens).

## Startup sequence

```text
1. MSBuildLocator.RegisterDefaults()    — locate MSBuild SDK
2. SolutionDiscovery.FindSolutionPath() — BFS for .sln/.slnx (max 3 levels)
3. Host starts MCP stdio transport      — listens on stdin, responds on stdout
4. WorkspaceInitializer (background)    — opens MSBuildWorkspace, loads solution
5. Tools become available               — respond "loading" until workspace is Ready
```

## Solution loading

`WorkspaceManager` wraps `MSBuildWorkspace` with:

- **Lazy compilation**: For solutions with 50+ projects, compilations are created
  on-demand instead of upfront
- **LRU cache**: Keeps the 50 most recently used compilations in memory (~250-750 MB)
- **File watcher**: `.cs` changes trigger incremental text updates; `.csproj` changes
  trigger full project reload (with 200ms debounce)

## Tool execution

Each tool class is auto-discovered via `[McpServerToolType]` and registered by
`WithToolsFromAssembly()`. When Claude Code calls a tool:

```text
Claude Code → JSON-RPC (stdin) → MCP SDK → Tool class → Roslyn API → JSON-RPC (stdout)
```

Tools access the workspace via dependency-injected `WorkspaceManager`.

## Anti-pattern detection

Detectors implement `IAntiPatternDetector`:

```csharp
public interface IAntiPatternDetector
{
    IEnumerable<AntiPatternViolation> Detect(
        SyntaxTree tree,
        SemanticModel? model,
        CancellationToken cancellationToken);
}
```

Two modes:

- **Syntax-only** (`model = null`): Fast, works without compilation. Most detectors
  use this mode (pattern matching on the syntax tree).
- **Semantic** (`model` provided): Required for type-aware analysis (e.g., checking
  if a method returns `Task`, resolving interface implementations).

## Logging

All logs go to **stderr** via `LogToStandardErrorThreshold = LogLevel.Trace`.
Stdout is reserved exclusively for JSON-RPC messages (MCP protocol requirement).
