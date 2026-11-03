// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>CamelModel.setupAnim</c>: the ported 1.5 s walk loop plus the 4 s idle loop. Walk time is
/// <c>walkPos * 0.1</c> and its target scale is <c>min(walkSpeed * 2.5, 1)</c>; idle time is age in seconds.
/// </summary>
public sealed class CamelAnimationProvider : IEntityAnimationProvider
{
    private static readonly string[] NominalClips = ["idle", "walk"];

    public CamelAnimationProvider(int typeId) => TypeId = typeId;

    public int TypeId { get; }

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose)
    {
        CamelAnimations.WalkBaked.Apply(frame.WalkAnimationPos * 0.1f, MathF.Min(frame.WalkAnimationSpeed * 2.5f, 1f), pose);
        CamelAnimations.IdleBaked.Apply(frame.AgeInTicks / 20f, 1f, pose);
    }

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        if (string.Equals(clip, "idle", StringComparison.Ordinal))
        {
            CamelAnimations.IdleBaked.Apply(seconds, 1f, pose);
        }
        else
        {
            CamelAnimations.WalkBaked.Apply(seconds, 1f, pose);
        }
    }

    private sealed class State : IEntityAnimationState
    {
    }
}
