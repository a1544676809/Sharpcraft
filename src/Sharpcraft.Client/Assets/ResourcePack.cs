// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.IO.Compression;

namespace Sharpcraft.Client.Assets;

/// <summary>Whether a pack is a folder or an archive.</summary>
public enum ResourcePackKind
{
    /// <summary>A directory holding <c>pack.mcmeta</c> and <c>assets/</c>.</summary>
    Directory,

    /// <summary>A zip archive with the same layout.</summary>
    Archive
}

/// <summary>
/// One resource pack in the stack (docs/analysis/assets/resource-pack-stack.md).
/// </summary>
/// <remarks>
/// The reference accepts both a folder and an archive, so both are modelled here. An archive keeps one handle open for
/// the life of the stack: opening it per read would cost a decompression setup for every glyph and every sprite.
/// </remarks>
public sealed class ResourcePack : IDisposable
{
    private readonly ZipArchive? _archive;

    private ResourcePack(string id, string root, ResourcePackKind kind, ZipArchive? archive)
    {
        Id = id;
        Root = root;
        Kind = kind;
        _archive = archive;
    }

    /// <summary>Identifier, derived from the folder or file name.</summary>
    public string Id { get; }

    /// <summary>Folder or archive path.</summary>
    public string Root { get; }

    /// <summary>Whether this pack is a folder or an archive.</summary>
    public ResourcePackKind Kind { get; }

    private ResourceFilter? _filter;
    private bool _filterRead;

    /// <summary>
    /// The pack's <c>filter</c> block, or null when it declares none.
    /// </summary>
    /// <remarks>
    /// Read on first use: a filter is only consulted when a lookup actually misses in this pack, so packs that declare
    /// none never pay for parsing their metadata.
    /// </remarks>
    public ResourceFilter? Filter
    {
        get
        {
            if (!_filterRead)
            {
                _filterRead = true;

                using var metadata = Open(MetadataFileName);

                if (metadata is not null)
                {
                    using var reader = new StreamReader(metadata);
                    _filter = ResourceFilter.TryParse(reader.ReadToEnd());
                }
            }

            return _filter;
        }
    }

    private const string MetadataFileName = "pack.mcmeta";

    private List<OverlayEntry>? _overlays;
    private bool _overlaysRead;

    /// <summary>
    /// Pack format in use, which decides whether an overlay applies. Null means unknown, in which case no overlay is
    /// applied and the pack root answers alone rather than guessing a version.
    /// </summary>
    public int? ClientFormat { get; set; }

    /// <summary>
    /// The overlay folders that apply to <see cref="ClientFormat"/>, in declaration order.
    /// </summary>
    /// <remarks>
    /// Metadata is never overlaid in the reference, so this is consulted only for asset lookups.
    /// </remarks>
    public IReadOnlyList<string> OverlayDirectories()
    {
        if (ClientFormat is not { } format)
        {
            return [];
        }

        if (!_overlaysRead)
        {
            _overlaysRead = true;

            using var metadata = Open(MetadataFileName, applyOverlays: false);

            if (metadata is not null)
            {
                using var reader = new StreamReader(metadata);
                _overlays = ResourcePackOverlays.TryParse(reader.ReadToEnd());
            }
        }

        if (_overlays is null)
        {
            return [];
        }

        var directories = new List<string>(_overlays.Count);

        foreach (var entry in _overlays)
        {
            if (entry.AppliesTo(format))
            {
                directories.Add(entry.Directory);
            }
        }

        return directories;
    }

    /// <summary>Opens a pack from a folder.</summary>
    /// <param name="root">Folder holding <c>pack.mcmeta</c>.</param>
    public static ResourcePack FromDirectory(string root) =>
        new(Path.GetFileName(Path.TrimEndingDirectorySeparator(root)), root, ResourcePackKind.Directory, null);

    /// <summary>Opens a pack from an archive.</summary>
    /// <param name="path">Archive path.</param>
    /// <exception cref="InvalidDataException">The file is not a readable archive.</exception>
    public static ResourcePack FromArchive(string path)
    {
        var archive = ZipFile.OpenRead(path);

        return new ResourcePack(Path.GetFileNameWithoutExtension(path), path, ResourcePackKind.Archive, archive);
    }

    /// <summary>Reads one file, or null when the pack does not carry it.</summary>
    /// <param name="relativePath">Path such as <c>assets/minecraft/textures/font/ascii.png</c>.</param>
    /// <remarks>
    /// The comparison is ordinal and case sensitive, which is what a pack written for the reference expects; a
    /// case insensitive file system must not silently make a wrong-cased path resolve.
    /// </remarks>
    public Stream? Open(string relativePath) => Open(relativePath, applyOverlays: true);

    private Stream? Open(string relativePath, bool applyOverlays)
    {
        if (applyOverlays && !string.Equals(relativePath, MetadataFileName, StringComparison.Ordinal))
        {
            // The reference builds a stack of the overlays reversed and the primary pack last, then takes the first hit,
            // so the overlay declared last wins and every overlay beats the pack root.
            var overlays = OverlayDirectories();

            for (var index = overlays.Count - 1; index >= 0; index--)
            {
                var stream = OpenAt(Path.Combine(overlays[index], relativePath.Replace('/', Path.DirectorySeparatorChar)));

                if (stream is not null)
                {
                    return stream;
                }
            }
        }

        return OpenAt(relativePath);
    }

    private Stream? OpenAt(string relativePath)
    {
        if (Kind == ResourcePackKind.Directory)
        {
            var candidate = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));

            return File.Exists(candidate) ? File.OpenRead(candidate) : null;
        }

        var entry = _archive?.GetEntry(relativePath.Replace('\\', '/'));

        return entry is null ? null : entry.Open();
    }

    /// <summary>The namespaces this pack provides, from <c>assets/</c>.</summary>
    public IEnumerable<string> Namespaces()
    {
        if (Kind == ResourcePackKind.Directory)
        {
            var assets = Path.Combine(Root, "assets");

            if (!Directory.Exists(assets))
            {
                yield break;
            }

            foreach (var directory in Directory.EnumerateDirectories(assets))
            {
                yield return Path.GetFileName(directory);
            }

            yield break;
        }

        if (_archive is null)
        {
            yield break;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in _archive.Entries)
        {
            const string prefix = "assets/";

            if (!entry.FullName.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var slash = entry.FullName.IndexOf('/', prefix.Length);

            if (slash > prefix.Length)
            {
                var name = entry.FullName[prefix.Length..slash];

                if (seen.Add(name))
                {
                    yield return name;
                }
            }
        }
    }

    /// <summary>
    /// Lists every file under a prefix, relative to the pack root.
    /// </summary>
    /// <param name="prefix">Prefix such as <c>assets/minecraft/textures/block</c>.</param>
    public IEnumerable<string> EnumerateFiles(string prefix)
    {
        var normalized = prefix.Replace('\\', '/').TrimEnd('/') + "/";

        if (Kind == ResourcePackKind.Directory)
        {
            var root = Path.Combine(Root, normalized.Replace('/', Path.DirectorySeparatorChar));

            if (!Directory.Exists(root))
            {
                yield break;
            }

            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                yield return normalized + Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
            }

            yield break;
        }

        if (_archive is null)
        {
            yield break;
        }

        foreach (var entry in _archive.Entries)
        {
            if (entry.FullName.StartsWith(normalized, StringComparison.Ordinal))
            {
                yield return entry.FullName;
            }
        }
    }

    /// <inheritdoc />
    public void Dispose() => _archive?.Dispose();
}
