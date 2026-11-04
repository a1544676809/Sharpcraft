// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Reference <c>HumanoidModel.setupAttackAnimation</c> assigns the arm's absolute part position, which is only a tiny
/// delta from the bind pivot when the body twists. The attack arm must stay attached to the torso (no outward jump).
/// </summary>
public sealed class HumanoidAttackArmTests
{
    private static Vector3 MaxOffsetX(EntityPose pose)
    {
        var mesher = new EntityModelMesher();
        var state = new EntityRenderState(1, HumanoidMobModelDefinition.ZombieEntityTypeId, 0f, 0f, 0f, 180f, 0f);
        var sprite = new SpriteRect(0, 0, 64, 64, 1);
        var geometry = mesher.Build([state], sprite, 64, 64, onlyPart: "right_arm", variants: EntityVariantTable.Builtin, pose: pose);
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        for (var o = 0; o < geometry.VertexFloats; o += 14)
        {
            var p = new Vector3(geometry.Vertices[o], geometry.Vertices[o + 1], geometry.Vertices[o + 2]);
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }

        return new Vector3(min.X, min.Y, min.Z);
    }

    [Fact]
    public void TheAttackArmStaysAttachedToTheTorso()
    {
        var provider = new HumanoidAnimationProvider(HumanoidMobModelDefinition.ZombieEntityTypeId);
        var restFrame = new EntityAnimationFrame(
            TypeId: HumanoidMobModelDefinition.ZombieEntityTypeId, AgeInTicks: 10f, WalkAnimationPos: 0f,
            WalkAnimationSpeed: 0f, Pitch: 0f, Yaw: 0f, Flap: 0f, FlapSpeed: 0f, TentacleAngle: 0f,
            IsResting: false, IsBaby: false, IsHidingInShell: false);

        var restPose = new EntityPose();
        provider.Apply(provider.CreateState(), in restFrame, restPose);

        var attackPose = new EntityPose();
        var attackFrame = restFrame with { SwingAnimation = 0.4f, SwingType = 1, SwingHand = 0, IsAggressive = true };
        provider.Apply(provider.CreateState(), in attackFrame, attackPose);

        var restInnerX = MaxOffsetX(restPose).X;
        var attackInnerX = MaxOffsetX(attackPose).X;

        // The reference only nudges the arm by the small body-twist delta; it must not shift outward by an arm width.
        Assert.InRange(MathF.Abs(attackInnerX - restInnerX), 0f, 0.08f);

        // The swing still changes the arm rotation (the attack is not a no-op).
        Assert.True(MathF.Abs(attackPose.Get("right_arm").Rotation.X - restPose.Get("right_arm").Rotation.X) > 0.05f);
    }
}
