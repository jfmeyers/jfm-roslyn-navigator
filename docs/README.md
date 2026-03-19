# RoslynLens Documentation

A token-efficient MCP server for .NET codebase navigation via Roslyn.
28 tools, 19 anti-pattern detectors, configurable and extensible.

## Getting Started

- [Quick Start](getting-started/quickstart.md) — up and running in 2 minutes
- [Installation](getting-started/installation.md) — NuGet, source build, updates
- [Configuration](getting-started/configuration.md) — MCP registration, solution
  discovery
- [Configuration Reference](getting-started/configuration-reference.md) — all
  environment variables, CLI args, tuning guide
- [Troubleshooting](getting-started/troubleshooting.md) — common issues and fixes

## Tools Reference (28 tools)

### Core tools (17)

- [Navigation Tools](tools/navigation.md) — find_symbol (with glob patterns),
  find_references, find_implementations, find_callers, find_overrides
- [Inspection Tools](tools/inspection.md) — get_public_api, get_symbol_detail,
  get_type_hierarchy, get_dependency_graph
- [Project Tools](tools/project.md) — get_project_graph, get_diagnostics,
  get_test_coverage_map
- [Analysis Tools](tools/analysis.md) — detect_antipatterns,
  detect_circular_dependencies, find_dead_code
- [Modular Architecture Tools](tools/modular.md) — get_module_depends_on,
  validate_conventions

### v1.1.0 tools (11)

- [Advanced Analysis Tools](tools/advanced.md) — get_complexity_metrics,
  analyze_data_flow, analyze_control_flow, detect_duplicates,
  resolve_external_source
- [Compound Tools](tools/compound.md) — analyze_method, get_type_overview,
  get_file_overview
- [Batch Tools](tools/batch.md) — find_symbols_batch, get_public_api_batch,
  get_symbol_detail_batch

## Anti-Pattern Detectors (19)

- [General .NET Detectors](detectors/general.md) — AP001-AP009 (async, error
  handling, resource management, observability)
- [Domain-Specific Detectors](detectors/domain.md) — GR-GUID, GR-SECRET,
  GR-DTO, and 7 more

## Architecture

- [How It Works](architecture/how-it-works.md) — startup sequence, workspace
  management, tool execution flow
- [Adding a Tool](architecture/adding-a-tool.md) — step-by-step guide with
  templates
- [Adding a Detector](architecture/adding-a-detector.md) — step-by-step guide
  with test templates

## Other

- [Comparison with Alternatives](comparison.md) — feature matrix vs
  SharpLensMcp, RoslynMCP, SharpToolsMCP, CodeAnalysisMCP
