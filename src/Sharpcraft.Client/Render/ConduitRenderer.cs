// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Reference;
using Sharpcraft.Common.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Draws the inactive conduit shell from <see cref="ConduitModelDefinition"/>. Active conduits keep
/// the same static shell in this slice; the active cage, wind and eye branches are not implemented.
/// </summary>
/// <remarks>
/// Source: <c>ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/ConduitRenderer.java</c>
/// (DEFAULT_TRANSFORMATION line 43, SHELL_TEXTURE line 45, inactive submit lines 105-119). The
/// inactive branch translates by (0.5, 0.5, 0.5), rotates around Y by the conduit's
/// <c>activeRotation</c>, and submits the shell with <c>entitySolid</c>. The shell is baked in
/// model space so the renderer applies that translation literally, with no baseline offset.
/// </remarks>
[ReferenceClass("net.minecraft.client.renderer.blockentity.ConduitRenderer", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/ConduitRenderer.java", Kind = ReferenceKind.Adapter, Status = ReferenceStatus.Partial)]
[Sharpcraft.Common.Diagnostics.Simplification("SIM-BE-CONDUIT-ACTIVE", "T-51", Note = "Active conduits reuse the static shell; active cage, wind, eye, isHunting, tickCount and activeRotation (fixed zero) are not wired.")]
public sealed class ConduitRenderer : IBlockEntityRenderer
{
    private readonly EntityModelMesher _mesher = new();
    private readonly DynamicGeometry _geometry = new();

    /// <inheritdoc />
    public bool Handles(int typeId) => VanillaBlockEntityTypes.IsConduit(typeId);

    /// <inheritdoc />
    public BlockEntityRenderMode RenderMode => BlockEntityRenderMode.Solid;

    /// <inheritdoc />
    public int LastEntityCount { get; private set; }

    /// <inheritdoc />
    public DynamicGeometry Build(ReadOnlySpan<BlockEntityRenderState> entities, ItemAtlas atlas)
    {
        ArgumentNullException.ThrowIfNull(atlas);
        _geometry.Clear();
        LastEntityCount = 0;

        for (var index = 0; index < entities.Length; index++)
        {
            var entity = entities[index];
            if (!Handles(entity.TypeId))
            {
                continue;
            }

            // The reference inactive branch is translate(0.5,0.5,0.5) then rotateDegrees(YP,
            // activeRotation). The raw model already matches the ModelProbe sample in the source
            // y-down frame; adding a renderer-side X/Y flip here moves the first shell vertex from
            // (x=max,y=min,z=max) to the opposite corner, so the sample transform is the literal
            // source translate/rotation only.
            var transform =
                Matrix4x4.CreateRotationY(entity.ConduitRotation * (MathF.PI / 180f))
                * Matrix4x4.CreateTranslation(entity.X + 0.5f, entity.Y + 0.5f, entity.Z + 0.5f);
            var state = new EntityRenderState(
                Id: index,
                TypeId: entity.TypeId,
                X: 0f,
                Y: 0f,
                Z: 0f,
                Yaw: 180f,
                Pitch: 0f);
            var shell = _mesher.BuildModel(
                ConduitModelDefinition.Shell,
                state,
                atlas.Get(ConduitModelDefinition.TexturePath)!,
                ConduitModelDefinition.TextureWidth,
                ConduitModelDefinition.TextureHeight,
                modelScale: 1f);
            if (shell.IndexCount > 0)
            {
                AppendTransformed(shell, transform);
                LastEntityCount++;
            }
        }

        return _geometry;
    }

    private void AppendTransformed(DynamicGeometry source, Matrix4x4 transform)
    {
        if (source.VertexFloats == 0)
        {
            return;
        }

        var vertexBase = (uint)(_geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
        var neededVertexFloats = _geometry.VertexFloats + source.VertexFloats;
        var neededIndexCount = _geometry.IndexCount + source.IndexCount;
        _geometry.Reserve(neededVertexFloats, neededIndexCount);

        for (var vertex = 0; vertex < source.VertexFloats; vertex += ChunkMesher.FloatsPerVertex)
        {
            var position = Vector3.Transform(
                new Vector3(source.Vertices[vertex], source.Vertices[vertex + 1], source.Vertices[vertex + 2]),
                transform);
            _geometry.Vertices[_geometry.VertexFloats + vertex] = position.X;
            _geometry.Vertices[_geometry.VertexFloats + vertex + 1] = position.Y;
            _geometry.Vertices[_geometry.VertexFloats + vertex + 2] = position.Z;
            for (var field = 3; field < ChunkMesher.FloatsPerVertex; field++)
            {
                _geometry.Vertices[_geometry.VertexFloats + vertex + field] = source.Vertices[vertex + field];
            }
        }

        for (var index = 0; index < source.IndexCount; index++)
        {
            _geometry.Indices[_geometry.IndexCount + index] = source.Indices[index] + vertexBase;
        }

        _geometry.VertexFloats = neededVertexFloats;
        _geometry.IndexCount = neededIndexCount;
    }
}
