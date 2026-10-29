// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Collections.Concurrent;

namespace Sharpcraft.Client.Ui;

/// <summary>A parsed document together with the values read out of it.</summary>
/// <param name="Root">Root element.</param>
/// <param name="Styles">Pre-parsed values for that tree.</param>
/// <remarks>
/// The two travel together because the style table is keyed by node reference: publishing a new tree without its
/// own table would make the frame path fail its lookup. One object means one atomic swap.
/// </remarks>
public sealed record CompiledScreen(UiNode Root, UiStyles Styles);

/// <summary>
/// Loads an interface document and keeps it up to date while the game runs (docs/modules/ui.md 8).
/// </summary>
/// <remarks>
/// Three properties are the whole point, and each is enforced rather than hoped for:
/// <list type="number">
/// <item>The frame path never touches the disk or the parser: it reads one reference. Reload work happens on a
/// background task, so the cost of a reload cannot land in a frame.</item>
/// <item>A failed load never replaces what is on screen. A half written file, a typo, an unknown element: the old
/// screen keeps rendering and the reason is recorded.</item>
/// <item>A reload that finishes after a newer change is discarded, otherwise a slow parse of an older version
/// could overwrite a newer one and the screen would appear to jump back.</item>
/// </list>
/// <para>
/// The watcher fires more than once per save, and editors write in stages, so a change is only read once the file
/// has been quiet for a moment and its contents stopped changing between two reads.
/// </para>
/// </remarks>
public sealed class UiDocumentLoader : IDisposable
{
    /// <summary>How long the file must be quiet before it is read.</summary>
    public static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(150);

    /// <summary>Gap between the two reads that must agree before a document is accepted.</summary>
    public static readonly TimeSpan StabilityGap = TimeSpan.FromMilliseconds(60);

    private readonly string _path;
    private readonly ConcurrentQueue<string> _errors = new();
    private CompiledScreen? _current;
    private long _requestedGeneration;
    private long _appliedGeneration;
    private FileSystemWatcher? _watcher;
    private Timer? _debounce;
    private bool _disposed;

    /// <summary>Creates a loader for one managed document path.</summary>
    /// <param name="path">Document path, which must come from <see cref="UiDocumentFile"/>.</param>
    public UiDocumentLoader(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
    }

    /// <summary>The screen currently on display, or null before the first successful load.</summary>
    public CompiledScreen? Current => Volatile.Read(ref _current);

    /// <summary>The most recent rejection reason, or empty when the last load succeeded.</summary>
    public string LastError { get; private set; } = string.Empty;

    /// <summary>How many loads were rejected since the process started.</summary>
    public int RejectedLoads { get; private set; }

    /// <summary>How many documents were accepted and put on screen.</summary>
    public int AppliedLoads { get; private set; }

    /// <summary>Loads the document once and then watches it for changes.</summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        LoadFromDisk();

        var directory = Path.GetDirectoryName(_path);
        var fileName = Path.GetFileName(_path);

        if (directory is null || !Directory.Exists(directory))
        {
            // No directory means nothing to watch; the built in fallback keeps the game usable.
            return;
        }

        try
        {
            _watcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };

            _watcher.Changed += OnFileTouched;
            _watcher.Created += OnFileTouched;
            _watcher.Renamed += OnFileTouched;
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or NotSupportedException)
        {
            // Watching is a convenience; failing to watch must not stop the game.
            Console.WriteLine($"ui: cannot watch {_path} ({exception.Message}); reload disabled");
            _watcher = null;
        }
    }

    /// <summary>
    /// Applies a document, unless a newer generation has already been applied.
    /// </summary>
    /// <param name="text">Document text.</param>
    /// <param name="generation">Generation this text was read at; higher wins.</param>
    /// <returns>True when the document is now on screen.</returns>
    public bool TryApply(string text, long generation)
    {
        if (generation < Volatile.Read(ref _appliedGeneration))
        {
            // A newer document already won; this one is stale.
            return false;
        }

        CompiledScreen compiled;

        try
        {
            var root = UiMarkup.Parse(text);

            // Compiling styles is part of validating: a bad colour is a document error, not a runtime surprise.
            compiled = new CompiledScreen(root, UiStyles.Compile(root));
        }
        catch (Exception exception) when (exception is FormatException or OverflowException)
        {
            RejectedLoads++;
            LastError = exception.Message;
            _errors.Enqueue(exception.Message);
            Console.WriteLine($"ui: rejected document ({exception.Message}); keeping the previous screen");
            return false;
        }

        Volatile.Write(ref _current, compiled);
        Volatile.Write(ref _appliedGeneration, generation);
        AppliedLoads++;
        LastError = string.Empty;
        return true;
    }

    /// <summary>Reserves the next generation for a read that is about to happen.</summary>
    public long NextGeneration() => Interlocked.Increment(ref _requestedGeneration);

    /// <summary>Reads the document from disk now, applying it when it is valid and stable.</summary>
    public void LoadFromDisk() => ScheduleSettledRead(isInitial: true);

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Stop the source before the timer, so no new event can schedule work after the timer is gone.
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Changed -= OnFileTouched;
            _watcher.Created -= OnFileTouched;
            _watcher.Renamed -= OnFileTouched;
            _watcher.Dispose();
            _watcher = null;
        }

        _debounce?.Dispose();
        _debounce = null;
    }

    private void OnFileTouched(object sender, FileSystemEventArgs args)
    {
        _ = sender;
        _ = args;

        if (_disposed)
        {
            return;
        }

        // Coalesce the burst of events an editor produces into one read after the file goes quiet.
        _debounce?.Dispose();
        _debounce = new Timer(_ => ScheduleSettledRead(isInitial: false), null, SettleDelay, Timeout.InfiniteTimeSpan);
    }

    private void ScheduleSettledRead(bool isInitial) =>
        _ = Task.Run(() => ReadWhenStable(isInitial ? 4 : 3));

    private void ReadWhenStable(int attemptsLeft)
    {
        if (_disposed)
        {
            return;
        }

        var generation = NextGeneration();

        try
        {
            var first = ReadSettled(out var firstLength);

            if (first is null)
            {
                if (attemptsLeft > 0)
                {
                    Thread.Sleep(StabilityGap);
                    ReadWhenStable(attemptsLeft - 1);
                }

                return;
            }

            // Editors often write in two passes: wait a moment and require the length to hold still, so a half
            // written document is never parsed into the screen.
            Thread.Sleep(StabilityGap);
            var second = ReadSettled(out var secondLength);

            if (second is null || secondLength != firstLength)
            {
                if (attemptsLeft > 0)
                {
                    ReadWhenStable(attemptsLeft - 1);
                }

                return;
            }

            TryApply(first, generation);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            if (attemptsLeft > 0)
            {
                Thread.Sleep(StabilityGap);
                ReadWhenStable(attemptsLeft - 1);
            }
            else
            {
                Console.WriteLine($"ui: cannot read {_path} ({exception.Message})");
            }
        }
    }

    private string? ReadSettled(out int length)
    {
        length = 0;

        try
        {
            var text = File.ReadAllText(_path);
            length = text.Length;
            return text;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
