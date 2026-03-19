# Configuration

## Claude Code — User scope (recommended)

Registers the MCP server globally, available in all projects:

```bash
claude mcp add --scope user --transport stdio roslyn-lens -- roslyn-lens
```

## Claude Code — Project scope

Registers for a single project only:

```bash
claude mcp add --transport stdio roslyn-lens -- roslyn-lens
```

## Manual `.mcp.json`

Create `.mcp.json` in your project root:

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

## Solution discovery

By default, the server finds the nearest `.sln` or `.slnx` file using breadth-first
search from the current directory (up to 3 levels).

To specify a solution explicitly:

```bash
claude mcp add --scope user --transport stdio roslyn-lens -- roslyn-lens --solution /path/to/My.slnx
```

## Verify connection

```bash
claude mcp list
```

Expected output:

```text
roslyn-lens: roslyn-lens — Connected
```

## Remove

```bash
claude mcp remove roslyn-lens
```
