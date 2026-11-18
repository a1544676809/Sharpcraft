// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Source-frame humanoid held-hand chain from ItemInHandLayer.submitArmWithItem:
/// H = translateToHand(root) * translateToHand(arm), then XP(-90), YP(180), T(handOffset).
/// Returned as a System.Numerics row-vector matrix in reference render units (pixel/16), so callers
/// can convert with ReferenceFrame.Frame(modelScale) before applying the entity transform.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.entity.layers.ItemInHandLayer", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/entity/layers/ItemInHandLayer.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class HumanoidHeldHandFrame
{
    /// <summary>Humanoid models whose held items use the source frame; CopperGolem and other raw non-humanoids keep the legacy adapter.</summary>
    public static bool IsHumanoid(int typeId) => typeId is
        159 // player
        or HumanoidMobModelDefinition.ZombieEntityTypeId
        or HumanoidMobModelDefinition.DrownedEntityTypeId
        or HumanoidMobModelDefinition.HuskEntityTypeId
        or HumanoidMobModelDefinition.SkeletonEntityTypeId
        or HumanoidMobModelDefinition.StrayEntityTypeId
        or HumanoidMobModelDefinition.BoggedEntityTypeId
        or HumanoidMobModelDefinition.WitherSkeletonEntityTypeId
        or HumanoidMobModelDefinition.EndermanEntityTypeId
        or ZombieVillagerModelDefinition.ZombieVillagerEntityTypeId
        or GiantModelDefinition.GiantEntityTypeId
        or VillagerModelDefinition.VillagerEntityTypeId
        or VillagerModelDefinition.WanderingTraderEntityTypeId
        or IllagerModelDefinition.VindicatorEntityTypeId
        or IllagerModelDefinition.EvokerEntityTypeId
        or IllagerModelDefinition.PillagerEntityTypeId
        or IllagerModelDefinition.IllusionerEntityTypeId
        or PiglinModelDefinition.PiglinEntityTypeId
        or PiglinModelDefinition.PiglinBruteEntityTypeId
        or PiglinModelDefinition.ZombifiedPiglinEntityTypeId
        or WitchModelDefinition.WitchEntityTypeId
        or ArmorStandModelDefinition.ArmorStandEntityTypeId;

    /// <summary>Source frame mapped into the production raw model frame, before the entity transform.</summary>
    public static Matrix4x4 SourceWorld(
        EntityModelDefinition definition,
        string armName,
        EntityPose? pose,
        bool offhand,
        bool baby) =>
        Source(definition, armName, pose, offhand, baby) * ReferenceFrame.Frame(baby ? 0.5f : 0.9f);

    /// <summary>Source hand frame for the given raw humanoid part, including T_off but excluding STAB/use/item display.</summary>
    public static Matrix4x4 Source(
        EntityModelDefinition definition,
        string armName,
        EntityPose? pose,
        bool offhand,
        bool baby)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var root = definition.Compiled?.Root ?? throw new ArgumentException("definition has no compiled tree", nameof(definition));
        var arm = Find(root, armName) ?? throw new ArgumentException($"missing part {armName}", nameof(armName));

        var rootPose = pose?.Get(root.Name) ?? PoseOffset.Zero;
        var armPose = pose?.Get(arm.PoseSource ?? arm.Name) ?? PoseOffset.Zero;
        var rootPivot = (rootPose.Position + new Vector3(root.PivotX, root.PivotY, root.PivotZ)) / 16f;
        var armPivot = (armPose.Position + new Vector3(arm.PivotX, arm.PivotY, arm.PivotZ)) / 16f;
        var rootRot = rootPose.Rotation + new Vector3(root.RotationX, root.RotationY, root.RotationZ);
        var armRot = armPose.Rotation + new Vector3(arm.RotationX, arm.RotationY, arm.RotationZ);

        // JOML chain in column-major form; transpose once at the end for System.Numerics row vectors.
        var hColumn = ColumnMultiply(
            ColumnMultiply(ColumnTranslation(rootPivot), ColumnRotation(rootRot)),
            ColumnMultiply(ColumnTranslation(armPivot), ColumnRotation(armRot)));
        var off = baby
            ? new Vector3(0f, 1f / 16f, -4.5f / 16f)
            : new Vector3((offhand ? -1f : 1f) / 16f, 2f / 16f, -10f / 16f);
        var pColumn = ColumnMultiply(
            ColumnMultiply(
                ColumnMultiply(hColumn, ColumnRotation(new Vector3(-MathF.PI / 2f, 0f, 0f))),
                ColumnRotation(new Vector3(0f, MathF.PI, 0f))),
            ColumnTranslation(off));
        return new Matrix4x4(
            pColumn.M11, pColumn.M12, pColumn.M13, 0f,
            pColumn.M21, pColumn.M22, pColumn.M23, 0f,
            pColumn.M31, pColumn.M32, pColumn.M33, 0f,
            pColumn.M14, pColumn.M24, pColumn.M34, 1f);
    }

    private static Matrix4x4 ColumnTranslation(Vector3 translation) =>
        new(1f, 0f, 0f, translation.X,
            0f, 1f, 0f, translation.Y,
            0f, 0f, 1f, translation.Z,
            0f, 0f, 0f, 1f);

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

    private static Matrix4x4 ColumnMultiply(Matrix4x4 a, Matrix4x4 b)
    {
        return new Matrix4x4(
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
    }

    private static EntityModelPartNode? Find(EntityModelPartNode node, string name)
    {
        if (string.Equals(node.Name, name, StringComparison.Ordinal))
        {
            return node;
        }

        foreach (var child in node.ChildArray)
        {
            if (Find(child, name) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
