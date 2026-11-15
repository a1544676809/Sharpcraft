// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Common.Reference;
using Sharpcraft.Common.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Builds shulker box geometry. The reference has no block model for a shulker box, only a block
/// entity renderer with two boxes: a base and a lid that lifts and turns as the box opens.
/// </summary>
/// <remarks>
/// Model space in the reference runs downward from y=24, so a box declared at y=-8 sits at block
/// height zero. Sizes and texture offsets are taken from <c>ShulkerModel.createShellMesh</c>.
/// </remarks>
[ReferenceClass("net.minecraft.client.renderer.blockentity.ShulkerBoxRenderer", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/ShulkerBoxRenderer.java")]
public sealed class ShulkerBoxMesher
{
    private const float Pixel = 1f / 16f;

    /// <summary>Base box: sixteen wide, eight tall, sixteen deep, texture offset (0, 28).</summary>
    private const float BaseHeight = 8f * Pixel;

    /// <summary>Lid box: sixteen wide, twelve tall, sixteen deep, texture offset (0, 0).</summary>
    private const float LidHeight = 12f * Pixel;

    /// <summary>The lid starts a quarter block above the block floor, overlapping the base.</summary>
    private const float LidBottom = 4f * Pixel;

    /// <summary>How far the lid rises when fully open: half a block.</summary>
    private const float LidRise = 8f * Pixel;

    /// <summary>Lid turn at full opening, in degrees.</summary>
    private const float LidTurnDegrees = 270f;

    private readonly DynamicGeometry _geometry = new();

    /// <summary>Entities drawn by the last call.</summary>
    public int LastEntityCount { get; private set; }

    /// <summary>Builds shulker box geometry for every shulker box in the span.</summary>
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

        const int boxesPerEntity = 2;
        const int verticesPerBox = 24;
        const int indicesPerBox = 36;
        _geometry.Reserve(
            entities.Length * boxesPerEntity * verticesPerBox * ChunkMesher.FloatsPerVertex,
            entities.Length * boxesPerEntity * indicesPerBox);

        var vertexFloats = 0;
        var indexCount = 0;

        foreach (var entity in entities)
        {
            if (!Sharpcraft.Common.World.VanillaBlockEntityTypes.IsShulkerBox(entity.TypeId))
            {
                continue;
            }

            var origin = new Vector3(entity.X, entity.Y, entity.Z);
            var lightSky = entity.Sky / 15f;
            var lightBlock = entity.Block / 15f;

            // The base fills the bottom half of the block.
            EntityPlaceholderMesher.AppendBox(
                _geometry,
                origin,
                width: 1f,
                height: BaseHeight,
                red: 1f,
                green: 1f,
                blue: 1f,
                sprite: sprite,
                ref vertexFloats,
                ref indexCount,
                depth: 1f,
                sky: lightSky,
                block: lightBlock,
                texU: 0,
                texV: 28,
                texDx: 16,
                texDy: 8,
                texDz: 16);

            // The lid sits on top of the base and lifts as the box opens.
            var lidFeet = origin + new Vector3(0f, LidBottom + (LidRise * entity.Open), 0f);
            var lidStart = vertexFloats;
            EntityPlaceholderMesher.AppendBox(
                _geometry,
                lidFeet,
                width: 1f,
                height: LidHeight,
                red: 1f,
                green: 1f,
                blue: 1f,
                sprite: sprite,
                ref vertexFloats,
                ref indexCount,
                depth: 1f,
                sky: lightSky,
                block: lightBlock,
                texU: 0,
                texV: 0,
                texDx: 16,
                texDy: 12,
                texDz: 16);

            RotateLid(_geometry, lidStart, vertexFloats, lidFeet + new Vector3(0.5f, LidHeight / 2f, 0.5f), LidTurnDegrees * entity.Open);

            LastEntityCount++;
        }

        return _geometry;
    }

    /// <summary>Turns the lid about its own centre, the way the reference's lid rotation does.</summary>
    private static void RotateLid(DynamicGeometry geometry, int startFloats, int endFloats, Vector3 centre, float degrees)
    {
        if (degrees == 0f)
        {
            return;
        }

        var radians = degrees * (MathF.PI / 180f);
        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);

        for (var vertex = startFloats; vertex < endFloats; vertex += ChunkMesher.FloatsPerVertex)
        {
            var dx = geometry.Vertices[vertex] - centre.X;
            var dz = geometry.Vertices[vertex + 2] - centre.Z;
            geometry.Vertices[vertex] = centre.X + (dx * cos) + (dz * sin);
            geometry.Vertices[vertex + 2] = centre.Z - (dx * sin) + (dz * cos);
        }
    }
}
