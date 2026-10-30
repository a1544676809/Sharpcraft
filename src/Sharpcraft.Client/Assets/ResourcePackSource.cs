// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Identifiers;

namespace Sharpcraft.Client.Assets;

/// <summary>
/// Exposes one installed resource pack as an asset source (docs/analysis/assets/resource-pack-stack.md).
/// </summary>
/// <remarks>
/// Attaching packs to the existing <see cref="AssetStack"/> is what gives them priority without touching any loader:
/// the stack already scans from the last source backwards, so appending a pack makes it override the base pack exactly
/// as the reference's fallback stack does.
/// </remarks>
public sealed class ResourcePackSource : IAssetSource
{
    private readonly ResourcePack _pack;

    /// <summary>Wraps a pack.</summary>
    /// <param name="pack">Pack to read through.</param>
    public ResourcePackSource(ResourcePack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        _pack = pack;
    }

    /// <inheritdoc />
    public string Id => _pack.Id;

    /// <inheritdoc />
    public bool TryOpen(ResourceKind kind, Identifier location, out Stream content)
    {
        var stream = _pack.Open(ResourcePath.FilePath(kind, location));

        if (stream is null)
        {
            content = Stream.Null;
            return false;
        }

        content = stream;
        return true;
    }

    /// <inheritdoc />
    public bool TryOpenPath(string relativePath, out Stream content)
    {
        var stream = _pack.Open(relativePath);

        content = stream ?? Stream.Null;
        return stream is not null;
    }

    /// <inheritdoc />
    public bool Blocks(ResourceKind kind, Identifier location) => _pack.Filter?.IsFiltered(location) ?? false;

    /// <inheritdoc />
    public IEnumerable<Identifier> List(string prefix)
    {
        var root = string.Concat(ResourcePath.ClientRoot, "/", prefix);

        foreach (var relative in _pack.EnumerateFiles(root))
        {
            if (Identifier.TryParse(relative[ResourcePath.ClientRoot.Length..], out var id))
            {
                yield return id;
            }
        }
    }

    /// <inheritdoc />
    public void Dispose() => _pack.Dispose();
}
