// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Source-frame pose of a witch's held item, ported from WitchItemLayer.applyTranslation (potion branch) and
/// CrossedArmsItemLayer.applyTranslation (non-potion branch). The pose is composed in y-down source model space
/// (pivot/16 + rotationZYX) and returned as a System.Numerics row-vector matrix, before the living-entity flip and
/// before the item display transform. Verified against witch-potion-pose.reference.txt (jar probe) keyed matrices.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.entity.layers.WitchItemLayer", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/entity/layers/WitchItemLayer.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "potion + crossed-arms pose chains; bind-pose nose override matches setupAnim(isHoldingItem)")]
public static class WitchHeldItemFrame
{
    private const float Rad = MathF.PI / 180f;

    /// <summary>
    /// Root y-offset of the baked source witch model in model pixels (jar probe witch-potion-pose.reference.txt #PART
    /// root y=1.501000047). The reference WitchModel root carries this so scale(-1,-1,1)+translate(0,-1.501,0) places
    /// it correctly; the pose chain must include it to match the jar frame before the flip is applied separately.
    /// </summary>
    private const float RootBaselinePx = 1.501f;

    /// <summary>
    /// POTION branch: root.TR * head.TR * nose.TR * T(0.0625,0.25,0) * Z180 * X140 * Z10 * X180.
    /// The nose uses its held-item bind state (pivot (0,1,-1.5), xRot -0.9) from setupAnim when isHoldingItem.
    /// </summary>
    public static Matrix4x4 PotionSource(EntityModelPartNode root) => ComposeSource(root, potion: true);

    /// <summary>CROSS branch: root.TR * arms.TR * X(0.75rad) * S(1.07) * T(0,0.13,-0.34) * X(PI).</summary>
    public static Matrix4x4 CrossSource(EntityModelPartNode root) => ComposeSource(root, potion: false);

    private static Matrix4x4 ComposeSource(EntityModelPartNode root, bool potion)
    {
        ArgumentNullException.ThrowIfNull(root);
        var head = Find(root, "head") ?? throw new ArgumentException("witch model missing head", nameof(root));
        var armPart = potion
            ? null
            : Find(root, "arms") ?? throw new ArgumentException("witch model missing arms", nameof(root));

        // JOML column-major composition, transposed to System.Numerics row vectors at the end (same contract as
        // HumanoidHeldHandFrame.Source). The baked source root carries a y baseline (1.501 px) which the C# DSL root
        // stores as 0; add it so the composed pose matches the jar probe frame. Head is zero in the bind pose.
        var rootColumn = ColumnMultiply(
            ColumnTranslation(new Vector3(root.PivotX / 16f, (root.PivotY + RootBaselinePx) / 16f, root.PivotZ / 16f)),
            ColumnRotation(new Vector3(root.RotationX, root.RotationY, root.RotationZ)));
        var column = ColumnMultiply(rootColumn, ColumnTR(head));
        if (potion)
        {
            // setupAnim(isHoldingItem): nose absolute pivot (0,1,-1.5), xRot -0.9, zRot 2.5deg wobble.
            var noseColumn = ColumnMultiply(
                ColumnTranslation(new Vector3(0f, 1f / 16f, -1.5f / 16f)),
                ColumnRotation(new Vector3(-0.9f, 0f, 2.5f * Rad)));
            column = ColumnMultiply(column, noseColumn);
            column = ColumnMultiply(column, ColumnTranslation(new Vector3(0.0625f, 0.25f, 0f)));
            column = ColumnMultiply(column, ColumnRotation(new Vector3(0f, 0f, 180f * Rad)));
            column = ColumnMultiply(column, ColumnRotation(new Vector3(140f * Rad, 0f, 0f)));
            column = ColumnMultiply(column, ColumnRotation(new Vector3(0f, 0f, 10f * Rad)));
            column = ColumnMultiply(column, ColumnRotation(new Vector3(180f * Rad, 0f, 0f)));
        }
        else
        {
            column = ColumnMultiply(column, ColumnTR(armPart!));
            column = ColumnMultiply(column, ColumnRotation(new Vector3(0.75f, 0f, 0f)));
            column = ColumnMultiply(column, ColumnScale(1.07f));
            column = ColumnMultiply(column, ColumnTranslation(new Vector3(0f, 0.13f, -0.34f)));
            column = ColumnMultiply(column, ColumnRotation(new Vector3(MathF.PI, 0f, 0f)));
        }

        return ToRowMatrix(column);
    }

