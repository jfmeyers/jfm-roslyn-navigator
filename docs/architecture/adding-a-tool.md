# Adding a Tool

## 1. Create the tool

Create `src/RoslynLens/Tools/MyTool.cs`:

```csharp
using System.ComponentModel;
using ModelContextProtocol.Server;
using RoslynLens.Responses;

namespace RoslynLens.Tools;

[McpServerToolType]
public static class MyTool
{
    [McpServerTool("my_tool"), Description("Short description of what this tool does.")]
    public static async Task<string> Execute(
        WorkspaceManager workspace,
        [Description("Parameter description")] string paramName,
        [Description("Optional filter")] string? optionalParam = null,
        CancellationToken cancellationToken = default)
    {
        if (workspace.State != WorkspaceState.Ready)
            return "Workspace is still loading. Please try again shortly.";

        var solution = workspace.CurrentSolution;
        if (solution is null)
            return "No solution loaded.";

        // Your Roslyn logic here
        var results = new List<SymbolLocation>();

        // ... query the solution ...

        return results.Count == 0
            ? "No results found."
            : string.Join("\n", results);
    }
}
```

### Conventions

- **Tool name**: `snake_case` (MCP convention)
- **Description**: One sentence, shown to Claude as tool documentation
- **Check workspace state**: Always return a helpful message if not `Ready`
- **Token efficiency**: Return minimal, structured text — not full file contents
- **CancellationToken**: Always accept it as last parameter

## 2. Registration

Tools are auto-discovered by `WithToolsFromAssembly()` via the `[McpServerToolType]`
attribute. No manual registration needed.

## 3. Verify

```bash
dotnet build
```

Test the tool appears in the tool list:

```bash
claude mcp list
```

Then ask Claude to use it in a conversation.
