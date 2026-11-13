// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.World;
using Sharpcraft.Common.Reference;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Keeps the set of sections the camera can currently see through open space, and answers the renderer's question
/// about one section (T-833).
/// </summary>
/// <remarks>
/// The walk is redone only when the camera changes section: standing still cannot change what is reachable, and the
/// set is a dictionary rebuild rather than something worth paying for every frame.
/// <para>
/// A chunk column is one section wide, so a section coordinate and its chunk share X and Z; the visible sections of one
/// chunk therefore fit in a single sixty four bit mask, which is what the dictionary stores.
/// </para>
/// </remarks>
[ReferenceClass("net.minecraft.client.renderer.SectionOcclusionGraph", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/SectionOcclusionGraph.java", Kind = ReferenceKind.Mirror, Status = ReferenceStatus.Partial, Note = "holds the walk result; the reference keeps a tree and rebuilds asynchronously")]
public sealed class SectionOcclusionState
{
    private readonly Dictionary<long, ulong> _visible = [];
    private readonly SectionOcclusionTraversal.TraversalNode[] _queue;
    private readonly byte[] _visited;
    private readonly SectionCoord[] _output;
    private readonly int _radius;
    private readonly int _minimumY;
    private readonly int _maximumY;
    private readonly Func<SectionCoord, bool> _isLoaded;
    private readonly Func<SectionCoord, bool> _alwaysInFrustum = _ => true;
    private SectionCoord? _lastCameraSection;

    /// <summary>Creates the state for a world of the given vertical extent.</summary>
    /// <param name="radius">Reach in sections along X and Z.</param>
    /// <param name="minimumY">Lowest section Y in the world.</param>
    /// <param name="maximumY">Highest section Y in the world.</param>
    /// <param name="isLoaded">Whether a section has a chunk behind it.</param>
    public SectionOcclusionState(int radius, int minimumY, int maximumY, Func<SectionCoord, bool> isLoaded, Func<SectionCoord> camera, ChunkMeshCache cache)
    {
        ArgumentNullException.ThrowIfNull(isLoaded);
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(cache);

        _radius = radius;
        _minimumY = minimumY;
        _maximumY = maximumY;
        _isLoaded = isLoaded;

        var volume = SectionOcclusionTraversal.Volume(radius, minimumY, maximumY);
        _queue = new SectionOcclusionTraversal.TraversalNode[volume];
        _visited = new byte[volume];
        _output = new SectionCoord[volume];
        IsVisible = (key, sectionIndex) =>
        {
            Refresh(camera(), cache);
            return Test(key, sectionIndex);
        };
    }

    /// <summary>Predicate the renderer calls per section; held in a field so passing it costs nothing.</summary>
    public Func<long, int, bool> IsVisible { get; }

    /// <summary>Redoes the walk when the camera has moved to a different section.</summary>
    /// <param name="cameraSection">Section the camera occupies.</param>
    /// <param name="cache">Source of the per section sight matrices.</param>
    /// <returns>True when the set changed.</returns>
    public bool Refresh(SectionCoord cameraSection, ChunkMeshCache cache)
    {
        ArgumentNullException.ThrowIfNull(cache);

        if (_lastCameraSection == cameraSection)
        {
            return false;
        }

        _lastCameraSection = cameraSection;
        _visible.Clear();

        SectionOcclusionTraversal.Traverse(
            cameraSection,
            _radius,
            _minimumY,
            _maximumY,
            _isLoaded,
            visible => VisibilityOf(visible, cache),
            _alwaysInFrustum,
            _queue,
            _visited,
            _output,
            out var count);

        for (var index = 0; index < count; index++)
        {
            var coord = _output[index];
            var key = ClientWorld.Pack(coord.X, coord.Z);
            var bit = coord.Y - _minimumY;

            if ((uint)bit >= 64u)
            {
                continue;
            }

            _visible[key] = _visible.TryGetValue(key, out var mask) ? mask | (1UL << bit) : 1UL << bit;
        }

        return true;
    }

    private SectionVisibility VisibilityOf(SectionCoord coord, ChunkMeshCache cache)
    {
        var key = ClientWorld.Pack(coord.X, coord.Z);

        if (!cache.TryGetMesh(key, out var mesh) || mesh.SectionVisibility is null)
        {
            // No geometry means nothing to occlude, which must not stop the walk.
            return SectionVisibility.AllVisible;
        }

        var index = coord.Y - mesh.SectionMinY;
        return (uint)index < (uint)mesh.SectionVisibility.Length
            ? mesh.SectionVisibility[index]
            : SectionVisibility.AllVisible;
    }

    /// <summary>Whether the walk kept the given section.</summary>
    /// <param name="chunkKey">Packed chunk key.</param>
    /// <param name="sectionIndex">Section index within the column.</param>
    public bool Test(long chunkKey, int sectionIndex)
    {
        if (!_visible.TryGetValue(chunkKey, out var mask))
        {
            return false;
        }

        var bit = sectionIndex;

        if ((uint)bit >= 64u)
        {
            return true;
        }

        return (mask & (1UL << bit)) != 0;
    }
}
