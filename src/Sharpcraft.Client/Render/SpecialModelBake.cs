// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Client.Render;

/// <summary>One box of a special model, in model pixels.</summary>
/// <param name="TextureU">Left edge of the box in the model texture, in pixels.</param>
/// <param name="TextureV">Top edge of the box in the model texture, in pixels.</param>
/// <param name="From">Minimum corner in model pixels.</param>
/// <param name="Size">Extent in model pixels.</param>
/// <param name="Mirror">Whether the reference mirrors this box on the X axis.</param>
internal readonly record struct SpecialCube(int TextureU, int TextureV, Vector3 From, Vector3 Size, bool Mirror = false);

/// <summary>One part of a special model: a pivot, an optional rotation, boxes and children.</summary>
/// <param name="Name">Reference part name, for diagnostics and tests.</param>
/// <param name="Pivot">Pivot in model pixels.</param>
/// <param name="RotationDegrees">Pose rotation in degrees, applied in the reference ZYX order.</param>
/// <param name="Cubes">Boxes carried by this part.</param>
/// <param name="Children">Child parts.</param>
internal sealed record SpecialPart(
    string Name,
    Vector3 Pivot,
    Vector3 RotationDegrees,
    IReadOnlyList<SpecialCube> Cubes,
    IReadOnlyList<SpecialPart> Children);

/// <summary>One quad emitted by <see cref="SpecialModelBake"/>, in item model space (16 pixels = 1).</summary>
/// <param name="P0">Corner 0, in reference order.</param>
/// <param name="P1">Corner 1.</param>
/// <param name="P2">Corner 2.</param>
/// <param name="P3">Corner 3.</param>
/// <param name="Uv0">Texture coordinate of corner 0.</param>
/// <param name="Uv1">Texture coordinate of corner 1.</param>
/// <param name="Uv2">Texture coordinate of corner 2.</param>
/// <param name="Uv3">Texture coordinate of corner 3.</param>
internal readonly record struct SpecialQuad(
    Vector3 P0,
    Vector3 P1,
    Vector3 P2,
    Vector3 P3,
    Vector2 Uv0,
    Vector2 Uv1,
    Vector2 Uv2,
    Vector2 Uv3);

/// <summary>
/// Canonical UV strip coordinates for a single reference <c>ModelPart.Cube</c> (net.minecraft.client.model.geom.ModelPart).
/// Centralised here so entity models, block entities and special item models never hand-roll <c>u0..u4 / v0..v2</c>.
/// </summary>
internal readonly record struct ReferenceCubeUvStrip(
    float U0,
    float U1,
    float U2,
    float U22,
    float U3,
    float U4,
    float V0,
    float V1,
    float V2)
{
    /// <summary>
    /// Computes the exact UV strip boundaries defined by <c>ModelPart.Cube</c> in Minecraft 26.3:
    /// <list type="bullet">
    /// <item><description>Top row (<c>V0..V1</c>): <c>[empty: U0..U1] | [DOWN: U1..U2] | [UP: U2..U22]</c></description></item>
    /// <item><description>Side row (<c>V1..V2</c>): <c>[WEST: U0..U1] | [NORTH: U1..U2] | [EAST: U2..U3] | [SOUTH: U3..U4]</c></description></item>
    /// </list>
    /// Note that <c>U3 = U2 + depth</c> (f8 = xTexOffs + d + w + d), NOT <c>U22 + depth</c>.
    /// </summary>
    public static ReferenceCubeUvStrip Compute(int texU, int texV, float width, float height, float depth)
    {
        var u0 = (float)texU;
        var u1 = u0 + depth;
        var u2 = u1 + width;
        var u22 = u2 + width;
        var u3 = u2 + depth;
        var u4 = u3 + width;

        var v0 = (float)texV;
        var v1 = v0 + depth;
        var v2 = v1 + height;

        return new ReferenceCubeUvStrip(u0, u1, u2, u22, u3, u4, v0, v1, v2);
    }
}

/// <summary>
/// Bakes a special model (the reference <c>ModelPart</c> tree behind <c>SpecialModelRenderer</c>) into item model space.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass(
    "net.minecraft.client.model.geom.ModelPart",
    "ref://mc-26.3-analysis/src/net/minecraft/client/model/geom/ModelPart.java",
    Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror,
    Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial,
    Note = "SpecialModelRenderer ModelPart cube compiler in item space")]
internal static class SpecialModelBake
{
    public const float PixelsPerUnit = 16f;

    /// <summary>Bakes <paramref name="root"/> into item-space quads (<c>16</c> model pixels = <c>1</c> unit).</summary>
    public static void Bake(SpecialPart root, int textureWidth, int textureHeight, Action<SpecialQuad> emit)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(emit);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(textureWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(textureHeight);

        Walk(root, Matrix4x4.Identity, textureWidth, textureHeight, emit);
    }

    /// <summary>Bakes <paramref name="root"/> and appends its quads to an <see cref="ItemModelQuad"/> list.</summary>
    public static void AppendQuads(
        List<ItemModelQuad> output,
        SpecialPart root,
        int textureWidth,
        int textureHeight,
        string texture)
    {
        ArgumentNullException.ThrowIfNull(output);

        Bake(root, textureWidth, textureHeight, quad =>
        {
            // Rotate corners once into the item quad convention while preserving outward CCW winding.
            output.Add(new ItemModelQuad(
                texture,
                quad.P1,
                quad.P2,
                quad.P3,
                quad.P0,
                quad.Uv1,
                quad.Uv2,
                quad.Uv3,
                quad.Uv0,
                TintIndex: -1,
                ModelFaceDirection.North));
        });
    }

