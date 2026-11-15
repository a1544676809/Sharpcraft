// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Reference;
using Sharpcraft.Common.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Builds copper golem statue geometry through the generic <see cref="EntityModelMesher.BuildModel"/>
/// block entity path. The standing model is used for every pose in this slice; the reference selects
/// separate model layers for sitting, running and star poses.
/// </summary>
/// <remarks>
/// Source: <c>ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/CopperGolemStatueBlockRenderer.java</c>
/// (<c>createModelTransformation</c>, <c>submit</c>) and
/// <c>ref://mc-26.3-analysis/src/net/minecraft/client/model/object/statue/CopperGolemStatueModel.java</c>.
/// The model is <see cref="CopperGolemModelDefinition.Definition"/> with a 64 by 64 texture. Facing uses
/// the reference Direction ordinal encoding (north 2, south 3, west 4, east 5); the model translation
/// places its origin at the block centre, <c>(0.5, 0, 0.5)</c>, before the Y rotation.
/// </remarks>
[ReferenceClass("net.minecraft.client.renderer.blockentity.CopperGolemStatueBlockRenderer", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/CopperGolemStatueBlockRenderer.java", Status = ReferenceStatus.Partial, Note = "Standing model only; the other three pose layers are not implemented.")]
[Sharpcraft.Common.Diagnostics.Simplification("SIM-BE-COPPER-GOLEM-POSES", "T-47", Note = "Only the standing CopperGolem model is used; SITTING/RUNNING/STAR are rendered with the standing model until their model layers are ported.")]
public sealed class CopperGolemStatueMesher
{
    /// <summary>Model texture width, in pixels.</summary>
    public const int TextureWidth = 64;

    /// <summary>Model texture height, in pixels.</summary>
    public const int TextureHeight = 64;

    /// <summary>Weathering stage texture indices, in reference order.</summary>
    public const int Unaffected = 0;
    public const int Exposed = 1;
    public const int Weathered = 2;
    public const int Oxidized = 3;

    private readonly EntityModelMesher _mesher = new();
    private readonly DynamicGeometry _geometry = new();
    private readonly List<EntityRenderState>[] _states = [new(), new(), new(), new()];

    /// <summary>Entities drawn by the last call.</summary>
    public int LastEntityCount { get; private set; }

    /// <summary>Builds copper golem statue geometry from the block entity atlas.</summary>
    /// <param name="entities">Block entities in world space.</param>
    /// <param name="atlas">Atlas that holds the four copper golem body textures.</param>
    public DynamicGeometry Build(ReadOnlySpan<BlockEntityRenderState> entities, ItemAtlas atlas) =>
        Build(entities, null, atlas);

    /// <summary>Builds copper golem statue geometry with explicit per-oxidation sprites.</summary>
    /// <param name="entities">Block entities in world space.</param>
    /// <param name="sprites">Four sprites indexed by weathering stage.</param>
    public DynamicGeometry Build(ReadOnlySpan<BlockEntityRenderState> entities, SpriteRect[] sprites) =>
        Build(entities, sprites, null);

    private DynamicGeometry Build(
        ReadOnlySpan<BlockEntityRenderState> entities,
        SpriteRect[]? fallbackSprites,
        ItemAtlas? atlas)
    {
        _geometry.Clear();
        LastEntityCount = 0;
        for (var stage = 0; stage < _states.Length; stage++)
        {
            _states[stage].Clear();
        }

        for (var index = 0; index < entities.Length; index++)
        {
            var entity = entities[index];
            if (entity.TypeId != VanillaBlockEntityTypes.CopperGolemStatue)
            {
                continue;
            }

            var stage = entity.ChestMaterial <= Oxidized ? entity.ChestMaterial : Unaffected;
            _states[stage].Add(new EntityRenderState(
                Id: index,
                TypeId: entity.TypeId,
                X: entity.X + 0.5f,
                Y: entity.Y,
                Z: entity.Z + 0.5f,
                Yaw: 180f - FacingDegrees(entity.Facing),
                Pitch: 0f));
        }

        var sprites = fallbackSprites;
        if (sprites is null && atlas is null)
        {
            throw new InvalidOperationException("either sprites or an atlas are required");
        }

        for (var stage = 0; stage < _states.Length; stage++)
        {
            if (_states[stage].Count == 0)
            {
                continue;
            }

            var sprite = sprites is not null
                ? sprites[stage]
                : ChestAtlas.SpriteForCopperGolemStatue(atlas!, stage);
            if (sprite is null)
            {
                continue;
            }

            var geometry = _mesher.BuildModel(
                CopperGolemModelDefinition.Definition,
                System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_states[stage]),
                sprite,
                TextureWidth,
                TextureHeight,
                modelScale: 1f);
            AppendGeometry(_geometry, geometry);
            LastEntityCount += _states[stage].Count;
        }

        return _geometry;
    }

    /// <summary>
    /// Yaw the reference <c>createModelTransformation</c> applies for a facing, in degrees. The
    /// input is the reference Direction ordinal; the reference rotates by
    /// <c>-facing.getOpposite().toYRot()</c>. This includes the vertical ordinals for completeness,
    /// though the statue block's FACING property is horizontal.
    /// Ref: <c>ref://mc-26.3-analysis/src/net/minecraft/core/Direction.java:33-38,66-77,311-312</c>
    /// and <c>ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/CopperGolemStatueBlockRenderer.java:85-87</c>.
    /// </summary>
    public static float FacingDegrees(byte facing) => facing switch
    {
        0 => -270f,
        1 => -270f,
        2 => 0f,
        3 => -180f,
        4 => -270f,
        5 => -90f,
        _ => -270f
    };

    private static void AppendGeometry(DynamicGeometry target, DynamicGeometry source)
    {
        if (source.VertexFloats == 0)
        {
            return;
        }

        var vertexFloats = target.VertexFloats + source.VertexFloats;
        var indexCount = target.IndexCount + source.IndexCount;
        target.Reserve(vertexFloats, indexCount);

        var vertexBase = (uint)(target.VertexFloats / ChunkMesher.FloatsPerVertex);
        Array.Copy(source.Vertices, 0, target.Vertices, target.VertexFloats, source.VertexFloats);
        for (var i = 0; i < source.IndexCount; i++)
        {
            target.Indices[target.IndexCount + i] = source.Indices[i] + vertexBase;
        }

        target.VertexFloats = vertexFloats;
        target.IndexCount = indexCount;
    }
}
