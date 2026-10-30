// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Xunit;

namespace Sharpcraft.Client.Tests.Assets;

/// <summary>
/// The pack file watcher (docs/decisions/0016-resource-pack-file-watch.md): a burst of writes reports once, and the whole
/// thing does not exist unless the switch is on.
/// </summary>
public sealed class PackWatcherTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sharpcraft-watch-" + Guid.NewGuid().ToString("N"));

    public PackWatcherTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void ABurstOfWritesIsReportedOnce()
    {
        using var watcher = new PackWatcherProbe(_root);

        // Three writes in quick succession, the way an editor saves.
        for (var index = 0; index < 3; index++)
        {
            File.WriteAllText(Path.Combine(_root, $"file{index}.json"), index.ToString());
        }

        Assert.True(watcher.WaitForReport(TimeSpan.FromSeconds(5)));
        Thread.Sleep(700);

        Assert.InRange(watcher.Reports, 1, 2);
    }

    [Fact]
    public void TheSwitchIsOffByDefault()
    {
        var previous = Environment.GetEnvironmentVariable(PackWatcher.SwitchName);
        Environment.SetEnvironmentVariable(PackWatcher.SwitchName, null);

        try
        {
            // Nothing is watched unless the player asked for it, so the default behaviour is the reference's.
            Assert.False(PackWatcher.IsEnabled);
            Assert.Null(PackWatcher.ForEnvironment([_root], () => { }));
        }
        finally
        {
            Environment.SetEnvironmentVariable(PackWatcher.SwitchName, previous);
        }
    }

    [Fact]
    public void NoFoldersMeansNoWatchers()
    {
        Assert.Null(PackWatcher.ForEnvironment([], () => { }));

        // The switch is off in the test process, so even a real folder yields nothing.
        Assert.Null(PackWatcher.ForEnvironment([_root], () => { }));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>Runs the watcher with the switch forced on, so the debounce can be observed without changing the process.</summary>
    private sealed class PackWatcherProbe : IDisposable
    {
        private readonly PackWatcher _watcher;
        private readonly ManualResetEventSlim _reported = new(false);

        public PackWatcherProbe(string folder)
        {
            Environment.SetEnvironmentVariable(PackWatcher.SwitchName, "1");
            _watcher = PackWatcher.ForEnvironment(
                [folder],
                () =>
                {
                    Reports++;
                    _reported.Set();
                },
                TimeSpan.FromMilliseconds(150))!;
        }

        public int Reports { get; private set; }

        public bool WaitForReport(TimeSpan timeout) => _reported.Wait(timeout);

        public void Dispose()
        {
            _watcher.Dispose();
            Environment.SetEnvironmentVariable(PackWatcher.SwitchName, null);
        }
    }
}
