// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>GhastModel.setupAnim</c> → <c>animateTentacles</c>: each of the nine tentacles sways around xRot with
/// <c>0.2·sin(ageInTicks·0.3 + index) + 0.4</c>. The body itself is not animated; charging only swaps the texture
/// (handled by <see cref="GhastModelDefinition"/>'s selector), so no animation input beyond the age is needed.
/// </summary>
/// <remarks>
/// The reference assigns the absolute tentacle xRot, and the bind xRot is 0, so the additive pose offset equals the
/// reference value. Part names match <see cref="GhastModelDefinition"/> (tentacle0 … tentacle8) and are held in a
/// precomputed array so the hot path never concatenates strings.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.ghast.GhastModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/ghast/GhastModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public sealed class GhastAnimationProvider(int typeId) : IEntityAnimationProvider
{
    // Precomputed so the hot path never concatenates strings; index i is "tentacle{i}".
    private static readonly string[] TentacleParts =
    [
        "tentacle0", "tentacle1", "tentacle2", "tentacle3", "tentacle4",
        "tentacle5", "tentacle6", "tentacle7", "tentacle8"
    ];

    private static readonly string[] NominalClips = ["idle"];

    public int TypeId { get; } = typeId;

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose) =>
        ApplyPose(frame.AgeInTicks, pose);

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        if (string.Equals(clip, "idle", StringComparison.Ordinal))
        {
            ApplyPose(seconds * 20f, pose);
        }
    }

    private static void ApplyPose(float ageInTicks, EntityPose pose)
    {
        for (var i = 0; i < TentacleParts.Length; i++)
        {
            var xRot = (0.2f * MathF.Sin((ageInTicks * 0.3f) + i)) + 0.4f;
            pose.Add(TentacleParts[i], new PoseOffset(Vector3.Zero, new Vector3(xRot, 0f, 0f), Vector3.Zero));
        }
    }

    private sealed class State : IEntityAnimationState
    {
    }
}
