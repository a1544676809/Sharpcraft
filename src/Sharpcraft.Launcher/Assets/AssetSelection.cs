// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Launcher.Assets;

/// <summary>
/// Which jar entries belong in the asset pack. The rules come from the measured entry map
/// (docs/analysis/assets/jar-entry-map.md): models, blockstates, items, textures, font, particles,
/// shaders and the two language files the jar carries; everything else stays out.
/// </summary>
public static class AssetSelection
{
    /// <summary>Prefix shared by every resource under the virtual file system.</summary>
    public const string AssetsPrefix = "assets/minecraft/";

    private static readonly string[] IncludedDirectories =
    [
        "models/",
        "blockstates/",
        "items/",
        "textures/",
        "font/",
        "particles/",
        "shaders/",
        "lang/"
    ];

    /// <summary>Directories that are never extracted, for the report.</summary>
    private static readonly string[] ExcludedTopLevel = ["net/", "data/", "com/", "META-INF/"];

    /// <summary>Returns true when an entry belongs in the pack.</summary>
    public static bool IsIncluded(string entryName)
    {
        if (!IsSafeEntryName(entryName))
        {
            return false;
        }

        // Directory markers are not payload.
        if (entryName.EndsWith('/'))
        {
            return false;
        }

        if (!entryName.StartsWith(AssetsPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var relative = entryName[AssetsPrefix.Length..];

        // The marker file some launchers leave behind.
        if (relative.Equals(".mcassetsroot", StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var directory in IncludedDirectories)
        {
            if (relative.StartsWith(directory, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Rejects entries that could escape the destination directory (zip-slip) or that use absolute
    /// paths and drive letters.
    /// </summary>
    public static bool IsSafeEntryName(string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName))
        {
            return false;
        }

        if (entryName[0] is '/' or '\\' || entryName.Contains(':', StringComparison.Ordinal))
        {
            return false;
        }

        var segments = entryName.Split('/', '\\');
        foreach (var segment in segments)
        {
            if (segment is ".." || segment is ".")
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Classifies an excluded entry by its top-level directory, for reporting.</summary>
    public static string? ExcludedCategory(string entryName)
    {
        foreach (var top in ExcludedTopLevel)
        {
            if (entryName.StartsWith(top, StringComparison.Ordinal))
            {
                return top.TrimEnd('/');
            }
        }

        return null;
    }
}
