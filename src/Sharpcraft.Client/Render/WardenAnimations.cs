// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference WardenAnimation data, written as behaviour data (the reference hardcodes these in Java, not JSON).
/// The six one-shot clips drive the Warden: attack, sonic boom, dig, emerge, roar and sniff. Part names match
/// <see cref="WardenModelDefinition"/>; positions use the reference Y inversion through
/// <see cref="KeyframeAnimations.PosVec"/> and angles are converted from degrees to radians through
/// <see cref="KeyframeAnimations.DegreeVec"/>.
/// </summary>
/// <remarks>
/// Every clip below is a non-looping <c>AnimationDefinition.Builder.withLength(...)</c> build in the reference (no
/// <c>.looping()</c> is ever called). Keyframe timestamps are the reference values in seconds; interpolation is
/// <c>CatmullRom</c> unless a keyframe is explicitly <c>LINEAR</c> (only the tail of dig and one head-position track).
/// The clips are referenced by the model's six <c>AnimationState</c> clocks, which the generic render-state frame does
/// not carry yet, so <see cref="WardenAnimationProvider.ApplyClip"/> is the only consumer for now.
/// </remarks>
public static class WardenAnimations
{
    public static readonly AnimationDefinition Attack = BuildAttack();
    public static readonly AnimationDefinition SonicBoom = BuildSonicBoom();
    public static readonly AnimationDefinition Dig = BuildDig();
    public static readonly AnimationDefinition Emerge = BuildEmerge();
    public static readonly AnimationDefinition Roar = BuildRoar();
    public static readonly AnimationDefinition Sniff = BuildSniff();

