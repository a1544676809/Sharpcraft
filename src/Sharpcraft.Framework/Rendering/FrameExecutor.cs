// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Framework.Rendering;

/// <summary>Colour and depth views one whole frame is recorded into, together with their size.</summary>
/// <remarks>
/// The recording unit never acquires or presents a surface: the caller owns the target. The windowed renderer passes
/// the view of its presentation texture; a later caller can pass a readback texture view and get the same passes.
/// </remarks>
internal readonly record struct FrameTarget(TextureViewHandle Colour, TextureViewHandle Depth, uint Width, uint Height);

/// <summary>
/// Records one complete frame into a caller supplied <see cref="FrameTarget"/>: camera, lightmap and fog uniforms,
/// the canonical scene passes, the cloud depth blit, the reference OIT sequence and the interface. Presentation and
/// submission stay with the caller, which is what makes the target parameterisable.
/// </summary>
/// <remarks>
/// Extracted verbatim from <c>WindowedRenderer.Run</c> (docs/specs/render/single-track-executor.md, step 1). Resources
/// that live for the whole renderer are captured once in <see cref="Resources"/>; values that change per frame arrive
/// in <see cref="Frame"/>.
/// <para>
/// Step 2A of that spec removed the four transitional delegates: every size-dependent OIT texture, view, bind group
/// and the cloud depth blit cache now live in <see cref="FrameOitTargets"/>, owned by the executor through
/// <see cref="Resources.OitTargets"/>. The only target-dependent value the caller still supplies is
/// <see cref="FrameTarget.Depth"/>, which keys the blit bind group. The recorder is therefore no longer coupled to
/// window-side locals and an offscreen caller can use it with its own target set (step 2B).
/// </para>
/// </remarks>
internal static class FrameExecutor
{
    /// <summary>Long-lived resources the frame is recorded with; built once by the owning renderer.</summary>
    internal sealed class Resources
    {
        /// <summary>Backend every pass is recorded through.</summary>
        public required IRenderBackend Backend { get; init; }

        /// <summary>The initial request, whose camera and field of view stay fixed for the renderer's lifetime.</summary>
        public required FrameRequest Request { get; init; }

        /// <summary>Fallback clear colour.</summary>
        public required Vector4 ClearColour { get; init; }

        /// <summary>Per-frame clear colour, or null to use the fallback.</summary>
        public Func<Vector4>? ClearColourPerFrame { get; init; }

        /// <summary>Per-frame lightmap state.</summary>
        public Func<LightmapUniform>? LightmapPerFrame { get; init; }

        /// <summary>Per-frame fog colour.</summary>
        public Func<Vector4>? FogPerFrame { get; init; }

        /// <summary>Per-frame atmospheric fog range.</summary>
        public Func<(float Start, float End)>? FogRangePerFrame { get; init; }

        /// <summary>Per-frame world text draws.</summary>
        public Func<IReadOnlyList<WorldTextDraw>?>? WorldTextPerFrame { get; init; }

        /// <summary>Per-frame cloud draw.</summary>
        public Func<CelestialColorDraw?>? CloudsPerFrame { get; init; }

        /// <summary>Visibility bitset lookup for a section key.</summary>
        public Func<long, ulong>? VisibleSections { get; init; }

        /// <summary>Stage timing sink, bound once so recording allocates nothing for it.</summary>
        public required FrameCpuTimings Cpu { get; init; }

        /// <summary>Main terrain pipeline bound when the scene pass opens.</summary>
        public required PipelineHandle TerrainPipeline { get; init; }

        /// <summary>Interface pipeline.</summary>
        public required PipelineHandle UiPipeline { get; init; }

        /// <summary>Interface text pipeline.</summary>
        public required PipelineHandle UiTextPipeline { get; init; }

        /// <summary>Interface sprite pipeline.</summary>
        public required PipelineHandle UiSpritePipeline { get; init; }

        /// <summary>Full-screen depth blit pipeline.</summary>
        public required PipelineHandle BlitDepthPipeline { get; init; }

        /// <summary>Terrain depth-bounds pipeline.</summary>
        public required PipelineHandle RefDepthBoundsPipeline { get; init; }

