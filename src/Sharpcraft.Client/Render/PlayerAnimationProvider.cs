// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Player pose: the reference <c>PlayerRenderer</c> drives <c>PlayerModel</c>, which inherits <c>HumanoidModel.setupAnim</c>
/// unchanged and only adds the overlay parts. Our player model carries those overlays on their base parts (hat on head,
/// jacket on body, sleeves on arms, pants on legs), so they follow the pose automatically and no copy step is needed.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.PlayerModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/PlayerModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public sealed class PlayerAnimationProvider : IEntityAnimationProvider
{
    /// <summary><c>minecraft:player</c> registry id, from the registration order in <c>EntityTypes.java</c>.</summary>
    public const int PlayerEntityTypeId = 159;

    private static readonly string[] NominalClips = ["idle", "walk"];

    public PlayerAnimationProvider() => TypeId = PlayerEntityTypeId;

    public int TypeId { get; }

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose) => ApplyPlayer(frame, pose);

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        var sandbox = frame with { IsAggressive = false, SwingAnimation = 0f, SwingType = 0 };
        if (string.Equals(clip, "walk", StringComparison.Ordinal))
        {
            ApplyPlayer(sandbox with { WalkAnimationPos = seconds * 10f, WalkAnimationSpeed = 1f }, pose);
        }
        else
        {
            ApplyPlayer(sandbox with { WalkAnimationSpeed = 0f }, pose);
        }
    }

    private static void ApplyPlayer(in EntityAnimationFrame frame, EntityPose pose)
    {
        // PlayerRenderer resolves poses through AvatarRenderer, not the mob renderer rule.
        HumanoidArmPoseResolver.ComputeForAvatar(frame, out var right, out var left);
        HumanoidModelPose.Apply(
            frame,
            pose,
            HumanoidVariant.Plain,
            right,
            left,
            HumanoidArmPoseResolver.IsHoldingBow(frame.MainHandItemId));
    }

    private sealed class State : IEntityAnimationState
    {
    }
}
