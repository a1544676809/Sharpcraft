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
/// <para>
/// Two different bases are in play and mixing them up silently misaligns every bit, so each is used for exactly one
/// job. A bit position is measured from the world's lowest section, because that is what the renderer counts from when
/// it names a section of a chunk. A mesh's own matrix array is measured from that chunk's lowest section, because that
/// is how the mesher filled it.
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
    private readonly Func<long, MeshData?> _meshSource;
    private readonly Func<SectionCoord, bool> _alwaysInFrustum = _ => true;
    private readonly Func<SectionCoord> _camera;
    private readonly MeshData?[] _columnMesh;
    private readonly byte[] _columnState;
    private SectionCoord _walkCamera;
    private readonly Func<int> _worldVersion;
    private SectionCoord? _lastCameraSection;
    private int _lastWorldVersion = -1;

    /// <summary>Creates the state for a world of the given vertical extent.</summary>
    /// <param name="radius">Reach in sections along X and Z.</param>
    /// <param name="minimumY">Lowest section Y in the world.</param>
    /// <param name="maximumY">Highest section Y in the world.</param>
    /// <param name="isLoaded">Whether a section has a chunk behind it.</param>
    /// <param name="meshSource">Supplies a chunk's mesh, or null when it has none yet.</param>
    /// <param name="camera">Current camera section, read when the predicate runs.</param>
    public SectionOcclusionState(
        int radius,
        int minimumY,
        int maximumY,
        Func<SectionCoord, bool> isLoaded,
        Func<long, MeshData?> meshSource,
        Func<SectionCoord> camera,
        Func<int> worldVersion)
    {
        ArgumentNullException.ThrowIfNull(isLoaded);
        ArgumentNullException.ThrowIfNull(meshSource);
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(worldVersion);

        _radius = radius;
        _minimumY = minimumY;
        _maximumY = maximumY;
        _isLoaded = isLoaded;
        _meshSource = meshSource;
        _camera = camera;
        _worldVersion = worldVersion;

        var volume = SectionOcclusionTraversal.Volume(radius, minimumY, maximumY);
        var side = (2 * radius) + 1;
        _columnMesh = new MeshData?[side * side];
        _columnState = new byte[side * side];
        _queue = new SectionOcclusionTraversal.TraversalNode[volume * SectionOcclusionTraversal.EntriesPerSection];
        _visited = new byte[volume];
        _output = new SectionCoord[volume];

        // Query only. Rebuilding here would run the walk once per section per frame, which is thousands of walks
        // while chunks are still streaming in and cost more than the drawing it saves.
        IsVisible = Test;

        // One lookup per chunk for the draw path, which asks per section otherwise: four dozen sections of a column
        // would repeat the same lookup forty eight times a frame.
        ChunkMask = key => _visible.TryGetValue(key, out var mask) ? mask : 0UL;
    }

    /// <summary>Predicate the renderer calls per section; held in a field so passing it costs nothing.</summary>
    public Func<long, int, bool> IsVisible { get; }

    /// <summary>Visible sections of one chunk column as a bit mask; zero when the walk kept none.</summary>
    public Func<long, ulong> ChunkMask { get; }

    /// <summary>Current camera section, so the caller can refresh once per frame rather than per query.</summary>
    public SectionCoord CameraSection => _camera();

    /// <summary>Whether the walk kept the given section.</summary>
    /// <param name="chunkKey">Packed chunk key.</param>
    /// <param name="sectionIndex">Section index counted from the world's lowest section.</param>
    public bool Test(long chunkKey, int sectionIndex)
    {
        if (!_visible.TryGetValue(chunkKey, out var mask))
        {
            return false;
        }

        // Outside the mask the walk has nothing to say, so err towards drawing.
        if ((uint)sectionIndex >= 64u)
        {
            return true;
        }

        return (mask & (1UL << sectionIndex)) != 0;
    }

    /// <summary>Redoes the walk when the camera has moved to a different section.</summary>
    /// <param name="cameraSection">Section the camera occupies.</param>
    /// <returns>True when the set was rebuilt.</returns>
    public bool Refresh(SectionCoord cameraSection)
    {
        // The camera moving is only one reason what is reachable can change: chunks stream in and their meshes
        // arrive over the first seconds of a session. Rebuilding only on camera movement froze the very first
        // walk, taken while a few dozen chunks existed, and kept drawing against it for the rest of the session.
        var version = _worldVersion();
        if (_lastCameraSection == cameraSection && _lastWorldVersion == version)
        {
            return false;
        }

        _lastCameraSection = cameraSection;
        _lastWorldVersion = version;
        _walkCamera = cameraSection;
        Array.Clear(_columnMesh);
        Array.Clear(_columnState);
        _visible.Clear();

        SectionOcclusionTraversal.Traverse(
            cameraSection,
            _radius,
            _minimumY,
            _maximumY,
            IsLoadedColumn,
            VisibilityOf,
            _alwaysInFrustum,
            _queue,
            _visited,
            _output,
            out var count);

        for (var index = 0; index < count; index++)
        {
            var coord = _output[index];
            var key = ClientWorld.Pack(coord.X, coord.Z);

            // Counted from the world's lowest section, matching how the renderer names a section.
            var bit = coord.Y - _minimumY;

            if ((uint)bit >= 64u)
            {
                continue;
            }

            _visible[key] = _visible.TryGetValue(key, out var mask) ? mask | (1UL << bit) : 1UL << bit;
        }

        return true;
    }

    /// <summary>Resolves a chunk column once per walk, not once per section that happens to sit in it.</summary>
    /// <remarks>
    /// The walk asks about sections, and a column holds two dozen of them, so asking the world and the mesh cache per
    /// section meant tens of thousands of hashed lookups every frame to answer six hundred and twenty five distinct
    /// questions. Answering each column once is what turns the walk from the largest part of the frame into noise.
    /// </remarks>
    private bool TryColumn(SectionCoord coord, out MeshData? mesh, out bool loaded)
    {
        var side = (2 * _radius) + 1;
        var x = coord.X - _walkCamera.X + _radius;
        var z = coord.Z - _walkCamera.Z + _radius;

        if ((uint)x >= (uint)side || (uint)z >= (uint)side)
        {
            mesh = null;
            loaded = false;
            return false;
        }

        var at = (z * side) + x;

        if (_columnState[at] == 0)
        {
            var worldX = _walkCamera.X - _radius + x;
            var worldZ = _walkCamera.Z - _radius + z;
            _columnMesh[at] = _meshSource(ClientWorld.Pack(worldX, worldZ));
            _columnState[at] = (byte)(_isLoaded(new SectionCoord(worldX, coord.Y, worldZ)) ? 1 : 2);
        }

        loaded = _columnState[at] == 1;
        mesh = _columnMesh[at];
        return true;
    }

    private bool IsLoadedColumn(SectionCoord coord) => TryColumn(coord, out _, out var loaded) && loaded;

    private SectionVisibility VisibilityOf(SectionCoord coord)
    {
        if (!TryColumn(coord, out var mesh, out _))
        {
            return SectionVisibility.AllVisible;
        }

        if (mesh is not { SectionVisibility: { } matrices })
        {
            // No geometry means nothing to occlude, which must not stop the walk.
            return SectionVisibility.AllVisible;
        }

        // This chunk's array is counted from its own lowest section, not the world's.
        var index = coord.Y - mesh.SectionMinY;
        return (uint)index < (uint)matrices.Length ? matrices[index] : SectionVisibility.AllVisible;
    }
}