    /// <summary>Returns the exact number of quads (<c>6</c> per cube) in <paramref name="root"/>.</summary>
    public static int QuadCount(SpecialPart root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var count = root.Cubes.Count * 6;
        foreach (var child in root.Children)
        {
            count += QuadCount(child);
        }

        return count;
    }

    private static void Walk(
        SpecialPart part,
        Matrix4x4 parent,
        int textureWidth,
        int textureHeight,
        Action<SpecialQuad> emit)
    {
        var current = Pivot(part) * parent;

        foreach (var cube in part.Cubes)
        {
            EmitCube(cube, current, textureWidth, textureHeight, emit);
        }

        foreach (var child in part.Children)
        {
            Walk(child, current, textureWidth, textureHeight, emit);
        }
    }

    private static Matrix4x4 Pivot(SpecialPart part)
    {
        var pivot = part.Pivot / PixelsPerUnit;
        var radX = MathF.PI / 180f * part.RotationDegrees.X;
        var radY = MathF.PI / 180f * part.RotationDegrees.Y;
        var radZ = MathF.PI / 180f * part.RotationDegrees.Z;

        // Row-vector convention matching PoseStack.translate(pivot) * rotationZYX(z, y, x):
        // a local point is rotated X -> Y -> Z and then translated by +pivot.
        return Matrix4x4.CreateRotationX(radX)
            * Matrix4x4.CreateRotationY(radY)
            * Matrix4x4.CreateRotationZ(radZ)
            * Matrix4x4.CreateTranslation(pivot);
    }

    private static void EmitCube(
        SpecialCube cube,
        Matrix4x4 transform,
        int textureWidth,
        int textureHeight,
        Action<SpecialQuad> emit)
    {
        var min = cube.From / PixelsPerUnit;
        var max = (cube.From + cube.Size) / PixelsPerUnit;

        if (cube.Mirror)
        {
            (min.X, max.X) = (max.X, min.X);
        }

        var x0 = min.X;
        var y0 = min.Y;
        var z0 = min.Z;
        var x1 = max.X;
        var y1 = max.Y;
        var z1 = max.Z;

        var t0 = Vector3.Transform(new Vector3(x0, y0, z0), transform);
        var t1 = Vector3.Transform(new Vector3(x1, y0, z0), transform);
        var t2 = Vector3.Transform(new Vector3(x1, y1, z0), transform);
        var t3 = Vector3.Transform(new Vector3(x0, y1, z0), transform);
        var l0 = Vector3.Transform(new Vector3(x0, y0, z1), transform);
        var l1 = Vector3.Transform(new Vector3(x1, y0, z1), transform);
        var l2 = Vector3.Transform(new Vector3(x1, y1, z1), transform);
        var l3 = Vector3.Transform(new Vector3(x0, y1, z1), transform);

        var uv = ReferenceCubeUvStrip.Compute(cube.TextureU, cube.TextureV, cube.Size.X, cube.Size.Y, cube.Size.Z);
        var invW = 1f / textureWidth;
        var invH = 1f / textureHeight;

        // 6 faces in exact ModelPart.Cube order: DOWN, UP, WEST (-X), NORTH (-Z), EAST (+X), SOUTH (+Z).
        EmitQuad(l1, l0, t0, t1, uv.U2 * invW, uv.U1 * invW, uv.V0 * invH, uv.V1 * invH, cube.Mirror, emit); // DOWN
        EmitQuad(t2, t3, l3, l2, uv.U22 * invW, uv.U2 * invW, uv.V1 * invH, uv.V0 * invH, cube.Mirror, emit); // UP
        EmitQuad(t0, l0, l3, t3, uv.U1 * invW, uv.U0 * invW, uv.V1 * invH, uv.V2 * invH, cube.Mirror, emit); // WEST (-X)
        EmitQuad(t1, t0, t3, t2, uv.U2 * invW, uv.U1 * invW, uv.V1 * invH, uv.V2 * invH, cube.Mirror, emit); // NORTH (-Z)
        EmitQuad(l1, t1, t2, l2, uv.U3 * invW, uv.U2 * invW, uv.V1 * invH, uv.V2 * invH, cube.Mirror, emit); // EAST (+X)
        EmitQuad(l0, l1, l2, l3, uv.U4 * invW, uv.U3 * invW, uv.V1 * invH, uv.V2 * invH, cube.Mirror, emit); // SOUTH (+Z)
    }

    private static void EmitQuad(
        Vector3 corner0,
        Vector3 corner1,
        Vector3 corner2,
        Vector3 corner3,
        float uRight,
        float uLeft,
        float vTop,
        float vBottom,
        bool mirror,
        Action<SpecialQuad> emit)
    {
        Span<Vector3> positions = [corner0, corner1, corner2, corner3];
        Span<Vector2> uvs =
        [
            new Vector2(uRight, vTop),
            new Vector2(uLeft, vTop),
            new Vector2(uLeft, vBottom),
            new Vector2(uRight, vBottom)
        ];

        if (mirror)
        {
            (positions[0], positions[3]) = (positions[3], positions[0]);
            (positions[1], positions[2]) = (positions[2], positions[1]);
            (uvs[0], uvs[3]) = (uvs[3], uvs[0]);
            (uvs[1], uvs[2]) = (uvs[2], uvs[1]);
        }

        emit(new SpecialQuad(
            positions[0],
            positions[1],
            positions[2],
            positions[3],
            uvs[0],
            uvs[1],
            uvs[2],
            uvs[3]));
    }
}
