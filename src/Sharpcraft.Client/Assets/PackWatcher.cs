// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Assets;

/// <summary>
/// Watches pack folders and reports a burst of file changes once, after the writing stops
/// (docs/decisions/0016-resource-pack-file-watch.md).
/// </summary>
/// <remarks>
/// Only directory packs can be watched meaningfully: an editor writes a file in several steps, and a zip is a single
/// archive that is replaced whole. The watcher therefore only signals "something changed"; the caller rebuilds the stack.
/// It exists only when the switch is on, because watching files is observable behaviour the reference does not have.
/// </remarks>
public sealed class PackWatcher : IDisposable
{
    /// <summary>Environment variable that turns watching on.</summary>
    public const string SwitchName = "MC_RESOURCE_PACK_WATCH";

    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly Action _onChange;
    private readonly TimeSpan _quiet;
    private readonly Lock _gate = new();
    private Timer? _timer;
    private bool _disposed;

    private PackWatcher(IEnumerable<string> folders, TimeSpan quiet, Action onChange)
    {
        _quiet = quiet;
        _onChange = onChange;

        foreach (var folder in folders)
        {
            if (!Directory.Exists(folder))
            {
                continue;
            }

            var watcher = new FileSystemWatcher(folder)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                EnableRaisingEvents = true
            };

            watcher.Changed += OnWatcherEvent;
            watcher.Created += OnWatcherEvent;
            watcher.Deleted += OnWatcherEvent;
            watcher.Renamed += OnWatcherEvent;
            _watchers.Add(watcher);
        }
    }

    /// <summary>True when the switch is on.</summary>
    public static bool IsEnabled =>
        string.Equals(Environment.GetEnvironmentVariable(SwitchName), "1", StringComparison.Ordinal);

    /// <summary>Number of folders actually being watched.</summary>
    public int FolderCount => _watchers.Count;

    /// <summary>
    /// Starts watching when the switch is on, otherwise returns null and creates nothing.
    /// </summary>
    /// <param name="folders">Pack folders, such as the entries named by <c>MC_RESOURCE_PACKS</c>.</param>
    /// <param name="onChange">Called once per burst.</param>
    /// <param name="quiet">How long the folders must be quiet before reporting. Defaults to 300 ms.</param>
    public static PackWatcher? ForEnvironment(
        IEnumerable<string> folders,
        Action onChange,
        TimeSpan? quiet = null)
    {
        ArgumentNullException.ThrowIfNull(folders);
        ArgumentNullException.ThrowIfNull(onChange);

        if (!IsEnabled)
        {
            return null;
        }

        return new PackWatcher(folders, quiet ?? TimeSpan.FromMilliseconds(300), onChange);
    }

    private void OnWatcherEvent(object sender, FileSystemEventArgs args)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            // Every event restarts the timer, so a save that writes five files reports once.
            _timer ??= new Timer(_ => Fire(), null, Timeout.Infinite, Timeout.Infinite);
            _timer.Change(_quiet, Timeout.InfiniteTimeSpan);
        }
    }

    private void Fire()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
        }

        _onChange();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _timer?.Dispose();
            _timer = null;
        }

        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }

        _watchers.Clear();
    }
}
