// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Registries;
using Sharpcraft.Common.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Keeps one mesh per chunk so a block change only re-meshes the chunk it touched, and can do that work on a
/// background thread so the render thread never pays for it
/// (docs/specs/network/play-movement.md 7.19 and 7.20).
/// </summary>
/// <remarks>
/// Meshing a full column costs tens of milliseconds, which is a dropped frame when it happens while drawing.
/// The synchronous <see cref="Build"/> path stays for tests and offscreen tools; the windowed path calls
/// <see cref="StartBackground"/>, drains finished meshes once per frame with <see cref="Drain"/>, and combines
/// the cached results itself, which is a cheap copy because greedy meshing keeps the vertex counts small.
/// </remarks>
public sealed class ChunkMeshCache
{
    private readonly Dictionary<long, MeshData> _meshes = [];
    private readonly List<MeshedChunk> _updates = [];
    private readonly List<long> _pruned = [];
    private readonly ChunkWorkerPool? _pool;
    private LightmapParameters _appliedLighting = LightmapParameters.DayOverworld;

    /// <summary>Creates a standalone cache that meshes on the calling thread.</summary>
    public ChunkMeshCache()
    {
    }

    /// <summary>Creates a cache fed by a background pool.</summary>
    /// <param name="pool">Pool that produces the meshes.</param>
    public ChunkMeshCache(ChunkWorkerPool pool)
    {
        ArgumentNullException.ThrowIfNull(pool);
        _pool = pool;
    }

    /// <summary>Meshes a whole world into one merged mesh, for the local demo world.</summary>
    /// <param name="world">World to mesh.</param>
    /// <param name="sprites">Atlas mapping, or null for untextured geometry.</param>
    /// <param name="lighting">Lightmap parameters baked into the vertex colours.</param>
    /// <param name="merge">Whether coplanar faces are greedily merged.</param>
    /// <returns>The merged geometry.</returns>
    public static MeshData MeshWorldOnce(ClientWorld world, BlockSpriteMap? sprites, LightmapParameters lighting, bool merge) =>
        ChunkMesher.Mesh(world, airState: 0, maxBlocks: 2_000_000, sprites: sprites, merge: merge, lighting: lighting);

    /// <summary>
    /// Meshes one chunk from an explicit parameter set, for callers that hold no cache (the offscreen frame).
    /// </summary>
    /// <remarks>
    /// This is the second and last meshing entry. The cache builds with the per pass <see cref="StateCullTable"/> and
    /// <see cref="StateVisualCache"/>, while this form forwards to the plain overload; both live here so that no code
    /// outside <c>Render</c> names the mesher, which is what the "meshing has one home" architecture test checks (T-793).
    /// </remarks>
    /// <param name="world">World the chunk belongs to.</param>
    /// <param name="chunk">Chunk to mesh.</param>
    /// <param name="solid">Buffer that receives the solid faces.</param>
    /// <param name="sprites">Atlas mapping, or null for untextured geometry.</param>
    /// <param name="translucent">Buffer that receives blended faces, or null to keep them solid.</param>
    /// <param name="layout">Layout that names block states, or null when no layer split is wanted.</param>
    /// <param name="cutout">Buffer that receives alpha tested faces, or null to keep them solid.</param>
    /// <param name="tint">Biome tint, or null for none.</param>
    /// <param name="lighting">Lightmap parameters baked into the vertex colours.</param>
    /// <param name="merge">Whether coplanar faces are greedily merged.</param>
    /// <param name="maxBlocks">Safety cap on the blocks this call may contribute.</param>
    /// <returns>Number of solid blocks that contributed at least one face.</returns>
    public static int MeshChunkOnce(
        ClientWorld world,
        LevelChunk chunk,
        MeshBuffer solid,
        BlockSpriteMap? sprites,
        MeshBuffer? translucent,
        BlockStateLayout? layout,
        MeshBuffer? cutout,
        BiomeTint? tint,
        LightmapParameters lighting,
        bool merge,
        int maxBlocks = 2_000_000) =>
        ChunkMesher.MeshChunk(
            world, chunk, airState: 0, solid, maxBlocks: maxBlocks, sprites: sprites, translucent: translucent,
            layout: layout, cutout: cutout, tint: tint, merge: merge, lighting: lighting);

