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
    public DynamicGeometry Build(ReadOnlySpan<BlockEntityRenderState> entities, ItemAtlas atlas) =>
        Build(entities, atlas, null);

    /// <summary>Builds with one sprite for every box, for callers that have no atlas.</summary>
    public DynamicGeometry Build(ReadOnlySpan<BlockEntityRenderState> entities, SpriteRect sprite) =>
        Build(entities, null, sprite);

    private DynamicGeometry Build(ReadOnlySpan<BlockEntityRenderState> entities, ItemAtlas? atlas, SpriteRect? singleSprite)
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
            var entityIndexStart = indexCount;
            var sprite = atlas is null
                ? singleSprite
                : atlas.Get(ChestAtlas.ShulkerTexture(entity.ChestMaterial));
            if (sprite is null)
            {
                continue;
            }

            var lightSky = entity.Sky / 15f;
            var lightBlock = entity.Block / 15f;
            var raw = RawMesh();
            var lidStart = vertexFloats;
            AppendRaw(raw, sprite, lightSky, lightBlock, ref vertexFloats, ref indexCount);
            var lidEnd = lidStart + 24 * ChunkMesher.FloatsPerVertex;

            // Source setupAnim(progress): lid y = 24 - progress*8 and yRot = 270*progress degrees.
            // Bind sample is in source model space; apply both on the raw lid vertices, then the
            // per-direction model transform.
            if (entity.Open != 0f)
            {
                for (var vertex = lidStart; vertex < lidEnd; vertex += ChunkMesher.FloatsPerVertex)
                {
                    _geometry.Vertices[vertex + 1] -= LidRise * entity.Open;
                }

                RotateAboutY(_geometry, lidStart, lidEnd, LidTurnDegrees * entity.Open);
            }

            var matrix = Transforms.TryGetValue(entity.Facing, out var found) ? found : Transforms[5];
            ApplyMatrix(_geometry, entityStart, vertexFloats, matrix, new Vector3(entity.X, entity.Y, entity.Z));

            // The sample's raw winding is source-positive; the model matrix flips handedness for some
            // directions, so reverse every triangle to keep faces outward after the transform.
            ReverseWinding(_geometry, entityIndexStart, indexCount);

            LastEntityCount++;
        }

        _geometry.VertexFloats = vertexFloats;
        _geometry.IndexCount = indexCount;

        return _geometry;
    }

    private sealed record RawShulkerMesh(float[] Vertices, uint[] Indices);

    private static readonly RawShulkerMesh?[] RawCache = new RawShulkerMesh?[1];

    private static RawShulkerMesh RawMesh()
    {
        lock (RawCache)
        {
            if (RawCache[0] is { } cached)
            {
                return cached;
            }

            var sprite = new SpriteRect(0, 0, 64, 64, 1) { U0 = 0f, V0 = 0f, U1 = 1f, V1 = 1f };
            var geometry = new EntityModelMesher().BuildModel(
                ShulkerBoxModelDefinition.Definition,
                new Sharpcraft.Client.World.EntityRenderState(1, 0, 0f, 0f, 0f, 180f, 0f),
                sprite,
                64,
                64,
                modelScale: 1f);
            var vertices = new float[geometry.VertexFloats];
            Array.Copy(geometry.Vertices, vertices, vertices.Length);
            var indices = new uint[geometry.IndexCount];
            Array.Copy(geometry.Indices, indices, indices.Length);
            var result = new RawShulkerMesh(vertices, indices);
            RawCache[0] = result;
            return result;
        }
    }

    private void AppendRaw(
        RawShulkerMesh raw,
        SpriteRect sprite,
        float sky,
        float block,
        ref int vertexFloats,
        ref int indexCount)
    {
        var baseVertex = (uint)(vertexFloats / ChunkMesher.FloatsPerVertex);
        var vertexCount = raw.Vertices.Length / ChunkMesher.FloatsPerVertex;
        for (var i = 0; i < vertexCount; i++)
        {
            var offset = i * ChunkMesher.FloatsPerVertex;
            var packedUv = BitConverter.SingleToUInt32Bits(raw.Vertices[offset + 4]);
            var u = (float)BitConverter.UInt16BitsToHalf((ushort)(packedUv & 0xFFFF));
            var v = (float)BitConverter.UInt16BitsToHalf((ushort)(packedUv >> 16));
            TerrainVertexWriter.Write(
                _geometry.Vertices,
                ref vertexFloats,
                raw.Vertices[offset],
                raw.Vertices[offset + 1],
                raw.Vertices[offset + 2],
                1f,
                1f,
                1f,
                u,
                v,
                sprite,
                sky,
                block);
        }

        for (var i = 0; i < raw.Indices.Length; i++)
        {
            _geometry.Indices[indexCount++] = baseVertex + raw.Indices[i];
        }
    }

    /// <summary>Swaps the second and third index of every triangle in a range.</summary>
    private static void ReverseWinding(DynamicGeometry geometry, int start, int end)
    {
        for (var i = start; i + 2 < end; i += 3)
        {
            (geometry.Indices[i + 1], geometry.Indices[i + 2]) = (geometry.Indices[i + 2], geometry.Indices[i + 1]);
        }
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
