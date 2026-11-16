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
/// Builds copper golem statue geometry through the generic <see cref="EntityModelMesher.BuildModel"/>
/// block entity path. One model definition is selected per reference <c>CopperGolemStatueBlock.Pose</c>;
/// all four share the statue root pose and the four oxidation sprites.
/// </summary>
/// <remarks>
/// Source: <c>ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/CopperGolemStatueBlockRenderer.java</c>
/// (<c>createModelTransformation</c>, <c>submit</c>), <c>CopperGolemStatueModel.setupAnim</c>
/// (<c>root.y = 0</c>, <c>root.zRot = π</c>) and the four <c>CopperGolemModel</c> layers in
/// <c>ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/golem/CopperGolemModel.java</c>.
/// Facing uses the reference Direction ordinal encoding (north 2, south 3, west 4, east 5); the
/// model translation places its origin at the block centre, <c>(0.5, 0, 0.5)</c>, before the Y rotation.
/// </remarks>
[ReferenceClass("net.minecraft.client.renderer.blockentity.CopperGolemStatueBlockRenderer", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/CopperGolemStatueBlockRenderer.java", Status = ReferenceStatus.Partial, Note = "All four pose layers and four oxidation sprites; breaking overlay not wired.")]
public sealed class CopperGolemStatueMesher
{
    /// <summary>Number of reference statue poses.</summary>
    public const int PoseCount = 4;

    /// <summary>Number of oxidation stages.</summary>
    public const int OxidationCount = 4;

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
    private readonly List<EntityRenderState>[] _states = new List<EntityRenderState>[PoseCount * OxidationCount];

    /// <summary>
    /// CopperGolemStatueModel wraps every CopperGolem layer with a root pose: <c>root.y = 0</c> and
    /// <c>root.zRot = π</c>. The project definition already stores the y-up baked coordinates, so the
    /// root pose is represented as an <see cref="EntityPose"/> row for the compiled root group.
    /// </summary>
    private static readonly EntityPose StatuePose = CreateStatuePose();

    /// <summary>Creates the fixed pose/oxidation state buckets.</summary>
    public CopperGolemStatueMesher()
    {
        for (var slot = 0; slot < _states.Length; slot++)
        {
            _states[slot] = [];
        }
    }

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
        for (var slot = 0; slot < _states.Length; slot++)
        {
            _states[slot].Clear();
        }

        for (var index = 0; index < entities.Length; index++)
        {
            var entity = entities[index];
            if (entity.TypeId != VanillaBlockEntityTypes.CopperGolemStatue)
            {
                continue;
            }

            var pose = (uint)entity.Pose < PoseCount ? entity.Pose : CopperGolemPose.Standing;
            var stage = entity.ChestMaterial <= Oxidized ? entity.ChestMaterial : Unaffected;
            _states[((int)pose * OxidationCount) + stage].Add(new EntityRenderState(
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

        for (var slot = 0; slot < _states.Length; slot++)
        {
            if (_states[slot].Count == 0)
            {
                continue;
            }

            var pose = slot / OxidationCount;
            var stage = slot % OxidationCount;
            var sprite = sprites is not null
                ? sprites[stage]
                : ChestAtlas.SpriteForCopperGolemStatue(atlas!, stage);
            if (sprite is null)
            {
                continue;
            }

            var geometry = _mesher.BuildModel(
                CopperGolemStatueModelDefinition.ByPose[pose],
                System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_states[slot]),
                sprite,
                TextureWidth,
                TextureHeight,
                pose: StatuePose,
                modelScale: 1f);
            AppendGeometry(_geometry, geometry);
            LastEntityCount += _states[slot].Count;
        }

        return _geometry;
    }

    private static EntityPose CreateStatuePose()
    {
        var pose = new EntityPose();
        // The reference layer's root pivot is 24 pixels; CopperGolemStatueModel sets root.y = 0,
        // which in the y-up baked representation is a +24 pixel root translation before zRot = π.
        pose.Set("root", new PoseOffset(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, -MathF.PI), Vector3.Zero));
        return pose;
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
