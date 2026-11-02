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
        var d = (int)(part.Depth);
        var w = (int)(part.Width);
        var h = (int)(part.Height);
        var tx = part.TextureX;
        var ty = part.TextureY;

        // +Z / -Z / -X / +X / +Y / -Y, using the standard cube texture layout.
        AddFace(min.X, min.Y, max.Z, max.X, min.Y, max.Z, max.X, max.Y, max.Z, min.X, max.Y, max.Z, tx + d + w + d, ty + d, w, h, skin, skinWidth, skinHeight, ref vertexFloats, ref indexCount);
        AddFace(max.X, min.Y, min.Z, min.X, min.Y, min.Z, min.X, max.Y, min.Z, max.X, max.Y, min.Z, tx + d, ty + d, w, h, skin, skinWidth, skinHeight, ref vertexFloats, ref indexCount);
        AddFace(min.X, min.Y, min.Z, min.X, min.Y, max.Z, min.X, max.Y, max.Z, min.X, max.Y, min.Z, tx, ty + d, d, h, skin, skinWidth, skinHeight, ref vertexFloats, ref indexCount);
        AddFace(max.X, min.Y, max.Z, max.X, min.Y, min.Z, max.X, max.Y, min.Z, max.X, max.Y, max.Z, tx + d + w, ty + d, d, h, skin, skinWidth, skinHeight, ref vertexFloats, ref indexCount);
        AddFace(min.X, max.Y, min.Z, max.X, max.Y, min.Z, max.X, max.Y, max.Z, min.X, max.Y, max.Z, tx + d, ty, w, d, skin, skinWidth, skinHeight, ref vertexFloats, ref indexCount);
        AddFace(min.X, min.Y, max.Z, max.X, min.Y, max.Z, max.X, min.Y, min.Z, min.X, min.Y, min.Z, tx + d + w, ty, w, d, skin, skinWidth, skinHeight, ref vertexFloats, ref indexCount);
    }

    private void AddFace(
        float ax, float ay, float az,
        float bx, float by, float bz,
        float cx, float cy, float cz,
        float dx, float dy, float dz,
        int tu, int tv, int tw, int th,
        SpriteRect skin, int skinWidth, int skinHeight,
        ref int vertexFloats, ref int indexCount)
    {
        var u0 = (tu + skin.U0 * skinWidth) / skinWidth;
        var v0 = (tv + skin.V0 * skinHeight) / skinHeight;
        var u1 = (tu + tw) / (float)skinWidth;
        var v1 = (tv + th) / (float)skinHeight;

        var first = (uint)(vertexFloats / 14);
        WriteVertex(_geometry.Vertices, ref vertexFloats, ax, ay, az, u0, v0, skin);
        WriteVertex(_geometry.Vertices, ref vertexFloats, bx, by, bz, u0, v1, skin);
        WriteVertex(_geometry.Vertices, ref vertexFloats, cx, cy, cz, u1, v1, skin);
        WriteVertex(_geometry.Vertices, ref vertexFloats, dx, dy, dz, u1, v0, skin);

        _geometry.Indices[indexCount++] = first;
        _geometry.Indices[indexCount++] = first + 1;
        _geometry.Indices[indexCount++] = first + 2;
        _geometry.Indices[indexCount++] = first;
        _geometry.Indices[indexCount++] = first + 2;
        _geometry.Indices[indexCount++] = first + 3;
    }

    private static void WriteVertex(float[] destination, ref int offset, float x, float y, float z, float u, float v, SpriteRect skin)
    {
        destination[offset++] = x;
        destination[offset++] = y;
        destination[offset++] = z;
        destination[offset++] = 1f;
        destination[offset++] = 1f;
        destination[offset++] = 1f;
        destination[offset++] = 0f; // U
        destination[offset++] = 0f; // V
        destination[offset++] = skin.U0;
        destination[offset++] = skin.V0;
        destination[offset++] = skin.U1;
        destination[offset++] = skin.V1;
        destination[offset++] = 1f;
        destination[offset++] = 0f;
        // The block vertex format uses per-vertex u/v inside the sprite rect; overwrite them.
        destination[offset - 8] = u;
        destination[offset - 7] = v;
    }
}
