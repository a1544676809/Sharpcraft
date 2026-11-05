// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference ArmadilloAnimation data, written as behaviour data (the reference hardcodes these in Java, not JSON).
/// The four clips drive the adult armadillo: roll-up, walk, peek and roll-out. Part names match
/// <see cref="ArmadilloModelDefinition"/>; positions use the reference Y inversion and angles are radians,
/// both applied through <see cref="KeyframeAnimations"/>.
/// </summary>
/// <remarks>
/// The reference drives the walk clip through <c>applyWalk(animationPos, animationSpeed, 16.5, 2.5)</c>, i.e. elapsed
/// seconds = animationPos * 0.825 and target scale = min(animationSpeed * 2.5, 1); it skips the walk clip while the
/// armadillo hides in its shell. Roll-up, roll-out and peek are one-shot <c>AnimationState</c> clips sampled at the
/// entity age.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.animation.definitions.ArmadilloAnimation", "ref://mc-26.3-analysis/src/net/minecraft/client/animation/definitions/ArmadilloAnimation.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class ArmadilloAnimations
{
    public static readonly AnimationDefinition RollUp = BuildRollUp();
    public static readonly AnimationDefinition Walk = BuildWalk();
    public static readonly AnimationDefinition Peek = BuildPeek();
    public static readonly AnimationDefinition RollOut = BuildRollOut();

    private static readonly IReadOnlySet<string> PartNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "body", "tail", "head", "right_hind_leg", "left_hind_leg", "right_front_leg",
        "left_front_leg", "cube",
    };

    public static readonly KeyframeAnimation RollUpBaked = RollUp.Bake(PartNames);
    public static readonly KeyframeAnimation WalkBaked = Walk.Bake(PartNames);
    public static readonly KeyframeAnimation PeekBaked = Peek.Bake(PartNames);
    public static readonly KeyframeAnimation RollOutBaked = RollOut.Bake(PartNames);

    private static AnimationKeyframe RotL(float t, float x, float y, float z) =>
        new(t, KeyframeAnimations.DegreeVec(x, y, z), AnimationInterpolation.Linear);

    private static AnimationKeyframe RotC(float t, float x, float y, float z) =>
        new(t, KeyframeAnimations.DegreeVec(x, y, z), AnimationInterpolation.CatmullRom);

    private static AnimationKeyframe PosL(float t, float x, float y, float z) =>
        new(t, KeyframeAnimations.PosVec(x, y, z), AnimationInterpolation.Linear);

    private static AnimationKeyframe PosC(float t, float x, float y, float z) =>
        new(t, KeyframeAnimations.PosVec(x, y, z), AnimationInterpolation.CatmullRom);

    private static AnimationChannel R(params AnimationKeyframe[] frames) => new(AnimationTarget.Rotation, frames);
    private static AnimationChannel P(params AnimationKeyframe[] frames) => new(AnimationTarget.Position, frames);

    private static AnimationDefinition BuildRollUp() =>
        new(0.5f, false, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["body"] =
            [
                R(RotL(0f, 0f, 0f, 0f), RotL(0.1667f, 0f, 0f, 0f)),
                P(PosL(0f, 0f, 0f, 0f), PosL(0.1667f, 0f, 5f, 0f), PosL(0.2083f, 0f, 6f, -1f), PosL(0.25f, 0f, 6f, -1f), PosL(0.375f, 0f, -1f, -1f))
            ],
            ["tail"] =
            [
                R(RotL(0f, 0f, 0f, 0f), RotL(0.1667f, 0f, 0f, 0f)),
                P(PosL(0f, 0f, 0f, 0f), PosL(0.1667f, 0f, 0f, 0f), PosL(0.2083f, 0f, 0f, -2f), PosL(0.25f, 0f, 0f, -2f))
            ],
            ["head"] =
            [
                R(RotL(0f, 0f, 0f, 0f), RotL(0.1667f, 17.5f, 0f, 0f), RotL(0.25f, -72.5f, 0f, 0f)),
                P(PosL(0f, 0f, 0f, 0f), PosL(0.1667f, 0f, -1f, 0f), PosL(0.2083f, 0f, 2f, 1f), PosL(0.25f, 0f, 2f, 1f), PosL(0.2917f, 0f, 2f, 6f), PosL(0.375f, 0f, 2f, 7f))
            ],
            ["right_hind_leg"] =
            [
                R(RotL(0f, 0f, 0f, 0f), RotL(0.1667f, -35f, 0f, 0f)),
                P(PosL(0f, 0f, 0f, 0f), PosL(0.1667f, 0f, 5f, -2f), PosL(0.1875f, 0f, 8f, -3f), PosL(0.2083f, 0f, 8f, -2f), PosL(0.25f, 0f, 8f, -2f), PosL(0.375f, 1f, 3f, -6f))
            ],
            ["left_hind_leg"] =
            [
                R(RotL(0f, 0f, 0f, 0f), RotL(0.1667f, -45f, 0f, 0f)),
                P(PosL(0f, 0f, 0f, 0f), PosL(0.1667f, 0f, 5f, -2f), PosL(0.1875f, 0f, 8f, -3f), PosL(0.2083f, 0f, 8f, -2f), PosL(0.25f, 0f, 8f, -2f), PosL(0.375f, -1f, 3f, -6f))
            ],
            ["right_front_leg"] =
            [
                R(RotL(0f, 0f, 0f, 0f), RotL(0.1667f, -27.5f, 0f, 0f), RotL(0.2083f, -32.5f, 0f, 0f), RotL(0.25f, -85f, 0f, 0f)),
                P(PosL(0f, 0f, 0f, 0f), PosL(0.1667f, 0f, 5f, 0f), PosL(0.1875f, -0.5f, 11.5f, 0.5f), PosL(0.2083f, -1f, 9f, -1f), PosL(0.25f, -1f, 9f, -1f), PosL(0.375f, -1f, 2f, 3f))
            ],
            ["left_front_leg"] =
            [
                R(RotL(0f, 0f, 0f, 0f), RotL(0.1667f, -12.5f, 0f, 0f), RotL(0.2083f, -35f, 0f, 0f), RotL(0.25f, -85f, 0f, 0f)),
                P(PosL(0f, 0f, 0f, 0f), PosL(0.1667f, 0f, 5f, 0f), PosL(0.1875f, 0.5f, 11.5f, 0.5f), PosL(0.2083f, 1f, 9f, -1f), PosL(0.25f, 1f, 9f, -1f), PosL(0.375f, 1f, 2f, 3f))
            ],
            ["cube"] =
            [
                R(RotL(0f, 0f, 0f, 0f), RotL(0.1667f, 0f, 0f, 0f), RotL(0.4167f, -2.5f, 0f, 0f), RotL(0.4583f, 5f, 0f, 0f), RotL(0.5f, 0f, 0f, 0f)),
                P(PosL(0f, 0f, 3f, 0f), PosL(0.1667f, 0f, 8f, 0f), PosL(0.2083f, 0f, 7f, 0f), PosL(0.25f, 0f, 7f, 0f), PosL(0.375f, 0f, 0f, 0f), PosL(0.4167f, 0f, 1f, 0f), PosL(0.4583f, 0f, 0.6f, 0f), PosL(0.5f, 0f, 0f, 0f))
            ],
        });

    private static AnimationDefinition BuildWalk() =>
        new(1.4583f, true, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["body"] =
            [
                R(RotC(0f, 0f, 0f, 0f), RotC(0.25f, 0f, 0f, 4.6f), RotC(0.2917f, 0f, 0f, 6.81f), RotC(0.5f, 0f, 0f, 0f), RotC(0.7083f, 0f, 0f, 0f), RotC(0.9583f, 0f, 0f, -4.6f), RotC(1f, 0f, 0f, -6.89f), RotC(1.25f, 0f, 0f, 0f), RotC(1.4583f, 0f, 0f, 0f)),
                P(PosC(0f, 0f, 0f, 0f), PosC(0.25f, 0f, -0.2f, 0f), PosC(0.5f, 0f, 0f, 0f), PosC(0.7083f, 0f, 0f, 0f), PosC(0.9583f, 0f, -0.2f, 0f), PosC(1.25f, 0f, 0f, 0f), PosC(1.4583f, 0f, 0f, 0f))
            ],
            ["tail"] = [R(RotL(0f, 0f, 0f, 0f), RotL(0.5f, -9.17f, 0f, 0f), RotL(0.75f, 5f, 0f, 0f), RotL(1.2083f, -8.24f, 0f, 0f), RotL(1.4583f, 0f, 0f, 0f))],
            ["right_hind_leg"] =
            [
                R(RotL(0f, -50f, 0f, 0f), RotL(0.25f, 0f, 0f, 0f), RotL(0.5f, 0f, 0f, 0f), RotL(0.75f, 50f, 0f, 0f), RotL(1f, 50f, 0f, 0f), RotL(1.2917f, -20f, 0f, 0f), RotL(1.4583f, -50f, 0f, 0f)),
                P(PosL(0f, 0f, 0f, 0f), PosL(0.25f, 0f, 0f, -0.5f), PosL(0.5f, 0f, 0f, 0f), PosL(1f, 0f, 0f, -0.5f), PosL(1.2917f, 0f, 1f, -0.18f), PosL(1.4583f, 0f, 0f, 0f))
            ],
            ["left_hind_leg"] =
            [
                R(RotL(0f, 50f, 0f, 0f), RotL(0.25f, 50f, 0f, 0f), RotL(0.5417f, -20f, 0f, 0f), RotL(0.7083f, -50f, 0f, 0f), RotL(0.9583f, 0f, 0f, 0f), RotL(1.2083f, 0f, 0f, 0f), RotL(1.4583f, 50f, 0f, 0f)),
                P(PosL(0f, 0f, 0f, -0.25f), PosL(0.25f, 0f, 0f, -0.5f), PosL(0.5417f, 0f, 1f, -0.18f), PosL(0.7083f, 0f, 0f, 0f), PosL(0.9583f, 0f, 0f, -0.5f), PosL(1.2083f, 0f, 0f, 0f), PosL(1.4583f, 0f, 0f, -0.25f))
            ],
            ["right_front_leg"] =
            [
                R(RotL(0f, 50f, 0f, 0f), RotL(0.2917f, 50f, 0f, 0f), RotL(0.5417f, -20f, 0f, 0f), RotL(0.7083f, -50f, 0f, 0f), RotL(0.9583f, 0f, 0f, 0f), RotL(1.2083f, 0f, 0f, 0f), RotL(1.4583f, 50f, 0f, 0f)),
                P(PosL(0f, 0f, 0f, -0.25f), PosL(0.25f, 0f, 0f, -0.5f), PosL(0.5417f, 0f, 1f, -0.18f), PosL(0.7083f, 0f, 0f, 0f), PosL(0.9583f, 0f, 0f, -0.5f), PosL(1.2083f, 0f, 0f, 0f), PosL(1.4583f, 0f, 0f, -0.25f))
            ],
            ["left_front_leg"] =
            [
                R(RotL(0f, -50f, 0f, 0f), RotL(0.25f, 0f, 0f, 0f), RotL(0.5f, 0f, 0f, 0f), RotL(0.75f, 50f, 0f, 0f), RotL(1f, 50f, 0f, 0f), RotL(1.2917f, -20f, 0f, 0f), RotL(1.4583f, -50f, 0f, 0f)),
                P(PosL(0f, 0f, 0f, 0f), PosL(0.25f, 0f, 0f, -0.5f), PosL(0.5f, 0f, 0f, 0f), PosL(1f, 0f, 0f, -0.5f), PosL(1.2917f, 0f, 1f, -0.18f), PosL(1.4583f, 0f, 0f, 0f))
            ],
            ["head"] = [R(RotL(0f, 0f, 0f, 0f), RotL(0.25f, 0f, 0f, -2.5f), RotL(0.5f, 0f, 0f, 0f), RotL(0.7083f, 0f, 0f, 0f), RotL(1f, 0f, 0f, 2.5f), RotL(1.25f, 0f, 0f, 0f), RotL(1.4583f, 0f, 0f, 0f))],
        });

    private static AnimationDefinition BuildPeek() =>
        new(2.5f, false, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["head"] =
            [
                R(RotL(0f, -70f, 0f, 0f), RotL(0.15f, -65f, 0f, 0f), RotL(0.4f, -50f, 0f, 0f), RotL(0.5f, 0f, 0f, 0f), RotL(0.7f, 0f, 0f, 0f), RotC(0.9f, -7.5f, 0f, 45f), RotL(1.15f, -7.5f, 0f, 45f), RotC(1.3f, -0.8639f, -1.4959f, -39.1287f), RotL(1.6f, -0.8639f, -1.4959f, -39.1287f), RotL(1.75f, 0f, 0f, 0f), RotL(1.8f, -25f, 0f, 0f), RotL(1.85f, -70f, 0f, 0f)),
                P(PosL(0f, 0f, 1f, 7f), PosL(0.05f, 0f, 1f, 4f), PosL(0.15f, 0f, 1f, 4f), PosL(0.25f, 0f, 1f, 5f), PosL(0.35f, 0f, 1f, 0.2f), PosL(0.4f, 0f, 1f, 0.2f), PosL(0.5f, 0f, 2.1f, 1.2f), PosL(0.6f, 0f, 1.1f, 0.2f), PosL(0.7f, 0f, 1.1f, 0.2f), PosL(1.75f, 0f, 1.1f, 0.2f), PosL(1.8f, 0f, 0.1f, 0.2f), PosL(1.95f, 0f, 0.1f, 5.2f), PosL(2f, 0f, 0.1f, 7.2f), PosL(2.15f, 0f, 0.1f, 8.2f), PosL(2.3f, 0f, 0.1f, 5.2f))
            ],
            ["right_hind_leg"] = [P(PosL(0f, 0f, 3f, -2f))],
            ["left_hind_leg"] = [P(PosL(0f, 0f, 3f, -2f))],
            ["right_front_leg"] =
            [
                R(RotL(0f, -90f, 0f, 0f), RotC(0.5833f, -90f, 0f, 0f), RotC(0.8333f, -45f, 0f, 0f), RotC(1f, -45f, 0f, 0f), RotC(1.75f, -45f, 0f, 0f), RotC(1.8f, -90f, 0f, 0f), RotC(1.95f, -90f, 0f, 0f)),
                P(PosL(0f, -1f, 2f, 2f), PosC(0.5833f, -1f, 2f, 2f), PosC(0.6667f, -1f, 2f, -2f), PosC(0.8333f, -1f, 2f, -1f), PosC(1f, -1f, 2f, -1f), PosC(1.75f, -1f, 2f, -1f), PosC(1.95f, -1f, 2f, -1f), PosC(2f, -1f, 2f, 3f), PosC(2.15f, -1f, 3f, 4f))
            ],
            ["left_front_leg"] =
            [
                R(RotL(0f, -90f, 0f, 0f), RotC(0.6667f, -90f, 0f, 0f), RotC(0.8333f, -45f, 0f, 0f), RotC(1f, -45f, 0f, 0f), RotC(1.75f, -45f, 0f, 0f), RotC(1.8f, -90f, 0f, 0f), RotC(1.95f, -90f, 0f, 0f)),
                P(PosL(0f, 1f, 2f, 2f), PosC(0.6667f, 1f, 2f, 2f), PosC(0.75f, 1f, 2f, -2f), PosC(0.8333f, 1f, 2f, -1f), PosC(1f, 1f, 2f, -1f), PosC(1.75f, 1f, 2f, -1f), PosC(1.95f, 1f, 2f, -1f), PosC(2f, 1f, 2f, 3f), PosC(2.15f, 1f, 3f, 4f))
            ],
            ["cube"] =
            [
                R(RotL(0f, 0f, 0f, 0f), RotL(0.25f, 0f, 0f, 0f), RotL(0.35f, 15f, 0f, 0f), RotL(0.5f, -7.5f, 0f, 0f), RotL(0.6f, 0f, 0f, 0f), RotL(2f, 0f, 0f, 0f), RotL(2.05f, -17.5f, 0f, 0f), RotL(2.15f, -25f, 0f, 0f), RotL(2.25f, 0f, 0f, 0f), RotL(2.3f, 12.5f, 0f, 0f), RotL(2.5f, 0f, 0f, 0f)),
                P(PosL(0.25f, 0f, 0f, 0f), PosL(0.35f, 0f, 1.6f, 0f), PosL(0.5f, 0f, 0.5f, 0f), PosL(0.6f, 0f, 0f, 0f), PosL(2f, 0f, 0f, 0f), PosL(2.05f, 0f, 1.2f, 0f), PosL(2.15f, 0f, 1.7f, 0f), PosL(2.25f, 0f, 0f, 0f), PosL(2.3f, 0f, 1.3f, 0f), PosL(2.5f, 0f, 0f, 0f))
            ],
        });

    private static AnimationDefinition BuildRollOut() =>
        new(1.5f, false, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["head"] =
            [
                R(RotL(0.1f, -50f, 0f, 0f), RotL(0.15f, 0f, 0f, 0f), RotL(0.25f, 0f, 0f, 0f), RotL(0.4f, 0f, 0f, 0f), RotL(0.65f, 0f, 0f, 0f), RotL(0.7f, 0f, 0f, 0f), RotL(0.85f, -2.5f, 0f, 0f), RotL(0.9f, 0f, 0f, 0f), RotL(0.95f, -7.5f, 0f, 0f), RotL(1.05f, 0f, 0f, 0f), RotL(1.1f, 7.5f, 0f, 0f)),
                P(PosL(0f, 0f, 1f, 5f), PosL(0.05f, 0f, 1f, 0.2f), PosL(0.1f, 0f, 1f, 0.2f), PosL(0.15f, 0f, 2.1f, 1.2f), PosL(0.25f, 0f, 1.03f, 0.13f), PosL(0.4f, 0f, 1.03f, 0.13f), PosL(0.65f, 0f, 1.03f, 0.13f), PosL(0.7f, 0f, 1.1f, 0.2f), PosL(0.75f, 0f, 4.1f, 2.2f), PosL(0.85f, 0f, 5.1f, 3.2f), PosL(0.9f, 0f, 0.1f, 0.2f), PosL(0.95f, 0f, 0.9f, -0.8f), PosL(1.05f, 0f, 0.9f, 0f), PosL(1.1f, 0f, 2.6f, 0.2f), PosL(1.15f, 0f, 2.4f, 0.2f), PosL(1.2f, 0f, 0f, 0.2f), PosL(1.25f, 0f, 0f, 0.2f), PosL(1.3f, 0f, 0f, 0.2f))
            ],
            ["right_hind_leg"] =
            [
                R(RotL(1.1f, 0f, 0f, 0f), RotL(1.3f, 0f, 0f, 30f), RotL(1.4f, 0f, 0f, 0f), RotL(1.45f, 0f, 0f, 0f), RotL(1.5f, 0f, 0f, 0f)),
                P(PosL(1.1f, 0f, 3f, -2f), PosL(1.2f, 0f, 8f, -2f), PosL(1.3f, -1f, 3f, 0f), PosL(1.4f, 0f, 0f, 0f), PosL(1.45f, 0f, 0f, 0f), PosL(1.5f, 0f, 0f, 0f))
            ],
            ["left_hind_leg"] =
            [
                R(RotL(1.1f, 0f, 0f, 0f), RotL(1.3f, 0f, 0f, -30f), RotL(1.4f, 0f, 0f, 0f), RotL(1.45f, 0f, 0f, 0f), RotL(1.5f, 0f, 0f, 0f)),
                P(PosL(1.1f, 0f, 3f, -2f), PosL(1.2f, 0f, 8f, -2f), PosL(1.3f, 1f, 3f, 0f), PosL(1.35f, 1f, 3f, 0f), PosL(1.4f, 0f, 0f, 0f), PosL(1.45f, 0f, 0f, 0f), PosL(1.5f, 0f, 0f, 0f))
            ],
            ["right_front_leg"] =
            [
                R(RotC(0f, -90f, 0f, 0f), RotC(0.05f, -90f, 0f, 0f), RotL(0.25f, -45f, 0f, 0f), RotL(0.55f, -45f, 0f, 0f), RotC(0.6f, -92.5f, 0f, 0f), RotC(1.1f, -90f, 0f, 0f), RotC(1.3f, 0f, 0f, 30f), RotC(1.4f, 0f, 0f, 0f), RotL(1.45f, 0f, 0f, 0f), RotC(1.5f, 0f, 0f, 0f)),
                P(PosL(0f, -1f, 2f, 2f), PosC(0.05f, -1f, 2f, 2f), PosL(0.25f, -1f, 2f, -1f), PosL(0.55f, -1f, 2f, -1f), PosC(0.7f, -1f, 2f, 2.63f), PosL(1.1f, -1f, 2f, 2f), PosL(1.2f, -1f, 7f, 2f), PosL(1.3f, -1f, 3f, 0f), PosL(1.4f, 0f, 0f, 0f), PosL(1.45f, 0f, 0f, 0f), PosL(1.5f, 0f, 0f, 0f))
            ],
            ["left_front_leg"] =
            [
                R(RotC(0f, -90f, 0f, 0f), RotC(0.05f, -90f, 0f, 0f), RotL(0.25f, -45f, 0f, 0f), RotL(0.55f, -45f, 0f, 0f), RotC(0.6f, -87.5f, 0f, 0f), RotC(1.1f, -90f, 0f, 0f), RotC(1.3f, 0f, 0f, -30f), RotC(1.4f, 0f, 0f, 0f), RotL(1.45f, 0f, 0f, 0f), RotC(1.5f, 0f, 0f, 0f)),
                P(PosC(0f, 1f, 2f, 2f), PosC(0.05f, 1f, 2f, 2f), PosC(0.15f, 1f, 2f, 0f), PosL(0.25f, 1f, 2f, -1f), PosL(0.55f, 1f, 2f, -1f), PosC(0.7f, 1f, 2f, 1.88f), PosC(0.75f, 1f, 2f, 2.67f), PosC(1.1f, 1f, 2f, 2f), PosC(1.2f, 1f, 8f, 2f), PosC(1.25f, 1.06f, 5.06f, 1f), PosC(1.3f, 1f, 3f, 0f), PosC(1.4f, 0f, 0f, 0f), PosL(1.45f, 0f, 0f, 0f), PosC(1.5f, 0f, 0f, 0f))
            ],
            ["cube"] =
            [
                R(RotL(0f, 0f, 0f, 0f), RotL(0.05f, 15f, 0f, 0f), RotL(0.15f, -7.5f, 0f, 0f), RotL(0.25f, 0f, 0f, 0f), RotL(0.7f, 0f, 0f, 0f), RotL(0.75f, -17.5f, 0f, 0f), RotL(0.85f, -25f, 0f, 0f), RotL(0.9f, 0f, 0f, 0f), RotL(0.95f, 12.5f, 0f, 0f), RotL(1.05f, 0f, 0f, 0f), RotL(1.1f, 0f, 0f, 0f)),
                P(PosL(0f, 0f, 0f, 0f), PosL(0.05f, 0f, 1.6f, 0f), PosL(0.15f, 0f, 0.5f, 0f), PosL(0.25f, 0f, 0f, 0f), PosL(0.7f, 0f, 0f, 0f), PosL(0.75f, 0f, 1.2f, 0f), PosL(0.85f, 0f, 1.7f, 0f), PosL(0.9f, 0f, 0f, 0f), PosL(0.95f, 0f, 1.3f, 0f), PosL(1.05f, 0f, 0f, 0f), PosL(1.2f, 0f, 5f, 0f), PosL(1.25f, 0f, 8f, 0f), PosL(1.5f, 0f, 1f, 0f))
            ],
            ["body"] = [P(PosL(1.1f, 0f, 0f, 0f), PosL(1.2f, 0f, 4f, 0f), PosL(1.25f, 0f, 5f, 0f), PosL(1.3f, 0f, 4f, 0f), PosL(1.4f, 0f, -1f, 0f), PosL(1.5f, 0f, 0f, 0f))],
        });

}
