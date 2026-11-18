// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// ArmorStand has no walk animation; its six synced pose groups drive head/body/arms/legs, and body pose also
/// rotates the two body sticks and shoulder stick. The base plate yaw is the source's -state.yRot.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.object.armorstand.ArmorStandModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/object/armorstand/ArmorStandModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public sealed class ArmorStandAnimationProvider : IEntityAnimationProvider
{
    public int TypeId => ArmorStandModelDefinition.ArmorStandEntityTypeId;

    public IReadOnlyList<string> Clips => [];

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose)
    {
        pose.Set("head", Rot(frame.HeadPose));
        pose.Set("body", Rot(frame.BodyPose));
        pose.Set("left_arm", Rot(frame.LeftArmPose));
        pose.Set("right_arm", Rot(frame.RightArmPose));
        pose.Set("left_leg", Rot(frame.LeftLegPose));
        pose.Set("right_leg", Rot(frame.RightLegPose));

        var body = Rot(frame.BodyPose);
        pose.Set("left_body_stick", body);
        pose.Set("right_body_stick", body);
        pose.Set("shoulder_stick", body);
        pose.Set("base_plate", Rot(new Vector3(0f, -frame.Yaw, 0f)));
    }

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose) =>
        Apply(state, frame, pose);

    private static PoseOffset Rot(Vector3 degrees) =>
        new(Vector3.Zero, degrees * (MathF.PI / 180f), Vector3.Zero);

    private sealed class State : IEntityAnimationState
    {
    }
}
