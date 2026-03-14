namespace JFM.RoslynNavigator.Responses;

/// <summary>
/// Token-optimized response DTOs for MCP tools.
/// Records with minimal property names to reduce JSON token count.
/// </summary>

// Status
public record StatusResponse(string State, string? Message = null, int? ProjectCount = null);

// find_symbol
public record SymbolMatch(string Name, string Kind, string? File, int? Line, string? Project);
public record SymbolSearchResult(List<SymbolMatch> Matches, int Total);

// find_references
public record ReferenceLocation(string File, int Line, string? Kind, string? Context);
public record ReferencesResult(string Symbol, List<ReferenceLocation> Locations, int Total);

// find_implementations
public record ImplementationMatch(string Name, string Kind, string? File, int? Line, string? Project);
public record ImplementationsResult(string Symbol, List<ImplementationMatch> Implementations, int Total);

// find_callers
public record CallerInfo(string Method, string? ContainingType, string? File, int? Line);
public record CallersResult(string Symbol, List<CallerInfo> Callers, int Total);

// find_overrides
public record OverrideInfo(string Method, string ContainingType, string? File, int? Line);
public record OverridesResult(string Symbol, List<OverrideInfo> Overrides, int Total);

// get_type_hierarchy
public record TypeHierarchyNode(string Name, string Kind, string? File, int? Line);
public record TypeHierarchyResult(
    TypeHierarchyNode Type,
    List<TypeHierarchyNode> BaseTypes,
    List<TypeHierarchyNode> Interfaces,
    List<TypeHierarchyNode> DerivedTypes);

// get_public_api
public record ApiMember(string Name, string Kind, string Signature, string? ReturnType);
public record PublicApiResult(string Type, List<ApiMember> Members, int Total);

// get_symbol_detail
public record ParameterDetail(string Name, string Type, bool HasDefault, string? DefaultValue);
public record SymbolDetail(
    string Name, string Kind, string FullSignature,
    string? ReturnType, string? File, int? Line,
    List<ParameterDetail>? Parameters, string? XmlDoc);

// get_project_graph
public record ProjectNode(string Name, string? Framework, List<string> References);
public record ProjectGraphResult(List<ProjectNode> Projects, int Total);

// get_dependency_graph
public record DependencyNode(string Symbol, string? File, int? Line, List<DependencyNode>? Calls);
public record DependencyGraphResult(DependencyNode Root, int Depth);

// get_diagnostics
public record DiagnosticInfo(string Id, string Severity, string Message, string? File, int? Line);
public record DiagnosticsResult(List<DiagnosticInfo> Diagnostics, int Total, string Scope);

// find_dead_code
public record DeadCodeEntry(string Name, string Kind, string? File, int? Line, string? Project);
public record DeadCodeResult(List<DeadCodeEntry> Entries, int Total);

// detect_antipatterns
public record AntiPatternEntry(string Id, string Severity, string Message, string? File, int? Line, string? Suggestion);
public record AntiPatternsResult(List<AntiPatternEntry> Violations, int Total);

// detect_circular_dependencies
public record CycleEntry(List<string> Nodes);
public record CircularDependenciesResult(string Scope, List<CycleEntry> Cycles, int Total);

// get_test_coverage_map
public record CoverageEntry(string ProductionType, string? TestType, string? TestFile, string Status);
public record TestCoverageMapResult(List<CoverageEntry> Entries, int Total, int Covered, int Uncovered);

// get_module_depends_on (Granit-specific)
public record ModuleDependency(string Module, string Project, string? File, int? Line, List<ModuleDependency>? Dependencies);
public record ModuleDependsOnResult(ModuleDependency Root, int TotalModules, string Direction);

// validate_granit_conventions (Granit-specific)
public record ConventionViolation(string Category, string Id, string Severity, string Message, string? File, int? Line, string? Suggestion);
public record GranitConventionsResult(List<ConventionViolation> Violations, int Total, Dictionary<string, int> ByCategory);
