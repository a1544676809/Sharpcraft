// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>CodModel.setupAnim</c>: only the tail fin moves. Its yRot is
/// <c>-amplitude·0.45·sin(0.6·ageInTicks)</c>, with <c>amplitude = 1.0</c> in water and <c>1.5</c> out of water (the
/// out-of-water flop is wider). The bind yRot is 0, so the value is used directly as an additive pose offset.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.fish.CodModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/fish/CodModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public sealed class CodAnimationProvider(int typeId) : IEntityAnimationProvider
{
    private const string TailFinPart = "tail_fin";

    private static readonly string[] NominalClips = ["idle", "swim"];

    public int TypeId { get; } = typeId;

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose) =>
        ApplyPose(frame.AgeInTicks, frame.IsInWater, pose);

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        // Procedural clip: "swim" previews the in-water amplitude, "idle" the wider out-of-water flop; seconds → ticks.
        switch (clip)
        {
            case "swim":
                ApplyPose(seconds * 20f, inWater: true, pose);
                break;
            case "idle":
                ApplyPose(seconds * 20f, inWater: false, pose);
                break;
        }
    }

    private static void ApplyPose(float ageInTicks, bool inWater, EntityPose pose)
    {
        var amplitude = inWater ? 1f : 1.5f;
        var yRot = -amplitude * 0.45f * MathF.Sin(0.6f * ageInTicks);
        pose.Add(TailFinPart, new PoseOffset(Vector3.Zero, new Vector3(0f, yRot, 0f), Vector3.Zero));
    }

    private sealed class State : IEntityAnimationState
    {
    }
}
