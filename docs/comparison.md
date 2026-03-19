# Comparison with alternatives

How RoslynLens compares to other Roslyn-based MCP servers.

## Feature matrix

| Feature | RoslynLens | SharpLensMcp | RoslynMCP | SharpToolsMCP | CodeAnalysisMCP |
| ------- | ---------- | ------------ | --------- | ------------- | --------------- |
| **Tools** | 28 | 58 | 5 | ~20 | 3 |
| **Anti-pattern detectors** | 19 | 0 | 0 | 0 | 0 |
| **Complexity metrics** | Yes | Yes | Yes | Yes | No |
| **Data flow analysis** | Yes | Yes | No | No | No |
| **Duplicate detection** | Yes | No | No | No | No |
| **Compound tools** | Yes | Yes | No | No | No |
| **Batch operations** | Yes | Yes | No | No | No |
| **Convention validation** | Yes | No | No | No | No |
| **Circular dependency detection** | Yes | No | No | No | No |
| **Test coverage mapping** | Yes | No | No | No | No |
| **Glob pattern search** | Yes | Yes | Yes | No | No |
| **Fuzzy FQN resolution** | Yes | No | No | Yes | No |
| **External source resolution** | Yes | No | No | Yes | No |
| **Refactoring tools** | No | Yes (13) | No | Yes (6) | No |
| **Auto solution discovery** | Yes (BFS) | No | No | No | No |
| **Global dotnet tool** | Yes | Yes | No | No | No |
| **File watcher** | Yes | No | No | No | No |
| **LRU compilation cache** | Yes (configurable) | Yes | Yes | No | No |
| **Token-optimized responses** | Yes (30-150 tokens) | Relative paths | No | Stripped indentation | No |
| **.NET version** | 10 | 8 | 8 | 8 | 9 |
| **License** | Apache-2.0 | MIT | MIT | MIT | MIT |

## Key differentiators

### RoslynLens strengths

1. **19 anti-pattern detectors** — No other MCP server provides automated code
   quality analysis. Covers async, security, EF Core, logging, and domain-specific
   patterns.

2. **Convention validation** — Check naming, security, and dependency rules
   against your team's standards.

3. **Auto solution discovery** — BFS traversal finds your `.sln`/`.slnx`
   automatically. No manual path configuration needed.

4. **Circular dependency detection** — At both project and type level.

5. **Test coverage mapping** — Heuristic mapping between production types and
   test classes.

6. **Token efficiency** — Responses are 30-150 tokens. Minimal property names,
   no unnecessary data.

### What others do better

1. **SharpLensMcp** — 13 refactoring tools (rename, extract method, etc.).
   RoslynLens is read-only by design.

2. **SharpToolsMCP** — Git integration with automatic undo branches. Good for
   AI-driven refactoring workflows.

## Architecture comparison

| Aspect | RoslynLens | SharpLensMcp | SharpToolsMCP |
| ------ | ---------- | ------------ | ------------- |
| Transport | stdio | stdio | stdio + SSE |
| Discovery | `WithToolsFromAssembly()` | Manual registration | Manual registration |
| Workspace | MSBuildWorkspace + LRU | MSBuildWorkspace | MSBuildWorkspace |
| File sync | File watcher (auto) | Manual `sync_documents()` | Git-based |
| Responses | Typed records (JSON) | Structured JSON | Diffs + errors |
| Config | Environment variables | Environment variables | CLI flags |
