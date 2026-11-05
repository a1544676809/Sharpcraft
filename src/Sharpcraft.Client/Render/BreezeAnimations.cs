// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference BreezeAnimation data, written as behaviour data (the reference hardcodes it in Java, not JSON).
/// Six clips drive the Breeze: idle (2 s loop), shoot (1.125 s), slide (0.2 s), slide_back (0.1 s),
/// inhale (2 s) and jump (0.5 s). Part names match <see cref="BreezeModelDefinition"/>; angles are radians,
/// positions use the reference Y inversion and scales the reference additive convention, all via
/// <see cref="KeyframeAnimations"/>.
/// </summary>
/// <remarks>
/// The reference <c>BreezeModel.setupAnim</c> bakes all six clips over the model root and plays each one through its
/// own <c>AnimationState</c> clock (<c>idle</c>, <c>shoot</c>, <c>slide</c>, <c>slideBack</c>, <c>inhale</c>,
/// <c>longJump</c>) sampled at <c>state.ageInTicks</c>. Every clip below is translated verbatim from the reference
/// keyframes; the per-state start ticks are supplied elsewhere when those clocks exist.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.animation.definitions.BreezeAnimation", "ref://mc-26.3-analysis/src/net/minecraft/client/animation/definitions/BreezeAnimation.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class BreezeAnimations
{
    public static readonly AnimationDefinition Idle = BuildIdle();
    public static readonly AnimationDefinition Shoot = BuildShoot();
    public static readonly AnimationDefinition Slide = BuildSlide();
    public static readonly AnimationDefinition SlideBack = BuildSlideBack();
    public static readonly AnimationDefinition Inhale = BuildInhale();
    public static readonly AnimationDefinition Jump = BuildJump();

    private static readonly IReadOnlySet<string> PartNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "body", "rods", "head", "wind_body", "wind_bottom", "wind_mid", "wind_top",
    };

    public static readonly KeyframeAnimation IdleBaked = Idle.Bake(PartNames);
    public static readonly KeyframeAnimation ShootBaked = Shoot.Bake(PartNames);
    public static readonly KeyframeAnimation SlideBaked = Slide.Bake(PartNames);
    public static readonly KeyframeAnimation SlideBackBaked = SlideBack.Bake(PartNames);
    public static readonly KeyframeAnimation InhaleBaked = Inhale.Bake(PartNames);
    public static readonly KeyframeAnimation JumpBaked = Jump.Bake(PartNames);

    private static AnimationKeyframe KL(float t, Vector3 target) =>
        new(t, target, AnimationInterpolation.Linear);

    private static AnimationKeyframe KC(float t, Vector3 target) =>
        new(t, target, AnimationInterpolation.CatmullRom);

    private static AnimationKeyframe PosL(float t, float x, float y, float z) =>
        KL(t, KeyframeAnimations.PosVec(x, y, z));

    private static AnimationKeyframe PosC(float t, float x, float y, float z) =>
        KC(t, KeyframeAnimations.PosVec(x, y, z));

    private static AnimationKeyframe RotL(float t, float x, float y, float z) =>
        KL(t, KeyframeAnimations.DegreeVec(x, y, z));

    private static AnimationKeyframe ScaleL(float t, float x, float y, float z) =>
        KL(t, KeyframeAnimations.ScaleVec(x, y, z));

    private static AnimationChannel R(params AnimationKeyframe[] frames) => new(AnimationTarget.Rotation, frames);
    private static AnimationChannel P(params AnimationKeyframe[] frames) => new(AnimationTarget.Position, frames);
    private static AnimationChannel S(params AnimationKeyframe[] frames) => new(AnimationTarget.Scale, frames);

    private static AnimationDefinition BuildIdle() =>
        new(2.0f, true, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["wind_top"] = [P(
                PosL(0.0f, 0.5f, 0.0f, 0.0f),
                PosL(0.25f, 0.5f, 0.0f, -0.5f),
                PosL(0.75f, -0.5f, 0.0f, -0.5f),
                PosL(1.25f, -0.5f, 0.0f, 0.5f),
                PosL(1.75f, 0.5f, 0.0f, 0.5f),
                PosL(2.0f, 0.5f, 0.0f, 0.0f))],
            ["wind_mid"] = [P(
                PosL(0.0f, 0.5f, 0.0f, -0.5f),
                PosL(0.5f, -0.5f, 0.0f, -0.5f),
                PosL(1.0f, -0.5f, 0.0f, 0.5f),
                PosL(1.5f, 0.5f, 0.0f, 0.5f),
                PosL(2.0f, 0.5f, 0.0f, -0.5f))],
            ["head"] = [P(
                PosC(0.0f, 0.0f, 0.0f, 0.0f),
                PosC(1.0f, 0.0f, 1.0f, 0.0f),
                PosC(2.0f, 0.0f, 0.0f, 0.0f))],
            ["rods"] =
            [
                R(RotL(0.0f, 0.0f, 0.0f, 0.0f), RotL(2.0f, 0.0f, 1080.0f, 0.0f)),
                P(PosL(0.0f, 0.0f, 0.0f, 0.0f), PosL(1.0f, 0.0f, -1.0f, 0.0f), PosL(2.0f, 0.0f, 0.0f, 0.0f))
            ],
        });

    private static AnimationDefinition BuildShoot() =>
        new(1.125f, false, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["head"] =
            [
                R(RotL(0.0f, 0.0f, 0.0f, 0.0f), RotL(0.25f, -12.5f, 0.0f, 0.0f), RotL(0.75f, -12.5f, 0.0f, 0.0f),
                  RotL(0.9167f, 5.0f, 0.0f, 0.0f), RotL(1.125f, 0.0f, 0.0f, 0.0f)),
                P(PosL(0.0f, 0.0f, 0.0f, 0.0f), PosL(0.25f, 0.0f, -2.0f, 0.0f), PosL(0.7917f, 0.0f, -1.0f, 2.0f),
                  PosL(0.9583f, 0.0f, -1.0f, 0.0f), PosL(1.125f, 0.0f, 0.0f, 0.0f))
            ],
            ["wind_bottom"] = [R(RotL(0.0f, 0.0f, 0.0f, 0.0f))],
            ["wind_mid"] =
            [
                R(RotL(0.0f, 0.0f, 0.0f, 0.0f), RotL(0.25f, 12.5f, 0.0f, 0.0f), RotL(0.75f, 12.5f, 0.0f, 0.0f),
                  RotL(0.9167f, -10.0f, 0.0f, 0.0f), RotL(1.125f, 0.0f, 0.0f, 0.0f)),
                P(PosL(0.0f, 0.0f, 0.0f, 0.0f), PosL(0.25f, 0.0f, 0.0f, 5.0f), PosL(0.75f, 0.0f, 0.0f, 6.0f),
                  PosL(0.9167f, 0.0f, 0.0f, -2.0f), PosL(1.125f, 0.0f, 0.0f, 0.0f))
            ],
            ["wind_top"] =
            [
                R(RotL(0.0f, 0.0f, 0.0f, 0.0f), RotL(0.25f, 15.0f, 0.0f, 0.0f), RotL(0.75f, 15.0f, 0.0f, 0.0f),
                  RotL(0.9167f, -10.0f, 0.0f, 0.0f), RotL(1.125f, 0.0f, 0.0f, 0.0f)),
                P(PosL(0.0f, 0.0f, 0.0f, 0.0f), PosL(0.25f, 0.0f, 0.0f, 3.0f), PosL(0.8333f, 0.0f, 0.0f, 4.0f),
                  PosL(0.9583f, 0.0f, 0.0f, -2.0f), PosL(1.125f, 0.0f, 0.0f, 0.0f))
            ],
            ["body"] =
            [
                R(RotL(0.0f, 0.0f, 0.0f, 0.0f), RotL(0.25f, 12.5f, 0.0f, 0.0f), RotL(0.75f, 12.5f, 0.0f, 0.0f),
                  RotL(0.9167f, -2.5f, 0.0f, 0.0f), RotL(1.125f, 0.0f, 0.0f, 0.0f)),
                P(PosL(0.0f, 0.0f, 0.0f, 0.0f), PosL(0.25f, 0.0f, 3.0f, 5.0f), PosL(0.8333f, 0.0f, 3.0f, 6.0f),
                  PosL(0.9583f, 0.0f, 3.0f, -1.0f), PosL(1.125f, 0.0f, 0.0f, 0.0f))
            ],
            ["rods"] = [R(RotL(0.0f, 0.0f, 0.0f, 0.0f), RotL(1.0f, 0.0f, 360.0f, 0.0f))],
        });

    private static AnimationDefinition BuildJump() =>
        new(0.5f, false, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["body"] = [P(PosL(0.0f, 0.0f, -10.0f, 0.0f), PosL(0.125f, 0.0f, 11.0f, 0.0f), PosL(0.5f, 0.0f, 0.0f, 0.0f))],
            ["head"] = [R(RotL(0.0f, 22.5f, 0.0f, 0.0f), RotL(0.2083f, -19.25f, 0.0f, 0.0f), RotL(0.5f, 0.0f, 0.0f, 0.0f))],
            ["wind_body"] = [S(ScaleL(0.0f, 1.0f, 1.0f, 1.0f), ScaleL(0.125f, 1.0f, 1.3f, 1.0f), ScaleL(0.5f, 1.0f, 1.0f, 1.0f))],
            ["wind_bottom"] =
            [
                R(RotL(0.0f, 0.0f, 90.0f, 0.0f), RotL(0.5f, 0.0f, 360.0f, 0.0f)),
                S(ScaleL(0.0f, 1.0f, 1.0f, 1.0f), ScaleL(0.125f, 1.0f, 1.1f, 1.0f), ScaleL(0.5f, 1.0f, 1.0f, 1.0f))
            ],
            ["wind_mid"] =
            [
                R(RotL(0.0f, 0.0f, 0.0f, 0.0f), RotL(0.5f, 0.0f, 180.0f, 0.0f)),
                P(PosL(0.0f, 0.0f, -6.0f, 0.0f), PosL(0.125f, 0.0f, 2.0f, 0.0f), PosL(0.5f, 0.0f, 0.0f, 0.0f))
            ],
            ["wind_top"] =
            [
                R(RotL(0.0f, 0.0f, 0.0f, 0.0f), RotL(0.5f, 0.0f, 90.0f, 0.0f)),
                P(PosL(0.0f, 0.0f, -5.0f, 0.0f), PosL(0.125f, 0.0f, 2.0f, 0.0f), PosL(0.5f, 0.0f, 0.0f, 0.0f))
            ],
            ["rods"] = [R(RotL(0.0f, 0.0f, 0.0f, 0.0f), RotL(0.5f, 0.0f, 360.0f, 0.0f))],
        });

    private static AnimationDefinition BuildInhale() =>
        new(2.0f, false, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["body"] = [P(PosL(0.0f, 0.0f, 0.0f, 0.0f), PosL(0.5f, 0.0f, -10.0f, 0.0f), PosL(0.625f, 0.0f, -10.0f, 0.0f))],
            ["head"] = [R(RotL(0.0f, 0.0f, 0.0f, 0.0f), RotL(0.5f, 22.5f, 0.0f, 0.0f), RotL(0.625f, 22.5f, 0.0f, 0.0f))],
            ["wind_body"] = [S(ScaleL(0.0f, 1.0f, 1.0f, 1.0f), ScaleL(0.5f, 1.0f, 1.0f, 1.0f), ScaleL(0.625f, 1.0f, 1.0f, 1.0f))],
            ["wind_bottom"] =
            [
                R(RotL(0.0f, 0.0f, 0.0f, 0.0f), RotL(0.625f, 0.0f, 90.0f, 0.0f)),
                S(ScaleL(0.0f, 1.0f, 1.0f, 1.0f), ScaleL(0.5f, 1.0f, 1.0f, 1.0f), ScaleL(0.625f, 1.0f, 1.0f, 1.0f))
            ],
            ["wind_mid"] =
            [
                R(RotL(0.0f, 0.0f, 0.0f, 0.0f), RotL(0.625f, 0.0f, 0.0f, 0.0f)),
                P(PosL(0.0f, 0.0f, 0.0f, 0.0f), PosL(0.5f, 0.0f, -6.0f, 0.0f), PosL(0.625f, 0.0f, -6.0f, 0.0f))
            ],
            ["wind_top"] =
            [
                R(RotL(0.0f, 0.0f, 0.0f, 0.0f), RotL(0.625f, 0.0f, 0.0f, 0.0f)),
                P(PosL(0.0f, 0.0f, 0.0f, 0.0f), PosL(0.5f, 0.0f, -5.0f, 0.0f), PosL(0.625f, 0.0f, -5.0f, 0.0f))
            ],
            ["rods"] = [R(RotL(0.0f, 0.0f, 0.0f, 0.0f), RotL(0.625f, 0.0f, 360.0f, 0.0f))],
        });

    private static AnimationDefinition BuildSlide() =>
        new(0.2f, false, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["body"] = [P(PosL(0.0f, 0.0f, 0.0f, 0.0f), PosL(0.2f, 0.0f, 0.0f, -6.0f))],
            ["wind_mid"] = [P(PosL(0.0f, 0.0f, 0.0f, 0.0f), PosL(0.2f, 0.0f, 0.0f, -3.0f))],
            ["wind_top"] = [P(PosL(0.0f, 0.0f, 0.0f, 0.0f), PosL(0.2f, 0.0f, 0.0f, -2.0f))],
        });

    private static AnimationDefinition BuildSlideBack() =>
        new(0.1f, false, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["body"] = [P(PosL(0.0f, 0.0f, 0.0f, -6.0f), PosL(0.1f, 0.0f, 0.0f, 0.0f))],
            ["wind_mid"] = [P(PosL(0.0f, 0.0f, 0.0f, -3.0f), PosL(0.1f, 0.0f, 0.0f, 0.0f))],
            ["wind_top"] = [P(PosL(0.0f, 0.0f, 0.0f, -2.0f), PosL(0.1f, 0.0f, 0.0f, 0.0f))],
        });
}
