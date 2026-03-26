# Troubleshooting

## Solution won't load

### Symptoms

- Tools return `{"state":"Loading","message":"Workspace not ready"}`
- Loading never completes

### Causes and fixes

**Missing .NET SDK**: RoslynLens requires the same SDK as your solution.

```bash
dotnet --list-sdks
```

Ensure the SDK version matches your `global.json` or `<TargetFramework>`.

**MSBuild restore needed**: The solution must be restorable.

```bash
dotnet restore MySolution.sln
```

**No solution found**: RoslynLens uses BFS auto-discovery (max 3 levels up).
If your solution is deeper, specify it explicitly:

```bash
roslyn-lens --solution /path/to/MySolution.sln
```

**Large solution**: Solutions with 50+ projects load asynchronously. Tools
return a "loading" status until ready. Wait for the background load to complete
(check stderr logs).

## Tools return empty results

### Symbol not found

- Check the exact name (case-sensitive first, then case-insensitive fallback)
- Use glob patterns: `*Service` instead of the full name
- Fuzzy resolution kicks in automatically for typos (Levenshtein distance <= 2)

### File not in solution

RoslynLens only sees files that are part of the loaded `.sln`/`.slnx`. Files
not referenced by any `.csproj` are invisible.

## Performance issues

### Slow first query

The first query for a project triggers compilation (can take 5-30s for large
projects). Subsequent queries hit the LRU cache.

### Tuning

| Issue | Environment variable | Default |
| ----- | -------------------- | ------- |
| Queries timing out | `ROSLYN_LENS_TIMEOUT_SECONDS` | 30 |
| Too many results | `ROSLYN_LENS_MAX_RESULTS` | 100 |
| Cache misses | `ROSLYN_LENS_CACHE_SIZE` | 50 |

For solutions with many projects, increase `ROSLYN_LENS_CACHE_SIZE` to reduce
recompilation.

## File watcher issues

### Changes not picked up

- `.cs` files: 200ms debounce, then incremental text update
- `.csproj` files: full cache clear (slower but necessary)
- Files outside the solution directory are not watched

### File locked by another process

The watcher silently ignores locked files. If edits aren't reflected, save the
file again.

## MCP connection issues

### Claude Code doesn't see the tools

1. Check registration: `claude mcp list`
2. Verify the tool is installed: `roslyn-lens --help`
3. Check stderr logs for errors
4. Restart Claude Code after adding MCP configuration

```bash
roslyn-lens 2>/tmp/roslyn-lens.log
```

### JSON-RPC errors in logs

RoslynLens logs to **stderr** (stdout is reserved for JSON-RPC). If you see
protocol errors, ensure nothing else writes to stdout (no `Console.WriteLine`
in your code that gets loaded).

## Anti-pattern false positives

### AP008 (MissingCancellationToken) on event handlers

Event handlers can't accept `CancellationToken`. This is a known limitation.
Filter by severity or file scope.

### GR-SECRET on test data

Test files with hardcoded passwords for testing trigger GR-SECRET. Use
placeholder values like `CHANGE_ME` or `${env:PASSWORD}` — these are
automatically excluded.

### AP005 (BroadCatch) in infrastructure code

Top-level error handlers legitimately catch `Exception`. Scope the detector
to specific files/projects to reduce noise.

## Getting help

- [GitHub Issues](https://github.com/jfmeyers/roslyn-lens/issues)
- Check stderr logs: `ROSLYN_LENS_LOG_LEVEL=Debug roslyn-lens`