        /// <summary>Depth-bounds cull pipeline.</summary>
        public required PipelineHandle RefCullPipeline { get; init; }

        /// <summary>Wavelet transmittance pipeline.</summary>
        public required PipelineHandle RefTransmittancePipeline { get; init; }

        /// <summary>Accumulate pipeline.</summary>
        public required PipelineHandle RefAccumulatePipeline { get; init; }

        /// <summary>Reference composite pipeline.</summary>
        public required PipelineHandle RefCompositePipeline { get; init; }

        /// <summary>Cloud depth-bounds pipeline, or default when cloud OIT is off.</summary>
        public required PipelineHandle CloudDepthBoundsPipeline { get; init; }

        /// <summary>Cloud transmittance pipeline, or default when cloud OIT is off.</summary>
        public required PipelineHandle CloudTransmittancePipeline { get; init; }

        /// <summary>Cloud accumulate pipeline, or default when cloud OIT is off.</summary>
        public required PipelineHandle CloudAccumulatePipeline { get; init; }

        /// <summary>Records the sky, opaque and cutout terrain.</summary>
        public required TerrainPassRecorder TerrainRecorder { get; init; }

        /// <summary>Records the entity layers.</summary>
        public required EntityPassRecorder EntityRecorder { get; init; }

        /// <summary>Records the sky backdrop and celestial bodies.</summary>
        public required SkyPassRecorder SkyRecorder { get; init; }

        /// <summary>Writes the camera, lightmap and fog uniform blocks.</summary>
        public required FrameUniformWriter UniformWriter { get; init; }

        /// <summary>Interface geometry buffers.</summary>
        public required GrowableGeometryBuffers UiBuffers { get; init; }

        /// <summary>Entity layer buffers.</summary>
        public required EntityLayerPool LayerPool { get; init; }

        /// <summary>Per-sprite interface bind groups.</summary>
        public required IReadOnlyList<BindGroupHandle> SpriteBindGroups { get; init; }

        /// <summary>Interface draw ranges.</summary>
        public required IReadOnlyList<UiGeometry.Range> UiRanges { get; init; }

        /// <summary>Scratch for the six frustum planes.</summary>
        public required Vector4[] FrustumPlanes { get; init; }

        /// <summary>World text resources, or null when the host draws no text.</summary>
        public WorldTextResources? WorldText { get; init; }

        /// <summary>Colour-only cloud draws, or null when cloud OIT is on.</summary>
        public OffscreenRenderer.CelestialColorResources? CloudColour { get; init; }

        /// <summary>Cloud OIT resources, or null when cloud OIT is off.</summary>
        public CloudOitResources? CloudOit { get; init; }

        /// <summary>Item transparency target set.</summary>
        public required ItemOitSet ItemOit { get; init; }

        /// <summary>Item atlas, whose bind group the item OIT draws bind.</summary>
        public required EntitySkinTextureGpu ItemAtlas { get; init; }

        /// <summary>Executor-owned size-dependent OIT targets and the cloud depth blit bind group cache.</summary>
        public required FrameOitTargets OitTargets { get; init; }
    }

