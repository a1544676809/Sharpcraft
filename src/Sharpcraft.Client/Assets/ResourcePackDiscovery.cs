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
    /// <param name="folder">Folder such as <c>resourcepacks</c>.</param>
    /// <param name="clientFormat">
    /// Format the client speaks. When given, a pack whose declared range excludes it is skipped with a diagnostic, which is
    /// what the reference's "incompatible" state amounts to; when null nothing is filtered and nothing is claimed.
    /// </param>
    public static List<ResourcePack> Discover(string folder, PackFormatVersion? clientFormat = null)
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
                    Accept(packs, ResourcePack.FromDirectory(entry), clientFormat);
                }

                continue;
            }

            if (!string.Equals(Path.GetExtension(entry), ".zip", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (HasMetadataInArchive(entry))
            {
                Accept(packs, ResourcePack.FromArchive(entry), clientFormat);
            }
        }

        return packs;
    }

    /// <summary>Keeps a pack when it fits the client format, otherwise says why it was skipped.</summary>
    private static void Accept(List<ResourcePack> packs, ResourcePack pack, PackFormatVersion? clientFormat)
    {
        if (clientFormat is not { } client)
        {
            packs.Add(pack);
            return;
        }

        pack.ClientPackFormat = client;

        if (pack.FitsFormat(client))
        {
            packs.Add(pack);
            return;
        }

        var declared = pack.SupportedFormats is { } range
            ? $"{range.Min}..{range.Max}"
            : "none";

        Console.WriteLine($"resource pack {pack.Id}: format {declared}, client {client}; skipped");
        pack.Dispose();
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
