// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Architecture.Tests;

/// <summary>
/// Locates the repository root from the test output directory so that gates can
/// inspect project files and documentation at their real location.
/// </summary>
internal static class RepositoryLocator
{
    private static readonly Lazy<string> CachedRoot = new(FindRoot, isThreadSafe: true);

    internal static string Root => CachedRoot.Value;

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Sharpcraft.slnx")) &&
                File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"repository root not found above {AppContext.BaseDirectory}");
    }
}