    private static Matrix4x4 ColumnTR(EntityModelPartNode p)
    {
        var m = ColumnTranslation(new Vector3(p.PivotX / 16f, p.PivotY / 16f, p.PivotZ / 16f));
        if (p.RotationX != 0f || p.RotationY != 0f || p.RotationZ != 0f)
        {
            m = ColumnMultiply(m, ColumnRotation(new Vector3(p.RotationX, p.RotationY, p.RotationZ)));
        }

        return m;
    }

    private static Matrix4x4 ColumnTranslation(Vector3 t) =>
        new(1f, 0f, 0f, t.X, 0f, 1f, 0f, t.Y, 0f, 0f, 1f, t.Z, 0f, 0f, 0f, 1f);

    private static Matrix4x4 ColumnScale(float s) =>
        new(s, 0f, 0f, 0f, 0f, s, 0f, 0f, 0f, 0f, s, 0f, 0f, 0f, 0f, 1f);

    private static Matrix4x4 ColumnRotation(Vector3 rotation)
    {
        var (sx, cx) = (MathF.Sin(rotation.X), MathF.Cos(rotation.X));
        var (sy, cy) = (MathF.Sin(rotation.Y), MathF.Cos(rotation.Y));
        var (sz, cz) = (MathF.Sin(rotation.Z), MathF.Cos(rotation.Z));
        return new Matrix4x4(
            cz * cy, -sz * cx + cz * sy * sx, sz * sx + cz * sy * cx, 0f,
            sz * cy, cz * cx + sz * sy * sx, -cz * sx + sz * sy * cx, 0f,
            -sy, cy * sx, cy * cx, 0f,
            0f, 0f, 0f, 1f);
    }

    private static Matrix4x4 ColumnMultiply(Matrix4x4 a, Matrix4x4 b) => new(
        a.M11 * b.M11 + a.M12 * b.M21 + a.M13 * b.M31 + a.M14 * b.M41,
        a.M11 * b.M12 + a.M12 * b.M22 + a.M13 * b.M32 + a.M14 * b.M42,
        a.M11 * b.M13 + a.M12 * b.M23 + a.M13 * b.M33 + a.M14 * b.M43,
        a.M11 * b.M14 + a.M12 * b.M24 + a.M13 * b.M34 + a.M14 * b.M44,
        a.M21 * b.M11 + a.M22 * b.M21 + a.M23 * b.M31 + a.M24 * b.M41,
        a.M21 * b.M12 + a.M22 * b.M22 + a.M23 * b.M32 + a.M24 * b.M42,
        a.M21 * b.M13 + a.M22 * b.M23 + a.M23 * b.M33 + a.M24 * b.M43,
        a.M21 * b.M14 + a.M22 * b.M24 + a.M23 * b.M34 + a.M24 * b.M44,
        a.M31 * b.M11 + a.M32 * b.M21 + a.M33 * b.M31 + a.M34 * b.M41,
        a.M31 * b.M12 + a.M32 * b.M22 + a.M33 * b.M32 + a.M34 * b.M42,
        a.M31 * b.M13 + a.M32 * b.M23 + a.M33 * b.M33 + a.M34 * b.M43,
        a.M31 * b.M14 + a.M32 * b.M24 + a.M33 * b.M34 + a.M34 * b.M44,
        a.M41 * b.M11 + a.M42 * b.M21 + a.M43 * b.M31 + a.M44 * b.M41,
        a.M41 * b.M12 + a.M42 * b.M22 + a.M43 * b.M32 + a.M44 * b.M42,
        a.M41 * b.M13 + a.M42 * b.M23 + a.M43 * b.M33 + a.M44 * b.M43,
        a.M41 * b.M14 + a.M42 * b.M24 + a.M43 * b.M34 + a.M44 * b.M44);

    private static Matrix4x4 ToRowMatrix(Matrix4x4 c)
    {
        var result = Matrix4x4.Transpose(new Matrix4x4(
            c.M11, c.M12, c.M13, 0f,
            c.M21, c.M22, c.M23, 0f,
            c.M31, c.M32, c.M33, 0f,
            c.M14, c.M24, c.M34, 1f));
        result.M14 = result.M24 = result.M34 = 0f;
        result.M41 = c.M14;
        result.M42 = c.M24;
        result.M43 = c.M34;
        return result;
    }

    private static EntityModelPartNode? Find(EntityModelPartNode node, string name)
    {
        if (string.Equals(node.Name, name, StringComparison.Ordinal)) return node;
        foreach (var child in node.ChildArray)
        {
            if (Find(child, name) is { } found) return found;
        }
        return null;
    }
}
