// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Identifiers;

namespace Sharpcraft.Client.Assets;

/// <summary>
/// The one place resources are read from (docs/analysis/assets/resource-pack-stack.md).
/// </summary>
/// <remarks>
/// The reference has a single resource manager that builds one fallback stack per namespace and hands it to every
/// consumer, so a pack is discovered once and every loader sees the same override. Building a stack per loader, which is
/// what we did before, discovered and opened the same packs several times and would have made a reload fire more than
/// once. This type owns the stack for the whole session instead.
/// </remarks>
public sealed class ResourceManager : IDisposable
{
    private readonly AssetStack _stack;

    /// <summary>Wraps a prepared stack.</summary>
    /// <param name="stack">Stack whose sources are owned by this manager.</param>
    /// <param name="packCount">How many installed packs were layered over the base pack.</param>
    public ResourceManager(AssetStack stack, int packCount)
    {
        ArgumentNullException.ThrowIfNull(stack);
        _stack = stack;
        PackCount = packCount;
    }

    /// <summary>How many installed packs are layered over the base pack.</summary>
    public int PackCount { get; }

    /// <summary>The stack, for diagnostics and for a future reload.</summary>
    public AssetStack Stack => _stack;

    /// <summary>Reads a resource, taking the highest priority pack that carries it.</summary>
    /// <param name="kind">Resource kind.</param>
    /// <param name="location">Identifier.</param>
    /// <returns>The bytes, or null when no pack carries it.</returns>
    public byte[]? Read(ResourceKind kind, Identifier location) => _stack.Read(kind, location);

    /// <summary>
    /// Opens a raw pack file, taking the highest priority pack that carries it.
    /// </summary>
    /// <param name="relativePath">Path from the pack root, such as a <c>.mcmeta</c> beside a texture.</param>
    /// <returns>The stream, or null when no pack carries the path.</returns>
    public Stream? OpenFile(string relativePath)
    {
        var sources = _stack.Sources;

        for (var index = sources.Count - 1; index >= 0; index--)
        {
            if (sources[index].TryOpenPath(relativePath, out var content))
            {
                return content;
            }
        }

        return null;
    }

    /// <inheritdoc />
    public void Dispose() => _stack.Dispose();
}
