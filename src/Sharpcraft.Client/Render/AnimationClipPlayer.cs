// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Plays a baked clip from an <see cref="AnimationState"/> clock, the way the reference
/// <c>KeyframeAnimation.apply(AnimationState, ageInTicks)</c> does: nothing while stopped, otherwise the elapsed
/// <c>(ageInTicks - startTick) * 50 ms</c> seconds into the clip at target scale 1.
/// </summary>
public static class AnimationClipPlayer
{
    /// <summary>Applies <paramref name="baked"/> to <paramref name="pose"/> when its clock is running.</summary>
    public static void Apply(AnimationState clock, KeyframeAnimation baked, float ageInTicks, EntityPose pose)
    {
        if (!clock.IsStarted)
        {
            return;
        }

        baked.Apply(clock.GetTimeInMillis(ageInTicks) / 1000f, 1f, pose);
    }
}
