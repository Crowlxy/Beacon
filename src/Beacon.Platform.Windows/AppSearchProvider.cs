using System.Runtime.CompilerServices;
using Beacon.Contracts;
using Beacon.Core;
using Beacon.Platform.Windows.Programs;

namespace Beacon.Platform.Windows;

public sealed class AppSearchProvider : ISearchProvider, IDisposable
{
    private readonly SemaphoreSlim _buildGate = new(1, 1);
    private readonly object _cacheGate = new();
    private readonly Dictionary<string, SearchResultDto> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<FileSystemWatcher> _watchers = [];
    private bool _initialized;
    private volatile bool _watchersHealthy;

    public const string Id = "windows.applications";
    public string ProviderId => Id;

    public async IAsyncEnumerable<SearchResultDto> SearchAsync(
        SearchRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (request.Scope is not (QueryScope.All or QueryScope.Applications) ||
            string.IsNullOrWhiteSpace(request.RawQuery)) yield break;
        await EnsureCacheAsync(false, cancellationToken);
        SearchResultDto[] matches;
        lock (_cacheGate)
            matches = _cache.Values
                .Where(item => FuzzyMatcher.Match(request.RawQuery, item.Title).Success)
                .ToArray();
        foreach (var item in matches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
        }
    }

    public Task RefreshIfWatcherUnavailableAsync() =>
        _initialized && _watchersHealthy ? Task.CompletedTask : EnsureCacheAsync(true, CancellationToken.None);

    public async Task<IReadOnlyList<SearchResultDto>> BrowseAsync(CancellationToken cancellationToken = default)
    {
        await EnsureCacheAsync(false, cancellationToken);
        lock (_cacheGate) return _cache.Values.ToArray();
    }

    private async Task EnsureCacheAsync(bool force, CancellationToken cancellationToken)
    {
        await _buildGate.WaitAsync(cancellationToken);
        try
        {
            if (_initialized && !force) return;
            var rebuilt = await Task.Run(BuildCache, cancellationToken);
            lock (_cacheGate)
            {
                _cache.Clear();
                foreach (var item in rebuilt) _cache[item.Id] = item;
            }
            _initialized = true;
            StartWatchers();
        }
        finally { _buildGate.Release(); }
    }

    private static List<SearchResultDto> BuildCache()
    {
        var results = new List<SearchResultDto>();
        foreach (var root in StartMenuRoots())
        {
            if (!Directory.Exists(root)) continue;
            foreach (var shortcut in EnumerateShortcuts(root)) results.Add(CreateResult(shortcut));
        }
        results.AddRange(UWPPackage.All().Select(CreateResult));
        return results;
    }

    private static SearchResultDto CreateResult(string shortcut)
    {
        var name = ShellLocalization.DisplayName(shortcut);
        var link = ShellLinkReader.Read(shortcut);
        return new SearchResultDto
        {
            Id = shortcut,
            ProviderId = Id,
            Title = name,
            Subtitle = string.IsNullOrWhiteSpace(link.TargetPath) ? shortcut : link.TargetPath,
            Kind = ResultKind.Application,
            Icon = new(IconSource.FileShellIcon, shortcut),
            FilePath = shortcut,
            ExecutionToken = shortcut,
            AutoCompleteText = name,
        };
    }

    private static SearchResultDto CreateResult(UWPPackage app) => new()
    {
        Id = app.Path,
        ProviderId = Id,
        Title = app.Name,
        Subtitle = "Windows app",
        Kind = ResultKind.Application,
        Icon = new(IconSource.FileShellIcon, app.Path),
        FilePath = app.Path,
        ExecutionToken = app.Path,
        AutoCompleteText = app.Name,
    };

    private void StartWatchers()
    {
        DisposeWatchers();
        _watchersHealthy = true;
        try
        {
            foreach (var root in StartMenuRoots())
            {
                if (!Directory.Exists(root)) continue;
                var watcher = new FileSystemWatcher(root, "*.lnk")
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                };
                watcher.Created += OnChanged;
                watcher.Changed += OnChanged;
                watcher.Deleted += OnDeleted;
                watcher.Renamed += OnRenamed;
                watcher.Error += OnWatcherError;
                watcher.EnableRaisingEvents = true;
                _watchers.Add(watcher);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _watchersHealthy = false;
            DisposeWatchers();
        }
    }

    private void OnChanged(object sender, FileSystemEventArgs args)
    {
        if (!File.Exists(args.FullPath)) return;
        var result = CreateResult(args.FullPath);
        lock (_cacheGate) _cache[result.Id] = result;
    }

    private void OnDeleted(object sender, FileSystemEventArgs args)
    {
        lock (_cacheGate) _cache.Remove(args.FullPath);
    }

    private void OnRenamed(object sender, RenamedEventArgs args)
    {
        lock (_cacheGate) _cache.Remove(args.OldFullPath);
        OnChanged(sender, args);
    }

    private void OnWatcherError(object sender, ErrorEventArgs args) => _watchersHealthy = false;

    private static IEnumerable<string> StartMenuRoots()
    {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
    }

    private static IEnumerable<string> EnumerateShortcuts(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.TryPop(out var directory))
        {
            string[] files;
            string[] directories;
            try
            {
                files = Directory.GetFiles(directory, "*.lnk");
                directories = Directory.GetDirectories(directory);
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException) { continue; }
            foreach (var file in files) yield return file;
            foreach (var child in directories) pending.Push(child);
        }
    }

    private void DisposeWatchers()
    {
        foreach (var watcher in _watchers) watcher.Dispose();
        _watchers.Clear();
    }

    public void Dispose()
    {
        DisposeWatchers();
        _buildGate.Dispose();
    }
}
