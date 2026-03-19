# Analysis Tools

## detect_antipatterns

Run anti-pattern detectors across the solution. See [General Detectors](../detectors/general.md)
and [Domain-Specific Detectors](../detectors/domain.md) for the full list.

| Parameter | Type | Required | Description |
| --------- | ---- | -------- | ----------- |
| `file` | string | no | Scan a specific file only |
| `projectFilter` | string | no | Filter by project name |
| `severity` | string | no | `error`, `warning`, `info`, or `all` (default: `all`) |
| `maxResults` | int | no | Max results to return (default: 100) |

**Example prompt:** "Detect anti-patterns in MyApp.Storage"

**Returns:** Violations with detector ID, severity, message, file path, and line number.

---

## detect_circular_dependencies

Detect circular dependencies at the project or type level using DFS cycle detection.

| Parameter | Type | Required | Description |
| --------- | ---- | -------- | ----------- |
| `scope` | string | no | `projects` (default) or `types` |
| `projectFilter` | string | no | Filter by project name |

**Example prompt:** "Check for circular project dependencies"

**Returns:** List of cycles found (e.g., `A → B → C → A`).

---

## find_dead_code

Detect unused types, methods, or properties across the solution. Supports
granular filtering to reduce false positives.

| Parameter | Type | Required | Description |
| --------- | ---- | -------- | ----------- |
| `scope` | string | no | `solution` (default), `project`, or `file` |
| `path` | string | no | Project/file path (required for `project`/`file` scopes) |
| `kind` | string | no | `type`, `method`, `property`, or `all` (default: `all`) |
| `maxResults` | int | no | Max results to return (default: 50) |
| `includePublicMembers` | bool | no | Include public members in results (default: `false`) |
| `includeEntryPoints` | bool | no | Include entry points and test methods (default: `false`) |
| `projectFilter` | string | no | Comma-separated project names to scope analysis |
| `fileFilter` | string | no | Comma-separated file path suffixes to scope analysis |

### Default behavior

By default, `find_dead_code` **excludes**:

- Public members (may be consumed by external code)
- Entry points (`Main`, `ConfigureServices`, `CreateHostBuilder`)
- Test methods (`[Fact]`, `[Test]`, `[Theory]`, etc.)
- API controllers (`[ApiController]`)
- MCP tool classes (`[McpServerToolType]`)
- Interface implementations
- Method overrides

Set `includePublicMembers=true` and/or `includeEntryPoints=true` to include
these in the results.

**Example prompts:**

- "Find dead code in MyApp.Caching"
- "Find unused public APIs" → `includePublicMembers=true`
- "Find dead code only in the Services project" → `projectFilter="Services"`

**Returns:** Unused symbols with kind, name, file path, and line number.
