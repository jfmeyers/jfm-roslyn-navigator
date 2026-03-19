# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-03-19

### Added

- 28 MCP navigation and analysis tools
- 18 anti-pattern detectors (AP001-AP009, GR-GUID, GR-SECRET, GR-SYNC-EF,
  GR-BADREQ, GR-REGEX, GR-SLEEP, GR-CONSOLE, GR-CFGAWAIT, GR-DTO)
- Compound tools (`analyze_method`, `get_type_overview`, `get_file_overview`)
- Batch tools (`find_symbols_batch`, `get_public_api_batch`, `get_symbol_detail_batch`)
- Advanced analysis (`analyze_data_flow`, `analyze_control_flow`,
  `get_complexity_metrics`, `detect_duplicates`, `resolve_external_source`)
- Glob pattern search (`*Service`, `Get*User`) in `find_symbol`
- Fuzzy FQN resolution with partial namespace matching and Levenshtein distance
- Dead code analysis with granular filters (`includePublicMembers`,
  `includeEntryPoints`, `projectFilter`, `fileFilter`)
- Environment variable configuration (`ROSLYN_NAV_TIMEOUT_SECONDS`,
  `ROSLYN_NAV_MAX_RESULTS`, `ROSLYN_NAV_CACHE_SIZE`, `ROSLYN_NAV_LOG_LEVEL`)
- BFS solution auto-discovery (`.sln`/`.slnx`, max 3 levels)
- LRU compilation cache (configurable, default 50 entries)
- File watcher for incremental updates
- Background async solution loading
- Cross-project symbol resolution via `SymbolResolver`
- ICSharpCode.Decompiler for external source resolution
- SonarCloud integration
- GitHub Actions CI/CD (build + release + NuGet publish)
- Global dotnet tool distribution (`dotnet tool install --global RoslynLens`)
- Comprehensive documentation in `docs/`

### Changed

- Renamed project from `JFM.RoslynNavigator` to `RoslynLens`
- NuGet package ID changed to `RoslynLens`
- MCP server name changed to `roslyn-lens`
- Tool command changed to `roslyn-lens`

## [0.1.1] - 2026-03-14

Initial release as `JFM.RoslynNavigator`.

### Added

- 17 MCP navigation tools (find_symbol, find_references, find_callers,
  find_implementations, find_overrides, find_dead_code, get_public_api,
  get_symbol_detail, get_project_graph, get_dependency_graph,
  get_module_depends_on, get_type_hierarchy, get_diagnostics,
  get_test_coverage_map, detect_antipatterns, detect_circular_dependencies,
  validate_granit_conventions)
- 18 anti-pattern detectors
- MSBuildWorkspace with LRU compilation cache
- BFS solution auto-discovery (`.sln`/`.slnx`, max 3 levels)
- Background async solution loading
- GitHub Actions CI/CD (build + release + NuGet publish)
- Global dotnet tool distribution (`dotnet tool install --global JFM.RoslynNavigator`)

[1.0.0]: https://github.com/jfmeyers/roslyn-lens/compare/v0.1.1...v1.0.0
[0.1.1]: https://github.com/jfmeyers/roslyn-lens/releases/tag/v0.1.1
