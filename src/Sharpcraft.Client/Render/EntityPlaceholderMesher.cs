// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Turns tracked entities into boxes so their motion can be seen before real models exist
/// (docs/specs/network/entity-sync.md 6).
/// </summary>
/// <remarks>
/// The buffers are reused across frames, so a steady frame allocates nothing. Boxes use the same vertex layout
/// and atlas as block geometry, which lets them share the pipeline; only the colours differ, so a player is
/// distinguishable from a mob at a glance.
/// </remarks>
public sealed class EntityPlaceholderMesher
{
    /// <summary>Width and depth of a player box.</summary>
    public const float PlayerWidth = 0.6f;

    /// <summary>Height of a player box.</summary>
    public const float PlayerHeight = 1.8f;

    /// <summary>Width and depth of the generic mob box.</summary>
    public const float MobWidth = 0.9f;

    /// <summary>Height of the generic mob box.</summary>
    public const float MobHeight = 0.9f;

    private readonly DynamicGeometry _geometry = new();

    /// <summary>Entities drawn by the last build.</summary>
    public int LastEntityCount { get; private set; }

    /// <summary>
    /// Builds the boxes for one frame.
    /// </summary>
    /// <param name="entities">Interpolated entity states.</param>
    /// <param name="sprite">Atlas cell to sample; boxes are tinted by colour.</param>
    public DynamicGeometry Build(ReadOnlySpan<EntityRenderState> entities, SpriteRect sprite)
    {
        _geometry.Clear();
        LastEntityCount = 0;

        if (entities.Length == 0)
        {
            return _geometry;
        }

        // Six faces of four vertices each, with two triangles per face.
        const int verticesPerBox = 24;
        const int indicesPerBox = 36;
        // A player is drawn as six parts, so reserve for the worst case.
        _geometry.Reserve(entities.Length * PlayerPartCount * verticesPerBox * 12, entities.Length * PlayerPartCount * indicesPerBox);

        var vertexFloats = 0;
        var indexCount = 0;

        foreach (var entity in entities)
        {
            var isPlayer = entity.TypeId == PlayerEntityTypeId;
            var width = isPlayer ? PlayerWidth : MobWidth;
            var height = isPlayer ? PlayerHeight : MobHeight;

            // Players read as blue, everything else as a warm grey, so the two are easy to tell apart.
            var red = isPlayer ? 0.45f : 0.65f;
            var green = isPlayer ? 0.65f : 0.6f;
            var blue = isPlayer ? 1f : 0.5f;

            if (isPlayer)
            {
                // Head, body, two arms and two legs: still boxes, but the silhouette reads as a person rather than a pillar.
                // The second argument is the bottom of each part, not its centre.
                // Proportions are the reference's: legs 0.75 tall, body 0.75, head 0.5, total height 1.8.
                var feet = new Vector3(entity.X, entity.Y, entity.Z);

                AppendBox(_geometry, feet + new Vector3(0f, 1.5f, 0f), 0.5f, 0.3f, red, green, blue, sprite, ref vertexFloats, ref indexCount);
                AppendBox(_geometry, feet + new Vector3(0f, 0.75f, 0f), 0.5f, 0.75f, red, green, blue, sprite, ref vertexFloats, ref indexCount);
                AppendBox(_geometry, feet + new Vector3(-0.375f, 0.75f, 0f), 0.25f, 0.75f, red, green, blue, sprite, ref vertexFloats, ref indexCount);
                AppendBox(_geometry, feet + new Vector3(0.375f, 0.75f, 0f), 0.25f, 0.75f, red, green, blue, sprite, ref vertexFloats, ref indexCount);
                AppendBox(_geometry, feet + new Vector3(-0.125f, 0f, 0f), 0.25f, 0.75f, red, green, blue, sprite, ref vertexFloats, ref indexCount);
                AppendBox(_geometry, feet + new Vector3(0.125f, 0f, 0f), 0.25f, 0.75f, red, green, blue, sprite, ref vertexFloats, ref indexCount);
            }
            else
            {
                AppendBox(
                    _geometry,
                    new Vector3(entity.X, entity.Y, entity.Z),
                    width,
                    height,
                    red,
                    green,
                    blue,
                    sprite,
                    ref vertexFloats,
                    ref indexCount);
            }

            LastEntityCount++;
        }

        _geometry.VertexFloats = vertexFloats;
        _geometry.IndexCount = indexCount;
        return _geometry;
    }

