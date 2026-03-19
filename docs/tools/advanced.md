# Advanced Analysis Tools

These tools provide deeper code analysis capabilities beyond navigation and
inspection: complexity metrics, data flow, control flow, duplicate detection,
and external source resolution.

## `get_complexity_metrics`

Compute cyclomatic complexity, cognitive complexity, maximum nesting depth, and
logical lines of code for methods.

### Parameters

| Name | Type | Required | Description |
| ---- | ---- | -------- | ----------- |
| `name` | string | yes | Method, type, or project name |
| `scope` | string | no | `method`, `type`, or `project` (default `method`) |
| `className` | string | no | Containing class for method disambiguation |
| `threshold` | int | no | Minimum cyclomatic complexity to include (default 0) |
| `maxResults` | int | no | Max results (default 50) |

### Metrics explained

| Metric | What it measures | Threshold guidance |
| ------ | --------------- | ------------------ |
| **Cyclomatic** | Number of linearly independent paths. Counts `if`, `while`, `for`, `foreach`, `case`, `&&`, `\|\|`, `??`, `catch`, ternary. Base = 1. | < 10 good, 10-20 moderate, > 20 high risk |
| **Cognitive** | Human-readability cost (SonarSource model). Nesting structures add 1 + nesting level as penalty. `else if` does not increase nesting. | < 15 good, 15-30 moderate, > 30 refactor |
| **Max nesting** | Deepest nesting level of control flow blocks. | < 3 good, 4 yellow, 5+ refactor |
| **Logical LOC** | Non-blank, non-comment lines. | < 30 good, 30-60 moderate, > 60 split |

### Example: scan a project for hotspots

```text
name: "MyProject"
scope: "project"
threshold: 10
```

Returns methods sorted by cyclomatic complexity (descending), filtered to those
above the threshold.

```json
{
  "Methods": [
    {
      "Method": "ProcessOrder",
      "ContainingType": "OrderService",
      "File": "OrderService.cs",
      "Line": 45,
      "Metrics": { "Cyclomatic": 18, "Cognitive": 24, "MaxNesting": 4, "LogicalLoc": 52 }
    },
    {
      "Method": "ValidateInput",
      "ContainingType": "InputValidator",
      "File": "InputValidator.cs",
      "Line": 12,
      "Metrics": { "Cyclomatic": 12, "Cognitive": 15, "MaxNesting": 3, "LogicalLoc": 28 }
    }
  ],
  "Total": 2
}
```

---

## `analyze_data_flow`

Analyze variable data flow within a method using Roslyn's `SemanticModel.AnalyzeDataFlow()`.

### Parameters

| Name | Type | Required | Description |
| ---- | ---- | -------- | ----------- |
| `methodName` | string | yes | Name of the method |
| `className` | string | no | Containing class to disambiguate |

### Response fields

| Field | Description |
| ----- | ----------- |
| `VariablesDeclared` | Variables declared inside the method body |
| `DataFlowsIn` | Variables whose values flow into the region from outside |
| `DataFlowsOut` | Variables whose values flow out of the region |
| `ReadInside` | Variables read inside the method |
| `WrittenInside` | Variables written inside the method |
| `AlwaysAssigned` | Variables always assigned before the method exits |
| `Captured` | Variables captured by lambdas or local functions |

### When to use

- Understanding side effects — which variables escape the method?
- Refactoring — can a block be safely extracted?
- Lambda analysis — which variables are captured (closures)?

---

## `analyze_control_flow`

Analyze control flow within a method using Roslyn's `SemanticModel.AnalyzeControlFlow()`.

### Parameters

| Name | Type | Required | Description |
| ---- | ---- | -------- | ----------- |
| `methodName` | string | yes | Name of the method |
| `className` | string | no | Containing class to disambiguate |

### Response fields

| Field | Description |
| ----- | ----------- |
| `StartReachable` | Whether the first statement is reachable |
| `EndReachable` | Whether the end of the method is reachable (no guaranteed return/throw) |
| `ReturnStatements` | Number of return statements |
| `ExitPoints` | Total exit points (returns, throws, breaks) |

### When to use

- Dead code detection — is the end of a method reachable?
- Complexity assessment — how many exit points?
- Refactoring — ensuring all paths return

---

## `detect_duplicates`

Detect structurally similar code blocks using AST fingerprinting.

### How it works

1. Extract all method bodies as statement sequences
2. Normalize each: replace identifiers with `ID`, literals with `LIT`
   (creates a structural fingerprint)
3. Hash the normalized form (SHA-256)
4. Group methods with identical hashes (exact structural duplicates)

Two methods that differ only in variable names or literal values will produce
the same fingerprint.

### Parameters

| Name | Type | Required | Description |
| ---- | ---- | -------- | ----------- |
| `projectFilter` | string | no | Scope to a specific project |
| `minStatements` | int | no | Minimum statements to consider (default 5) |
| `maxResults` | int | no | Maximum duplicate groups (default 20) |

### Example response

```json
{
  "Groups": [
    {
      "Occurrences": [
        { "Method": "ValidateEmail", "ContainingType": "UserService", "File": "UserService.cs", "Line": 34 },
        { "Method": "ValidatePhone", "ContainingType": "ContactService", "File": "ContactService.cs", "Line": 22 }
      ],
      "Similarity": 1.0,
      "StatementCount": 8
    }
  ],
  "TotalGroups": 1,
  "TotalDuplicates": 2
}
```

### When to use

- Before a refactoring sprint — find extraction candidates
- Code review — spot copy-paste patterns
- Technical debt assessment — quantify duplication

---

## `resolve_external_source`

Resolve source code for external (NuGet/framework) symbols via SourceLink or
decompilation.

### Parameters

| Name | Type | Required | Description |
| ---- | ---- | -------- | ----------- |
| `symbolName` | string | yes | External symbol name |
| `assemblyName` | string | no | Assembly name to narrow search |

### Resolution hierarchy

1. **SourceLink** — if the symbol has source in the solution or SourceLink
   metadata in the PDB
2. **Decompilation** — using ICSharpCode.Decompiler as a fallback
3. **None** — if neither method succeeds

### Example response

```json
{
  "Symbol": "Microsoft.Extensions.Logging.ILogger",
  "Assembly": "Microsoft.Extensions.Logging.Abstractions",
  "SourceLinkUrl": null,
  "DecompiledSource": "using System;\n\nnamespace Microsoft.Extensions.Logging\n{\n    public interface ILogger\n    {\n        ...\n    }\n}",
  "ResolutionMethod": "decompilation"
}
```

Decompiled source is truncated to 60 lines for token efficiency.

### When to use

- Understanding a third-party API's implementation details
- Debugging framework behavior without leaving the MCP server
- Verifying NuGet package behavior
