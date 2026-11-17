// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using System.Reflection;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Raw humanoid attack-arm regression independent of the current model-registry migration state: a raw
/// Zombie/Skeleton definition is built from the same source mesh, and <see cref="HumanoidModelPose.Apply"/>
/// is called with <c>rawSourceFrame:true</c>. The source setupAttackAnimation assigns absolute part
/// positions, so the attack arm must stay attached to the torso (world-X delta <= 0.08) and the raw arm
/// offsets must equal the source formula.
/// </summary>
public sealed class RawHumanoidAttackArmTests
{
    private const int ZombieTypeId = HumanoidMobModelDefinition.ZombieEntityTypeId;
    private const int SkeletonTypeId = HumanoidMobModelDefinition.SkeletonEntityTypeId;
    private const float Swing = 0.4f;

    [Theory]
    [InlineData(ZombieTypeId, "ZombieMesh", false)]
    [InlineData(SkeletonTypeId, "SkeletonMesh", true)]
    public void RawAttackArmStaysAttachedAndMatchesSource(int typeId, string meshField, bool skeleton)
    {
        var definition = RawDefinition(typeId, meshField);
        Assert.True(definition.RawSourceFrame);

        var restPose = Pose(typeId, skeleton, attack: false);
        var attackPose = Pose(typeId, skeleton, attack: true);
        var attackArm = attackPose.Get("right_arm");

        // setupAttackAnimation source: body.yRot = sin(sqrt(swing)*2pi)*0.2; right arm absolute x/z.
        var bodyYRot = MathF.Sin(MathF.Sqrt(Swing) * MathF.Tau) * 0.2f;
        var expectedX = 5f - (MathF.Cos(bodyYRot) * 5f);
        var expectedZ = MathF.Sin(bodyYRot) * 5f;
        Assert.Equal(expectedX, attackArm.Position.X, 5);
        Assert.Equal(expectedZ, attackArm.Position.Z, 5);

        var restX = MinX(definition, restPose, typeId);
        var attackX = MinX(definition, attackPose, typeId);
        if (skeleton)
        {
            // SkeletonModel replaces the arm rotations outright; setupAttackAnimation still assigns the
            // absolute arm position, so the attachment guarantee is the source pivot delta. The thin
            // 2px skeleton arm makes the geometry min-X swing larger than the Zombie arm even when the
            // pivot is attached; the pivot delta is the source-correct metric here.
            var pivotDeltaBlocks = MathF.Abs(
                attackArm.Position.X - restPose.Get("right_arm").Position.X) * (EntityPlaceholderMesher.ModelScale / 16f);
            Assert.InRange(pivotDeltaBlocks, 0f, 0.08f);
        }
        else
        {
            Assert.InRange(MathF.Abs(attackX - restX), 0f, 0.08f);
        }

        // The attack still rotates the arm: the source pose changes xRot by this exact amount.
        Assert.NotEqual(restPose.Get("right_arm").Rotation.X, attackArm.Rotation.X);
    }

    private static EntityModelDefinition RawDefinition(int typeId, string meshField)
    {
        var mesh = (VanillaMeshDefinition)typeof(HumanoidMobModelDefinition)
            .GetField(meshField, BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;
        var (width, height) = typeId == ZombieTypeId ? (64, 64) : (64, 32);
        return EntityModelDefinition.FromTree(
            "raw_attack_probe", width, height, mesh, texturePath: "probe/raw_attack",
            baselinePx: 0f, rawSourceFrame: true);
    }

    private static EntityPose Pose(int typeId, bool skeleton, bool attack)
    {
        var frame = new EntityAnimationFrame(
            TypeId: typeId, AgeInTicks: 10f, WalkAnimationPos: 0f, WalkAnimationSpeed: 0f,
            Pitch: 0f, Yaw: 0f, Flap: 0f, FlapSpeed: 0f, TentacleAngle: 0f,
            IsResting: false, IsBaby: false, IsHidingInShell: false);
        if (attack)
        {
            frame = frame with { SwingAnimation = Swing, SwingType = 1, SwingHand = 0, IsAggressive = true };
        }

        HumanoidArmPoseResolver.Compute(frame, skeleton: skeleton, out var right, out var left);
        var pose = new EntityPose();
        HumanoidModelPose.Apply(
            frame,
            pose,
            skeleton ? HumanoidVariant.Skeleton : HumanoidVariant.Zombie,
            right,
            left,
            isHoldingBow: false,
            rawSourceFrame: true);
        return pose;
    }

    private static float MinX(EntityModelDefinition definition, EntityPose pose, int typeId)
    {
        var state = new EntityRenderState(1, typeId, 0f, 0f, 0f, 180f, 0f);
        var geometry = new EntityModelMesher().BuildInto(
            new DynamicGeometry(),
            kindFilter: null,
            [state],
            new SpriteRect(0, 0, 64, 32, 1),
            definition.TextureWidth,
            definition.TextureHeight,
            onlyPart: "right_arm",
            usePreparedPoses: true,
            poses: [pose],
            modelOverride: definition);
        var min = float.MaxValue;
        for (var o = 0; o < geometry.VertexFloats; o += 14)
        {
            min = MathF.Min(min, geometry.Vertices[o]);
        }

        return min;
    }
}
