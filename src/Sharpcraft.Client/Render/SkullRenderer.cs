// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;
using Sharpcraft.Common.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Draws static block/wall skulls for all seven supported families: skeleton, wither skeleton, zombie,
/// creeper, Player (default slim Steve), Piglin and Dragon. Each head model is generated once by
/// <see cref="SkullModelDefinition"/>; this renderer applies only the outer ground/wall pose from the
/// reference.
/// </summary>
/// <remarks>
/// Source: <c>ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/SkullBlockRenderer.java</c>
/// (<c>createWallTransformation</c> lines 154-162, <c>createGroundTransformation</c> lines 164-168).
/// Ground: translation (0.5,0,0.5), Y rotation -<see cref="RotationDegrees"/>, scale (-1,-1,1).
/// Wall: translation (0.5 - stepX*0.25, 0.25, 0.5 - stepZ*0.25), Y rotation
/// -facing.getOpposite().toYRot(), scale (-1,-1,1).
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.blockentity.SkullBlockRenderer", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/SkullBlockRenderer.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
[Sharpcraft.Common.Diagnostics.Simplification("SIM-BE-SKULL-VARIANTS", "T-47", Note = "All known skull head variants (skeleton, wither skeleton, zombie, creeper, player, piglin and dragon) are handled; no variant remains skipped.")]
[Sharpcraft.Common.Diagnostics.Simplification("SIM-BE-SKULL-PLAYER-SKIN", "T-47", Note = "Player heads use the default slim Steve skin; profile skin cache and custom skins are not wired.")]
[Sharpcraft.Common.Diagnostics.Simplification("SIM-BE-SKULL-DRAGON-ANIM", "T-47", Note = "Dragon jaw animation (setupAnim jaw xRot) is not applied; the static jaw pose is used.")]
[Sharpcraft.Common.Diagnostics.Simplification("SIM-BE-SKULL-ZOFFSET", "T-47", Note = "entityCutoutZOffset depth offset is not applied; the dependency is Issue #45.")]
[Sharpcraft.Common.Diagnostics.Simplification("SIM-BE-SKULL-ANIM", "T-47", Note = "Static head model; skull animationProgress / setupAnim rotations are not wired.")]
public sealed class SkullRenderer : IBlockEntityRenderer
{
    private readonly EntityModelMesher _mesher = new();
    private readonly DynamicGeometry _geometry = new();

    /// <inheritdoc />
    public bool Handles(int typeId) => VanillaBlockEntityTypes.IsSkull(typeId);

    /// <inheritdoc />
    public int LastEntityCount { get; private set; }

    /// <inheritdoc />
    public DynamicGeometry Build(ReadOnlySpan<BlockEntityRenderState> entities, ItemAtlas atlas)
    {
        _geometry.Clear();
        // Dragon is the worst case: seven boxes = 168 vertices / 252 indices per entity.
        // Reserve the whole batch before appending: DynamicGeometry.Reserve does not copy existing
        // content, so a mid-loop growth would drop every previously written skull.
        _geometry.Reserve(
            entities.Length * 168 * ChunkMesher.FloatsPerVertex,
            entities.Length * 252);
        LastEntityCount = 0;

        for (var index = 0; index < entities.Length; index++)
        {
            var entity = entities[index];
            if (!Handles(entity.TypeId) || entity.SkullType == SkullType.None)
            {
                continue;
            }

            var sprite = ChestAtlas.SpriteForSkull(atlas, entity.SkullType);
            if (sprite is null)
            {
                continue;
            }

            var modelState = new EntityRenderState(
                Id: index,
                TypeId: entity.TypeId,
                X: 0f,
                Y: 0f,
                Z: 0f,
                Yaw: 180f,
                Pitch: 0f);
            var definition = SkullModelDefinition.DefinitionFor(entity.SkullType);
            var built = _mesher.BuildModel(
                definition,
                modelState,
                sprite,
                definition.TextureWidth,
                definition.TextureHeight,
                modelScale: 1f);
            var start = _geometry.VertexFloats;
            AppendGeometry(_geometry, built);
            ApplyOuterPose(start, _geometry.VertexFloats, entity);
            LastEntityCount++;
        }

        return _geometry;
    }

    /// <summary>
    /// Applies the reference ground/wall PoseStack to the BuildModel output. The source pose is
    /// T * Ry * S in column-vector order, so a vertex is scaled, rotated Y, then translated.
    /// </summary>
    private void ApplyOuterPose(int startFloats, int endFloats, BlockEntityRenderState entity)
    {
        var radians = MathF.PI / 180f;
        Vector3 translation;
        float yRot;
        if (entity.SkullWall)
        {
            translation = new Vector3(
                entity.X + 0.5f - StepX(entity.SkullFacing) * 0.25f,
                entity.Y + 0.25f,
                entity.Z + 0.5f - StepZ(entity.SkullFacing) * 0.25f);
            yRot = WallYRot(entity.SkullFacing);
        }
        else
        {
            translation = new Vector3(entity.X + 0.5f, entity.Y, entity.Z + 0.5f);
            yRot = -RotationDegrees(entity.SkullRotation);
        }

        var scale = Matrix4x4.CreateScale(-1f, -1f, 1f);
        var rotation = Matrix4x4.CreateRotationY(yRot * radians);
        for (var vertex = startFloats; vertex < endFloats; vertex += ChunkMesher.FloatsPerVertex)
        {
            var position = new Vector3(_geometry.Vertices[vertex], _geometry.Vertices[vertex + 1], _geometry.Vertices[vertex + 2]);
            position = Vector3.Transform(position, scale);
            position = Vector3.Transform(position, rotation);
            position += translation;
            _geometry.Vertices[vertex] = position.X;
            _geometry.Vertices[vertex + 1] = position.Y;
            _geometry.Vertices[vertex + 2] = position.Z;
        }
    }

    /// <summary>
    /// <c>RotationSegment.convertToDegrees</c> with the 4-bit segmented-angle precision:
    /// segment * 22.5 degrees, with the upper half normalised to negative degrees.
    /// Ref: <c>ref://mc-26.3-analysis/src/net/minecraft/world/level/block/state/properties/RotationSegment.java</c>
    /// and <c>ref://mc-26.3-analysis/src/net/minecraft/util/SegmentedAnglePrecision.java</c>.
    /// </summary>
    public static float RotationDegrees(byte segment)
    {
        var degrees = (segment & 15) * 22.5f;
        return degrees >= 180f ? degrees - 360f : degrees;
    }

    /// <summary><c>Direction.getStepX</c> for a horizontal direction ordinal.</summary>
    public static int StepX(byte facing) => facing switch
    {
        4 => -1, // west
        5 => 1,  // east
        _ => 0
    };

    /// <summary><c>Direction.getStepZ</c> for a horizontal direction ordinal.</summary>
    public static int StepZ(byte facing) => facing switch
    {
        2 => -1, // north
        3 => 1,  // south
        _ => 0
    };

    /// <summary><c>-facing.getOpposite().toYRot()</c> for a horizontal direction ordinal.</summary>
    public static float WallYRot(byte facing) => facing switch
    {
        2 => 0f,    // north -> opposite south
        3 => -180f, // south -> opposite north
        4 => -270f, // west -> opposite east
        5 => -90f,  // east -> opposite west
        _ => 0f
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
