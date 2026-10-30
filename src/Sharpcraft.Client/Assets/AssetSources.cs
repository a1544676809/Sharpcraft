// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Imaging;

namespace Sharpcraft.Client.Assets;

/// <summary>
/// Assets stored in a zip: either our own pack (with <c>manifest.json</c> and per-entry hashes) or a
/// user resource pack (plain zip, <c>pack.mcmeta</c> optional). Covers both the base pack and the
/// custom resource pack slots in the stack.
/// </summary>
public sealed class ZipAssetSource : IAssetSource, IMetadataAssetSource
{
    private readonly ZipArchive _archive;
    private readonly Dictionary<string, ZipArchiveEntry> _entries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _expectedHashes = new(StringComparer.Ordinal);
    private readonly bool _verify;

    /// <summary>Opens a zip as an asset source.</summary>
    /// <param name="path">Zip path.</param>
    /// <param name="id">Diagnostic name.</param>
    /// <param name="verifyManifestEntries">
    /// When the zip carries our <c>manifest.json</c>, verify each entry's SHA-256 on read.
    /// </param>
    public ZipAssetSource(string path, string? id = null, bool verifyManifestEntries = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        Path = path;
        Id = id ?? System.IO.Path.GetFileNameWithoutExtension(path);
        _verify = verifyManifestEntries;
        _archive = ZipFile.OpenRead(path);

        foreach (var entry in _archive.Entries)
        {
            _entries[entry.FullName] = entry;
        }

        if (_entries.TryGetValue("manifest.json", out var manifestEntry))
        {
            LoadManifestHashes(manifestEntry);
        }
        else if (_entries.TryGetValue("pack.mcmeta", out var metadataEntry))
        {
            Metadata = ReadMetadata(metadataEntry);
        }
    }

    /// <inheritdoc />
    public bool TryOpenPath(string relativePath, out Stream content)
    {
        if (_entries.TryGetValue(relativePath, out var entry))
        {
            content = entry.Open();
            return true;
        }

        content = Stream.Null;
        return false;
    }

    /// <summary>Path of the zip.</summary>
    public string Path { get; }

    /// <summary>Declared pack metadata, when the zip ships a <c>pack.mcmeta</c>.</summary>
    public PackMetadata? Metadata { get; private set; }

    /// <inheritdoc />
    public string Id { get; }

    /// <inheritdoc />
    public bool TryOpen(ResourceKind kind, Identifier location, out Stream content)
    {
        var path = ResourcePath.FilePath(kind, location);
        if (_entries.TryGetValue(path, out var entry))
        {
            content = OpenVerified(path, entry);
            return true;
        }

        content = Stream.Null;
        return false;
    }

    /// <inheritdoc />
    public bool TryOpenMetadata(ResourceKind kind, Identifier location, out Stream content)
    {
        var path = ResourcePath.MetadataPath(ResourcePath.FilePath(kind, location));
        if (_entries.TryGetValue(path, out var entry))
        {
            content = OpenVerified(path, entry);
            return true;
        }

        content = Stream.Null;
        return false;
    }

    /// <inheritdoc />
    public IEnumerable<Identifier> List(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        // The archive order is deterministic for a given file, which keeps listing stable.
        foreach (var name in _entries.Keys)
        {
            if (!name.StartsWith(prefix, StringComparison.Ordinal) ||
                name.EndsWith(".mcmeta", StringComparison.Ordinal))
            {
                continue;
            }

            if (ResourceLookup.TryParseUnderPrefix(prefix, name, out var location))
            {
                yield return location;
            }
        }
    }

    /// <inheritdoc />
    public void Dispose() => _archive.Dispose();

    private Stream OpenVerified(string name, ZipArchiveEntry entry)
    {
        using var raw = entry.Open();
        using var buffer = new MemoryStream();
        raw.CopyTo(buffer);
        var bytes = buffer.ToArray();

        if (_verify && _expectedHashes.TryGetValue(name, out var expected))
        {
            var actual = Convert.ToHexStringLower(SHA256.HashData(bytes));
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"asset '{name}' in {Path} does not match its manifest hash " +
                    $"(expected {expected}, actual {actual})");
            }
        }

        return new MemoryStream(bytes, writable: false);
    }

    private void LoadManifestHashes(ZipArchiveEntry manifestEntry)
    {
        using var stream = manifestEntry.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        var manifest = AssetPackManifest.ReadJson(buffer.ToArray());
        foreach (var entry in manifest.Entries)
        {
            _expectedHashes[entry.Name] = entry.Sha256;
        }

        if (_entries.TryGetValue("pack.mcmeta", out var metadataEntry))
        {
            Metadata = ReadMetadata(metadataEntry);
        }
    }

    private static PackMetadata? ReadMetadata(ZipArchiveEntry entry)
    {
        try
        {
            using var stream = entry.Open();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return PackMetadata.Parse(buffer.ToArray());
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }
}

