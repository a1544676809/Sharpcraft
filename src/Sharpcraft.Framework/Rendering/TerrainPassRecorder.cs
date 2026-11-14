// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// Records the two terrain draws of the scene pass: the opaque layer and the cutout layer.
/// </summary>
/// <remarks>
/// They look alike but differ in one way worth keeping visible: the cutout layer only draws when its arena
/// actually holds chunks, while the opaque layer reports whether it drew so the timing around it stays
/// meaningful. Both hand their arena the frustum and the optional per-section visibility predicate rather
/// than walking chunks themselves.
/// </remarks>
internal sealed class TerrainPassRecorder
{
    private readonly IRenderBackend _backend;
    private readonly PipelineHandle _opaquePipeline;
    private readonly PipelineHandle _cutoutPipeline;
    private readonly BindGroupHandle _bindGroup;
    private readonly ChunkGeometryStore? _arena;
    private readonly ChunkGeometryStore? _arenaCutout;

    /// <summary>Captures the pipelines and arenas a terrain draw needs.</summary>
    /// <param name="backend">Backend to record through.</param>
    /// <param name="opaquePipeline">Pipeline the opaque layer draws with.</param>
    /// <param name="cutoutPipeline">Pipeline the cutout layer draws with.</param>
    /// <param name="bindGroup">Bind group both layers share.</param>
    /// <param name="arena">Opaque geometry, or null when the host supplies static geometry instead.</param>
    /// <param name="arenaCutout">Cutout geometry, or null when the host supplies static geometry instead.</param>
    public TerrainPassRecorder(
        IRenderBackend backend,
        PipelineHandle opaquePipeline,
        PipelineHandle cutoutPipeline,
        BindGroupHandle bindGroup,
        ChunkGeometryStore? arena,
        ChunkGeometryStore? arenaCutout)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _opaquePipeline = opaquePipeline;
        _cutoutPipeline = cutoutPipeline;
        _bindGroup = bindGroup;
        _arena = arena;
        _arenaCutout = arenaCutout;
    }

    /// <summary>Records the opaque terrain layer.</summary>
    /// <param name="pass">Scene pass being recorded.</param>
    /// <param name="skipTerrain">Host asked for terrain to be left out.</param>
    /// <param name="disableCulling">Host asked for every chunk, culled or not.</param>
    /// <param name="frustumPlanes">Six planes in the same order the store expects.</param>
    /// <param name="visibleSections">Per-section visibility, or null to draw every reached chunk.</param>
    /// <returns>Whether anything was drawn, so the caller can attribute the time it took.</returns>
    public bool RecordOpaque(
        RenderPassHandle pass,
        bool skipTerrain,
        bool disableCulling,
        ReadOnlySpan<System.Numerics.Vector4> frustumPlanes,
        Func<long, ulong>? visibleSections)
    {
        if (skipTerrain)
        {
            return false;
        }

        _backend.SetPipeline(pass, _opaquePipeline);
        _backend.SetBindGroup(pass, 0, _bindGroup);

        if (_arena is not { ChunkCount: > 0 } arena)
        {
            // Nothing to draw; clouds are drawn after the transparent terrain further on.
            return false;
        }

        if (disableCulling)
        {
            arena.Draw(pass);
        }
        else
        {
            arena.Draw(pass, frustumPlanes, visibleSections);
        }

        return true;
    }

    /// <summary>Records the cutout terrain layer.</summary>
    /// <param name="pass">Scene pass being recorded.</param>
    /// <param name="skipTerrain">Host asked for terrain to be left out.</param>
    /// <param name="disableCulling">Host asked for every chunk, culled or not.</param>
    /// <param name="frustumPlanes">Six planes in the same order the store expects.</param>
    /// <param name="visibleSections">Per-section visibility, or null to draw every reached chunk.</param>
    public void RecordCutout(
        RenderPassHandle pass,
        bool skipTerrain,
        bool disableCulling,
        ReadOnlySpan<System.Numerics.Vector4> frustumPlanes,
        Func<long, ulong>? visibleSections)
    {
        if (skipTerrain || _arenaCutout is not { ChunkCount: > 0 } arenaCutout)
        {
            return;
        }

        _backend.SetPipeline(pass, _cutoutPipeline);
        _backend.SetBindGroup(pass, 0, _bindGroup);
        if (disableCulling)
        {
            arenaCutout.Draw(pass);
        }
        else
        {
            arenaCutout.Draw(pass, frustumPlanes, visibleSections);
        }
    }
}
