// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Assets;

/// <summary>
/// An ordered resource pack stack (docs/analysis/assets/resource-pack-stack.md).
/// </summary>
/// <remarks>
/// Order is the whole contract: the reference pushes packs in list order and looks from the last one backwards, so
/// <em>the pack added last wins</em>. Every lookup here scans in reverse for exactly that reason, and the namespaces are
/// the union over all packs.
/// </remarks>
public sealed class ResourcePackStack : IDisposable
{
    private readonly List<ResourcePack> _packs = [];

    /// <summary>Packs in priority order, lowest first.</summary>
    public IReadOnlyList<ResourcePack> Packs => _packs;

    /// <summary>Number of packs.</summary>
    public int Count => _packs.Count;

    /// <summary>
    /// Adds a pack at the end of the list, which gives it the highest priority.
    /// </summary>
    /// <param name="pack">Pack to add.</param>
    public void Add(ResourcePack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        _packs.Add(pack);
    }

    /// <summary>
    /// Opens a file, taking the highest priority pack that carries it.
    /// </summary>
    /// <param name="relativePath">Path such as <c>assets/minecraft/textures/font/ascii.png</c>.</param>
    /// <returns>The stream, or null when no pack carries the path.</returns>
    public Stream? Open(string relativePath)
    {
        for (var index = _packs.Count - 1; index >= 0; index--)
        {
            var stream = _packs[index].Open(relativePath);

            if (stream is not null)
            {
                return stream;
            }
        }

        return null;
    }

    /// <summary>Every namespace provided by any pack, without duplicates.</summary>
    public IReadOnlyList<string> Namespaces()
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        // Walking forwards keeps the first sighting of a namespace, which makes the result stable across runs.
        foreach (var pack in _packs)
        {
            foreach (var name in pack.Namespaces())
            {
                if (seen.Add(name))
                {
                    result.Add(name);
                }
            }
        }

        return result;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var pack in _packs)
        {
            pack.Dispose();
        }

        _packs.Clear();
    }
}