/// <summary>Assets stored as a directory tree, used for resource packs and mod resources.</summary>
public sealed class DirectoryAssetSource : IAssetSource, IMetadataAssetSource
{
    private readonly string _root;

    /// <summary>Creates a directory source.</summary>
    /// <param name="root">Directory that contains <c>assets/…</c>.</param>
    /// <param name="id">Diagnostic name.</param>
    public DirectoryAssetSource(string root, string? id = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        _root = System.IO.Path.GetFullPath(root);
        Id = id ?? new DirectoryInfo(_root).Name;
        Metadata = TryReadMetadata();
    }

    /// <inheritdoc />
    public string Id { get; }

    /// <summary>Declared pack metadata, when the directory ships a <c>pack.mcmeta</c>.</summary>
    public PackMetadata? Metadata { get; }

    /// <inheritdoc />
    public bool TryOpen(ResourceKind kind, Identifier location, out Stream content) =>
        TryOpenFile(ResourcePath.FilePath(kind, location), out content);

    /// <inheritdoc />
    public bool TryOpenMetadata(ResourceKind kind, Identifier location, out Stream content) =>
        TryOpenFile(ResourcePath.MetadataPath(ResourcePath.FilePath(kind, location)), out content);

    /// <inheritdoc />
    public IEnumerable<Identifier> List(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        var directory = Resolve(prefix);
        if (!Directory.Exists(directory))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            if (file.EndsWith(".mcmeta", StringComparison.Ordinal))
            {
                continue;
            }

            var relative = System.IO.Path.GetRelativePath(_root, file).Replace('\\', '/');
            if (ResourceLookup.TryParseUnderPrefix(prefix, relative, out var location))
            {
                yield return location;
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }

    private bool TryOpenFile(string filePath, out Stream content)
    {
        var candidate = Resolve(filePath);
        if (File.Exists(candidate))
        {
            content = File.OpenRead(candidate);
            return true;
        }

        content = Stream.Null;
        return false;
    }

    private string Resolve(string filePath) =>
        System.IO.Path.Combine(_root, filePath.Replace('/', System.IO.Path.DirectorySeparatorChar));

    private PackMetadata? TryReadMetadata()
    {
        var path = System.IO.Path.Combine(_root, "pack.mcmeta");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return PackMetadata.Parse(File.ReadAllBytes(path));
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }
}

/// <summary>
/// Read-only mount of the official asset index: <c>indexes/&lt;id&gt;.json</c> plus
/// <c>objects/&lt;ab&gt;/&lt;sha1&gt;</c>. Nothing is copied (ADR-0005).
/// </summary>
public sealed class OfficialObjectsSource : IAssetSource
{
    private readonly Dictionary<string, string> _objects = new(StringComparer.Ordinal);
    private readonly string _objectsRoot;

    private OfficialObjectsSource(string indexPath, string objectsRoot, string indexId)
    {
        Path = indexPath;
        _objectsRoot = objectsRoot;
        Id = "official-objects:" + indexId;
    }

    /// <summary>Path of the index JSON.</summary>
    public string Path { get; }

    /// <summary>Number of objects the index declares.</summary>
    public int ObjectCount => _objects.Count;

    /// <summary>Missing-object warnings raised so far, for diagnostics.</summary>
    public int MissingCount { get; private set; }

    /// <inheritdoc />
    public string Id { get; }

    /// <summary>Opens an official index as a source.</summary>
    /// <param name="indexPath">Path to <c>indexes/&lt;id&gt;.json</c>.</param>
    /// <param name="expectedIndexId">When supplied, the index file name must match it.</param>
    /// <exception cref="InvalidOperationException">The index is missing or its id does not match.</exception>
    public static OfficialObjectsSource Open(string indexPath, string? expectedIndexId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(indexPath);

        if (!File.Exists(indexPath))
        {
            throw new InvalidOperationException(
                $"asset index not found: {indexPath}. Point the launcher at the official assets directory " +
                "or let the launcher download the index first.");
        }

        var indexId = System.IO.Path.GetFileNameWithoutExtension(indexPath);
        if (expectedIndexId is not null && !string.Equals(indexId, expectedIndexId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"asset index id mismatch: {indexPath} is '{indexId}' but the version manifest expects " +
                $"'{expectedIndexId}'. Using the wrong index would mix resources from another version.");
        }

        var assetsRoot = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(indexPath)) ?? ".";
        assetsRoot = System.IO.Path.GetDirectoryName(assetsRoot) ?? ".";
        var objectsRoot = System.IO.Path.Combine(assetsRoot, "objects");

