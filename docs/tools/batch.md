# Batch Tools

Batch tools resolve multiple queries in a single MCP call. Each item in the
batch is resolved independently — one failure does not fail the entire batch.

All batch tools accept **comma-separated** names as input.

## `find_symbols_batch`

Resolve multiple symbol names at once.

### Parameters

| Name | Type | Required | Description |
| ---- | ---- | -------- | ----------- |
| `names` | string | yes | Comma-separated symbol names |
| `kind` | string | no | Kind filter (class, method, etc.) |

### Example

Input: `names = "WorkspaceManager, SymbolResolver, RoslynLensConfig"`

```json
{
  "Items": [
    {
      "Query": "WorkspaceManager",
      "Result": { "Matches": [{ "Name": "RoslynLens.WorkspaceManager", "Kind": "class", "File": "WorkspaceManager.cs", "Line": 11 }], "Total": 1 },
      "Error": null
    },
    {
      "Query": "SymbolResolver",
      "Result": { "Matches": [{ "Name": "RoslynLens.SymbolResolver", "Kind": "class", "File": "SymbolResolver.cs", "Line": 9 }], "Total": 1 },
      "Error": null
    },
    {
      "Query": "RoslynLensConfig",
      "Result": { "Matches": [{ "Name": "RoslynLens.RoslynLensConfig", "Kind": "class", "File": "RoslynLensConfig.cs", "Line": 8 }], "Total": 1 },
      "Error": null
    }
  ],
  "Total": 3,
  "Succeeded": 3,
  "Failed": 0
}
```

---

## `get_public_api_batch`

Get the public API surface of multiple types in one call.

### Parameters

| Name | Type | Required | Description |
| ---- | ---- | -------- | ----------- |
| `typeNames` | string | yes | Comma-separated type names |

### When to use

- Comparing interfaces of related types
- Understanding a module's public surface quickly
- Generating API documentation

---

## `get_symbol_detail_batch`

Get full details (signature, parameters, return type, XML docs) for multiple
symbols in one call.

### Parameters

| Name | Type | Required | Description |
| ---- | ---- | -------- | ----------- |
| `symbolNames` | string | yes | Comma-separated symbol names |

### When to use

- Understanding a group of related methods
- Documenting an API surface
- Reviewing method signatures before a refactor

## Error handling

Each batch item is resolved independently. If one item fails (e.g., symbol not
found), the error is captured in that item's `Error` field:

```json
{
  "Query": "NonExistentType",
  "Result": null,
  "Error": "Type 'NonExistentType' not found"
}
```

The batch continues processing remaining items. The response includes `Succeeded`
and `Failed` counts for quick assessment.

## Token efficiency

Batch tools save tokens by eliminating repeated JSON-RPC overhead. For N queries:

| Approach | Round-trips | Overhead tokens |
| -------- | ----------- | --------------- |
| Individual calls | N | ~50 per call |
| Batch | 1 | ~50 total |

For 5 queries, that's ~200 tokens saved in overhead alone.
