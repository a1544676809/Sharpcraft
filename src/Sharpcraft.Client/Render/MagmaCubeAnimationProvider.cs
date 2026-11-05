// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>MagmaCubeModel.setupAnim</c>: the eight stacked cubes spread apart by the positive squish value
/// (<c>cube[i].y = -(4 - i) * max(0, squish) * 1.7</c> in y-down model space; our pose offsets are y-up).
/// Slime has no model animation: its squish is only the renderer scale, applied by <see cref="EntityModelTransform"/>.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.slime.MagmaCubeModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/slime/MagmaCubeModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public sealed class MagmaCubeAnimationProvider(int typeId) : IEntityAnimationProvider
{
    private static readonly string[] NominalClips = ["idle"];

    private static readonly string[] CubeParts =
        ["cube0", "cube1", "cube2", "cube3", "cube4", "cube5", "cube6", "cube7"];

    public int TypeId { get; } = typeId;

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose)
    {
        var squish = MathF.Max(0f, frame.Squish);
        for (var i = 0; i < CubeParts.Length; i++)
        {
            // y-up offset: the reference's y-down value -(4 - i) becomes (i - 4).
            pose.Add(CubeParts[i], new PoseOffset(new Vector3(0f, (i - 4) * squish * 1.7f, 0f), Vector3.Zero, Vector3.Zero));
        }
    }

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        if (clip == "idle")
        {
            Apply(state, in frame, pose);
        }
    }

    private sealed class State : IEntityAnimationState
    {
    }
}