    /// <summary>Chunks re-meshed by the last synchronous <see cref="Build"/> call.</summary>
    public int LastRemeshedChunks { get; private set; }

    /// <summary>Chunks this cache has meshed over its lifetime.</summary>
    /// <remarks>
    /// The cache is the only mesher the world geometry comes from (T-793). A request builder that meshes on its own
    /// leaves this counter unchanged, which is what the "requesting a frame must not mesh" test asserts.
    /// </remarks>
    public int TotalChunksMeshed { get; private set; }

    /// <summary>Chunks in the cache after the last build.</summary>
    public int LastTotalChunks => _meshes.Count;

    /// <summary>Chunks still queued for the pool or waiting to be ingested.</summary>
    public int PendingChunks => _pool is null ? 0 : _pool.MeshesPending + _pool.MeshesReady;

    /// <summary>Milliseconds the last synchronous <see cref="Build"/> call spent meshing.</summary>
    public double LastBuildMilliseconds { get; private set; }

    /// <summary>Drops every cached mesh; the next build re-meshes everything.</summary>
    public void Clear() => _meshes.Clear();

    /// <summary>
    /// Meshes the missing chunks synchronously, at most <paramref name="maxChunksPerBuild"/> of them.
    /// </summary>
    /// <param name="world">World to mesh.</param>
    /// <param name="sprites">Atlas mapping.</param>
    /// <param name="maxBlocks">Safety cap.</param>
    /// <param name="maxChunksPerBuild">Upper bound on chunks meshed in this call.</param>
    /// <param name="maxMillisecondsPerBuild">Wall-clock budget; a chunk is never cut in half.</param>
    /// <summary>Builds with a shared policy, so callers do not each pick their own numbers.</summary>
    /// <param name="world">World to build for.</param>
    /// <param name="sprites">Atlas mapping, or null for untextured.</param>
    /// <param name="policy">Batch and time budget.</param>
    /// <param name="maxBlocks">Block ceiling for one call.</param>
    public MeshData Build(ClientWorld world, BlockSpriteMap? sprites, MeshBuildPolicy policy, int maxBlocks = int.MaxValue) =>
        Build(
            world,
            sprites,
            maxBlocks: maxBlocks,
            maxChunksPerBuild: policy.MaxChunksPerPass,
            maxMillisecondsPerBuild: policy.MaxMillisecondsPerPass);

    /// <summary>Sinks that take the cutout and translucent faces out of the solid mesh while the cache builds.</summary>
    /// <remarks>
    /// Without them every block, leaves and water included, lands in the arena mesh as well as in its own layer, and the
    /// solid copy of a leaf shows its transparent texels as black holes.
    /// </remarks>
    public LayerSinks? Layers { get; set; }

    /// <summary>
    /// Lightmap parameters used when meshing. Because the lightmap is baked into vertex colours, changing this (day/night)
    /// invalidates every cached chunk on the next <see cref="Build"/>; a live renderer sets it when the day time drifts far
    /// enough to matter. Defaults to overworld day, so callers that never touch it keep the previous behaviour
    /// (docs/specs/render/lighting.md 6).
    /// </summary>
    public LightmapParameters Lighting { get; set; } = LightmapParameters.DayOverworld;

    /// <summary>Whether greedy face merging is enabled when meshing.</summary>
    public bool MergeFaces { get; set; } = true;

    /// <summary>Whether per-vertex ambient occlusion is enabled when meshing.</summary>
    public bool AmbientOcclusion { get; set; } = true;

