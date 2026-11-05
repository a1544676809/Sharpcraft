// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>TropicalFishSmallModel.setupAnim</c> / <c>TropicalFishLargeModel.setupAnim</c>: the two fish shapes share
/// the exact same formula and only the tail sways. Its yRot is <c>-amplitude·0.45·sin(0.6·ageInTicks)</c> with
/// <c>amplitude = 1.0</c> in water and <c>1.5</c> out of water (the wider out-of-water flop). The tail bind yRot is 0, so
/// the additive pose offset (<c>delta = assigned − bind</c>) is the assigned value itself; rotation Y keeps its sign
/// through <see cref="EntityPose"/> (the mesher applies the reference-space conjugation via <c>PartFrame</c>).
/// </summary>
/// <remarks>
/// The small and large shapes differ only in geometry/texture; both tails are named <c>tail</c> and use the same formula,
/// so one provider animates every tropical-fish pattern (the render state's pattern selects the shape, not a different
/// animation).
/// <para>
/// Not expressible here (renderer-level, reported as blocked): <c>TropicalFishRenderer.setupRotations</c> adds a
/// whole-entity yaw wobble <c>rotateDegrees(YP, 4.3·sin(0.6·ageInTicks))</c>, and out of water a pose-stack
/// <c>translate(0.2, 0.1, 0)</c> plus <c>rotateDegrees(ZP, 90°)</c> flop. Those are pose-stack transforms, not model-part
/// pose offsets, so they cannot be reproduced through <see cref="EntityPose"/> without approximation.
/// </para>
/// </remarks>
public sealed class TropicalFishAnimationProvider : IEntityAnimationProvider
{
    private const string TailPart = "tail";

    // Reference amplitude multiplier: 1.0 in water, 1.5 out of water.
    private const float InWaterAmplitude = 1f;
    private const float OutOfWaterAmplitude = 1.5f;

    private static readonly string[] NominalClips = ["idle", "swim"];

    /// <summary>Creates the provider for the tropical-fish entity type id.</summary>
    public TropicalFishAnimationProvider()
        : this(TropicalFishModelDefinition.TropicalFishEntityTypeId)
    {
    }

    /// <param name="typeId">The tropical-fish entity type id (normally <see cref="TropicalFishModelDefinition.TropicalFishEntityTypeId"/>).</param>
    public TropicalFishAnimationProvider(int typeId) => TypeId = typeId;

    public int TypeId { get; }

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose) =>
        ApplyPose(frame.AgeInTicks, frame.IsInWater, pose);

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        // Procedural clip: "swim" previews the in-water amplitude, "idle" the wider out-of-water flop; seconds -> ticks.
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
        var amplitude = inWater ? InWaterAmplitude : OutOfWaterAmplitude;
        var yRot = -amplitude * 0.45f * MathF.Sin(0.6f * ageInTicks);
        pose.Add(TailPart, new PoseOffset(Vector3.Zero, new Vector3(0f, yRot, 0f), Vector3.Zero));
    }

    /// <summary>Tropical fish have no stateful clocks; the tail sway depends only on the frame's age and water flag.</summary>
    private sealed class State : IEntityAnimationState
    {
    }
}