    /// <summary>Boxes a player is drawn from until real models are extracted from the jar.</summary>
    public const int PlayerPartCount = 6;

    /// <summary>Entity type id of a player in 26.3, observed in the live world.</summary>
    private const int PlayerEntityTypeId = 159;

    private static void AppendBox(
        DynamicGeometry geometry,
        Vector3 feet,
        float width,
        float height,
        float red,
        float green,
        float blue,
        SpriteRect sprite,
        ref int vertexFloats,
        ref int indexCount)
    {
        var half = width / 2f;
        var minX = feet.X - half;
        var maxX = feet.X + half;
        var minY = feet.Y;
        var maxY = feet.Y + height;
        var minZ = feet.Z - half;
        var maxZ = feet.Z + half;

        // +X, -X, +Y, -Y, +Z, -Z; each face is two triangles wound consistently.
        AppendFace(geometry, new Vector3(maxX, minY, minZ), new Vector3(maxX, minY, maxZ), new Vector3(maxX, maxY, maxZ), new Vector3(maxX, maxY, minZ), red, green, blue, sprite, ref vertexFloats, ref indexCount);
        AppendFace(geometry, new Vector3(minX, minY, maxZ), new Vector3(minX, minY, minZ), new Vector3(minX, maxY, minZ), new Vector3(minX, maxY, maxZ), red, green, blue, sprite, ref vertexFloats, ref indexCount);
        AppendFace(geometry, new Vector3(minX, maxY, minZ), new Vector3(maxX, maxY, minZ), new Vector3(maxX, maxY, maxZ), new Vector3(minX, maxY, maxZ), red, green, blue, sprite, ref vertexFloats, ref indexCount);
        AppendFace(geometry, new Vector3(minX, minY, maxZ), new Vector3(maxX, minY, maxZ), new Vector3(maxX, minY, minZ), new Vector3(minX, minY, minZ), red, green, blue, sprite, ref vertexFloats, ref indexCount);
        AppendFace(geometry, new Vector3(minX, minY, maxZ), new Vector3(maxX, minY, maxZ), new Vector3(maxX, maxY, maxZ), new Vector3(minX, maxY, maxZ), red, green, blue, sprite, ref vertexFloats, ref indexCount);
        AppendFace(geometry, new Vector3(maxX, minY, minZ), new Vector3(minX, minY, minZ), new Vector3(minX, maxY, minZ), new Vector3(maxX, maxY, minZ), red, green, blue, sprite, ref vertexFloats, ref indexCount);
    }

    private static void AppendFace(
        DynamicGeometry geometry,
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Vector3 d,
        float red,
        float green,
        float blue,
        SpriteRect sprite,
        ref int vertexFloats,
        ref int indexCount)
    {
        var first = (uint)(vertexFloats / 12);

        WriteVertex(geometry.Vertices, ref vertexFloats, a, red, green, blue, 0f, 0f, sprite);
        WriteVertex(geometry.Vertices, ref vertexFloats, b, red, green, blue, 1f, 0f, sprite);
        WriteVertex(geometry.Vertices, ref vertexFloats, c, red, green, blue, 1f, 1f, sprite);
        WriteVertex(geometry.Vertices, ref vertexFloats, d, red, green, blue, 0f, 1f, sprite);

        geometry.Indices[indexCount++] = first;
        geometry.Indices[indexCount++] = first + 1;
        geometry.Indices[indexCount++] = first + 2;
        geometry.Indices[indexCount++] = first;
        geometry.Indices[indexCount++] = first + 2;
        geometry.Indices[indexCount++] = first + 3;
    }

    private static void WriteVertex(
        float[] destination,
        ref int offset,
        Vector3 position,
        float red,
        float green,
        float blue,
        float u,
        float v,
        SpriteRect sprite)
    {
        destination[offset++] = position.X;
        destination[offset++] = position.Y;
        destination[offset++] = position.Z;
        destination[offset++] = red;
        destination[offset++] = green;
        destination[offset++] = blue;
        destination[offset++] = u;
        destination[offset++] = v;
        destination[offset++] = sprite.U0;
        destination[offset++] = sprite.V0;
        destination[offset++] = sprite.U1;
        destination[offset++] = sprite.V1;
    }
}