    private static readonly IReadOnlySet<string> PartNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "bone", "body", "head", "right_tendril", "left_tendril", "left_leg", "left_arm",
        "left_ribcage", "right_arm", "right_leg", "right_ribcage",
    };

    public static readonly KeyframeAnimation AttackBaked = Attack.Bake(PartNames);
    public static readonly KeyframeAnimation SonicBoomBaked = SonicBoom.Bake(PartNames);
    public static readonly KeyframeAnimation DigBaked = Dig.Bake(PartNames);
    public static readonly KeyframeAnimation EmergeBaked = Emerge.Bake(PartNames);
    public static readonly KeyframeAnimation RoarBaked = Roar.Bake(PartNames);
    public static readonly KeyframeAnimation SniffBaked = Sniff.Bake(PartNames);

    private static AnimationKeyframe RotC(float t, float x, float y, float z) =>
        new(t, KeyframeAnimations.DegreeVec(x, y, z), AnimationInterpolation.CatmullRom);

    private static AnimationKeyframe RotL(float t, float x, float y, float z) =>
        new(t, KeyframeAnimations.DegreeVec(x, y, z), AnimationInterpolation.Linear);

    private static AnimationKeyframe PosC(float t, float x, float y, float z) =>
        new(t, KeyframeAnimations.PosVec(x, y, z), AnimationInterpolation.CatmullRom);

    private static AnimationKeyframe PosL(float t, float x, float y, float z) =>
        new(t, KeyframeAnimations.PosVec(x, y, z), AnimationInterpolation.Linear);

    private static AnimationChannel R(params AnimationKeyframe[] frames) => new(AnimationTarget.Rotation, frames);

    private static AnimationChannel P(params AnimationKeyframe[] frames) => new(AnimationTarget.Position, frames);

    private static AnimationDefinition BuildAttack() =>
        new(0.33333f, false, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["body"] =
            [
                R(RotC(0f, 0f, 0f, 0f), RotC(0.0417f, -22.5f, 0f, 0f), RotC(0.2083f, 22.5f, 0f, 0f), RotC(0.3333f, 0f, 0f, 0f)),
                P(PosC(0f, 0f, 0f, 0f), PosC(0.0417f, 0f, 0f, 0f), PosC(0.2083f, 0f, -1f, -2f), PosC(0.3333f, 0f, 0f, 0f))
            ],
            ["head"] =
            [
                R(RotC(0f, 0f, 0f, 0f), RotC(0.0417f, 22.5f, 0f, 0f), RotC(0.25f, -30.17493f, 0f, 0f), RotC(0.3333f, 0f, 0f, 0f)),
                P(PosC(0f, 0f, 0f, 0f), PosC(0.0417f, 0f, 0f, 0f), PosC(0.25f, 0f, -2f, -2f), PosC(0.3333f, 0f, 0f, 0f))
            ],
            ["right_arm"] =
            [
                R(RotC(0f, 0f, 0f, 0f), RotC(0.0417f, -120.36119f, 40.78947f, -20.94102f), RotC(0.1667f, -90f, -45f, 0f), RotC(0.3333f, 0f, 0f, 0f)),
                P(PosC(0f, 0f, 0f, 0f), PosC(0.0417f, 4f, 0f, 5f), PosC(0.1667f, 0f, 0f, 0f), PosC(0.3333f, 0f, 0f, 0f))
            ],
            ["left_arm"] =
            [
                R(RotC(0f, 0f, 0f, 0f), RotC(0.0417f, -120.36119f, -40.78947f, 20.94102f), RotC(0.1667f, -61.1632f, 42.85882f, 11.52421f), RotC(0.3333f, 0f, 0f, 0f)),
                P(PosC(0f, 0f, 0f, 0f), PosC(0.0417f, -4f, 0f, 5f), PosC(0.1667f, 0f, 0f, 0f), PosC(0.3333f, 0f, 0f, 0f))
            ],
        });

    private static AnimationDefinition BuildSonicBoom() =>
        new(3f, false, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["body"] =
            [
                R(RotC(0f, 0f, 0f, 0f), RotC(1.0833f, 47.5f, 0f, 0f), RotC(1.625f, 55f, 0f, 0f), RotC(1.9167f, 0f, 0f, 0f),
                  RotC(2f, -32.5f, 0f, 0f), RotC(2.4583f, -32.5f, 0f, 0f), RotC(2.7083f, 0f, 0f, 0f), RotC(2.875f, 0f, 0f, 0f)),
                P(PosC(0f, 0f, 0f, 0f), PosC(1.0833f, 0f, -3f, 0f), PosC(1.625f, 0f, -4f, -1f), PosC(1.9167f, 0f, 0f, 0f),
                  PosC(2.7083f, 0f, 0f, 0f), PosC(2.875f, 0f, 0f, 0f))
            ],
            ["right_ribcage"] =
            [
                R(RotC(0f, 0f, 0f, 0f), RotC(1.5417f, 0f, 0f, 0f), RotC(1.7917f, 0f, 0f, 0f), RotC(1.875f, 0f, 125f, 0f),
                  RotC(2.5f, 0f, 125f, 0f), RotC(2.6667f, 0f, 0f, 0f))
            ],
            ["left_ribcage"] =
            [
                R(RotC(0f, 0f, 0f, 0f), RotC(1.5417f, 0f, 0f, 0f), RotC(1.7917f, 0f, 0f, 0f), RotC(1.875f, 0f, -125f, 0f),
                  RotC(2.5f, 0f, -125f, 0f), RotC(2.6667f, 0f, 0f, 0f))
            ],
            ["head"] =
            [
                R(RotC(0f, 0f, 0f, 0f), RotC(1f, 67.5f, 0f, 0f), RotC(1.75f, 80f, 0f, 0f), RotC(1.9167f, -45f, 0f, 0f),
                  RotC(2.5f, -45f, 0f, 0f), RotC(2.7083f, -45f, 0f, 0f), RotC(2.875f, 0f, 0f, 0f)),
                P(PosC(0f, 0f, 0f, 0f), PosC(1.9167f, 0f, 0f, -3f), PosC(2.5f, 0f, 0f, -3f), PosC(2.7083f, 0f, 0f, -3f),
                  PosC(2.875f, 0f, 0f, 0f))
            ],
            ["right_arm"] =
            [
                R(RotC(0f, 0f, 0f, 0f), RotC(0.875f, -42.28659f, -32.69813f, -5.00825f), RotC(1.1667f, -29.83757f, -35.39626f, -45.28089f),
                  RotC(1.3333f, -29.83757f, -35.39626f, -45.28089f), RotC(1.6667f, -72.28659f, -32.69813f, -5.00825f),
                  RotC(1.8333f, 35.26439f, -30f, 35.26439f), RotC(1.9167f, 73.75484f, -13.0931f, 19.20518f),
                  RotC(2.5f, 73.75484f, -13.0931f, 19.20518f), RotC(2.75f, 58.20713f, -21.1064f, 28.7261f), RotC(3f, 0f, 0f, 0f)),
                P(PosC(0f, 0f, 0f, 0f), PosC(1.8333f, 3f, 0f, 0f), PosC(2.75f, 3f, 0f, 0f), PosC(3f, 0f, 0f, 0f))
            ],
            ["left_arm"] =
            [
                R(RotC(0f, 0f, 0f, 0f), RotC(0.875f, -33.80694f, 32.31058f, 6.87997f), RotC(1.1667f, -17.87827f, 34.62115f, 49.02433f),
                  RotC(1.3333f, -17.87827f, 34.62115f, 49.02433f), RotC(1.6667f, -51.30694f, 32.31058f, 6.87997f),
                  RotC(1.8333f, 35.26439f, 30f, -35.26439f), RotC(1.9167f, 73.75484f, 13.0931f, -19.20518f),
                  RotC(2.5f, 73.75484f, 13.0931f, -19.20518f), RotC(2.75f, 58.20713f, 21.1064f, -28.7261f), RotC(3f, 0f, 0f, 0f)),
                P(PosC(0f, 0f, 0f, 0f), PosC(1.8333f, -3f, 0f, 0f), PosC(2.75f, -3f, 0f, 0f), PosC(3f, 0f, 0f, 0f))
            ],
        });

    private static AnimationDefinition BuildDig() =>
        new(5f, false, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["body"] =
            [
                R(RotC(0f, 0f, 0f, 0f), RotC(0.25f, 4.13441f, 0.94736f, 1.2694f), RotC(0.5f, 50f, 0f, 0f),
                  RotC(0.7083f, 54.45407f, -13.53935f, -18.14183f), RotC(1.0417f, 59.46442f, -10.8885f, 35.7954f),
                  RotC(1.3333f, 82.28261f, 0f, 0f), RotC(1.625f, 53.23606f, 10.04715f, -29.72932f), RotC(2.2083f, -17.71739f, 0f, 0f),
                  RotC(2.5417f, 112.28261f, 0f, 0f), RotC(2.6667f, 116.06889f, 5.11581f, -24.50117f),
                  RotC(2.8333f, 121.56244f, -4.17248f, 19.57737f), RotC(3.0417f, 138.5689f, 5.11581f, -24.50117f),
                  RotC(3.25f, 144.06244f, -4.17248f, 19.57737f), RotC(3.375f, 147.28261f, 0f, 0f), RotC(3.625f, 147.28261f, 0f, 0f),
                  RotC(3.875f, 134.36221f, 8.81113f, -8.90172f), RotC(4.0417f, 132.05966f, -8.35927f, 9.70506f),
                  RotC(4.25f, 134.36221f, 8.81113f, -8.90172f), RotL(4.5f, 147.5f, 0f, 0f)),
                P(PosC(0f, 0f, 0f, 0f), PosC(0.5f, 0f, -16.48454f, -6.5784f), PosC(0.7083f, 0f, -16.48454f, -6.5784f),
                  PosC(1.0417f, 0f, -16.97f, -7.11f), PosC(1.625f, 0f, -13.97f, -7.11f), PosC(2.2083f, 0f, -11.48454f, -0.5784f),
                  PosC(2.5417f, 0f, -16.48454f, -6.5784f), PosC(2.6667f, 0f, -20.27f, -5.42f), PosC(3.375f, 0f, -21.48454f, -5.5784f),
                  PosC(4.0417f, 0f, -22.48454f, -5.5784f), PosL(4.5f, 0f, -40f, -8f))
            ],
            ["head"] =
            [
                R(RotC(0f, 0f, 0f, 0f), RotC(0.6667f, 12.5f, 0f, 0f), RotC(1.2083f, 12.5f, 0f, 0f), RotC(1.75f, 45f, 0f, 0f),
                  RotC(2.375f, -22.5f, 0f, 0f), RotC(2.5417f, 67.5f, 0f, 0f), RotC(4.375f, 67.5f, 0f, 0f)),
                P(PosL(0f, 0f, 0f, 0f), PosL(4.375f, 0f, 0f, 0f))
            ],
            ["right_arm"] =
            [
                R(RotC(0f, 0f, 0f, 0f), RotC(0.5f, -101.8036f, -21.29587f, 30.61478f), RotC(0.7083f, -101.8036f, -21.29587f, 30.61478f),
                  RotC(1f, 48.7585f, -17.61941f, 9.9865f), RotC(1.1667f, 48.7585f, -17.61941f, 9.9865f),
                  RotC(1.4583f, -101.8036f, -21.29587f, 30.61478f), RotC(1.75f, -89.04994f, -4.19657f, -1.47845f),
                  RotC(2.2083f, -158.30728f, 3.7152f, -1.52352f), RotC(2.5417f, -89.04994f, -4.19657f, -1.47845f),
                  RotC(4.375f, -120f, 0f, 0f)),
                P(PosC(0f, 0f, 0f, 0f), PosC(0.7083f, 2.22f, 0f, 0.86f), PosC(1f, 3.12f, 0f, 4.29f), PosC(2.2083f, 1f, 0f, 4f),
                  PosC(4.375f, 0f, 0f, 4f))
            ],
            ["left_arm"] =
            [
                R(RotC(0f, 0f, 0f, 0f), RotC(0.2917f, -63.89288f, -0.52011f, 2.09491f), RotC(0.5f, -63.89288f, -0.52011f, 2.09491f),
                  RotC(0.7083f, -62.87857f, 15.15061f, 9.97445f), RotC(0.9167f, -86.93642f, 17.45026f, 4.05284f),
                  RotC(1.1667f, -86.93642f, 17.45026f, 4.05284f), RotC(1.4583f, -86.93642f, 17.45026f, 4.05284f),
                  RotC(1.6667f, 63.0984f, 8.83573f, -8.71284f), RotC(1.8333f, 35.5984f, 8.83573f, -8.71284f),
                  RotC(2.2083f, -153.27473f, -0.02953f, 3.5235f), RotC(2.5417f, -87.07754f, -0.02625f, 3.132f),
                  RotL(4.375f, -120f, 0f, 0f)),
                P(PosC(0f, 0f, 0f, 0f), PosC(0.5f, -0.28f, 5f, 10f), PosC(0.7083f, -1.51f, 4.35f, 4.33f), PosC(0.9167f, -0.6f, 3.61f, 4.63f),
                  PosC(1.1667f, -0.6f, 3.61f, 0.63f), PosC(1.6667f, -2.85f, -0.1f, 3.33f), PosC(2.2083f, -1f, 0f, 4f), PosL(4.375f, 0f, 0f, 4f))
            ],
            ["right_leg"] =
            [
                R(RotC(0f, 0f, 0f, 0f), RotC(0.5f, 113.27f, 0f, 0f), RotC(0.7083f, 113.27f, 0f, 0f), RotC(3.3333f, 113.27f, 0f, 0f),
                  RotC(3.5833f, 182.5f, 0f, 0f), RotC(3.8333f, 120f, 0f, 0f), RotC(4.0833f, 182.5f, 0f, 0f), RotC(4.2917f, 120f, 0f, 0f),
                  RotL(4.5f, 90f, 0f, 0f)),
                P(PosC(0f, 0f, 0f, 0f), PosC(0.5f, 0f, -13.98f, -2.37f), PosC(0.7083f, 0f, -13.98f, -2.37f), PosC(3.3333f, 0f, -13.98f, -2.37f),
                  PosC(3.5833f, 0f, -7f, -3f), PosC(3.8333f, 0f, -9f, -3f), PosC(4.0833f, 0f, -16.71f, -3.69f), PosL(4.2917f, 0f, -28f, -5f))
            ],
            ["left_leg"] =
            [
                R(RotC(0f, 0f, 0f, 0f), RotC(0.5f, 114.98f, 0f, 0f), RotC(0.7083f, 114.98f, 0f, 0f), RotC(3.3333f, 114.98f, 0f, 0f),
                  RotC(3.5833f, 90f, 0f, 0f), RotC(3.8333f, 172.5f, 0f, 0f), RotC(4.0833f, 90f, 0f, 0f), RotC(4.2917f, 197.5f, 0f, 0f),
                  RotL(4.5f, 90f, 0f, 0f)),
                P(PosC(0f, 0f, 0f, 0f), PosC(0.5f, 0f, -14.01f, -2.35f), PosC(0.7083f, 0f, -14.01f, -2.35f), PosC(3.3333f, 0f, -14.01f, -2.35f),
                  PosC(3.5833f, 0f, -5f, -4f), PosC(3.8333f, 0f, -7f, -4f), PosC(4.0833f, 0f, -15.5f, -3.76f), PosL(4.2917f, 0f, -28f, -5f))
            ],
        });

    private static AnimationDefinition BuildEmerge() =>
        new(6.68f, false, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["body"] =
            [
                R(RotC(0f, 0f, 0f, 0f), RotC(0.52f, 0f, 0f, -22.5f), RotC(1.2f, 0f, 0f, -7.5f), RotC(1.68f, 0f, 0f, 10f),
                  RotC(1.8f, 0f, 0f, 10f), RotC(2.28f, 0f, 0f, 10f), RotC(2.88f, 0f, 0f, 10f), RotC(3.76f, 25f, 0f, -7.5f),
                  RotC(3.92f, 35f, 0f, -7.5f), RotC(4.08f, 25f, 0f, -7.5f), RotC(4.44f, 47.5f, 0f, 0f), RotC(4.56f, 47.5f, 0f, 0f),
                  RotC(4.68f, 47.5f, 0f, 0f), RotC(5f, 70f, 0f, 2.5f), RotC(5.8f, 70f, 0f, 2.5f), RotC(6.64f, 0f, 0f, 0f)),
                P(PosC(0f, 0f, -63f, 0f), PosC(0.52f, 0f, -56f, 0f), PosC(1.2f, 0f, -32f, 0f), PosC(1.68f, 0f, -32f, 0f),
                  PosC(1.8f, 0f, -32f, 0f), PosC(2.28f, 0f, -32f, 0f), PosC(2.88f, 0f, -32f, 0f), PosC(3.16f, 0f, -27f, 0f),
                  PosC(3.76f, 0f, -14f, 0f), PosC(3.92f, 0f, -11f, 0f), PosC(4.08f, 0f, -14f, 0f), PosC(4.44f, 0f, -6f, -3f),
                  PosC(4.56f, 0f, -4f, -3f), PosC(4.68f, 0f, -6f, -3f), PosC(5f, 0f, -3f, -4f), PosC(5.8f, 0f, -3f, -4f),
                  PosC(6.64f, 0f, 0f, 0f))
            ],
            ["head"] =
            [
                R(RotC(0f, 0f, 0f, 0f), RotC(0.52f, 0f, 0f, 0f), RotC(0.92f, 0.74f, 0f, -40.38f), RotC(1.16f, -67.5f, 0f, -2.5f),
                  RotC(1.24f, -67.5f, 0f, -2.5f), RotC(1.32f, -47.5f, 0f, -2.5f), RotC(1.4f, -67.5f, 0f, -2.5f), RotC(1.68f, -67.5f, 0f, 15f),
                  RotC(1.76f, -67.5f, 0f, -5f), RotC(1.84f, -52.5f, 0f, -5f), RotC(1.92f, -67.5f, 0f, -5f), RotC(2.64f, -17.5f, 0f, -10f),
                  RotC(3.76f, 70f, 0f, 12.5f), RotC(4.04f, 70f, 0f, 12.5f), RotC(4.12f, 80f, 0f, 12.5f), RotC(4.24f, 70f, 0f, 12.5f),
                  RotC(5f, 77.5f, 0f, -2.5f), RotC(6.64f, 0f, 0f, 0f)),
                P(PosC(0f, 0f, 0f, 0f), PosC(0.52f, -8f, -11f, 0f), PosC(0.92f, 0f, 0f, 0f), PosC(1.24f, 0f, 0.47f, -0.95f),
                  PosC(1.32f, 0f, 0.47f, -0.95f), PosC(1.4f, 0f, 0.47f, -0.95f), PosC(1.68f, 0f, 1f, -2f), PosC(1.76f, 0f, 1f, -2f),
                  PosC(1.84f, 0f, 1f, -2f), PosC(1.92f, 0f, 1f, -2f), PosC(2.64f, 0f, -2f, -2f), PosC(3.76f, 0f, -4f, 1f),
                  PosC(4.04f, 0f, -1f, 1f), PosC(4.12f, 0f, -1f, 1f), PosC(4.24f, 0f, -1f, 1f), PosC(5f, 0f, -1f, 1f),
                  PosC(6.64f, 0f, 0f, 0f))
            ],
            ["right_arm"] =
            [
                R(RotC(0f, 0f, 0f, 0f), RotC(0.52f, 0f, 0f, 0f), RotC(1.2f, -152.5f, 2.5f, 7.5f), RotC(1.68f, -180f, 12.5f, -10f),
                  RotC(1.8f, -90f, 12.5f, -10f), RotC(2.28f, -90f, 12.5f, -10f), RotC(2.88f, -90f, 12.5f, -10f), RotC(3.08f, -95f, 12.5f, -10f),
                  RotC(3.24f, -83.93f, 3.93f, 5.71f), RotC(3.36f, -80f, 7.5f, 17.5f), RotC(3.76f, -67.5f, 2.5f, 0f), RotC(4.08f, -67.5f, 2.5f, 0f),
                  RotC(4.44f, -55f, 2.5f, 0f), RotC(4.56f, -60f, 2.5f, 0f), RotC(4.68f, -55f, 2.5f, 0f), RotC(5f, -67.5f, 0f, 0f),
                  RotC(5.56f, -50.45f, 0f, 2.69f), RotC(6.08f, -62.72f, 0f, 4.3f), RotC(6.64f, 0f, 0f, 0f)),
                P(PosC(0f, 0f, 0f, 0f), PosC(0.52f, 0f, 0f, 0f), PosC(1.2f, 0f, -21f, 9f), PosC(1.68f, 2f, -2f, 0f), PosC(1.8f, 2f, 0f, 0f),
                  PosC(2.28f, 2f, 0f, 0f), PosC(2.88f, 2f, 0f, 0f), PosC(3.08f, 2f, -2f, 0f), PosC(3.24f, 2f, 2.71f, 3.86f),
                  PosC(3.36f, 2f, 1f, 5f), PosC(3.76f, 2f, 3f, 3f), PosC(4.08f, 2f, 3f, 3f), PosC(4.44f, 2.67f, 4f, 0f),
                  PosC(4.56f, 2.67f, 0f, 0f), PosC(4.68f, 2.67f, 4f, 0f), PosC(5f, 0.67f, 3f, 4f), PosC(6.64f, 0f, 0f, 0f))
            ],
            ["left_arm"] =
            [
                R(RotC(0f, 0f, 0f, 0f), RotC(0.12f, -167.5f, -17.5f, -7.5f), RotC(0.6f, -167.5f, -17.5f, -7.5f), RotC(0.88f, -175f, -17.5f, 15f),
                  RotC(1.16f, -190f, -17.5f, 5f), RotC(1.28f, -90f, -5f, 5f), RotC(1.68f, -90f, -17.5f, -12.5f), RotC(1.8f, -90f, -17.5f, -12.5f),
                  RotC(2.28f, -90f, -17.5f, -12.5f), RotC(2.88f, -90f, -17.5f, -12.5f), RotC(3.04f, -81.29f, -10.64f, -14.21f),
                  RotC(3.16f, -83.5f, -5.5f, -15.5f), RotC(3.76f, -62.5f, -7.5f, 5f), RotC(3.92f, -58.75f, -3.75f, 5f), RotC(4.08f, -55f, 0f, 0f),
                  RotC(4.44f, -52.5f, 0f, 5f), RotC(4.56f, -50f, 0f, 5f), RotC(4.68f, -52.5f, 0f, 5f), RotC(5f, -72.5f, -2.5f, 5f),
                  RotC(5.56f, -57.5f, -4.54f, 2.99f), RotC(6.08f, -70.99f, -5.77f, 1.78f), RotC(6.64f, 0f, 0f, 0f)),
                P(PosC(0f, 0f, 0f, 0f), PosC(0.12f, 0f, -8f, 0f), PosC(0.6f, 0f, -8f, 0f), PosC(0.88f, 0f, 0f, 0f), PosC(1.2f, -2f, 0f, 0f),
                  PosC(1.68f, -4f, 3f, 0f), PosC(1.8f, -4f, 3f, 0f), PosC(2.28f, -4f, 3f, 0f), PosC(2.88f, -4f, 3f, 0f),
                  PosC(3.04f, -3.23f, 5.7f, 4.97f), PosC(3.16f, -1.49f, 2.22f, 5.25f), PosC(3.76f, -1.14f, 1.71f, 1.86f),
                  PosC(3.92f, -1.14f, 1.21f, 3.86f), PosC(4.08f, -1.14f, 2.71f, 4.86f), PosC(4.44f, -1f, 1f, 3f), PosC(4.56f, 0f, 1f, 1f),
                  PosC(4.68f, 0f, 1f, 3f), PosC(5f, -2f, 0f, 4f), PosC(6.64f, 0f, 0f, 0f))
            ],
            ["right_leg"] =
            [
                R(RotC(0f, 0f, 0f, 0f), RotC(0.52f, 0f, 0f, 0f), RotC(2.28f, 0f, 0f, 0f), RotC(2.88f, 0f, 0f, 0f), RotC(3.36f, 0f, 0f, 0f),
                  RotC(4.32f, 0f, 0f, 0f), RotC(4.48f, 55f, 0f, 0f), RotC(4.6f, 0f, 0f, 0f), RotC(5f, 0f, 0f, 0f), RotC(5.8f, 0f, 0f, 0f),
                  RotC(6.64f, 0f, 0f, 0f)),
                P(PosC(0f, 0f, -63f, 0f), PosC(0.52f, 0f, -56f, 0f), PosC(1.2f, 0f, -32f, 0f), PosC(1.68f, 0f, -32f, 0f),
                  PosC(1.8f, 0f, -32f, 0f), PosC(2.28f, 0f, -32f, 0f), PosC(2.88f, 0f, -32f, 0f), PosC(3.36f, 0f, -22f, 0f),
                  PosC(3.76f, 0f, -12.28f, 2.48f), PosC(3.92f, 0f, -9.28f, 2.48f), PosC(4.08f, 0f, -12.28f, 2.48f),
                  PosC(4.32f, 0f, -4.14f, 4.14f), PosC(4.48f, 0f, -0.57f, -8.43f), PosC(4.6f, 0f, 0f, 0f), PosC(5f, 0f, 0f, 0f),
                  PosC(5.8f, 0f, 0f, 0f), PosC(6.64f, 0f, 0f, 0f))
            ],
            ["left_leg"] =
            [
                R(RotC(0f, 0f, 0f, 0f), RotC(0.52f, 0f, 0f, 0f), RotC(2.28f, 0f, 0f, 0f), RotC(2.88f, 0f, 0f, 0f), RotC(3.36f, 0f, 0f, 0f),
                  RotC(3.84f, 20f, 0f, -17.5f), RotC(4f, 0f, 0f, 0f), RotC(4.68f, 20f, 0f, 0f), RotC(4.84f, 10f, 0f, 0f),
                  RotC(5f, 0f, 0f, 0f), RotC(5.8f, 0f, 0f, 0f), RotC(6.64f, 0f, 0f, 0f)),
                P(PosC(0f, 0f, -63f, 0f), PosC(0.52f, 0f, -56f, 0f), PosC(1.2f, 0f, -32f, 0f), PosC(1.68f, 0f, -32f, 0f),
                  PosC(1.8f, 0f, -32f, 0f), PosC(2.28f, 0f, -32f, 0f), PosC(2.88f, 0f, -32f, 0f), PosC(3.36f, 0f, -22f, 0f),
                  PosC(3.84f, -4f, 2f, -7f), PosC(4f, -4f, 0f, -5f), PosC(4.68f, -4f, 0f, -9f), PosC(4.84f, -2f, 2f, -3.5f),
                  PosC(5f, 0f, 0f, 0f), PosC(5.8f, 0f, 0f, 0f), PosC(6.64f, 0f, 0f, 0f))
            ],
        });

    private static AnimationDefinition BuildRoar() =>
        new(4.2f, false, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["body"] =
            [
                R(RotC(0f, 0f, 0f, 0f), RotC(1.24f, -25f, 0f, 0f), RotC(1.6f, 32.5f, 0f, -7.5f), RotC(1.84f, 38.33f, 0f, 2.99f),
                  RotC(2.08f, 40.97f, 0f, -4.3f), RotC(2.36f, 44.41f, 0f, 6.29f), RotC(3f, 47.5f, 0f, 0f), RotC(4.2f, 0f, 0f, 0f)),
                P(PosC(0f, 0f, 0f, 0f), PosC(1.24f, 0f, -1f, 3f), PosC(1.6f, 0f, -3f, -6f), PosC(3f, 0f, -3f, -6f), PosC(4.2f, 0f, 0f, 0f))
            ],
            ["head"] =
            [
                R(RotC(0f, 0f, 0f, 0f), RotC(1.24f, -32.5f, 0f, 0f), RotC(1.6f, -32.5f, 0f, -27.5f), RotC(1.8f, -32.5f, 0f, 26f),
                  RotC(2.04f, -32.5f, 0f, -27.5f), RotC(2.44f, -32.5f, 0f, 26f), RotC(2.84f, -5f, 0f, -12.5f), RotC(4.2f, 0f, 0f, 0f)),
                P(PosC(0f, 0f, 0f, 0f), PosC(1.24f, 0f, 0f, 0f), PosC(1.6f, 0f, -2f, -6f), PosC(2.2f, 0f, -2f, -6f), PosC(2.48f, 0f, -2f, -6f),
                  PosC(4.2f, 0f, 0f, 0f))
            ],
            ["right_arm"] =
            [
                R(RotC(0f, 0f, 0f, 0f), RotC(0.72f, -120f, 0f, -20f), RotC(1.24f, -77.5f, 3.75f, 15f), RotC(1.48f, 67.5f, -32.5f, 20f),
                  RotC(2.48f, 37.5f, -32.5f, 25f), RotC(2.88f, 27.6f, -17.1f, 32.5f), RotC(4.2f, 0f, 0f, 0f)),
                P(PosC(0f, 0f, 0f, 0f), PosC(0.72f, 3f, -2f, 0f), PosC(1.48f, 4f, -2f, 0f), PosC(2.48f, 4f, -2f, 0f), PosC(4.2f, 0f, 0f, 0f))
            ],
            ["left_arm"] =
            [
                R(RotC(0f, 0f, 0f, 0f), RotC(0.72f, -125f, 0f, 20f), RotC(1.24f, -76.25f, -17.5f, -7.5f), RotC(1.48f, 62.5f, 42.5f, -12.5f),
                  RotC(2.48f, 37.5f, 27.5f, -27.5f), RotC(2.88f, 25f, 18.4f, -30f), RotC(4.2f, 0f, 0f, 0f)),
                P(PosC(0f, 0f, 0f, 0f), PosC(0.72f, -3f, -2f, 0f), PosC(1.48f, -4f, -2f, 0f), PosC(2.48f, -4f, -2f, 0f), PosC(4.2f, 0f, 0f, 0f))
            ],
        });

    private static AnimationDefinition BuildSniff() =>
        new(4.16f, false, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["body"] =
            [
                R(RotC(0f, 0f, 0f, 0f), RotC(0.56f, 17.5f, 32.5f, 0f), RotC(0.96f, 0f, 32.5f, 0f), RotC(2.2f, 10f, 0f, 0f),
                  RotC(2.8f, 10f, -30f, 0f), RotC(3.32f, 0f, 0f, 0f))
            ],
            ["head"] =
            [
                R(RotC(0f, 0f, 0f, 0f), RotC(0.68f, 0f, 40f, 0f), RotC(0.96f, -22.5f, 40f, 0f), RotC(1.24f, 0f, 20f, 0f),
                  RotC(1.52f, -35f, 20f, 0f), RotC(1.76f, 0f, 20f, 0f), RotC(2.28f, 0f, -20f, 0f), RotC(2.88f, 0f, -20f, 0f),
                  RotC(3.32f, 0f, 0f, 0f))
            ],
            ["right_arm"] =
            [
                R(RotC(0f, 0f, 0f, 0f), RotC(0.96f, 17.5f, 0f, 0f), RotC(2.2f, 0f, 0f, 0f), RotC(2.76f, -15f, 0f, 0f), RotC(3.32f, 0f, 0f, 0f))
            ],
            ["left_arm"] =
            [
                R(RotC(0f, 0f, 0f, 0f), RotC(0.96f, -15f, 0f, 0f), RotC(2.2f, 0f, 0f, 0f), RotC(2.76f, 17.5f, 0f, 0f), RotC(3.32f, 0f, 0f, 0f))
            ],
        });
}
