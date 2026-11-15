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
/// Geometry is assembled in the reference's model space, where y runs downward from the part pose,
/// and then transformed into the world by the per-direction matrix. Sizes and texture offsets come
/// from the shell mesh; the matrices were sampled from the reference renderer.
/// </remarks>
[ReferenceClass("net.minecraft.client.renderer.blockentity.ShulkerBoxRenderer", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/ShulkerBoxRenderer.java")]
public sealed class ShulkerBoxMesher
{
    private const float Pixel = 1f / 16f;

    /// <summary>Base box: sixteen wide, eight tall, sixteen deep, texture offset (0, 28).</summary>
    private const float BaseHeight = 8f * Pixel;

    /// <summary>Base box bottom, in model space: the pose sits at y=24 and the box starts eight pixels above.</summary>
    private const float BaseBottom = 16f * Pixel;

    /// <summary>Lid box: sixteen wide, twelve tall, sixteen deep, texture offset (0, 0).</summary>
    private const float LidHeight = 12f * Pixel;

    /// <summary>Lid box bottom at rest, in model space.</summary>
    private const float LidBottom = 8f * Pixel;

    /// <summary>Lid pose height at rest, in model space.</summary>
    private const float LidPivot = 24f * Pixel;

    /// <summary>How far the lid drops as the box opens: half a block.</summary>
    private const float LidRise = 8f * Pixel;

    /// <summary>Lid turn at full opening, in degrees.</summary>
    private const float LidTurnDegrees = 270f;

    /// <summary>Uniform shrink the reference applies so neighbouring boxes never z-fight.</summary>
    private const float Shrink = 0.9995f;

    /// <summary>
    /// Per-direction model-to-world matrices, row major, sampled from the reference. Directions the
    /// reference does not expect a shulker box to face fall back to the upward transform.
    /// </summary>
    private static readonly Dictionary<byte, float[]> Transforms = new()
    {
        // Direction ids: 0 down, 1 up, 2 north, 3 south, 4 west, 5 east.
        [0] = [0.999500f, 0f, 0f, 0f, 0f, 0.999500f, 0f, 0f, 0f, 0f, 0.999500f, 0f, 0.500000f, -0.499500f, 0.500000f, 1.000000f],
        [1] = [0.999500f, 0f, 0f, 0f, 0f, -0.999500f, 0f, 0f, 0f, 0f, -0.999500f, 0f, 0.500000f, 1.499500f, 0.500000f, 1.000000f],
        [2] = [-0.999500f, 0f, 0f, 0f, 0f, 0f, 0.999500f, 0f, 0f, 0.999500f, 0f, 0f, 0.500000f, 0.500000f, -0.499500f, 1.000000f],
        [3] = [0.999500f, 0f, 0f, 0f, 0f, 0f, -0.999500f, 0f, 0f, 0.999500f, 0f, 0f, 0.500000f, 0.500000f, 1.499500f, 1.000000f],
        [4] = [0f, 0f, 0.999500f, 0f, 0.999500f, 0f, 0f, 0f, 0f, 0.999500f, 0f, 0f, -0.499500f, 0.500000f, 0.500000f, 1.000000f],
        [5] = [0f, 0f, -0.999500f, 0f, -0.999500f, 0f, 0f, 0f, 0f, 0.999500f, 0f, 0f, 1.499500f, 0.500000f, 0.500000f, 1.000000f]
    };

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
            if (!VanillaBlockEntityTypes.IsShulkerBox(entity.TypeId))
            {
                continue;
            }

            var entityStart = vertexFloats;
            var lightSky = entity.Sky / 15f;
            var lightBlock = entity.Block / 15f;

            // The base fills the bottom half of the block.
            EntityPlaceholderMesher.AppendBox(
                _geometry,
                new Vector3(0f, BaseBottom, 0f),
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

            // The lid overlaps the base and lifts as the box opens.
            var lidBottom = LidBottom - (LidRise * entity.Open);
            var lidStart = vertexFloats;
            EntityPlaceholderMesher.AppendBox(
                _geometry,
                new Vector3(0f, lidBottom, 0f),
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

            // The lid turns about the vertical line through the middle of the block, not its own centre.
            RotateAboutY(_geometry, lidStart, vertexFloats, LidTurnDegrees * entity.Open);

            var matrix = Transforms.TryGetValue(entity.Facing, out var found) ? found : Transforms[5];
            ApplyMatrix(_geometry, entityStart, vertexFloats, matrix, new Vector3(entity.X, entity.Y, entity.Z));

            LastEntityCount++;
        }

        _geometry.VertexFloats = vertexFloats;
        _geometry.IndexCount = indexCount;

        return _geometry;
    }

    /// <summary>Turns vertices about the vertical line through x=0 and z=0.</summary>
    private static void RotateAboutY(DynamicGeometry geometry, int startFloats, int endFloats, float degrees)
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
            var x = geometry.Vertices[vertex];
            var z = geometry.Vertices[vertex + 2];
            geometry.Vertices[vertex] = (x * cos) + (z * sin);
            geometry.Vertices[vertex + 2] = (z * cos) - (x * sin);
        }
    }

    /// <summary>Applies a row-major model-to-world matrix, then moves the result to the block's corner.</summary>
    private static void ApplyMatrix(DynamicGeometry geometry, int startFloats, int endFloats, float[] m, Vector3 origin)
    {
        for (var vertex = startFloats; vertex < endFloats; vertex += ChunkMesher.FloatsPerVertex)
        {
            var x = geometry.Vertices[vertex];
            var y = geometry.Vertices[vertex + 1];
            var z = geometry.Vertices[vertex + 2];

            geometry.Vertices[vertex] = (m[0] * x) + (m[1] * y) + (m[2] * z) + m[12] + origin.X;
            geometry.Vertices[vertex + 1] = (m[4] * x) + (m[5] * y) + (m[6] * z) + m[13] + origin.Y;
            geometry.Vertices[vertex + 2] = (m[8] * x) + (m[9] * y) + (m[10] * z) + m[14] + origin.Z;
        }
    }
}