    /// <summary>Per-frame inputs, including the render target the whole frame is recorded into.</summary>
    internal readonly struct Frame
    {
        /// <summary>Colour view, depth view and size of the target.</summary>
        public required FrameTarget Target { get; init; }

        /// <summary>Camera eye for this frame.</summary>
        public required Vector3 Eye { get; init; }

        /// <summary>Camera look-at point for this frame.</summary>
        public required Vector3 CameraTarget { get; init; }

        /// <summary>Timestamp the camera stage started, used for the diagnostic camera cost.</summary>
        public required long CameraStarted { get; init; }

        /// <summary>The live request for this frame.</summary>
        public required FrameRequest CurrentRequest { get; init; }

        /// <summary>World bind group, replaced when the atlas is reloaded.</summary>
        public required BindGroupHandle BindGroup { get; init; }

        /// <summary>Interface bind group, replaced when the atlas is reloaded.</summary>
        public required BindGroupHandle UiBindGroup { get; init; }

        /// <summary>Interface text bind group, replaced when textures are reloaded.</summary>
        public required BindGroupHandle UiTextBindGroup { get; init; }

        /// <summary>Solid chunk store, replaced on a geometry epoch change.</summary>
        public ChunkGeometryStore? Arena { get; init; }

        /// <summary>Cutout chunk store, replaced on a geometry epoch change.</summary>
        public ChunkGeometryStore? ArenaCutout { get; init; }

        /// <summary>Translucent chunk store, replaced on a geometry epoch change.</summary>
        public ChunkGeometryStore? ArenaTranslucent { get; init; }

        /// <summary>Dynamic geometry index count for this frame.</summary>
        public required uint DynamicIndexCount { get; init; }

        /// <summary>Number of entity layers drawn this frame.</summary>
        public required int EntityLayerDrawn { get; init; }

        /// <summary>Interface scale, used by the scissor rectangles.</summary>
        public required float UiScale { get; init; }

        /// <summary>Host asked to keep the previous clear.</summary>
        public required bool SkipClear { get; init; }

        /// <summary>Host asked to leave the sky out.</summary>
        public required bool SkipSky { get; init; }

        /// <summary>Host asked to leave the celestial bodies out.</summary>
        public required bool SkipCelestial { get; init; }

        /// <summary>Host asked to leave the terrain out.</summary>
        public required bool SkipTerrain { get; init; }

        /// <summary>Host asked to leave the entities out.</summary>
        public required bool SkipEntities { get; init; }

        /// <summary>Host asked to leave the OIT sequence out.</summary>
        public required bool SkipOit { get; init; }

        /// <summary>Host asked to leave the clouds out.</summary>
        public required bool SkipClouds { get; init; }

        /// <summary>Host asked to drop the translucent draw only.</summary>
        public required bool SkipTranslucent { get; init; }

        /// <summary>Host asked to leave the interface out.</summary>
        public required bool SkipInterface { get; init; }

        /// <summary>Host asked to disable frustum culling.</summary>
        public required bool DisableCulling { get; init; }
    }

    /// <summary>The recorded encoder plus the two counters the caller still accumulates.</summary>
    internal readonly record struct Result(CommandEncoderHandle Encoder, double CameraMs, bool ItemOitRan);

