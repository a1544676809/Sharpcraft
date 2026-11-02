// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Builds the static player model from <see cref="PlayerModelDefinition"/> and the default skin. This is render-only:
/// it consumes <see cref="EntityRenderState"/> and never touches world or entity simulation.
/// </summary>
public sealed class EntityModelMesher
{
    private readonly DynamicGeometry _geometry = new();

    public DynamicGeometry Build(ReadOnlySpan<EntityRenderState> entities, SpriteRect skin, int skinWidth, int skinHeight)
    {
        _geometry.Clear();
        _geometry.Reserve(entities.Length * 6 * 24 * 14, entities.Length * 6 * 36);
        var vertexFloats = 0;
        var indexCount = 0;
        var px = (1f / 16f) * EntityPlaceholderMesher.ModelScale;

        foreach (var entity in entities)
        {
            if (entity.TypeId != EntityPlaceholderMesher.PlayerEntityTypeId)
            {
                continue;
            }

            var feet = new Vector3(entity.X, entity.Y, entity.Z);
            foreach (var part in PlayerModelDefinition.AdultParts)
            {
                var centreX = part.CentreX * px;
                var bottom = part.Bottom * px;
                var halfW = part.Width * px * 0.5f;
                var halfD = part.Depth * px * 0.5f;
                var min = new Vector3(feet.X + centreX - halfW, feet.Y + bottom, feet.Z - halfD);
                var max = new Vector3(feet.X + centreX + halfW, feet.Y + bottom + (part.Height * px), feet.Z + halfD);
                AddPart(part, min, max, skin, skinWidth, skinHeight, ref vertexFloats, ref indexCount);
            }
        }

        _geometry.VertexFloats = vertexFloats;
        _geometry.IndexCount = indexCount;
        return _geometry;
    }

    /// <summary>
    /// Emits the six faces in the reference <c>ModelPart.Cube</c> order. The UV corners below are the exact
    /// Polygon constructor values; no per-face rectangle flip or 180-degree rotation is applied.
    /// </summary>
    private void AddPart(
        EntityModelPart part,
        Vector3 min,
        Vector3 max,
        SpriteRect skin,
        int skinWidth,
        int skinHeight,
        ref int vertexFloats,
        ref int indexCount)
    {
        var d = part.Depth;
        var w = part.Width;
        var h = part.Height;
        var tx = part.TextureX;
        var ty = part.TextureY;

        var u0 = tx;
        var u1 = tx + d;
        var u2 = tx + d + w;
        var u22 = tx + d + (2f * w);
        var u3 = tx + d + w + d;
        var u4 = tx + d + w + d + w;
        var v0 = ty;
        var v1 = ty + d;
        var v2 = ty + d + h;

        var t0 = new Vector3(min.X, min.Y, min.Z);
        var t1 = new Vector3(max.X, min.Y, min.Z);
        var t2 = new Vector3(max.X, max.Y, min.Z);
        var t3 = new Vector3(min.X, max.Y, min.Z);
        var l0 = new Vector3(min.X, min.Y, max.Z);
        var l1 = new Vector3(max.X, min.Y, max.Z);
        var l2 = new Vector3(max.X, max.Y, max.Z);
        var l3 = new Vector3(min.X, max.Y, max.Z);

        AddFace(l0, l1, t1, t0, u1, v0, u2, v0, u2, v1, u1, v1, skin, skinWidth, skinHeight, ref vertexFloats, ref indexCount); // DOWN
        AddFace(t3, t2, l2, l3, u2, v1, u22, v1, u22, v0, u2, v0, skin, skinWidth, skinHeight, ref vertexFloats, ref indexCount); // UP
        AddFace(l0, t0, t3, l3, u0, v1, u1, v1, u1, v2, u0, v2, skin, skinWidth, skinHeight, ref vertexFloats, ref indexCount); // WEST
        AddFace(t1, t0, t3, t2, u2, v1, u1, v1, u1, v2, u2, v2, skin, skinWidth, skinHeight, ref vertexFloats, ref indexCount); // NORTH
        AddFace(t1, l1, l2, t2, u2, v1, u3, v1, u3, v2, u2, v2, skin, skinWidth, skinHeight, ref vertexFloats, ref indexCount); // EAST
        AddFace(l0, l1, l2, l3, u4, v1, u3, v1, u3, v2, u4, v2, skin, skinWidth, skinHeight, ref vertexFloats, ref indexCount); // SOUTH
    }

    private void AddFace(
        Vector3 a, Vector3 b, Vector3 c, Vector3 d,
        float au, float av, float bu, float bv, float cu, float cv, float du, float dv,
        SpriteRect skin, int skinWidth, int skinHeight,
        ref int vertexFloats, ref int indexCount)
    {
        var first = (uint)(vertexFloats / 14);
        WriteVertex(_geometry.Vertices, ref vertexFloats, a, au / skinWidth, av / skinHeight, skin);
        WriteVertex(_geometry.Vertices, ref vertexFloats, b, bu / skinWidth, bv / skinHeight, skin);
        WriteVertex(_geometry.Vertices, ref vertexFloats, c, cu / skinWidth, cv / skinHeight, skin);
        WriteVertex(_geometry.Vertices, ref vertexFloats, d, du / skinWidth, dv / skinHeight, skin);

        _geometry.Indices[indexCount++] = first;
        _geometry.Indices[indexCount++] = first + 1;
        _geometry.Indices[indexCount++] = first + 2;
        _geometry.Indices[indexCount++] = first;
        _geometry.Indices[indexCount++] = first + 2;
        _geometry.Indices[indexCount++] = first + 3;
    }

    private static void WriteVertex(float[] destination, ref int offset, Vector3 position, float u, float v, SpriteRect skin)
    {
        destination[offset++] = position.X;
        destination[offset++] = position.Y;
        destination[offset++] = position.Z;
        destination[offset++] = 1f;
        destination[offset++] = 1f;
        destination[offset++] = 1f;
        destination[offset++] = u;
        destination[offset++] = v;
        destination[offset++] = skin.U0;
        destination[offset++] = skin.V0;
        destination[offset++] = skin.U1;
        destination[offset++] = skin.V1;
        destination[offset++] = 1f;
        destination[offset++] = 0f;
    }
}