    public MeshData Build(
        ClientWorld world,
        BlockSpriteMap? sprites = null,
        int maxBlocks = 2_000_000,
        int maxChunksPerBuild = 1,
        double maxMillisecondsPerBuild = 15d)
    {
        ArgumentNullException.ThrowIfNull(world);

        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var remeshed = 0;

        if (Lighting != _appliedLighting)
        {
            // Lightmap is per-vertex, so a changed day/night state invalidates every mesh, not just dirty chunks.
            _meshes.Clear();
            _appliedLighting = Lighting;
        }

        foreach (var key in world.TakeDirtyChunks())
        {
            _meshes.Remove(key);
        }

        // One table for the whole pass: it depends only on the atlas and the layout, not on the chunk.
        var layers = Layers;
        var cullTable = layers?.Layout is { } cullLayout && sprites is not null
            ? new StateCullTable(cullLayout, sprites)
            : null;
        var visualCache = new StateVisualCache(sprites, layers?.Layout);

        // Built once per pass so every chunk meshes with identical parameters, matching the worker pool path.
        var context = new MeshingContext(
            Sprites: sprites,
            Layout: layers?.Layout,
            Tint: layers?.Tint,
            Cull: cullTable,
            Visuals: visualCache,
            Lighting: Lighting,
            MergeFaces: MergeFaces,
            AmbientOcclusion: AmbientOcclusion,
            AirState: 0,
            MaxBlocks: maxBlocks);

        foreach (var chunk in world.GetChunks())
        {
            var key = ClientWorld.Pack(chunk.ChunkX, chunk.ChunkZ);
            if (_meshes.ContainsKey(key))
            {
                continue;
            }

            if (remeshed >= maxChunksPerBuild)
            {
                break;
            }

            if (remeshed > 0 &&
                (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000d
                / System.Diagnostics.Stopwatch.Frequency >= maxMillisecondsPerBuild)
            {
                break;
            }

            using var buffer = new MeshBuffer(512);
            ChunkMesher.MeshChunk(world, chunk, buffer, layers?.Cutout, layers?.Translucent, in context);
            _meshes[key] = buffer.ToMeshData(0, chunk.MinSectionY);
            TotalChunksMeshed++;
            remeshed++;
        }

        LastRemeshedChunks = remeshed;
        LastBuildMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000d
            / System.Diagnostics.Stopwatch.Frequency;

        return Combine();
    }

    /// <summary>Queues a chunk for re-meshing on the pool.</summary>
    /// <param name="chunkKey">Packed chunk key.</param>
    public void Invalidate(long chunkKey)
    {
        // The stale mesh is kept on purpose: dropping it would leave a hole until the replacement arrives.
        _pool?.TryEnqueueMesh(chunkKey);
    }

    /// <summary>Drops a cached chunk because it left the view; the caller also releases its GPU buffers.</summary>
    public void Remove(long chunkKey) => _meshes.Remove(chunkKey);

    /// <summary>
    /// Every mesh currently held, for a full re-upload.
    /// </summary>
    /// <remarks>
    /// A rebuild that resets the renderer's geometry storage has to put every mesh back, and the incremental queue only
    /// carries what changed. This is the difference between a world that reappears and a world that stays empty.
    /// </remarks>
    public List<MeshedChunk> SnapshotAll() => [.. _meshes.Select(entry => MeshedChunk.FromOpaque(entry.Key, entry.Value))];

    /// <summary>Queues every loaded chunk that has no mesh yet; called as terrain streams in.</summary>
    /// <param name="world">World to scan.</param>
    public void QueueMissing(ClientWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (_pool is null)
        {
            return;
        }

        foreach (var chunk in world.GetChunks())
        {
            var key = ClientWorld.Pack(chunk.ChunkX, chunk.ChunkZ);
            if (!_meshes.ContainsKey(key))
            {
                _pool.TryEnqueueMesh(key);
            }
        }
    }

    /// <summary>
    /// Picks up the meshes the pool finished and returns them, so the renderer can upload only those chunks
    /// instead of recomposing the whole world (docs/specs/network/play-movement.md 7.25).
    /// </summary>
    public IReadOnlyList<MeshedChunk> DrainUpdates(ClientWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        _updates.Clear();

        if (_pool is null)
        {
            return _updates;
        }

        while (_pool.TryTakeMesh(out var key, out var mesh, out var cutout, out var translucent))
        {
            // The chunk can leave the view between the pool's own existence check and this insert. Installing the mesh
            // then would leave an entry that nothing removes, because the removal path already ran. The reference asks
            // its cancellation flag once a build finishes and releases the mesh instead of installing it; this is the
            // same decision with "the chunk is gone" as the signal.
            var (chunkX, chunkZ) = ClientWorld.Unpack(key);
            if (world.GetChunk(chunkX, chunkZ) is null)
            {
                continue;
            }

            _meshes[key] = mesh;
            _updates.Add(new MeshedChunk(key, mesh, cutout, translucent));
        }

        return _updates;
    }

    /// <summary>
    /// Drops every cached mesh whose chunk is no longer loaded, returning the keys it released.
    /// </summary>
    /// <param name="world">World whose loaded chunks define what may stay.</param>
    /// <returns>Keys whose meshes were released, for the caller to release their geometry.</returns>
    /// <remarks>
    /// The removal path only reports the chunks it removes itself, so any mesh that finished after its chunk was
    /// already gone survives it forever. A measured session held 1281 cached meshes against 637 loaded chunks, 613 MB
    /// of geometry, which is what this sweep reclaims. It is deliberately a reconciliation against the world rather
    /// than another removal hook, so it also covers paths that forget to report.
    /// </remarks>
    public List<long> PruneTo(ClientWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        _pruned.Clear();

        foreach (var key in _meshes.Keys)
        {
            var (chunkX, chunkZ) = ClientWorld.Unpack(key);
            if (world.GetChunk(chunkX, chunkZ) is null)
            {
                _pruned.Add(key);
            }
        }

        foreach (var key in _pruned)
        {
            _meshes.Remove(key);
        }

        return _pruned;
    }

    /// <summary>Picks up the meshes the pool finished; true when the cached geometry changed.</summary>
    public bool Drain(ClientWorld world) => DrainUpdates(world).Count > 0;

    /// <summary>Combines the meshes currently cached; cheap, no meshing.</summary>
    public MeshData Snapshot() => Combine();

    /// <summary>Looks up the mesh of one chunk, for the occlusion walk (T-833).</summary>
    /// <param name="chunkKey">Packed chunk key.</param>
    /// <param name="mesh">Mesh of that chunk when present.</param>
    public bool TryGetMesh(long chunkKey, out MeshData mesh) => _meshes.TryGetValue(chunkKey, out mesh!);

    private MeshData Combine()
    {
        var vertexCount = 0;
        var indexCount = 0;
        foreach (var mesh in _meshes.Values)
        {
            vertexCount += mesh.Vertices.Length;
            indexCount += mesh.Indices.Length;
        }

        var vertices = new float[vertexCount];
        var indices = new uint[indexCount];
        var vertexOffset = 0;
        var indexOffset = 0;

        foreach (var mesh in _meshes.Values)
        {
            mesh.Vertices.CopyTo(vertices, vertexOffset);
            var baseVertex = (uint)(vertexOffset / ChunkMesher.FloatsPerVertex);
            for (var index = 0; index < mesh.Indices.Length; index++)
            {
                indices[indexOffset + index] = mesh.Indices[index] + baseVertex;
            }

            vertexOffset += mesh.Vertices.Length;
            indexOffset += mesh.Indices.Length;
        }

        return new MeshData(vertices, indices, _meshes.Count);
    }
}
