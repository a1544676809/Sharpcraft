// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Identifiers;

namespace Sharpcraft.Client.Assets;

/// <summary>How a missing resource is treated (docs/modules/asset-resolution.md §6).</summary>
public enum MissingResourcePolicy
{
    /// <summary>Warn once per resource and let the caller fall back (default).</summary>
    Warn,

    /// <summary>Throw, for tests and strict runs.</summary>
    Throw
}

/// <summary>
/// One read-only source of assets. Sources are stacked in an ordered list; the higher entry wins,
/// mirroring the vanilla pack list (docs/analysis/assets/resource-resolution.md §3.1).
/// </summary>
public interface IAssetSource : IDisposable
{
    /// <summary>Diagnostic name: pack id, directory name, or a built-in marker.</summary>
    string Id { get; }

    /// <summary>
    /// Opens one resource, or returns false when this source does not have it. The location's path
    /// is relative to the resource kind's directory, mirroring vanilla's FileToIdConverter.
    /// </summary>
    bool TryOpen(ResourceKind kind, Identifier location, out Stream content);

    /// <summary>Lists every resource under a path prefix, in a stable order.</summary>
    IEnumerable<Identifier> List(string prefix);
}

/// <summary>
/// The ordered asset stack. Index 0 is the lowest priority (built-in fallback) and lookup scans
/// from the end backwards, exactly like the vanilla fallback manager.
/// </summary>
public sealed class AssetStack : IDisposable
{
    private readonly List<IAssetSource> _sources = [];
    private readonly HashSet<string> _warned = new(StringComparer.Ordinal);

    /// <summary>Sources from lowest to highest priority.</summary>
    public IReadOnlyList<IAssetSource> Sources => _sources;

    /// <summary>How missing resources are handled.</summary>
    public MissingResourcePolicy Policy { get; init; } = MissingResourcePolicy.Warn;

    /// <summary>Optional sink for warnings; defaults to the console.</summary>
    public Action<string>? Warn { get; init; }

    /// <summary>Adds a source above the current stack.</summary>
    public AssetStack Add(IAssetSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _sources.Add(source);
        return this;
    }

    /// <summary>Opens the highest-priority copy of a resource.</summary>
    /// <param name="kind">Resource family, which fixes the directory and extension.</param>
    /// <param name="location">Resource location inside its namespace.</param>
    /// <param name="content">Opened stream when found.</param>
    /// <param name="sourceId">Which source answered, for diagnostics.</param>
    public bool TryOpen(ResourceKind kind, Identifier location, out Stream content, out string sourceId)
    {
        for (var i = _sources.Count - 1; i >= 0; i--)
        {
            if (_sources[i].TryOpen(kind, location, out content))
            {
                sourceId = _sources[i].Id;
                return true;
            }
        }

        content = Stream.Null;
        sourceId = string.Empty;
        return false;
    }

    /// <summary>Reads a resource fully, or warns/throws according to the policy.</summary>
    public byte[]? Read(ResourceKind kind, Identifier location)
    {
        if (TryOpen(kind, location, out var stream, out _))
        {
            return ReadAll(stream);
        }

        ReportMissing(location);
        return null;
    }

    /// <summary>
    /// Reads the <c>.mcmeta</c> sibling of a resource, if any. Vanilla resolves metadata as
    /// "resource path plus .mcmeta" (resource-resolution.md §4).
    /// </summary>
    public byte[]? ReadMetadata(ResourceKind kind, Identifier location)
    {
        foreach (var source in _sources.AsEnumerable().Reverse())
        {
            if (source is IMetadataAssetSource metadata && metadata.TryOpenMetadata(kind, location, out var stream))
            {
                return ReadAll(stream);
            }
        }

        return null;
    }

    /// <summary>
    /// Lists every location under a prefix once. Higher sources hide lower ones, and the order is
    /// stable because sources are walked from the top and their own listing order preserved.
    /// </summary>
    public IReadOnlyList<Identifier> ListAll(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        var seen = new HashSet<Identifier>();
        var result = new List<Identifier>();

        for (var i = _sources.Count - 1; i >= 0; i--)
        {
            foreach (var location in _sources[i].List(prefix))
            {
                if (seen.Add(location))
                {
                    result.Add(location);
                }
            }
        }

        return result;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        for (var i = _sources.Count - 1; i >= 0; i--)
        {
            _sources[i].Dispose();
        }

        _sources.Clear();
    }

    private void ReportMissing(Identifier location)
    {
        if (Policy == MissingResourcePolicy.Throw)
        {
            throw new AssetNotFoundException(location, _sources.Select(source => source.Id).ToArray());
        }

        var key = location.ToString();
        if (_warned.Add(key))
        {
            var chain = string.Join(" -> ", _sources.Select(source => source.Id));
            (Warn ?? Console.Error.WriteLine)($"[assets] missing {key} in [{chain}]");
        }
    }

    private static byte[] ReadAll(Stream stream)
    {
        using (stream)
        {
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return buffer.ToArray();
        }
    }
}

/// <summary>
/// Optional capability for sources that can serve the <c>.mcmeta</c> sibling of a resource, which
/// lives at a path the kind-to-file rule cannot derive on its own.
/// </summary>
public interface IMetadataAssetSource
{
    /// <summary>Opens the metadata sibling of a resource, when present.</summary>
    bool TryOpenMetadata(ResourceKind kind, Identifier location, out Stream content);
}

/// <summary>Raised when a resource is missing and the policy is <see cref="MissingResourcePolicy.Throw"/>.</summary>
public sealed class AssetNotFoundException : Exception
{
    /// <summary>Creates the exception for a location and the chain that was searched.</summary>
    public AssetNotFoundException(Identifier location, IReadOnlyList<string> chain)
        : base($"asset not found: {location} (searched {string.Join(" -> ", chain)})")
    {
        Location = location;
        Chain = chain;
    }

    /// <summary>The location that was requested.</summary>
    public Identifier Location { get; }

    /// <summary>Sources that were searched, lowest priority first.</summary>
    public IReadOnlyList<string> Chain { get; }
}
