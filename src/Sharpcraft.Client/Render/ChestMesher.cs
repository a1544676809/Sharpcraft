// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Common.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Draws chests from the reference chest model: a body, a lid that swings on its back bottom edge,
/// and a latch that swings with the lid.
/// </summary>
/// <remarks>
/// Geometry and animation constants come from <c>docs/specs/render/golden-vectors/chest.md</c>,
/// which records them against the reference classes. The model works in block model space where
/// one block is sixteen units and positive Y is up.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.blockentity.ChestRenderer", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/ChestRenderer.java")]
public sealed class ChestMesher
{
    /// <summary>One sixteenth of a block, the model's unit.</summary>
    public const float Pixel = 1f / 16f;

    /// <summary>Body spans one to fifteen pixels across and nought to ten up.</summary>
    public const float BodyWidth = 14f * Pixel;

    /// <summary>Body height.</summary>
    public const float BodyHeight = 10f * Pixel;

    /// <summary>Lid width, matching the body.</summary>
    public const float LidWidth = 14f * Pixel;

    /// <summary>Lid height, five pixels as the reference model has it.</summary>
    public const float LidHeight = 5f * Pixel;

    /// <summary>Latch width, two pixels.</summary>
    public const float LockWidth = 2f * Pixel;

    /// <summary>Latch height, four pixels.</summary>
    public const float LockHeight = 4f * Pixel;

    /// <summary>Latch depth, one pixel.</summary>
    public const float LockDepth = 1f * Pixel;

    /// <summary>Height of the lid pivot, nine pixels up.</summary>
    public const float PivotY = 9f * Pixel;

    /// <summary>Depth of the lid pivot, one pixel back.</summary>
    public const float PivotZ = 1f * Pixel;

    private readonly DynamicGeometry _geometry = new();

    /// <summary>Entities drawn by the last call.</summary>
    public int LastEntityCount { get; private set; }

    /// <summary>Builds chest geometry for every chest-like entity in the span.</summary>
    /// <param name="entities">Block entities in world space.</param>
    /// <param name="sprite">Atlas rect used for the faces.</param>
    public DynamicGeometry Build(ReadOnlySpan<BlockEntityRenderState> entities, SpriteRect sprite)
    {
        _geometry.Clear();
        LastEntityCount = 0;

        if (entities.Length == 0)
        {
            return _geometry;
        }

        const int boxesPerChest = 3;
        const int verticesPerBox = 24;
        const int indicesPerBox = 36;
        _geometry.Reserve(
            entities.Length * boxesPerChest * verticesPerBox * ChunkMesher.FloatsPerVertex,
            entities.Length * boxesPerChest * indicesPerBox);

        var vertexFloats = 0;
        var indexCount = 0;

        for (var index = 0; index < entities.Length; index++)
        {
            var entity = entities[index];
            if (!VanillaBlockEntityTypes.IsChestLike(entity.TypeId))
            {
                continue;
            }

            var origin = new Vector3(entity.X, entity.Y, entity.Z);
            var lightSky = entity.Sky / 15f;
            var lightBlock = entity.Block / 15f;

            // The body does not move.
            EntityPlaceholderMesher.AppendBox(
                _geometry,
                origin + new Vector3(8f * Pixel, 0f, 8f * Pixel),
                BodyWidth,
                BodyHeight,
                1f,
                1f,
                1f,
                sprite,
                ref vertexFloats,
                ref indexCount,
                BodyWidth,
                sky: lightSky,
                block: lightBlock);

            // The lid and the latch swing about the back bottom edge of the lid.
            var angle = -OpenAngle(entity.Open);
            var pivot = origin + new Vector3(0f, PivotY, PivotZ);
            var swing = new EntityModelTransform(
                Translation: Vector3.Zero,
                YawDegrees: 0f,
                PitchDegrees: 0f,
                Scale: 1f,
                ScaleXZ: 1f,
                ScaleY: 1f,
                HasAttachRotation: true,
                AttachRotation: Quaternion.CreateFromAxisAngle(Vector3.UnitX, angle),
                AttachPivot: pivot);

            EntityPlaceholderMesher.AppendBox(
                _geometry,
                origin + new Vector3(8f * Pixel, PivotY, 8f * Pixel),
                LidWidth,
                LidHeight,
                1f,
                1f,
                1f,
                sprite,
                ref vertexFloats,
                ref indexCount,
                LidWidth,
                swing,
                lightSky,
                lightBlock);

            // The latch sits on the front face, seven to nine pixels across and seven to eleven up.
            EntityPlaceholderMesher.AppendBox(
                _geometry,
                origin + new Vector3(8f * Pixel, 7f * Pixel, 15.5f * Pixel),
                LockWidth,
                LockHeight,
                1f,
                1f,
                1f,
                sprite,
                ref vertexFloats,
                ref indexCount,
                LockDepth,
                swing,
                lightSky,
                lightBlock);

            LastEntityCount++;
        }

        _geometry.VertexFloats = vertexFloats;
        _geometry.IndexCount = indexCount;
        return _geometry;
    }

    /// <summary>
    /// The lid angle for an opening amount, applying the reference's cubic ease before the quarter
    /// turn. Exposed so the animation can be pinned against the golden vectors.
    /// </summary>
    /// <param name="open">Opening amount, zero closed and one fully open.</param>
    public static float OpenAngle(float open)
    {
        var eased = 1f - open;
        return (1f - (eased * eased * eased)) * (MathF.PI / 2f);
    }
}