        var source = new OfficialObjectsSource(indexPath, objectsRoot, indexId);
        source.LoadIndex();
        return source;
    }

    /// <inheritdoc />
    public bool TryOpen(ResourceKind kind, Identifier location, out Stream content)
    {
        content = Stream.Null;

        // Index keys are relative to the client root: "minecraft/textures/block/stone.png".
        var key = ResourcePath.FilePath(kind, location)["assets/".Length..];
        if (!_objects.TryGetValue(key, out var hash))
        {
            return false;
        }

        var file = System.IO.Path.Combine(_objectsRoot, hash[..2], hash);
        if (!File.Exists(file))
        {
            MissingCount++;
            if (MissingCount <= 3)
            {
                Console.Error.WriteLine($"[assets] object missing on disk: {key} -> {file}");
            }

            return false;
        }

        content = File.OpenRead(file);
        return true;
    }

    /// <inheritdoc />
    public IEnumerable<Identifier> List(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        var relativePrefix = prefix.StartsWith(ResourcePath.ClientRoot + "/", StringComparison.Ordinal)
            ? prefix[(ResourcePath.ClientRoot.Length + 1)..]
            : prefix;

        foreach (var key in _objects.Keys)
        {
            if (!key.StartsWith(relativePrefix, StringComparison.Ordinal) ||
                key.EndsWith(".mcmeta", StringComparison.Ordinal))
            {
                continue;
            }

            if (ResourceLookup.TryParseUnderPrefix(prefix, ResourcePath.ClientRoot + "/" + key, out var location))
            {
                yield return location;
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }

    private void LoadIndex()
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path));
        if (!document.RootElement.TryGetProperty("objects", out var objects))
        {
            throw new InvalidOperationException($"asset index has no 'objects' map: {Path}");
        }

        foreach (var property in objects.EnumerateObject())
        {
            if (property.Value.TryGetProperty("hash", out var hash) && hash.GetString() is { Length: > 0 } value)
            {
                _objects[property.Name] = value;
            }
        }
    }
}

/// <summary>
/// Last resort in the stack: a purple/black checkerboard for any texture so missing art is
/// obvious, generated in code so the repository never carries Minecraft assets.
/// </summary>
public sealed class BuiltInFallbackSource : IAssetSource
{
    private const int TileSize = 16;
    private const int TileCells = 4;

    private readonly byte[] _checkerboard;

    /// <summary>Creates the built-in source.</summary>
    public BuiltInFallbackSource() => _checkerboard = BuildCheckerboard();

    /// <inheritdoc />
    public string Id => "builtin";

    /// <summary>The raw PNG bytes of the placeholder texture (exposed for tests).</summary>
    public byte[] Checkerboard => _checkerboard;

    /// <inheritdoc />
    public bool TryOpen(ResourceKind kind, Identifier location, out Stream content)
    {
        if (kind == ResourceKind.Texture)
        {
            content = new MemoryStream(_checkerboard, writable: false);
            return true;
        }

        content = Stream.Null;
        return false;
    }

    /// <inheritdoc />
    public IEnumerable<Identifier> List(string prefix) => [];

    /// <inheritdoc />
    public void Dispose()
    {
    }

    private static byte[] BuildCheckerboard()
    {
        var pixels = new byte[TileSize * TileSize * 4];
        var cell = TileSize / TileCells;

        for (var y = 0; y < TileSize; y++)
        {
            for (var x = 0; x < TileSize; x++)
            {
                var purple = ((x / cell) + (y / cell)) % 2 == 0;
                var offset = ((y * TileSize) + x) * 4;
                pixels[offset] = purple ? (byte)0xFF : (byte)0x00;
                pixels[offset + 1] = 0x00;
                pixels[offset + 2] = purple ? (byte)0xFF : (byte)0x00;
                pixels[offset + 3] = 0xFF;
            }
        }

        return PngEncoder.EncodeRgba(TileSize, TileSize, pixels);
    }
}
