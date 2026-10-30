// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.IO.Compression;

namespace Sharpcraft.Client.Assets;

/// <summary>
/// Finds the packs a player has installed (docs/analysis/assets/resource-pack-stack.md).
/// </summary>
/// <remarks>
/// The reference scans the pack folder once, accepting a directory or a zip that carries <c>pack.mcmeta</c> and ignoring
/// everything else, including suspicious links. Discovery keeps the same rules so a pack that the reference would skip
/// is skipped here too; the order returned is the file system order, which the caller then orders by priority.
/// </remarks>
public static class ResourcePackDiscovery
{
    /// <summary>File that marks a folder or archive as a pack.</summary>
    public const string MetadataFile = "pack.mcmeta";

    /// <summary>
    /// Lists usable packs under a folder, lowest priority first as found.
    /// </summary>
    /// <param name="folder">Folder such as <c>resourcepacks</c>.</param>
    /// <returns>Packs that could be opened; unreadable entries are skipped rather than failing the whole scan.</returns>
    public static List<ResourcePack> Discover(string folder)
    {
        var packs = new List<ResourcePack>();

        if (!Directory.Exists(folder))
        {
            return packs;
        }

        foreach (var entry in Directory.EnumerateFileSystemEntries(folder).OrderBy(path => path, StringComparer.Ordinal))
        {
            var attributes = File.GetAttributes(entry);

            // A link can point anywhere; the reference refuses suspicious ones, and following them here could escape the
            // folder entirely.
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                continue;
            }

            if ((attributes & FileAttributes.Directory) != 0)
            {
                if (File.Exists(Path.Combine(entry, MetadataFile)))
                {
                    packs.Add(ResourcePack.FromDirectory(entry));
                }

                continue;
            }

            if (!string.Equals(Path.GetExtension(entry), ".zip", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (HasMetadataInArchive(entry))
            {
                packs.Add(ResourcePack.FromArchive(entry));
            }
        }

        return packs;
    }

    private static bool HasMetadataInArchive(string path)
    {
        try
        {
            using var archive = ZipFile.OpenRead(path);

            return archive.GetEntry(MetadataFile) is not null;
        }
        catch (InvalidDataException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
