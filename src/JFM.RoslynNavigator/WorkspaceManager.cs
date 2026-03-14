using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace JFM.RoslynNavigator;

/// <summary>
/// Manages the Roslyn MSBuildWorkspace lifecycle, including lazy compilation loading,
/// LRU caching, and file watching for incremental updates.
/// </summary>
public sealed class WorkspaceManager : IDisposable
{
    private const int LazyLoadThreshold = 50;
    private const int MaxCachedCompilations = 50;

    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ConcurrentDictionary<ProjectId, (Compilation Compilation, long AccessCount)> _compilationCache = new();
    private long _accessCounter;

    private MSBuildWorkspace? _workspace;
    private Solution? _solution;
    private FileSystemWatcher? _sourceWatcher;
    private FileSystemWatcher? _projectWatcher;
    private string? _solutionDirectory;

    public WorkspaceState State { get; private set; } = WorkspaceState.NotStarted;
    public string? ErrorMessage { get; private set; }
    public int ProjectCount => _solution?.Projects.Count() ?? 0;

    public async Task LoadSolutionAsync(string solutionPath, CancellationToken ct)
    {
        State = WorkspaceState.Loading;

        try
        {
            _workspace = MSBuildWorkspace.Create();
            _workspace.RegisterWorkspaceFailedHandler(_ =>
            {
                // Log but don't fail — some projects may have missing references
            });

            _solution = await _workspace.OpenSolutionAsync(solutionPath, cancellationToken: ct);
            _solutionDirectory = Path.GetDirectoryName(Path.GetFullPath(solutionPath));

            if (_solution.Projects.Count() <= LazyLoadThreshold)
            {
                await WarmCompilationsAsync(ct);
            }

            SetupFileWatchers();
            State = WorkspaceState.Ready;
        }
        catch (Exception ex)
        {
            State = WorkspaceState.Error;
            ErrorMessage = ex.Message;
            throw;
        }
    }

    public string? EnsureReadyOrStatus(CancellationToken ct)
    {
        if (State == WorkspaceState.Ready)
            return null;

        var status = new { state = State.ToString(), message = ErrorMessage ?? "Workspace not ready", projectCount = ProjectCount };
        return System.Text.Json.JsonSerializer.Serialize(status);
    }

    public Solution? GetSolution() => _solution;

    public async Task<Compilation?> GetCompilationAsync(Project project, CancellationToken ct)
    {
        var accessCount = Interlocked.Increment(ref _accessCounter);

        if (_compilationCache.TryGetValue(project.Id, out var cached))
        {
            _compilationCache[project.Id] = (cached.Compilation, accessCount);
            return cached.Compilation;
        }

        var compilation = await project.GetCompilationAsync(ct);
        if (compilation is null)
            return null;

        _compilationCache[project.Id] = (compilation, accessCount);

        // Evict LRU if over capacity
        if (_compilationCache.Count > MaxCachedCompilations)
        {
            var lruKey = _compilationCache
                .OrderBy(kvp => kvp.Value.AccessCount)
                .First()
                .Key;
            _compilationCache.TryRemove(lruKey, out _);
        }

        return compilation;
    }

    public async Task<IReadOnlyList<(Project Project, Compilation Compilation)>> GetAllCompilationsAsync(CancellationToken ct)
    {
        if (_solution is null) return [];

        var results = new List<(Project, Compilation)>();
        foreach (var project in _solution.Projects)
        {
            var compilation = await GetCompilationAsync(project, ct);
            if (compilation is not null)
                results.Add((project, compilation));
        }

        return results;
    }

    private async Task WarmCompilationsAsync(CancellationToken ct)
    {
        if (_solution is null) return;

        foreach (var project in _solution.Projects)
        {
            ct.ThrowIfCancellationRequested();
            await GetCompilationAsync(project, ct);
        }
    }

    private void SetupFileWatchers()
    {
        if (_solutionDirectory is null) return;

        _sourceWatcher = new FileSystemWatcher(_solutionDirectory, "*.cs")
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime
        };
        _sourceWatcher.Changed += OnSourceFileChanged;
        _sourceWatcher.Created += OnSourceFileChanged;
        _sourceWatcher.EnableRaisingEvents = true;

        _projectWatcher = new FileSystemWatcher(_solutionDirectory, "*.csproj")
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite
        };
        _projectWatcher.Changed += OnProjectFileChanged;
        _projectWatcher.EnableRaisingEvents = true;
    }

    private void OnSourceFileChanged(object sender, FileSystemEventArgs e)
    {
        if (_solution is null || !File.Exists(e.FullPath)) return;

        _ = Task.Run(async () =>
        {
            // Debounce: wait a bit before processing
            await Task.Delay(200);

            await _writeLock.WaitAsync();
            try
            {
                var docId = _solution.GetDocumentIdsWithFilePath(e.FullPath).FirstOrDefault();
                if (docId is null) return;

                var text = await File.ReadAllTextAsync(e.FullPath);
                var sourceText = Microsoft.CodeAnalysis.Text.SourceText.From(text);
                _solution = _solution.WithDocumentText(docId, sourceText);

                // Invalidate compilation cache for the affected project
                var doc = _solution.GetDocument(docId);
                if (doc?.Project.Id is { } projectId)
                    _compilationCache.TryRemove(projectId, out _);
            }
            catch
            {
                // File may be locked or deleted; ignore
            }
            finally
            {
                _writeLock.Release();
            }
        });
    }

    private void OnProjectFileChanged(object sender, FileSystemEventArgs e)
    {
        // Project file changes require a full reload — clear everything
        _compilationCache.Clear();
    }

    public void Dispose()
    {
        _sourceWatcher?.Dispose();
        _projectWatcher?.Dispose();
        _workspace?.Dispose();
        _writeLock.Dispose();
    }
}
