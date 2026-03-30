# CLAUDE.md — RoslynLens

## Project

- **Type**: MCP server for token-efficient .NET codebase navigation via Roslyn
- **Distribution**: Global dotnet tool (`roslyn-lens`)
- **License**: Apache-2.0
- **NuGet**: `RoslynLens`
- **GitHub**: `jfmeyers/roslyn-lens`

## Stack

.NET 10 | C# 14 | Roslyn 5.3 | ModelContextProtocol 1.1.0 | xUnit v3

## Commands

```bash
dotnet build
dotnet test
dotnet pack -c Release -o ./nupkgs
dotnet tool install --global --add-source ./nupkgs RoslynLens
```

## Architecture

| Path | Role |
| ---- | ---- |
| `Program.cs` | Host + MCP stdio transport (logs → stderr) |
| `SolutionDiscovery.cs` | BFS `.sln`/`.slnx` auto-discovery (max 3 levels) |
| `WorkspaceManager.cs` | MSBuildWorkspace, LRU cache (50), wait-for-ready |
| `WorkspaceInitializer.cs` | BackgroundService for async solution loading |
| `SymbolResolver.cs` | Cross-project symbol resolution with file/line disambiguation |
| `Tools/` | 28 MCP tool implementations (one class per tool) |
| `Analyzers/` | 18 detectors + 2 base classes (`InvocationDetectorBase`, `ObjectCreationDetectorBase`) |
| `Responses/` | Token-optimized DTOs (records) |

## Conventions

- **Logs to stderr** — stdout is reserved for JSON-RPC (MCP protocol)
- **One class per file** — tools in `Tools/`, detectors in `Analyzers/`
- **Detector IDs** — `APxxx` for general .NET, `GR-*` for domain-specific
- **Token efficiency** — responses must be minimal (30-150 tokens). No full file contents.
- **Null semantic model** — all detectors must handle `SemanticModel? model = null` gracefully (syntax-only mode)
- **Tests** — each new detector needs a test class in `tests/.../Analyzers/` (xUnit v3 + Shouldly)

## Adding a new detector

1. Create `src/.../Analyzers/MyDetector.cs`:
   - Simple invocation match (`Foo.Bar()`) → extend `InvocationDetectorBase`
   - Simple creation match (`new Foo()`) → extend `ObjectCreationDetectorBase`
   - Complex logic → implement `IAntiPatternDetector` directly
2. Create `tests/.../Analyzers/MyDetectorTests.cs`
3. Use `CSharpSyntaxTree.ParseText(source)` in tests — no full compilation needed
4. Use `TestContext.Current.CancellationToken` (not `CancellationToken.None`)

## Adding a new tool

1. Create `src/.../Tools/MyTool.cs` with `[McpServerToolType]` attribute
2. Tools are auto-discovered via `WithToolsFromAssembly()`
3. Inject `WorkspaceManager` to access the solution/compilations
4. Call `workspace.EnsureReadyOrStatus(ct)` — waits for ready (up to timeout), returns status JSON if not

## Release

```bash
# 1. Check for outdated/vulnerable packages
dotnet list package --outdated
dotnet list package --vulnerable --include-transitive
# 2. Update <Version> in RoslynLens.csproj
# 3. Update THIRD-PARTY-NOTICES.md if dependencies changed
# 4. Commit and push
# 5. Tag and push — triggers CI + NuGet publish + GitHub Release
git tag v1.2.0
git push origin v1.2.0
```

Requires `NUGET_API_KEY` secret in GitHub repo settings.
