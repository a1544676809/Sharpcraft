// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Side faces of a generated item sprite: the reference scans the sprite for opaque pixels whose neighbour in a
/// direction is transparent and extrudes one quad per such transition, which gives a flat item visible thickness.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.resources.model.cuboid.ItemModelGenerator", "ref://mc-26.3-analysis/src/net/minecraft/client/resources/model/cuboid/ItemModelGenerator.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "side-face extrusion of the generated sprite; the front and back faces are built by the caller")]
public static class GeneratedItemGeometry
{
    /// <summary>Extruded side quads live between these model pixel depths, same as the front and back faces.</summary>
    public const float MinZ = 7.5f;

    /// <summary>Upper depth of the extrusion.</summary>
    public const float MaxZ = 8.5f;

    /// <summary>Reference <c>UV_SHRINK</c>: side face UVs sit one tenth of a pixel inside the sprite cell.</summary>
    public const float UvShrink = 0.1f;

    /// <summary>
    /// Appends one quad per opaque/transparent transition. <paramref name="alphaAt"/> returns the alpha byte for a pixel
    /// and may be called with out-of-range coordinates, which the reference also treats as transparent.
    /// </summary>
    public static void AppendSideFaces(
        List<ItemModelQuad> output,
        string texture,
        int width,
        int height,
        Func<int, int, byte> alphaAt)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentException.ThrowIfNullOrEmpty(texture);
        ArgumentNullException.ThrowIfNull(alphaAt);
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var xScale = 16f / width;
        var yScale = 16f / height;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (alphaAt(x, y) == 0)
                {
                    continue;
                }

                // Neighbour tests mirror the reference: UP looks above, DOWN below, LEFT left, RIGHT right.
                if (alphaAt(x, y - 1) == 0)
                {
                    AppendFace(output, texture, x, y, SideDirection.Up, xScale, yScale);
                }

                if (alphaAt(x, y + 1) == 0)
                {
                    AppendFace(output, texture, x, y, SideDirection.Down, xScale, yScale);
                }

                if (alphaAt(x - 1, y) == 0)
                {
                    AppendFace(output, texture, x, y, SideDirection.Left, xScale, yScale);
                }

                if (alphaAt(x + 1, y) == 0)
                {
                    AppendFace(output, texture, x, y, SideDirection.Right, xScale, yScale);
                }
            }
        }
    }

    private static void AppendFace(
        List<ItemModelQuad> output,
        string texture,
        int x,
        int y,
        SideDirection side,
        float xScale,
        float yScale)
    {
        var u0 = x + UvShrink;
        var u1 = x + 1f - UvShrink;
        float v0;
        float v1;
        if (side is SideDirection.Up or SideDirection.Down)
        {
            v0 = y + UvShrink;
            v1 = y + 1f - UvShrink;
        }
        else
        {
            v0 = y + 1f - UvShrink;
            v1 = y + UvShrink;
        }

        var startX = (float)x;
        var startY = (float)y;
        var endX = (float)x;
        var endY = (float)y;
        switch (side)
        {
            case SideDirection.Up:
                endX++;
                break;
            case SideDirection.Down:
                endX++;
                startY++;
                endY++;
                break;
            case SideDirection.Left:
                endY++;
                break;
            default:
                startX++;
                endX++;
                endY++;
                break;
        }

        startX *= xScale;
        endX *= xScale;
        startY = 16f - (startY * yScale);
        endY = 16f - (endY * yScale);

        var from = side switch
        {
            SideDirection.Up => new Vector3(startX, startY, MinZ),
            SideDirection.Down => new Vector3(startX, endY, MinZ),
            SideDirection.Left => new Vector3(startX, startY, MinZ),
            _ => new Vector3(endX, startY, MinZ)
        };
        var to = side switch
        {
            SideDirection.Up => new Vector3(endX, startY, MaxZ),
            SideDirection.Down => new Vector3(endX, endY, MaxZ),
            SideDirection.Left => new Vector3(startX, endY, MaxZ),
            _ => new Vector3(endX, endY, MaxZ)
        };

        var uv = new Vector4(u0 * xScale, v0 * yScale, u1 * xScale, v1 * yScale);
        var face = new ItemModelFace(texture, uv);
        var direction = side switch
        {
            SideDirection.Up => ModelFaceDirection.Up,
            SideDirection.Down => ModelFaceDirection.Down,
            SideDirection.Left => ModelFaceDirection.East,
            _ => ModelFaceDirection.West
        };
        ItemModelGeometry.Append(
            output,
            new ItemModelElement(
                from,
                to,
                new Dictionary<ModelFaceDirection, ItemModelFace> { [direction] = face }));
    }

    private enum SideDirection
    {
        Up,
        Down,
        Left,
        Right
    }
}
