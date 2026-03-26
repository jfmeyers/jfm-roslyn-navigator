# Configuration Reference

RoslynLens is configured via environment variables and CLI arguments.

## Environment variables

All environment variables use the `ROSLYN_LENS_` prefix.

| Variable | Type | Default | Description |
| -------- | ---- | ------- | ----------- |
| `ROSLYN_LENS_TIMEOUT_SECONDS` | int | 30 | Maximum time (seconds) for any Roslyn operation before timeout |
| `ROSLYN_LENS_MAX_RESULTS` | int | 100 | Maximum number of results returned per query |
| `ROSLYN_LENS_CACHE_SIZE` | int | 50 | Number of project compilations kept in the LRU cache |
| `ROSLYN_LENS_LOG_LEVEL` | string | Information | Log verbosity: `Trace`, `Debug`, `Information`, `Warning`, `Error` |

### Setting env vars in `.mcp.json`

```json
{
  "mcpServers": {
    "roslyn-lens": {
      "type": "stdio",
      "command": "roslyn-lens",
      "args": [],
      "env": {
        "ROSLYN_LENS_TIMEOUT_SECONDS": "60",
        "ROSLYN_LENS_CACHE_SIZE": "100",
        "ROSLYN_LENS_LOG_LEVEL": "Debug"
      }
    }
  }
}
```

### Setting env vars via Claude Code CLI

```bash
claude mcp add --scope user --transport stdio \
  -e ROSLYN_LENS_TIMEOUT_SECONDS=60 \
  -e ROSLYN_LENS_CACHE_SIZE=100 \
  roslyn-lens -- roslyn-lens
```

## CLI arguments

| Argument | Short | Description |
| -------- | ----- | ----------- |
| `--solution <path>` | `-s` | Explicit path to `.sln` or `.slnx` file |

When no `--solution` is provided, RoslynLens uses BFS auto-discovery from the
current working directory (max 3 levels up). It prefers `.slnx` over `.sln`.

### Skipped directories during auto-discovery

`.git`, `.vs`, `.idea`, `node_modules`, `bin`, `obj`, `packages`, `artifacts`,
`TestResults`, `.claude`, `nupkgs`

## Tuning guide

### Small solutions (< 10 projects)

Default settings work well. All compilations are warmed at startup.

### Medium solutions (10-50 projects)

Defaults are fine. Compilations are lazy-loaded on first access.

### Large solutions (50+ projects)

```bash
ROSLYN_LENS_CACHE_SIZE=100
ROSLYN_LENS_TIMEOUT_SECONDS=60
```

Increase cache size to avoid recompilation churn. Increase timeout for initial
compilation of large projects.

### Monorepos with multiple solutions

Specify the solution explicitly:

```bash
roslyn-lens --solution ./src/Backend/Backend.slnx
```

## Logging

All logs go to **stderr** (stdout is reserved for MCP JSON-RPC).

```bash
# Debug logging to a file
ROSLYN_LENS_LOG_LEVEL=Debug roslyn-lens 2>/tmp/roslyn-lens.log
```

Log levels:

- **Trace**: All Roslyn operations, cache hits/misses
- **Debug**: Symbol resolution, file watcher events
- **Information**: Startup, solution loading, tool execution
- **Warning**: Missing projects, failed compilations
- **Error**: Unrecoverable failures
