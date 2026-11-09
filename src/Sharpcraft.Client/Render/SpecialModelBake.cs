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
/// Bakes a special model (the reference <c>ModelPart</c> tree behind <c>SpecialModelRenderer</c>) into item model space.
/// </summary>
/// <remarks>
/// The reference renders these models inside the item pipeline without touching the pose
/// (<c>ChestSpecialRenderer.submit</c>), and its geometry is pixel space divided per vertex by 16
/// (<c>ModelPart.Vertex.worldX</c>). So baking here means: walk the tree, apply pivot and pose rotation, and divide by
/// 16 — no entity flip and no model-height offset (docs/modules/item-rendering.md 3.5.18).
/// <para>
/// Face order, corner order and UV assignment follow the reference <c>ModelPart.Cube</c> table: the six faces take their
/// corner lists from the t0..t3 (near) and l0..l3 (far) corners, and each face maps its (right, top), (left, top),
/// (left, bottom), (right, bottom) rectangle corners onto the four corners in that order, reversing both when the box is
/// mirrored.
/// </para>
/// </remarks>
internal static class SpecialModelBake
{
    /// <summary>Model pixels per unit; the reference divides every vertex by this.</summary>
    public const float PixelsPerUnit = 16f;

    /// <summary>Bakes <paramref name="root"/> and reports each quad to <paramref name="emit"/>.</summary>
    /// <param name="root">Model tree to bake.</param>
    /// <param name="textureWidth">Model texture width in pixels, used to normalize U.</param>
    /// <param name="textureHeight">Model texture height in pixels, used to normalize V.</param>
    /// <param name="emit">Receives every quad, in item model space.</param>
    public static void Bake(SpecialPart root, int textureWidth, int textureHeight, Action<SpecialQuad> emit)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(emit);
        Walk(root, Matrix4x4.Identity, textureWidth, textureHeight, emit);
    }

    /// <summary>
    /// Bakes <paramref name="root"/> straight into item model quads for one atlas texture.
    /// </summary>
    /// <param name="output">Quad list to append to.</param>
    /// <param name="root">Model tree to bake.</param>
    /// <param name="textureWidth">Model texture width in pixels.</param>
    /// <param name="textureHeight">Model texture height in pixels.</param>
    /// <param name="texture">Atlas key the model samples.</param>
    public static void AppendQuads(List<ItemModelQuad> output, SpecialPart root, int textureWidth, int textureHeight, string texture)
    {
        ArgumentNullException.ThrowIfNull(output);
        // The reference renders entity models with culling disabled, so a face shows whichever way its corner ring winds.
        // The item pipelines cull back faces, so each face is emitted in both windings instead: the same visible result
        // without relaxing culling for every item (docs/modules/item-rendering.md 3.5.18).
        Bake(root, textureWidth, textureHeight, quad =>
        {
            output.Add(new ItemModelQuad(texture, quad.P0, quad.P1, quad.P2, quad.P3, quad.Uv0, quad.Uv1, quad.Uv2, quad.Uv3, TintIndex: 0, ModelFaceDirection.North));
            output.Add(new ItemModelQuad(texture, quad.P3, quad.P2, quad.P1, quad.P0, quad.Uv3, quad.Uv2, quad.Uv1, quad.Uv0, TintIndex: 0, ModelFaceDirection.North));
        });
    }
    /// <summary>Total quads a bake of <paramref name="root"/> produces (six per box).</summary>
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

    private static void Walk(SpecialPart part, Matrix4x4 parent, int textureWidth, int textureHeight, Action<SpecialQuad> emit)
    {
        var local = Pivot(part) * parent;
        foreach (var cube in part.Cubes)
        {
            EmitCube(cube, local, textureWidth, textureHeight, emit);
        }

        foreach (var child in part.Children)
        {
            Walk(child, local, textureWidth, textureHeight, emit);
        }
    }

    private static Matrix4x4 Pivot(SpecialPart part)
    {
        var pivot = part.Pivot / PixelsPerUnit;
        var matrix = Matrix4x4.CreateTranslation(pivot);
        if (part.RotationDegrees != Vector3.Zero)
        {
            const float toRadians = MathF.PI / 180f;
            // The reference composes rotationZYX(z, y, x); in row-vector order that applies Z, then Y, then X.
            matrix *=
                Matrix4x4.CreateRotationZ(part.RotationDegrees.Z * toRadians)
                * Matrix4x4.CreateRotationY(part.RotationDegrees.Y * toRadians)
                * Matrix4x4.CreateRotationX(part.RotationDegrees.X * toRadians);
        }

        // The reference PartPose.offset translates to the pivot and rotates there; box origins are already relative
        // to that pivot, so the inverse translation must NOT be applied.
        return matrix;
    }

    private static void EmitCube(SpecialCube cube, Matrix4x4 transform, int textureWidth, int textureHeight, Action<SpecialQuad> emit)
    {
        var min = cube.From;
        var max = cube.From + cube.Size;
        if (cube.Mirror)
        {
            (min.X, max.X) = (max.X, min.X);
        }

        var t0 = new Vector3(min.X, min.Y, min.Z);
        var t1 = new Vector3(max.X, min.Y, min.Z);
        var t2 = new Vector3(max.X, max.Y, min.Z);
        var t3 = new Vector3(min.X, max.Y, min.Z);
        var l0 = new Vector3(min.X, min.Y, max.Z);
        var l1 = new Vector3(max.X, min.Y, max.Z);
        var l2 = new Vector3(max.X, max.Y, max.Z);
        var l3 = new Vector3(min.X, max.Y, max.Z);

        var u0 = cube.TextureU;
        var u1 = u0 + cube.Size.Z;
        var u2 = u1 + cube.Size.X;
        var u22 = u2 + cube.Size.X;
        var u3 = u22 + cube.Size.Z;
        var u4 = u3 + cube.Size.X;
        var v0 = cube.TextureV;
        var v1 = v0 + cube.Size.Z;
        var v2 = v1 + cube.Size.Y;

        EmitFace([l1, l0, t0, t1], u1, v0, u2, v1, cube, transform, textureWidth, textureHeight, emit);
        EmitFace([t2, t3, l3, l2], u2, v1, u22, v0, cube, transform, textureWidth, textureHeight, emit);
        EmitFace([t0, l0, l3, t3], u0, v1, u1, v2, cube, transform, textureWidth, textureHeight, emit);
        EmitFace([t1, t0, t3, t2], u1, v1, u2, v2, cube, transform, textureWidth, textureHeight, emit);
        EmitFace([l1, t1, t2, l2], u2, v1, u3, v2, cube, transform, textureWidth, textureHeight, emit);
        EmitFace([l0, l1, l2, l3], u3, v1, u4, v2, cube, transform, textureWidth, textureHeight, emit);
    }

    private static void EmitFace(
        ReadOnlySpan<Vector3> corners,
        float uLeft,
        float vTop,
        float uRight,
        float vBottom,
        SpecialCube cube,
        Matrix4x4 transform,
        int textureWidth,
        int textureHeight,
        Action<SpecialQuad> emit)
    {
        Span<Vector3> baked = stackalloc Vector3[4];
        for (var index = 0; index < 4; index++)
        {
            baked[index] = Vector3.Transform(corners[index] / PixelsPerUnit, transform);
        }

        // Reference Polygon: corner 0 is (right, top), then (left, top), (left, bottom), (right, bottom).
        var uLeftNormalized = uLeft / textureWidth;
        var uRightNormalized = uRight / textureWidth;
        var vTopNormalized = vTop / textureHeight;
        var vBottomNormalized = vBottom / textureHeight;
        Span<Vector2> uvs =
        [
            new Vector2(uRightNormalized, vTopNormalized),
            new Vector2(uLeftNormalized, vTopNormalized),
            new Vector2(uLeftNormalized, vBottomNormalized),
            new Vector2(uRightNormalized, vBottomNormalized)
        ];

        if (cube.Mirror)
        {
            (baked[0], baked[3]) = (baked[3], baked[0]);
            (baked[1], baked[2]) = (baked[2], baked[1]);
            (uvs[0], uvs[3]) = (uvs[3], uvs[0]);
            (uvs[1], uvs[2]) = (uvs[2], uvs[1]);
        }

        emit(new SpecialQuad(baked[0], baked[1], baked[2], baked[3], uvs[0], uvs[1], uvs[2], uvs[3]));
    }
}
