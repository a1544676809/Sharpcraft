// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>SquidModel.setupAnim</c>: all eight tentacles rotate by the synced tentacle angle. Squid and glow
/// squid share this controller.
/// </summary>
public sealed class SquidAnimationProvider : IEntityAnimationProvider
{
    private static readonly string[] NominalClips = ["idle"];

    // Precomputed so the hot path never concatenates strings (docs/analysis/render/entity-animation-architecture.md 6.2).
    private static readonly string[] TentacleParts =
        ["tentacle0", "tentacle1", "tentacle2", "tentacle3", "tentacle4", "tentacle5", "tentacle6", "tentacle7"];

    public SquidAnimationProvider(int typeId) => TypeId = typeId;

    public int TypeId { get; }

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose)
    {
        for (var i = 0; i < TentacleParts.Length; i++)
        {
            pose.Add(TentacleParts[i], new PoseOffset(Vector3.Zero, new Vector3(frame.TentacleAngle, 0f, 0f), Vector3.Zero));
        }
    }

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose) =>
        Apply(state, frame, pose);

    private sealed class State : IEntityAnimationState
    {
    }
}
