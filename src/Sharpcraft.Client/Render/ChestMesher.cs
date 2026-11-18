// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.World;
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
        => Build(entities, sprite, null);

    /// <summary>Builds chest geometry, choosing each chest's sprite from its material and half.</summary>
    /// <param name="entities">Block entities in world space.</param>
    /// <param name="atlas">Chest atlas the sprites come from.</param>
    public DynamicGeometry Build(ReadOnlySpan<BlockEntityRenderState> entities, ItemAtlas atlas)
        => Build(entities, default, atlas);

    private DynamicGeometry Build(
        ReadOnlySpan<BlockEntityRenderState> entities,
        SpriteRect? fallbackSprite,
        ItemAtlas? atlas)
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
            var sprite = atlas is null
                ? fallbackSprite ?? throw new InvalidOperationException("either a sprite or an atlas is required")
                : ChestAtlas.SpriteFor(atlas, entity.ChestMaterial, entity.ChestType);

            // The reference turns the whole chest about the middle of its bottom face by minus the
            // facing's yaw. Every facing is a quarter turn, so the axis aligned boxes stay axis
            // aligned and only their centres and extents swap.
            var radians = FacingDegrees(entity.Facing) * (MathF.PI / 180f);
            var cos = MathF.Cos(radians);
            var sin = MathF.Sin(radians);
            var quarterTurn = MathF.Abs(cos) < 0.5f;

            // A double chest half is fifteen pixels wide and shifted away from the joining edge; a
            // single chest is fourteen and centred. The latch narrows to one pixel on a double.
            var (bodyCentre, bodyWidth, bodyWidthPx, lockCentre, lockWidth, lockWidthPx) = entity.ChestType switch
            {
                1 => (7.5f * Pixel, 15f * Pixel, 15, 0.5f * Pixel, 1f * Pixel, 1),
                2 => (8.5f * Pixel, 15f * Pixel, 15, 15.5f * Pixel, 1f * Pixel, 1),
                _ => (8f * Pixel, 14f * Pixel, 14, 8f * Pixel, 2f * Pixel, 2)
            };

            var entityStartFloats = vertexFloats;
            var definition = entity.ChestType switch
            {
                1 => ChestModelDefinition.DoubleLeft,
                2 => ChestModelDefinition.DoubleRight,
                _ => ChestModelDefinition.Single
            };
            var raw = RawMesh(definition);
            AppendRawChest(raw, origin, sprite, lightSky, lightBlock, entity.Facing, entity.Open,
                ref vertexFloats, ref indexCount);
            _ = entityStartFloats;

            LastEntityCount++;
        }

        _geometry.VertexFloats = vertexFloats;
        _geometry.IndexCount = indexCount;
        return _geometry;
    }

    private sealed record RawChestMesh(int VertexCount, int LidStart, float[] Vertices, uint[] Indices);

    private static readonly Dictionary<EntityModelDefinition, RawChestMesh> RawCache = [];

    private static RawChestMesh RawMesh(EntityModelDefinition definition)
    {
        lock (RawCache)
        {
            if (RawCache.TryGetValue(definition, out var cached))
            {
                return cached;
            }

            var sprite = new SpriteRect(0, 0, 64, 64, 1) { U0 = 0f, V0 = 0f, U1 = 1f, V1 = 1f };
            var geometry = new EntityModelMesher().BuildModel(
                definition,
                new EntityRenderState(1, 0, 0f, 0f, 0f, 180f, 0f),
                sprite,
                64,
                64,
                modelScale: 1f);
            var vertexCount = geometry.VertexFloats / ChunkMesher.FloatsPerVertex;
            var vertices = new float[vertexCount * ChunkMesher.FloatsPerVertex];
            Array.Copy(geometry.Vertices, vertices, vertices.Length);
            var indices = new uint[geometry.IndexCount];
            Array.Copy(geometry.Indices, indices, indices.Length);
            var lidStart = definition.Parts.TakeWhile(part => part.Name != "lid")
                .Sum(part => System.Numerics.BitOperations.PopCount((uint)part.VisibleSides) * 4);
            var result = new RawChestMesh(vertexCount, lidStart, vertices, indices);
            RawCache[definition] = result;
            return result;
        }
    }

    private void AppendRawChest(
        RawChestMesh raw,
        Vector3 origin,
        SpriteRect sprite,
        float sky,
        float block,
        byte facing,
        float open,
        ref int vertexFloats,
        ref int indexCount)
    {
        var baseVertex = (uint)(vertexFloats / ChunkMesher.FloatsPerVertex);
        var radians = FacingDegrees(facing) * (MathF.PI / 180f);
        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);
        var openAngle = -OpenAngle(open);
        var openCos = MathF.Cos(openAngle);
        var openSin = MathF.Sin(openAngle);
        const float pivotY = 9f / 16f;
        const float pivotZ = 1f / 16f;

        for (var i = 0; i < raw.VertexCount; i++)
        {
            var offset = i * ChunkMesher.FloatsPerVertex;
            var x = raw.Vertices[offset];
            var y = raw.Vertices[offset + 1];
            var z = raw.Vertices[offset + 2];
            if (i >= raw.LidStart)
            {
                var dy = y - pivotY;
                var dz = z - pivotZ;
                y = pivotY + (dy * openCos) - (dz * openSin);
                z = pivotZ + (dy * openSin) + (dz * openCos);
            }

            var dx = x - 0.5f;
            var dz2 = z - 0.5f;
            x = 0.5f + (dx * cos) + (dz2 * sin);
            z = 0.5f - (dx * sin) + (dz2 * cos);

            var packedUv = BitConverter.SingleToUInt32Bits(raw.Vertices[offset + 4]);
            var u = (float)BitConverter.UInt16BitsToHalf((ushort)(packedUv & 0xFFFF));
            var v = (float)BitConverter.UInt16BitsToHalf((ushort)(packedUv >> 16));
            TerrainVertexWriter.Write(
                _geometry.Vertices,
                ref vertexFloats,
                origin.X + x,
                origin.Y + y,
                origin.Z + z,
                1f,
                1f,
                1f,
                u,
                v,
                sprite,
                sky,
                block);
        }

        for (var i = 0; i + 2 < raw.Indices.Length; i += 3)
        {
            _geometry.Indices[indexCount++] = baseVertex + raw.Indices[i];
            _geometry.Indices[indexCount++] = baseVertex + raw.Indices[i + 2];
            _geometry.Indices[indexCount++] = baseVertex + raw.Indices[i + 1];
        }
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

    /// <summary>
    /// Yaw the reference applies for a facing: minus that facing's <c>toYRot</c>, in degrees. Face
    /// indices follow <see cref="SectionCoord.Step"/>: 0 west, 1 east, 4 north, 5 south.
    /// </summary>
    /// <param name="facing">Face index.</param>
    public static float FacingDegrees(byte facing) => facing switch
    {
        0 => -90f,
        1 => 90f,
        4 => -180f,
        _ => 0f
    };

    /// <summary>Turns a model space point about the middle of the block's horizontal plane.</summary>
    private static Vector2 Rotate(float x, float z, float cos, float sin)
    {
        var dx = x - 0.5f;
        var dz = z - 0.5f;
        return new Vector2(
            0.5f + (dx * cos) + (dz * sin),
            0.5f - (dx * sin) + (dz * cos));
    }
}
