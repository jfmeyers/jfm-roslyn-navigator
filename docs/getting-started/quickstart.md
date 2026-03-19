# Quick Start

Get RoslynLens running with Claude Code in under 2 minutes.

## 1. Install

```bash
dotnet tool install --global RoslynLens
```

## 2. Register with Claude Code

```bash
claude mcp add --scope user --transport stdio roslyn-lens -- roslyn-lens
```

## 3. Try it

Open a .NET project directory and start Claude Code. The MCP server auto-discovers
your `.sln` or `.slnx` file (BFS up to 3 levels).

### Find a type

> "Find the WorkspaceManager class"

Claude Code calls `find_symbol` and gets back ~50 tokens instead of reading the
entire file (~1500 tokens).

### Inspect the public API

> "What's the public API of IAntiPatternDetector?"

Claude Code calls `get_public_api` and receives a concise list of members.

### Check complexity

> "Which methods have the highest cyclomatic complexity in this project?"

Claude Code calls `get_complexity_metrics` with `scope=project` and gets a
sorted list of hotspots.

### Detect anti-patterns

> "Run the anti-pattern detectors on Program.cs"

Claude Code calls `detect_antipatterns` scoped to the file and gets violations
with suggestions.

### Get a full method analysis

> "Analyze the ExecuteAsync method in FindSymbolTool"

Claude Code calls `analyze_method` and receives signature, callers, dependency
graph, and complexity metrics — all in a single round-trip.

## How it works

```text
Claude Code ──stdin/stdout──▶ RoslynLens MCP Server ──▶ Roslyn SemanticModel
     │                              │
     │  JSON-RPC request            │  Compilation + symbol resolution
     │  (tool name + params)        │  LRU cache (configurable)
     │                              │
     ◀──── JSON response ◀──────────┘
           (30-150 tokens)
```

Instead of Claude Code reading `.cs` files (500-2000+ tokens each), it queries
RoslynLens and receives focused, semantic results. This dramatically reduces
token consumption on large .NET solutions.

## Next steps

- [Configuration](configuration.md) — MCP registration options, solution
  discovery, environment variables
- [Tools Reference](../tools/navigation.md) — all 28 tools documented
- [Anti-Pattern Detectors](../detectors/general.md) — 19 detectors explained
