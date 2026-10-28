// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

namespace MinecraftCSharp.Launcher.Assets;

/// <summary>
/// Location rules for host-side data. The asset pipeline must never write inside the repository
/// (AGENTS.md principle 3, risk R-014).
/// </summary>
public static class HostPaths
{
    /// <summary>Files that identify the repository root.</summary>
    private static readonly string[] RepositoryMarkers = ["AGENTS.md", "MinecraftCSharp.slnx"];

    /// <summary>
    /// True when <paramref name="path"/> is inside the repository that hosts this build. The check
    /// walks up from the application directory looking for the repository markers, then compares
    /// canonical paths.
    /// </summary>
    public static bool IsInsideRepository(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var repositoryRoot = FindRepositoryRoot();
        if (repositoryRoot is null)
        {
            return false;
        }

        var candidate = Path.GetFullPath(path);
        var root = Path.GetFullPath(repositoryRoot);

        return candidate.Equals(root, StringComparison.OrdinalIgnoreCase) ||
               candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Returns the repository root above the running assembly, or null when not in a checkout.</summary>
    public static string? FindRepositoryRoot() => FindRepositoryRoot(AppContext.BaseDirectory);

    /// <summary>Returns the repository root above <paramref name="startDirectory"/>, or null.</summary>
    public static string? FindRepositoryRoot(string startDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startDirectory);

        var directory = new DirectoryInfo(Path.GetFullPath(startDirectory));
        while (directory is not null)
        {
            foreach (var marker in RepositoryMarkers)
            {
                if (File.Exists(Path.Combine(directory.FullName, marker)))
                {
                    return directory.FullName;
                }
            }

            directory = directory.Parent;
        }

        return null;
    }

    /// <summary>Default packs directory under the user's local application data.</summary>
    public static string DefaultPacksDirectory()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(root, "MinecraftCSharp", "packs");
    }
}
