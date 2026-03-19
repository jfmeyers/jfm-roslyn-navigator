# Compound Tools

Compound tools aggregate multiple queries into a single MCP call, reducing agent
round-trips and token consumption. Each replaces 3-4 individual tool calls.

## `analyze_method`

Returns a method's signature, callers, dependency graph, and complexity metrics
in a single call.

### Parameters

| Name | Type | Required | Description |
| ---- | ---- | -------- | ----------- |
| `methodName` | string | yes | Name of the method to analyze |
| `className` | string | no | Containing class to disambiguate overloads |
| `depth` | int | no | Dependency graph depth (default 2) |

### What it combines

| Sub-result | Equivalent standalone tool |
| ---------- | ------------------------- |
| `Detail` | `get_symbol_detail` |
| `Callers` | `find_callers` |
| `Dependencies` | `get_dependency_graph` |
| `Complexity` | `get_complexity_metrics` |

### Example response

```json
{
  "Detail": {
    "Name": "ExecuteAsync",
    "Kind": "Method",
    "FullSignature": "RoslynLens.FindSymbolTool.ExecuteAsync(...)",
    "ReturnType": "Task<string>",
    "Parameters": [
      { "Name": "workspace", "Type": "WorkspaceManager" },
      { "Name": "name", "Type": "string" }
    ]
  },
  "Callers": {
    "Symbol": "...",
    "Callers": [
      { "Method": "Main", "ContainingType": "Program", "File": "Program.cs", "Line": 12 }
    ],
    "Total": 1
  },
  "Dependencies": {
    "Root": { "Symbol": "ExecuteAsync", "Calls": [...] },
    "Depth": 2
  },
  "Complexity": {
    "Cyclomatic": 4,
    "Cognitive": 6,
    "MaxNesting": 2,
    "LogicalLoc": 25
  }
}
```

### When to use

- Starting to understand an unfamiliar method
- Before refactoring — assess complexity and impact
- Code review — check who calls what and how complex it is

---

## `get_type_overview`

Returns a type's public API, hierarchy, implementations, and diagnostics in a
single call.

### Parameters

| Name | Type | Required | Description |
| ---- | ---- | -------- | ----------- |
| `typeName` | string | yes | Name of the type to analyze |

### What it combines

| Sub-result | Equivalent standalone tool |
| ---------- | ------------------------- |
| `Api` | `get_public_api` |
| `Hierarchy` | `get_type_hierarchy` |
| `Implementations` | `find_implementations` (interfaces only) |
| `Diagnostics` | `get_diagnostics` (file-scoped) |

### Example response

```json
{
  "Api": {
    "Type": "RoslynLens.WorkspaceManager",
    "Members": [
      { "Name": "LoadSolutionAsync", "Kind": "method", "Signature": "..." },
      { "Name": "GetCompilationAsync", "Kind": "method", "Signature": "..." },
      { "Name": "State", "Kind": "property", "ReturnType": "WorkspaceState" }
    ],
    "Total": 8
  },
  "Hierarchy": {
    "Type": { "Name": "WorkspaceManager", "Kind": "class" },
    "BaseTypes": [],
    "Interfaces": [{ "Name": "IDisposable", "Kind": "interface" }],
    "DerivedTypes": []
  },
  "Implementations": null,
  "Diagnostics": { "Diagnostics": [], "Total": 0, "Scope": "file" }
}
```

### When to use

- First encounter with a type — understand its shape quickly
- Reviewing an interface — see who implements it
- Checking for compiler issues on a specific type

---

## `get_file_overview`

Returns all types defined in a file, compiler diagnostics, and anti-pattern
violations in a single call.

### Parameters

| Name | Type | Required | Description |
| ---- | ---- | -------- | ----------- |
| `filePath` | string | yes | File path or suffix (e.g., `WorkspaceManager.cs`) |

### What it combines

| Sub-result | Equivalent standalone tool |
| ---------- | ------------------------- |
| `Types` | (custom — lists type declarations) |
| `Diagnostics` | `get_diagnostics` (file-scoped) |
| `AntiPatterns` | `detect_antipatterns` (file-scoped) |

### Example response

```json
{
  "File": "/src/RoslynLens/WorkspaceManager.cs",
  "Types": [
    { "Name": "WorkspaceManager", "Kind": "class", "Line": 11 }
  ],
  "Diagnostics": { "Diagnostics": [], "Total": 0, "Scope": "file" },
  "AntiPatterns": {
    "Violations": [
      {
        "Id": "AP005",
        "Severity": "Warning",
        "Message": "Broad catch without re-throw",
        "File": "WorkspaceManager.cs",
        "Line": 173,
        "Suggestion": "Catch specific exceptions or re-throw"
      }
    ],
    "Total": 1
  }
}
```

### When to use

- Opening a file for the first time — get the lay of the land
- Pre-commit check — any compiler warnings or anti-patterns?
- Code review — quick quality assessment of a file