    /// <summary>Records the whole frame into the supplied target and returns the encoder, still open for presentation.</summary>
    /// <param name="r">Long-lived resources captured once by the owning renderer.</param>
    /// <param name="f">Per-frame inputs, including the render target.</param>
    /// <returns>The recorded encoder and the diagnostic values the caller accumulates.</returns>
    public static Result Record(Resources r, in Frame f)
    {
        var backend = r.Backend;
        var request = r.Request;
        var colour = r.ClearColour;
        var clearColourPerFrame = r.ClearColourPerFrame;
        var lightmapPerFrame = r.LightmapPerFrame;
        var fogPerFrame = r.FogPerFrame;
        var fogRangePerFrame = r.FogRangePerFrame;
        var worldTextPerFrame = r.WorldTextPerFrame;
        var cloudsPerFrame = r.CloudsPerFrame;
        var visibleSections = r.VisibleSections;
        var cpu = r.Cpu;

        void Cpu(int stage) => cpu.Mark(stage);

        Span<float> cameraBlock = stackalloc float[36];

        var frameView = f.Target.Colour;
        var depthViewHandle = f.Target.Depth;
        var frameWidth = f.Target.Width;
        var frameHeight = f.Target.Height;
        var eye = f.Eye;
        var target = f.CameraTarget;
        var cameraStarted = f.CameraStarted;
        var currentRequest = f.CurrentRequest;
        var bindGroup = f.BindGroup;
        var uiBindGroup = f.UiBindGroup;
        var uiTextBindGroup = f.UiTextBindGroup;
        var arena = f.Arena;
        var arenaCutout = f.ArenaCutout;
        var arenaTranslucent = f.ArenaTranslucent;
        var dynamicIndexCount = f.DynamicIndexCount;
        var entityLayerDrawn = f.EntityLayerDrawn;
        var uiScale = f.UiScale;
        var skipClear = f.SkipClear;
        var skipSky = f.SkipSky;
        var skipCelestial = f.SkipCelestial;
        var skipTerrain = f.SkipTerrain;
        var skipEntities = f.SkipEntities;
        var skipOit = f.SkipOit;
        var skipClouds = f.SkipClouds;
        var skipTranslucent = f.SkipTranslucent;
        var skipInterface = f.SkipInterface;
        var disableCulling = f.DisableCulling;

        var pipeline = r.TerrainPipeline;
        var uiPipeline = r.UiPipeline;
        var uiTextPipeline = r.UiTextPipeline;
        var uiSpritePipeline = r.UiSpritePipeline;
        var blitDepthPipeline = r.BlitDepthPipeline;
        var refDepthBoundsPipeline = r.RefDepthBoundsPipeline;
        var refCullPipeline = r.RefCullPipeline;
        var refTransmittancePipeline = r.RefTransmittancePipeline;
        var refAccumulatePipeline = r.RefAccumulatePipeline;
        var refCompositePipeline = r.RefCompositePipeline;
        var cloudDepthBoundsPipeline = r.CloudDepthBoundsPipeline;
        var cloudTransmittancePipeline = r.CloudTransmittancePipeline;
        var cloudAccumulatePipeline = r.CloudAccumulatePipeline;
        var terrainRecorder = r.TerrainRecorder;
        var entityRecorder = r.EntityRecorder;
        var skyRecorder = r.SkyRecorder;
        var uniformWriter = r.UniformWriter;
        var uiBuffers = r.UiBuffers;
        var layerPool = r.LayerPool;
        var spriteBindGroups = r.SpriteBindGroups;
        var uiRanges = r.UiRanges;
        var frustumPlanes = r.FrustumPlanes;
        var worldText = r.WorldText;
        var cloudColour = r.CloudColour;
        var cloudOit = r.CloudOit;
        var itemOit = r.ItemOit;
        var oit = r.OitTargets;
        var itemAtlasGpu = r.ItemAtlas;
        var itemOitRan = false;

            // Sun/moon/stars are built relative to the initial camera; keep them centred as the player moves.
            var celestialDelta = eye - request.CameraPosition;

            // Sample the cloud draw once: the OIT stages need its vertices before the main transparency passes, and the
            // non-OIT path reuses the same draw later.
            var cloudDrawFrame = skipClouds ? null : cloudsPerFrame?.Invoke();
            if (cloudOit is not null && cloudDrawFrame is { } cloudForOit && cloudForOit.CloudFaces is { } cloudFaces)
            {
                cloudOit.Update(cloudFaces, cloudForOit.CloudFaceCount, cloudForOit.CloudOrigin, cloudForOit.Tint, cloudForOit.CloudFogEnd, cloudForOit.CloudEye);
            }

            Cpu(FrameCpuTimings.CloudsStage);

            var encoder = backend.CreateCommandEncoder();
            var frameColour = clearColourPerFrame?.Invoke() ?? colour;
            var frameAttachment = skipClear
                ? ColorAttachmentDesc.Keep(frameView)
                : ColorAttachmentDesc.Clear(frameView, frameColour);
            var depthAttachment = skipClear
                ? DepthAttachmentDesc.Keep(depthViewHandle)
                : DepthAttachmentDesc.Clear(depthViewHandle, 0f);
            var pass = backend.BeginRenderPass(encoder, [frameAttachment], depthAttachment);

            // Camera/light/fog uniforms are frame setup, not pass ordering. The scene pass sequence itself comes from
            // WorldRenderPipeline so the windowed path and the offscreen path share one execution order.
            backend.SetPipeline(pass, pipeline);
            // Rewriting 64 bytes is the whole cost of moving the camera; the staging buffers are reused.
            var cameraMs = (System.Diagnostics.Stopwatch.GetTimestamp() - cameraStarted) * 1000d
                / System.Diagnostics.Stopwatch.Frequency;

            // Per-frame lightmap parameters: day/night changes only this block, never the mesh.
            var frameLightmap = lightmapPerFrame?.Invoke() ?? currentRequest.Lightmap;
            var glintTime = currentRequest.GlintTime != 0f
                ? currentRequest.GlintTime
                : (float)(System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency);

            // Per-frame fog colour (time/weather driven); the range stays as configured.
            var frameFogColour = fogPerFrame?.Invoke() ?? currentRequest.FogColour;
            var frameFogRange = fogRangePerFrame?.Invoke() ?? AtmosphericFog.Range(currentRequest.RainLevel);

            uniformWriter.Write(
                cameraBlock,
                eye,
                target,
                request.FieldOfViewDegrees,
                (int)frameWidth,
                (int)frameHeight,
                frameLightmap with { Padding = glintTime },
                frameFogColour,
                currentRequest.Diagnostics?.FogStart ?? 64f,
                currentRequest.Diagnostics?.FogEnd ?? 160f,
                // Always on; the host pulls it in as rain rises, so rain closes the view rather than only tinting.
                frameFogRange.Start,
                frameFogRange.End);

            backend.SetBindGroup(pass, 0, bindGroup);

            var drawsWorld = arena is { ChunkCount: > 0 } || arenaCutout is { ChunkCount: > 0 };
            if (drawsWorld && !disableCulling)
            {
                var viewProjection = CameraMath.BuildViewProjection(
                    eye,
                    target,
                    request.FieldOfViewDegrees,
                    (int)frameWidth,
                    (int)frameHeight);
                FrustumPlanes.Extract(viewProjection, frustumPlanes);
            }

            // The reference draws every section from the same per section buffers; it has no separate static copy of the
            // world. Drawing the preloaded combined mesh next to the arena kept the first few chunks on a second path with
            // its own colour, so the static copy is used only until the arena holds anything.
            var useStaticWorld = arena is null || arena.ChunkCount == 0;
            Cpu(FrameCpuTimings.SceneOpenStage);

            void DrawScenePass(WorldRenderPassKind kind)
            {
                switch (kind)
                {
                    case WorldRenderPassKind.Sky:
                    {
                        skyRecorder.Record(
                            pass, skipSky, skipCelestial, currentRequest.SkipGradientSky, celestialDelta);
                        break;
                    }

                    case WorldRenderPassKind.TerrainOpaque:
                    {
                        if (terrainRecorder.RecordOpaque(
                            pass, skipTerrain, disableCulling, frustumPlanes, visibleSections))
                        {
                            Cpu(FrameCpuTimings.SceneTerrainStage);
                        }

                        // Clouds are drawn after the transparent terrain (below), matching the reference.
                        break;
                    }

                    case WorldRenderPassKind.TerrainCutout:
                    {
                        terrainRecorder.RecordCutout(
                            pass, skipTerrain, disableCulling, frustumPlanes, visibleSections);
                        break;
                    }

                    case WorldRenderPassKind.Entities:
                    {
                        Cpu(FrameCpuTimings.SceneEntitiesStage);
                        entityRecorder.Record(
                            pass, dynamicIndexCount, entityLayerDrawn, skipEntities, useStaticWorld, currentRequest);
                        break;
                    }
                }
            }

            WorldRenderPipeline.RecordScenePasses(DrawScenePass);

            // The main pass ends here: sky, opaque terrain and the cutout layer (which shares the opaque depth) have all
            // been drawn. Translucent terrain and the interface each get their own pass so the order independent resolve
            // can run between them.
            // The scene pass declares locals the rest of the frame uses, so it cannot sit inside a try block; the
            // body between begin and end has no return and no throw, which is what keeps the single end safe.
            backend.EndRenderPass(pass);
            Cpu(FrameCpuTimings.SceneStage);

            if (oit.CloudDepthTargetsCreated)
            {
                // Blit the opaque main depth into the cloud depth attachment (reference BLIT_DEPTH_DURING_DEPTH_BOUNDS).
                // The first pass recorded through the seam: it needs no helper that still takes a native pass, so it
                // proves the whole path - encoder adoption, attachments, bindings and the single end - on its own.
                var blitPass = backend.BeginRenderPass(
                    encoder,
                    [],
                    DepthAttachmentDesc.Keep(oit.CloudDepth));
                try
                {
                    backend.SetPipeline(blitPass, blitDepthPipeline);
                    backend.SetBindGroup(blitPass, 0, oit.BlitDepthGroup(depthViewHandle));
                    backend.Draw(blitPass, 3, 1, 0, 0);
                }
                finally
                {
                    backend.EndRenderPass(blitPass);
                }
            }

            // Reference transparency (docs/specs/render/oit-reference.md): depth bounds, a culling pass, wavelet
            // transmittance, accumulation and a composite that writes depth. The three stages redraw the same
            // translucent geometry, so no sort is needed at all.
            // Resident is not the same as drawn: asking the arena how much translucent geometry it holds keeps
            // the whole group alive while the camera is somewhere none of it can be seen (T-833).
            var needsOit = (arenaTranslucent is not null && arenaTranslucent.AnyVisible(frustumPlanes, visibleSections))
                || currentRequest.TranslucentIndices is { Length: > 0 }
                || cloudOit is not null;
            if (needsOit && !oit.Created)
            {
                oit.Create(frameWidth, frameHeight);
            }

            if (oit.Created && needsOit && !skipOit)
            {
                var refDepthBoundsView = oit.DepthBounds;
                var refDepthBoundsCulledView = oit.DepthBoundsCulled;
                var refTransmittance0View = oit.Transmittance0;
                var refTransmittance1View = oit.Transmittance1;
                var refAccumulateView = oit.Accumulate;
                var refCullBindGroup = oit.Cull;
                var refTransmittanceBindGroup = oit.TransmittanceGroup;
                var refAccumulateBindGroup = oit.AccumulateGroup;
                var refCompositeBindGroup = oit.CompositeGroup;
                var cloudDepthViewHandle = oit.CloudDepth;
                var cloudDepthTargetsCreated = oit.CloudDepthTargetsCreated;
                var hasRefStatic = useStaticWorld
                    && currentRequest.TranslucentVertices is { Length: > 0 }
                    && currentRequest.TranslucentIndices is { Length: > 0 };
                BufferHandle refStaticVertexBuffer = default;
                BufferHandle refStaticIndexBuffer = default;
                var refStaticIndexCount = 0u;

                if (hasRefStatic)
                {
                    var staticVertices = currentRequest.TranslucentVertices!;
                    var staticIndices = currentRequest.TranslucentIndices!;
                    refStaticVertexBuffer = backend.CreateBufferInit(
                        new BufferDesc((ulong)staticVertices.Length * sizeof(float), BufferUsages.Vertex),
                        System.Runtime.InteropServices.MemoryMarshal.AsBytes(staticVertices.AsSpan()));
                    refStaticIndexBuffer = backend.CreateBufferInit(
                        new BufferDesc((ulong)staticIndices.Length * sizeof(uint), BufferUsages.Index),
                        System.Runtime.InteropServices.MemoryMarshal.AsBytes(staticIndices.AsSpan()));
                    refStaticIndexCount = (uint)staticIndices.Length;
                }

                void DrawReferenceGeometry(RenderPassHandle p)
                {
                    if (skipTranslucent)
                    {
                        // Measurement only (MC_SKIP_TRANSLUCENT): the layers keep uploading, only the draw is dropped.
                        return;
                    }

                    if (arenaTranslucent is not null && arenaTranslucent.ChunkCount > 0)
                    {
                        // Order independent by construction (docs/specs/render/oit-reference.md 6: additive accumulation
                        // and transmittance are commutative), so the paged arena draws page major and binds once per page.
                        // Culled like the other two layers: the box test treats a box that merely intersects the frustum
                        // as visible, so a column straddling the edge is still drawn (T-822).
                        arenaTranslucent.Draw(p, frustumPlanes);
                    }

                    if (hasRefStatic && refStaticIndexCount > 0)
                    {
                        backend.SetVertexBuffer(p, 0, refStaticVertexBuffer);
                        backend.SetIndexBuffer(p, refStaticIndexBuffer, IndexFormat.Uint32);
                        backend.DrawIndexed(p, refStaticIndexCount, 1, 0, 0, 0);
                    }
                }

                // Translucent item geometry joins the same reference OIT stages (the reference binds OIT_ITEM to
                // RenderTypes.ITEM_TRANSLUCENT). The OIT pipelines already declare the entity vertex layout and the shared
                // pipeline layout, so only the atlas bind group differs from the terrain draw.
                void DrawItemOitGeometry(RenderPassHandle p)
                {
                    var atlasBound = false;
                    for (var itemLayer = 0; itemLayer < entityLayerDrawn; itemLayer++)
                    {
                        if (layerPool.Kind(itemLayer) != EntityGeometryLayerKind.ItemTranslucent ||
                            layerPool.IndexCount(itemLayer) == 0)
                        {
                            continue;
                        }

                        if (!atlasBound)
                        {
                            backend.SetBindGroup(p, 0, itemAtlasGpu.BindGroup);
                            atlasBound = true;
                        }

                        backend.SetVertexBuffer(p, 0, layerPool.Vertex(itemLayer), 0, layerPool.VertexCapacity(itemLayer));
                        backend.SetIndexBuffer(
                            p,
                            layerPool.Index(itemLayer),
                            IndexFormat.Uint32,
                            0,
                            (ulong)layerPool.IndexCount(itemLayer) * sizeof(uint));
                        backend.DrawIndexed(p, layerPool.IndexCount(itemLayer), 1, 0, 0, 1);
                    }
                }

                // Terrain transparency (glass/water) tests depth against the opaque main depth only; cloud depth writes
                // go to a separate target below so they cannot cull translucent terrain behind a cloud.
                var depthLoadStore = DepthAttachmentDesc.Keep(depthViewHandle);

                // Cloud OIT stages use the independent cloud depth target (blitted from the opaque depth above), so the
                // cloud depth-bounds write culls only the cloud's own interior faces without hiding terrain behind it.
                var cloudOitDepth = cloudDepthTargetsCreated
                    ? DepthAttachmentDesc.Keep(cloudDepthViewHandle)
                    : depthLoadStore;

                // The item transparency set has its own four full resolution targets; with no translucent item geometry every
                // one of its passes would only clear and then discard, so the whole set is skipped instead (T-799).
                var itemOitGeometryPresent = false;
                for (var itemLayer = 0; itemLayer < entityLayerDrawn; itemLayer++)
                {
                    if (layerPool.Kind(itemLayer) == EntityGeometryLayerKind.ItemTranslucent &&
                        layerPool.IndexCount(itemLayer) > 0)
                    {
                        itemOitGeometryPresent = true;
                        break;
                    }
                }

                ReferenceOitPasses.Record(new ReferenceOitPasses.Parameters
                {
                    Backend = backend,
                    Encoder = encoder,
                    Frame = frameView,
                    Depth = depthLoadStore,
                    CloudDepth = cloudOitDepth,
                    Bounds = refDepthBoundsView,
                    BoundsCulled = refDepthBoundsCulledView,
                    Transmittance0 = refTransmittance0View,
                    Transmittance1 = refTransmittance1View,
                    Accumulate = refAccumulateView,
                    BoundsPipeline = refDepthBoundsPipeline,
                    CullPipeline = refCullPipeline,
                    TransmittancePipeline = refTransmittancePipeline,
                    AccumulatePipeline = refAccumulatePipeline,
                    CompositePipeline = refCompositePipeline,
                    TerrainAtlas = bindGroup,
                    Cull = refCullBindGroup,
                    TransmittanceGroup = refTransmittanceBindGroup,
                    AccumulateGroup = refAccumulateBindGroup,
                    CompositeGroup = refCompositeBindGroup,
                    Clouds = skipClouds ? null : cloudOit,
                    CloudBoundsPipeline = cloudDepthBoundsPipeline,
                    CloudTransmittancePipeline = cloudTransmittancePipeline,
                    CloudAccumulatePipeline = cloudAccumulatePipeline,
                    Items = itemOit,
                    ItemAtlas = itemAtlasGpu.BindGroup,
                    DrawTerrain = DrawReferenceGeometry,
                    DrawItems = DrawItemOitGeometry,
                    MaxStages = currentRequest.Diagnostics?.OitStages ?? 0,
                    ItemsHasGeometry = itemOitGeometryPresent
                });

                if (itemOitGeometryPresent)
                {
                    itemOitRan = true;
                }

                if (hasRefStatic)
                {
                    backend.Destroy(refStaticVertexBuffer);
                    backend.Destroy(refStaticIndexBuffer);
                }
            }

            Cpu(FrameCpuTimings.OitStage);
            // (The weighted-blended OIT path that used to live here was shelved for its visual quality and is no longer
            // compiled; the reference OIT above is the only transparency path.)

            // Text, clouds and the interface share one pass: same colour target, same depth attachment, same drawing
            // order. What costs is the pass object itself (encoder validation and driver setup), not the draws inside it
            // (T-804).
            var uiPass = backend.BeginRenderPass(
                encoder,
                [ColorAttachmentDesc.Keep(frameView)],
                DepthAttachmentDesc.Keep(depthViewHandle));
            try
            {
            if (worldText is not null)
            {
                worldText.SetDraws(worldTextPerFrame?.Invoke() ?? []);
                worldText.Draw(backend, uiPass);

                Cpu(FrameCpuTimings.TextStage);
            }
            // Clouds come after the opaque and transparent terrain, with depth testing so terrain and the cloud layer
            // occlude each other correctly (the reference draws them in executeClassicTransparency).
            if (cloudColour is not null && cloudOit is null && !skipClouds)
            {
                if (cloudDrawFrame is { } cloudDraw)
                {
                    cloudColour.SetDraws([cloudDraw]);
                }

                cloudColour.Draw(backend, uiPass);
            }

            Cpu(FrameCpuTimings.CloudsStage);

            // Pass 4: the interface draws last, over the resolved frame, in its own pass so it never participates in the
            // world depth test or the transparency resolve.
            if (!skipInterface)
            {

            if (uiBuffers.IndexCount > 0 && uiRanges.Count > 0)
            {
                backend.SetVertexBuffer(uiPass, 0, uiBuffers.Vertex, 0, uiBuffers.VertexCapacity);
                backend.SetIndexBuffer(uiPass, uiBuffers.Index, IndexFormat.Uint32, 0, (ulong)uiBuffers.IndexCount * sizeof(uint));

                var boundMaterial = (UiMaterial?)null;

                // A sprite range also depends on which texture it binds, so the index is part of what decides a rebind.
                var boundSprite = -2;

                // One draw per scissor range, which is what keeps a screen of many widgets down to a few draws.
                // The pipeline and bind group only change when the material does, so panels then text cost two
                // bindings for the whole screen rather than one per range.
                foreach (var range in uiRanges)
                {
                    if (boundMaterial != range.Material || boundSprite != range.SpriteIndex)
                    {
                        boundMaterial = range.Material;
                        boundSprite = range.SpriteIndex;

                        if (range.Material == UiMaterial.Text)
                        {
                            backend.SetPipeline(uiPass, uiTextPipeline);
                            backend.SetBindGroup(uiPass, 0, uiTextBindGroup);
                        }
                        else if (range.Material == UiMaterial.Sprite &&
                                 range.SpriteIndex >= 0 &&
                                 range.SpriteIndex < spriteBindGroups.Count)
                        {
                            backend.SetPipeline(uiPass, uiSpritePipeline);
                            backend.SetBindGroup(uiPass, 0, spriteBindGroups[range.SpriteIndex]);
                        }
                        else
                        {
                            backend.SetPipeline(uiPass, uiPipeline);
                            backend.SetBindGroup(uiPass, 0, uiBindGroup);
                        }
                    }

                    var left = Math.Max(0f, range.MinX * uiScale);
                    var top = Math.Max(0f, range.MinY * uiScale);
                    var right = Math.Min(frameWidth, range.MaxX * uiScale);
                    var bottom = Math.Min(frameHeight, range.MaxY * uiScale);

                    if (right <= left || bottom <= top)
                    {
                        continue;
                    }

                    backend.SetScissorRect(
                        uiPass,
                        (uint)left,
                        (uint)top,
                        (uint)(right - left),
                        (uint)(bottom - top));

                    backend.DrawIndexed(uiPass, (uint)range.IndexCount, 1, (uint)range.FirstIndex, 0, 1);
                }
            }

            }
            }
            finally
            {
                backend.EndRenderPass(uiPass);
            }

            Cpu(FrameCpuTimings.UiStage);

        return new Result(encoder, cameraMs, itemOitRan);
    }
}
