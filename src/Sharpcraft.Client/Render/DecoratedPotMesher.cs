// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Common.Reference;
using Sharpcraft.Common.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Builds the decorated pot base and four blank side planes. The reference also picks a pottery-pattern
/// sprite for each side from the block entity's sherd data; this slice always uses the blank side.
/// </summary>
/// <remarks>
/// Source: <c>ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/DecoratedPotRenderer.java</c>
/// (<c>createBaseLayer</c>, <c>createSidesLayer</c>, <c>modelTransformation</c>). The
/// <c>facing</c> property comes from
/// <c>ref://mc-26.3-analysis/src/net/minecraft/world/level/block/DecoratedPotBlock.java</c> and
/// <c>ref://mc-26.3-analysis/src/net/minecraft/world/level/block/entity/DecoratedPotBlockEntity.java</c>.
/// In the model space the base uses 32 by 32 pixels and the sides 16 by 16; the neck's part pose
/// offsets it by (0, 37, 16) pixels and rotates it by pi about X, which is why its two boxes sit at
/// the top of the block. The whole model is then rotated about the block centre by the reference
/// <c>modelTransformation</c>, <c>180 - facing.toYRot()</c> degrees. The reference's wobble pose
/// animation is not wired; see <c>SIM-BE-POT-NO-WOBBLE</c> in <c>docs/modules/render.md</c>.
/// </remarks>
[ReferenceClass("net.minecraft.client.renderer.blockentity.DecoratedPotRenderer", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/DecoratedPotRenderer.java", Status = ReferenceStatus.Partial, Note = "Base and blank side planes only; pottery patterns from block entity NBT are not parsed.")]
[Sharpcraft.Common.Diagnostics.Simplification("SIM-BE-POT-NO-PATTERNS", "T-47", Note = "Only the blank decorated_pot_side texture is drawn; the reference selects a pottery pattern per side from the block entity's sherds.")]
[Sharpcraft.Common.Diagnostics.Simplification("SIM-BE-POT-NO-WOBBLE", "T-47", Note = "Static geometry only; the reference's wobbleProgress / wobbleStyle pose animation is not wired.")]
public sealed class DecoratedPotMesher
{
    /// <summary>One sixteenth of a block, the model's unit.</summary>
    public const float Pixel = 1f / 16f;

    /// <summary>The base texture is 32 by 32 pixels.</summary>
    public const int BaseTextureSize = 32;

    /// <summary>The side texture is 16 by 16 pixels.</summary>
    public const int SideTextureSize = 16;

    /// <summary>Neck ring bottom in its local part space, 17.1 pixels (17 with the -0.1 deflate).</summary>
    public const float NeckRingFeetY = 17.1f * Pixel;

    /// <summary>Neck ring width, 8 pixels with the -0.1 deflate.</summary>
    public const float NeckRingWidth = 7.8f * Pixel;

    /// <summary>Neck ring height, 3 pixels with the -0.1 deflate.</summary>
    public const float NeckRingHeight = 2.8f * Pixel;

    /// <summary>Neck ring depth, 8 pixels with the -0.1 deflate.</summary>
    public const float NeckRingDepth = 7.8f * Pixel;

    /// <summary>Neck rim bottom in its local part space, 20 pixels with the +0.2 inflate.</summary>
    public const float NeckRimFeetY = 19.8f * Pixel;

    /// <summary>Neck rim width, 6 pixels with the +0.2 inflate.</summary>
    public const float NeckRimWidth = 6.4f * Pixel;

    /// <summary>Neck rim height, 1 pixel with the +0.2 inflate.</summary>
    public const float NeckRimHeight = 1.4f * Pixel;

    /// <summary>Neck rim depth, 6 pixels with the +0.2 inflate.</summary>
    public const float NeckRimDepth = 6.4f * Pixel;

    /// <summary>Neck part pose Y offset, 37 pixels.</summary>
    public const float NeckPoseY = 37f * Pixel;

    /// <summary>Neck part pose Z offset, 16 pixels.</summary>
    public const float NeckPoseZ = 16f * Pixel;

    /// <summary>Top and bottom planes start one pixel in from the block edge.</summary>
    public const float PlaneMin = 1f * Pixel;

    /// <summary>Top and bottom planes end fifteen pixels from the block edge.</summary>
    public const float PlaneMax = 15f * Pixel;

    /// <summary>Top plane height, sixteen pixels.</summary>
    public const float TopPlaneY = 16f * Pixel;

    /// <summary>Bottom plane height, zero pixels.</summary>
    public const float BottomPlaneY = 0f;

    /// <summary>Side planes span the full block height, sixteen pixels.</summary>
    public const float SidePlaneHeight = 16f * Pixel;

    private readonly DynamicGeometry _geometry = new();

    /// <summary>Entities drawn by the last call.</summary>
    public int LastEntityCount { get; private set; }

    /// <summary>Builds decorated pot geometry for every pot in the span, using the two atlas sprites.</summary>
    /// <param name="entities">Block entities in world space.</param>
    /// <param name="baseSprite">The decorated pot base sprite.</param>
    /// <param name="sideSprite">The blank decorated pot side sprite.</param>
    public DynamicGeometry Build(ReadOnlySpan<BlockEntityRenderState> entities, SpriteRect baseSprite, SpriteRect sideSprite) =>
        Build(entities, baseSprite, sideSprite, null);

    /// <summary>Builds decorated pot geometry, resolving both sprites from the block entity atlas.</summary>
    /// <param name="entities">Block entities in world space.</param>
    /// <param name="atlas">Atlas that holds the base and side textures.</param>
    public DynamicGeometry Build(ReadOnlySpan<BlockEntityRenderState> entities, ItemAtlas atlas) =>
        Build(entities, null, null, atlas);

    private DynamicGeometry Build(
        ReadOnlySpan<BlockEntityRenderState> entities,
        SpriteRect? baseSprite,
        SpriteRect? sideSprite,
        ItemAtlas? atlas)
    {
        _geometry.Clear();
        LastEntityCount = 0;

        var resolvedBase = baseSprite;
        var resolvedSide = sideSprite;
        if (atlas is not null)
        {
            resolvedBase = ChestAtlas.SpriteForDecoratedPotBase(atlas);
            resolvedSide = ChestAtlas.SpriteForDecoratedPotSide(atlas);
        }

        if (resolvedBase is null || resolvedSide is null)
        {
            if (atlas is null)
            {
                throw new InvalidOperationException("either both sprites or an atlas are required");
            }

            return _geometry;
        }

        if (entities.Length == 0)
        {
            return _geometry;
        }

        const int boxesPerPot = 2;
        const int quadsPerPot = 6;
        const int verticesPerBox = 24;
        const int verticesPerQuad = 4;
        const int indicesPerBox = 36;
        const int indicesPerQuad = 6;
        _geometry.Reserve(
            entities.Length * ((boxesPerPot * verticesPerBox) + (quadsPerPot * verticesPerQuad)) * ChunkMesher.FloatsPerVertex,
            entities.Length * ((boxesPerPot * indicesPerBox) + (quadsPerPot * indicesPerQuad)));

        var vertexFloats = 0;
        var indexCount = 0;

        for (var index = 0; index < entities.Length; index++)
        {
            var entity = entities[index];
            if (entity.TypeId != VanillaBlockEntityTypes.DecoratedPot)
            {
                continue;
            }

            var entityStart = vertexFloats;
            var lightSky = entity.Sky / 15f;
            var lightBlock = entity.Block / 15f;

            // The neck's part pose is offset (0, 37, 16) pixels and rotated pi around X. The boxes are
            // built in the part's local pixel space and the transform applies the pose.
            var neckTransform = new EntityModelTransform(
                Translation: new Vector3(0f, NeckPoseY, NeckPoseZ),
                YawDegrees: 0f,
                PitchDegrees: 180f);

            EntityPlaceholderMesher.AppendBox(
                _geometry,
                new Vector3(0.5f, NeckRingFeetY, 0.5f),
                width: NeckRingWidth,
                height: NeckRingHeight,
                red: 1f,
                green: 1f,
                blue: 1f,
                sprite: resolvedBase,
                ref vertexFloats,
                ref indexCount,
                depth: NeckRingDepth,
                entityTransform: neckTransform,
                sky: lightSky,
                block: lightBlock,
                texU: 0,
                texV: 0,
                texDx: 8,
                texDy: 3,
                texDz: 8,
                textureSize: BaseTextureSize);

            EntityPlaceholderMesher.AppendBox(
                _geometry,
                new Vector3(0.5f, NeckRimFeetY, 0.5f),
                width: NeckRimWidth,
                height: NeckRimHeight,
                red: 1f,
                green: 1f,
                blue: 1f,
                sprite: resolvedBase,
                ref vertexFloats,
                ref indexCount,
                depth: NeckRimDepth,
                entityTransform: neckTransform,
                sky: lightSky,
                block: lightBlock,
                texU: 0,
                texV: 5,
                texDx: 6,
                texDy: 1,
                texDz: 6,
                textureSize: BaseTextureSize);

            // Top and bottom are flat planes: one outward-facing quad each. Their UVs are the reference
            // zero-height cube's UP and DOWN rectangles (texOffs(-14, 13)).
            AppendTopAndBottom(resolvedBase, ref vertexFloats, ref indexCount, lightSky, lightBlock);

            // The four sides are the reference's 14 by 16 pixel planes under their part poses; the
            // blank side texture is 16 by 16, so its UV rectangle is 1..15 by 0..16 pixels.
            AppendSide(resolvedSide, new Vector3(15f * Pixel, 16f * Pixel, 1f * Pixel), 0f, 0f, 180f, ref vertexFloats, ref indexCount, lightSky, lightBlock);
            AppendSide(resolvedSide, new Vector3(1f * Pixel, 16f * Pixel, 1f * Pixel), 0f, -90f, 180f, ref vertexFloats, ref indexCount, lightSky, lightBlock);
            AppendSide(resolvedSide, new Vector3(15f * Pixel, 16f * Pixel, 15f * Pixel), 0f, 90f, 180f, ref vertexFloats, ref indexCount, lightSky, lightBlock);
            AppendSide(resolvedSide, new Vector3(1f * Pixel, 16f * Pixel, 15f * Pixel), 180f, 0f, 0f, ref vertexFloats, ref indexCount, lightSky, lightBlock);

            ApplyFacing(entityStart, vertexFloats, entity);
            LastEntityCount++;
        }

        _geometry.VertexFloats = vertexFloats;
        _geometry.IndexCount = indexCount;
        return _geometry;
    }

    /// <summary>The two flat cap planes at the pot's bottom and top.</summary>
    private void AppendTopAndBottom(
        SpriteRect sprite,
        ref int vertexFloats,
        ref int indexCount,
        float sky,
        float block)
    {
        // Reference UP face of the zero-height cube: U 14..28, V 13..27 on the 32x32 base sheet.
        EntityPlaceholderMesher.AppendQuad(
            _geometry,
            new Vector3(PlaneMax, TopPlaneY, PlaneMin),
            new Vector3(PlaneMin, TopPlaneY, PlaneMin),
            new Vector3(PlaneMin, TopPlaneY, PlaneMax),
            new Vector3(PlaneMax, TopPlaneY, PlaneMax),
            1f,
            1f,
            1f,
            sprite,
            ref vertexFloats,
            ref indexCount,
            (28f / BaseTextureSize, 27f / BaseTextureSize),
            (14f / BaseTextureSize, 27f / BaseTextureSize),
            (14f / BaseTextureSize, 13f / BaseTextureSize),
            (28f / BaseTextureSize, 13f / BaseTextureSize),
            sky: sky,
            block: block);

        // Reference DOWN face of the zero-height cube: U 0..14, V 13..27.
        EntityPlaceholderMesher.AppendQuad(
            _geometry,
            new Vector3(PlaneMax, BottomPlaneY, PlaneMax),
            new Vector3(PlaneMin, BottomPlaneY, PlaneMax),
            new Vector3(PlaneMin, BottomPlaneY, PlaneMin),
            new Vector3(PlaneMax, BottomPlaneY, PlaneMin),
            1f,
            1f,
            1f,
            sprite,
            ref vertexFloats,
            ref indexCount,
            (14f / BaseTextureSize, 13f / BaseTextureSize),
            (0f, 13f / BaseTextureSize),
            (0f, 27f / BaseTextureSize),
            (14f / BaseTextureSize, 27f / BaseTextureSize),
            sky: sky,
            block: block);
    }

    /// <summary>Appends one side plane from the reference's zero-depth cube and its part pose.</summary>
    private void AppendSide(
        SpriteRect sprite,
        Vector3 translation,
        float xDegrees,
        float yDegrees,
        float zDegrees,
        ref int vertexFloats,
        ref int indexCount,
        float sky,
        float block)
    {
        var transform = new EntityModelTransform(
            Translation: translation,
            YawDegrees: 0f,
            PitchDegrees: 0f,
            HasAttachRotation: true,
            AttachRotation: EulerZyx(xDegrees, yDegrees, zDegrees),
            AttachPivot: Vector3.Zero);

        // North face of the reference's 14x16x0 cube: U 1..15, V 0..16 on the 16x16 side sheet.
        var maxX = 14f * Pixel;
        var top = SidePlaneHeight;
        EntityPlaceholderMesher.AppendQuad(
            _geometry,
            new Vector3(maxX, 0f, 0f),
            new Vector3(0f, 0f, 0f),
            new Vector3(0f, top, 0f),
            new Vector3(maxX, top, 0f),
            1f,
            1f,
            1f,
            sprite,
            ref vertexFloats,
            ref indexCount,
            (15f / SideTextureSize, 0f),
            (1f / SideTextureSize, 0f),
            (1f / SideTextureSize, 1f),
            (15f / SideTextureSize, 1f),
            transform,
            sky,
            block);
    }

    private static Quaternion EulerZyx(float xDegrees, float yDegrees, float zDegrees)
    {
        // ModelPart.translateAndRotate applies Quaternionf().rotationZYX(zRot, yRot, xRot);
        // ref://mc-26.3-analysis/src/net/minecraft/client/model/geom/ModelPart.java:163-167.
        var radians = MathF.PI / 180f;
        var matrix = Matrix4x4.CreateRotationX(xDegrees * radians)
            * Matrix4x4.CreateRotationY(yDegrees * radians)
            * Matrix4x4.CreateRotationZ(zDegrees * radians);
        return Quaternion.CreateFromRotationMatrix(matrix);
    }

    /// <summary>Rotates the pot about the block centre and moves it from model space to the block origin.</summary>
    private void ApplyFacing(int startFloats, int endFloats, BlockEntityRenderState entity)
    {
        var radians = FacingDegrees(entity.Facing) * (MathF.PI / 180f);
        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);

        for (var vertex = startFloats; vertex < endFloats; vertex += ChunkMesher.FloatsPerVertex)
        {
            var dx = _geometry.Vertices[vertex] - 0.5f;
            var dz = _geometry.Vertices[vertex + 2] - 0.5f;
            _geometry.Vertices[vertex] = entity.X + 0.5f + (dx * cos) + (dz * sin);
            _geometry.Vertices[vertex + 1] += entity.Y;
            _geometry.Vertices[vertex + 2] = entity.Z + 0.5f - (dx * sin) + (dz * cos);
        }
    }

    /// <summary>
    /// Yaw the reference applies for a facing: <c>180 - facing.toYRot()</c>, in degrees. The input is
    /// the reference's Direction ordinal (down 0, up 1, north 2, south 3, west 4, east 5), which is
    /// what <c>FrameCommand.DirectionOrdinalOf</c> hands the mesher; it is not the chest face-step
    /// encoding.
    /// Ref: <c>ref://mc-26.3-analysis/src/net/minecraft/core/Direction.java:33-38,66-77,311-312</c>
    /// and <c>ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/DecoratedPotRenderer.java:187</c>.
    /// </summary>
    /// <param name="facing">Direction ordinal.</param>
    public static float FacingDegrees(byte facing) => facing switch
    {
        0 => -90f,
        1 => -90f,
        2 => 0f,
        3 => 180f,
        4 => 90f,
        _ => -90f
    };
}
