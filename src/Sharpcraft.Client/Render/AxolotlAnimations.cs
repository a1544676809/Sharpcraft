// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Axolotl animation behaviour data. The adult model blends four procedural factors per frame; the baby model
/// plays the reference keyframe clips. Both are expressed as behaviour data, not resources.
/// </summary>
/// <remarks>
/// The baby clips are the reference <c>BabyAxolotlAnimation</c> definitions (part names match
/// <see cref="AxolotlModelDefinition"/>). The reference <c>root</c> channels in the play-dead clip are identity
/// transforms for a part that does not exist in the Sharpcraft axolotl model and are therefore omitted; every other
/// keyframe, timestamp, pre/post target and interpolation is preserved. <c>baby_axolotl_dash</c> is not referenced by
/// <c>BabyAxolotlModel.setupAnim</c> and is not ported.
/// </remarks>
public static class AxolotlAnimations
{
    /// <summary>Parts animated by the baby clips and present in <see cref="AxolotlModelDefinition"/>.</summary>
    public static readonly IReadOnlySet<string> PartNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "body", "head", "top_gills", "left_gills", "right_gills", "right_hind_leg", "left_hind_leg", "right_front_leg", "left_front_leg", "tail"
    };

    private static AnimationKeyframe K(float timestamp, Vector3 target) =>
        new(timestamp, target, AnimationInterpolation.CatmullRom);

    private static AnimationKeyframe KL(float timestamp, Vector3 target) =>
        new(timestamp, target, AnimationInterpolation.Linear);

    private static AnimationChannel Rotation(params AnimationKeyframe[] frames) => new(AnimationTarget.Rotation, frames);
    private static AnimationChannel Position(params AnimationKeyframe[] frames) => new(AnimationTarget.Position, frames);
    private static AnimationChannel Scale(params AnimationKeyframe[] frames) => new(AnimationTarget.Scale, frames);

    public static readonly AnimationDefinition BabyIdleFloor = BuildBabyIdleFloor();
    public static readonly AnimationDefinition IdleFloorUnderwater = BuildIdleFloorUnderwater();
    public static readonly AnimationDefinition IdleUnderwater = BuildIdleUnderwater();
    public static readonly AnimationDefinition BabySwim = BuildBabySwim();
    public static readonly AnimationDefinition WalkFloor = BuildWalkFloor();
    public static readonly AnimationDefinition WalkFloorUnderwater = BuildWalkFloorUnderwater();
    public static readonly AnimationDefinition PlayDead = BuildPlayDead();

    public static readonly KeyframeAnimation BabyIdleFloorBaked = BabyIdleFloor.Bake(PartNames);
    public static readonly KeyframeAnimation IdleFloorUnderwaterBaked = IdleFloorUnderwater.Bake(PartNames);
    public static readonly KeyframeAnimation IdleUnderwaterBaked = IdleUnderwater.Bake(PartNames);
    public static readonly KeyframeAnimation BabySwimBaked = BabySwim.Bake(PartNames);
    public static readonly KeyframeAnimation WalkFloorBaked = WalkFloor.Bake(PartNames);
    public static readonly KeyframeAnimation WalkFloorUnderwaterBaked = WalkFloorUnderwater.Bake(PartNames);
    public static readonly KeyframeAnimation PlayDeadBaked = PlayDead.Bake(PartNames);

    private static AnimationDefinition BuildBabyIdleFloor() =>
        new(2.88f, true, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["body"] = [Rotation(K(0f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)), K(2.8f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)))],
            ["right_front_leg"] =
            [
                Rotation(K(0f, KeyframeAnimations.DegreeVec(160f, -10f, -37.5f))),
                Position(KL(0f, KeyframeAnimations.PosVec(0.2f, 0f, 0f)))
            ],
            ["right_hind_leg"] = [Rotation(K(1.72f, KeyframeAnimations.DegreeVec(360f, 0f, -45f)))],
            ["left_front_leg"] =
            [
                Rotation(K(0f, KeyframeAnimations.DegreeVec(160f, 10f, 37.5f))),
                Position(KL(0f, KeyframeAnimations.PosVec(-0.2f, 0f, 0f)))
            ],
            ["left_hind_leg"] = [Rotation(K(0f, KeyframeAnimations.DegreeVec(0f, 0f, 37.5f)))],
            ["tail"] = [Rotation(K(0f, KeyframeAnimations.DegreeVec(0f, 10f, 0f)), KL(0.72f, KeyframeAnimations.DegreeVec(0f, -14f, 0f)), KL(0.96f, KeyframeAnimations.DegreeVec(0f, -21f, 0f)), K(1.4f, KeyframeAnimations.DegreeVec(0f, -25f, 0f)), K(2.04f, KeyframeAnimations.DegreeVec(0f, -16.75f, 0f)), K(2.84f, KeyframeAnimations.DegreeVec(0f, 10f, 0f)))],
            ["head"] = [Rotation(K(0.04f, KeyframeAnimations.DegreeVec(0f, 0f, -6f)), KL(1.32f, KeyframeAnimations.DegreeVec(-6.45f, -1.45f, -6.5f)), KL(1.48f, KeyframeAnimations.DegreeVec(-6.45f, -1.45f, -6.5f)), K(2.84f, KeyframeAnimations.DegreeVec(0f, 0f, -6f)))],
            ["left_gills"] = [Rotation(K(0f, KeyframeAnimations.DegreeVec(0f, 38f, 0f)), K(1.28f, KeyframeAnimations.DegreeVec(0f, 45.5f, 0f)), K(1.64f, KeyframeAnimations.DegreeVec(0f, 45.5f, 0f)), K(2.8f, KeyframeAnimations.DegreeVec(0f, 38f, 0f)))],
            ["right_gills"] = [Rotation(K(0.04f, KeyframeAnimations.DegreeVec(0f, -50f, 0f)), K(1.32f, KeyframeAnimations.DegreeVec(0f, -59f, 0f)), K(1.6f, KeyframeAnimations.DegreeVec(0f, -59f, 0f)), K(2.84f, KeyframeAnimations.DegreeVec(0f, -50f, 0f)))],
            ["top_gills"] = [Rotation(K(0f, KeyframeAnimations.DegreeVec(33f, 0f, 0f)), K(1.28f, KeyframeAnimations.DegreeVec(47.4f, 0f, 0f)), K(1.64f, KeyframeAnimations.DegreeVec(47.4f, 0f, 0f)), K(2.8f, KeyframeAnimations.DegreeVec(33f, 0f, 0f)))]
        });

    private static AnimationDefinition BuildIdleFloorUnderwater() =>
        new(5.12f, true, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["body"] =
            [
                Rotation(K(0f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)), K(5.12f, KeyframeAnimations.DegreeVec(0f, 0f, 0f))),
                Position(K(0f, KeyframeAnimations.PosVec(0f, 1.5f, 0f)), K(5.12f, KeyframeAnimations.PosVec(0f, 1.5f, 0f)))
            ],
            ["right_front_leg"] =
            [
                Rotation(K(5.04f, KeyframeAnimations.DegreeVec(190f, -15f, -30f))),
                Position(K(0f, KeyframeAnimations.PosVec(0.3f, 0f, 0.5f)), K(5.12f, KeyframeAnimations.PosVec(0.3f, 0f, 0.5f)))
            ],
            ["right_hind_leg"] =
            [
                Rotation(K(0f, KeyframeAnimations.DegreeVec(360f, 0f, -30f))),
                Position(KL(0f, KeyframeAnimations.PosVec(0.18f, 0f, 0f)), KL(5.12f, KeyframeAnimations.PosVec(0.18f, 0f, 0f)))
            ],
            ["left_front_leg"] =
            [
                Rotation(K(5.04f, KeyframeAnimations.DegreeVec(190f, 18f, 30f))),
                Position(K(0f, KeyframeAnimations.PosVec(-0.1f, 0f, 0.5f)), K(5.12f, KeyframeAnimations.PosVec(-0.1f, 0f, 0.5f)))
            ],
            ["left_hind_leg"] =
            [
                Rotation(K(5.04f, KeyframeAnimations.DegreeVec(180f, 0f, 30f))),
                Position(KL(0f, KeyframeAnimations.PosVec(-0.2f, 0f, 0f)), KL(5.12f, KeyframeAnimations.PosVec(-0.2f, 0f, 0f)))
            ],
            ["tail"] = [Rotation(K(0.04f, KeyframeAnimations.DegreeVec(0f, -12f, 0f)), K(2.56f, KeyframeAnimations.DegreeVec(0f, 12f, 0f)), K(5.12f, KeyframeAnimations.DegreeVec(0f, -12f, 0f)))],
            ["left_gills"] = [Rotation(K(0f, KeyframeAnimations.DegreeVec(0f, 10f, 0f)), K(3.12f, KeyframeAnimations.DegreeVec(0f, -16f, 0f)), K(5.12f, KeyframeAnimations.DegreeVec(0f, 10f, 0f)))],
            ["right_gills"] = [Rotation(K(0f, KeyframeAnimations.DegreeVec(0f, -16f, 0f)), K(2.08f, KeyframeAnimations.DegreeVec(0f, 12f, 0f)), K(5.12f, KeyframeAnimations.DegreeVec(0f, -16f, 0f)))],
            ["top_gills"] = [Rotation(K(0f, KeyframeAnimations.DegreeVec(10f, 0f, 0f)), K(2.56f, KeyframeAnimations.DegreeVec(-16f, 0f, 0f)), K(5.12f, KeyframeAnimations.DegreeVec(10f, 0f, 0f)))]
        });

    private static AnimationDefinition BuildIdleUnderwater() =>
        new(5.2f, true, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["body"] =
            [
                Rotation(K(0f, KeyframeAnimations.DegreeVec(-8.9f, 0f, 0f)), K(2.6f, KeyframeAnimations.DegreeVec(-2.6f, 0f, 0f)), K(5.2f, KeyframeAnimations.DegreeVec(-8.9f, 0f, 0f))),
                Position(K(0f, KeyframeAnimations.PosVec(0f, 0f, 0f)), K(2.6f, KeyframeAnimations.PosVec(0f, -0.5f, 0f)), K(5.2f, KeyframeAnimations.PosVec(0f, 0f, 0f)))
            ],
            ["right_front_leg"] =
            [
                Rotation(K(0f, KeyframeAnimations.DegreeVec(-5f, -20f, -55f)), K(2.6f, KeyframeAnimations.DegreeVec(1f, -20f, -65f)), K(5.2f, KeyframeAnimations.DegreeVec(-5f, -20f, -55f))),
                Position(K(0f, KeyframeAnimations.PosVec(0.15f, -0.3391f, 0.0876f)))
            ],
            ["right_hind_leg"] =
            [
                Rotation(K(0f, KeyframeAnimations.DegreeVec(60f, -60f, 15f)), K(2.6f, KeyframeAnimations.DegreeVec(75f, -60f, 0f)), K(5.2f, KeyframeAnimations.DegreeVec(60f, -60f, 15f))),
                Position(K(5.2f, KeyframeAnimations.PosVec(0f, -0.4f, 0f)))
            ],
            ["left_front_leg"] =
            [
                Rotation(K(0f, KeyframeAnimations.DegreeVec(0f, 20f, 50f)), K(2.6f, KeyframeAnimations.DegreeVec(0f, 20f, 60f)), K(5.2f, KeyframeAnimations.DegreeVec(0f, 20f, 50f))),
                Position(K(2.68f, KeyframeAnimations.PosVec(-0.15f, -0.4f, 0.3f)))
            ],
            ["left_hind_leg"] =
            [
                Rotation(K(0f, KeyframeAnimations.DegreeVec(-135f, -50f, -330f)), K(2.6f, KeyframeAnimations.DegreeVec(-160f, -60f, -295f)), K(5.2f, KeyframeAnimations.DegreeVec(-135f, -50f, -330f))),
                Position(K(5.2f, KeyframeAnimations.PosVec(0f, -0.5f, 0f)))
            ],
            ["tail"] = [Rotation(K(0f, KeyframeAnimations.DegreeVec(0f, 15f, 0f)), K(2.6f, KeyframeAnimations.DegreeVec(0f, -32f, 0f)), K(5.2f, KeyframeAnimations.DegreeVec(0f, 15f, 0f)))],
            ["head"] = [Rotation(K(0f, KeyframeAnimations.DegreeVec(11.7f, 0f, 0f)), K(2.6f, KeyframeAnimations.DegreeVec(2.4f, 0f, 0f)), K(5.2f, KeyframeAnimations.DegreeVec(11.7f, 0f, 0f)))],
            ["left_gills"] = [Rotation(K(0f, KeyframeAnimations.DegreeVec(0f, -25.6f, 0f)), K(3.12f, KeyframeAnimations.DegreeVec(0f, 16f, 0f)), K(5.2f, KeyframeAnimations.DegreeVec(0f, -25.6f, 0f)))],
            ["right_gills"] = [Rotation(K(0f, KeyframeAnimations.DegreeVec(0f, 23f, 0f)), K(2.08f, KeyframeAnimations.DegreeVec(0f, -26f, 0f)), K(5.2f, KeyframeAnimations.DegreeVec(0f, 23f, 0f)))],
            ["top_gills"] = [Rotation(K(0f, KeyframeAnimations.DegreeVec(-19.2f, 0f, 0f)), K(2.6f, KeyframeAnimations.DegreeVec(12.3f, 0f, 0f)), K(5.2f, KeyframeAnimations.DegreeVec(-19.2f, 0f, 0f)))]
        });

    private static AnimationDefinition BuildBabySwim() =>
        new(1f, true, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["body"] =
            [
                Rotation(K(0f, KeyframeAnimations.DegreeVec(8f, 0f, 0f)), K(0.24f, KeyframeAnimations.DegreeVec(12f, 0f, 0f)), K(0.68f, KeyframeAnimations.DegreeVec(-7.5f, 0f, 0f)), KL(1f, KeyframeAnimations.DegreeVec(8f, 0f, 0f))),
                Position(K(0f, KeyframeAnimations.PosVec(0f, 0f, 0f)), K(0.52f, KeyframeAnimations.PosVec(0f, -1f, 0f)), K(0.96f, KeyframeAnimations.PosVec(0f, -0.05f, 0f)))
            ],
            ["right_front_leg"] = [Rotation(K(0f, KeyframeAnimations.DegreeVec(310f, 70f, 400f)), K(0.24f, KeyframeAnimations.DegreeVec(330f, 70f, 420f)), K(0.52f, KeyframeAnimations.DegreeVec(290f, 65f, 384f)), K(0.76f, KeyframeAnimations.DegreeVec(290f, 70f, 380f)), K(0.96f, KeyframeAnimations.DegreeVec(310f, 70f, 400f)))],
            ["right_hind_leg"] = [Rotation(K(0f, KeyframeAnimations.DegreeVec(105f, -95f, 180f)), K(0.28f, KeyframeAnimations.DegreeVec(105f, -85f, 180f)), K(0.52f, KeyframeAnimations.DegreeVec(105f, -80f, 180f)), K(0.76f, KeyframeAnimations.DegreeVec(105f, -95f, 180f)), K(0.96f, KeyframeAnimations.DegreeVec(105f, -95f, 180f)))],
            ["left_front_leg"] = [Rotation(K(0f, KeyframeAnimations.DegreeVec(-50f, -70f, -40f)), K(0.52f, KeyframeAnimations.DegreeVec(-60f, -68f, -30f)), K(0.76f, KeyframeAnimations.DegreeVec(-55f, -70f, -30f)), K(0.96f, KeyframeAnimations.DegreeVec(-50f, -70f, -40f)))],
            ["left_hind_leg"] = [Rotation(K(0f, KeyframeAnimations.DegreeVec(70f, -80f, 15f)), K(0.28f, KeyframeAnimations.DegreeVec(130f, -80f, -45f)), K(0.52f, KeyframeAnimations.DegreeVec(120f, -70f, -35f)), K(0.76f, KeyframeAnimations.DegreeVec(80f, -70f, 5f)), K(0.96f, KeyframeAnimations.DegreeVec(70f, -80f, 15f)))],
            ["tail"] = [Rotation(K(0f, KeyframeAnimations.DegreeVec(0f, 15f, 0f)), K(0.52f, KeyframeAnimations.DegreeVec(0f, -15f, 0f)), K(0.96f, KeyframeAnimations.DegreeVec(0f, 13.36f, 0f)))],
            ["head"] = [Rotation(K(0f, KeyframeAnimations.DegreeVec(-7.5f, 0f, 0f)), K(0.28f, KeyframeAnimations.DegreeVec(-13.9f, 0f, 0f)), K(0.72f, KeyframeAnimations.DegreeVec(14.23f, 0f, 0f)), K(1f, KeyframeAnimations.DegreeVec(-7.5f, 0f, 0f)))],
            ["left_gills"] = [Rotation(K(0f, KeyframeAnimations.DegreeVec(0f, -72f, 0f)), K(0.2f, KeyframeAnimations.DegreeVec(0f, -79.9f, 0f)), K(0.64f, KeyframeAnimations.DegreeVec(0f, -38.1f, 0f)), K(0.96f, KeyframeAnimations.DegreeVec(0f, -72f, 0f)))],
            ["right_gills"] = [Rotation(K(0f, KeyframeAnimations.DegreeVec(0f, 72f, 0f)), K(0.2f, KeyframeAnimations.DegreeVec(0f, 86.5f, 0f)), K(0.64f, KeyframeAnimations.DegreeVec(0f, 26.7f, 0f)), K(0.96f, KeyframeAnimations.DegreeVec(0f, 72f, 0f)))],
            ["top_gills"] = [Rotation(K(0f, KeyframeAnimations.DegreeVec(-57f, 0f, 0f)), K(0.2f, KeyframeAnimations.DegreeVec(-68.7f, 0f, 0f)), K(0.64f, KeyframeAnimations.DegreeVec(-24.2f, 0f, 0f)), K(0.96f, KeyframeAnimations.DegreeVec(-57f, 0f, 0f)))]
        });

    private static AnimationDefinition BuildWalkFloor() =>
        new(2.72f, true, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["body"] = [Rotation(KL(0f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)), KL(2.72f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)))],
            ["right_front_leg"] =
            [
                Rotation(K(0f, KeyframeAnimations.DegreeVec(2.5f, 10f, -30f)), K(1.32f, KeyframeAnimations.DegreeVec(2.5f, -22.5f, -30f)), K(2.72f, KeyframeAnimations.DegreeVec(2.5f, 10f, -30f))),
                Position(K(1.92f, KeyframeAnimations.PosVec(0.5f, 0f, 0.8f)))
            ],
            ["right_hind_leg"] =
            [
                Rotation(K(0f, KeyframeAnimations.DegreeVec(260f, -10f, 230f)), K(1.32f, KeyframeAnimations.DegreeVec(90f, -20f, 50f)), K(2.72f, KeyframeAnimations.DegreeVec(260f, -10f, 230f))),
                Position(K(0f, KeyframeAnimations.PosVec(0.5f, 0f, 0f)), K(2.72f, KeyframeAnimations.PosVec(0.5f, 0f, 0f)))
            ],
            ["left_front_leg"] =
            [
                Rotation(K(0f, KeyframeAnimations.DegreeVec(-2.5f, 32.5f, 30f)), K(1.32f, KeyframeAnimations.DegreeVec(-2.5f, -12f, 30f)), K(2.72f, KeyframeAnimations.DegreeVec(-2.5f, 32.5f, 30f))),
                Position(K(0f, KeyframeAnimations.PosVec(-0.5f, 0f, 0.7f)))
            ],
            ["left_hind_leg"] =
            [
                Rotation(K(0f, KeyframeAnimations.DegreeVec(-2.5f, -30f, 30f)), K(1.32f, KeyframeAnimations.DegreeVec(2.5f, 12f, 30f)), K(2.72f, KeyframeAnimations.DegreeVec(-2.5f, -30f, 30f))),
                Position(K(0f, KeyframeAnimations.PosVec(-0.5f, 0f, 0f)), K(2.72f, KeyframeAnimations.PosVec(-0.5f, 0f, 0f)))
            ],
            ["tail"] = [Rotation(K(0f, KeyframeAnimations.DegreeVec(0f, -10f, 0f)), K(1.32f, KeyframeAnimations.DegreeVec(0f, 10f, 0f)), K(2.72f, KeyframeAnimations.DegreeVec(0f, -10f, 0f)))],
            ["head"] = [Rotation(K(0f, KeyframeAnimations.DegreeVec(0f, -7.5f, 0f)), K(1.32f, KeyframeAnimations.DegreeVec(0f, 6.7f, 0f)), K(2.72f, KeyframeAnimations.DegreeVec(0f, -7.5f, 0f)))],
            ["left_gills"] = [Rotation(K(0f, KeyframeAnimations.DegreeVec(0f, 38.1f, 0f)), K(1.08f, KeyframeAnimations.DegreeVec(0f, 45.6f, 0f)), K(1.44f, KeyframeAnimations.DegreeVec(0f, 45.6f, 0f)), K(2.72f, KeyframeAnimations.DegreeVec(0f, 38.1f, 0f)))],
            ["right_gills"] = [Rotation(K(0f, KeyframeAnimations.DegreeVec(0f, -50f, 0f)), K(1.08f, KeyframeAnimations.DegreeVec(0f, -59f, 0f)), K(1.36f, KeyframeAnimations.DegreeVec(0f, -59f, 0f)), K(2.72f, KeyframeAnimations.DegreeVec(0f, -50f, 0f)))],
            ["top_gills"] = [Rotation(K(0f, KeyframeAnimations.DegreeVec(33f, 0f, 0f)), K(1.08f, KeyframeAnimations.DegreeVec(47.4f, 0f, 0f)), K(1.44f, KeyframeAnimations.DegreeVec(47.4f, 0f, 0f)), K(2.72f, KeyframeAnimations.DegreeVec(33f, 0f, 0f)))]
        });

    private static AnimationDefinition BuildWalkFloorUnderwater() =>
        new(2.04f, true, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["body"] =
            [
                Rotation(K(0f, KeyframeAnimations.DegreeVec(0f, -5f, 6f)), K(1.04f, KeyframeAnimations.DegreeVec(0f, 5f, -4f)), K(2.04f, KeyframeAnimations.DegreeVec(0f, -5f, 6f))),
                Position(K(0f, KeyframeAnimations.PosVec(0f, 1.5f, 0f)), K(2.04f, KeyframeAnimations.PosVec(0f, 1.5f, 0f)))
            ],
            ["right_front_leg"] =
            [
                Rotation(K(0f, KeyframeAnimations.DegreeVec(0f, -15f, -30f)), K(0.72f, KeyframeAnimations.DegreeVec(0f, 9f, -30f)), K(1.12f, KeyframeAnimations.DegreeVec(0f, 13f, -24f)), K(1.44f, KeyframeAnimations.DegreeVec(0f, 9f, -30f)), K(2.04f, KeyframeAnimations.DegreeVec(0f, -15f, -30f))),
                Position(K(0f, KeyframeAnimations.PosVec(0f, 0f, 0f)), K(0.96f, KeyframeAnimations.PosVec(0.4f, 0f, 0f)), K(2.04f, KeyframeAnimations.PosVec(0f, 0f, 0f)))
            ],
            ["right_hind_leg"] =
            [
                Rotation(K(0f, KeyframeAnimations.DegreeVec(95f, -20f, 70f)), K(0.68f, KeyframeAnimations.DegreeVec(255f, -20f, 230f)), K(1.12f, KeyframeAnimations.DegreeVec(280f, -25f, 255f)), K(1.52f, KeyframeAnimations.DegreeVec(255f, -20f, 230f)), K(2.04f, KeyframeAnimations.DegreeVec(95f, -20f, 70f))),
                Position(K(0f, KeyframeAnimations.PosVec(0.5f, 0f, 0f)), K(0.96f, KeyframeAnimations.PosVec(0.5f, 0f, 0f)), K(2.04f, KeyframeAnimations.PosVec(0.5f, 0f, 0f)))
            ],
            ["left_front_leg"] =
            [
                Rotation(K(0f, KeyframeAnimations.DegreeVec(-5f, -20f, 30f)), K(0.72f, KeyframeAnimations.DegreeVec(0f, 10f, 25f)), K(1.44f, KeyframeAnimations.DegreeVec(0f, 10f, 25f)), K(2.04f, KeyframeAnimations.DegreeVec(-5f, -20f, 30f))),
                Position(K(0f, KeyframeAnimations.PosVec(-0.5f, 0f, 0f)), K(0.96f, KeyframeAnimations.PosVec(0f, 0f, 0f)), K(2.04f, KeyframeAnimations.PosVec(-0.5f, 0f, 0f)))
            ],
            ["left_hind_leg"] =
            [
                Rotation(K(0f, KeyframeAnimations.DegreeVec(5f, 15f, 25f)), K(0.72f, KeyframeAnimations.DegreeVec(-10f, -20f, 35f)), K(1.44f, KeyframeAnimations.DegreeVec(-10f, -20f, 35f)), K(2.04f, KeyframeAnimations.DegreeVec(5f, 15f, 25f))),
                Position(K(0f, KeyframeAnimations.PosVec(-0.7f, 0f, 0f)), K(0.96f, KeyframeAnimations.PosVec(-0.4f, 0f, 0f)), K(2.04f, KeyframeAnimations.PosVec(-0.7f, 0f, 0f)))
            ],
            ["tail"] = [Rotation(K(0f, KeyframeAnimations.DegreeVec(0f, 16.5f, 0f)), K(0.56f, KeyframeAnimations.DegreeVec(0f, 1.12f, 0f)), K(1.44f, KeyframeAnimations.DegreeVec(0f, 17.5f, 0f)), K(2.04f, KeyframeAnimations.DegreeVec(0f, 16.5f, 0f)))],
            ["head"] = [Rotation(K(0f, KeyframeAnimations.DegreeVec(0f, 4f, -5.8f)), K(1.04f, KeyframeAnimations.DegreeVec(0f, -4f, 5f)), K(2.04f, KeyframeAnimations.DegreeVec(0f, 4f, -5.8f)))],
            ["left_gills"] = [Rotation(K(0f, KeyframeAnimations.DegreeVec(0f, -60f, 0f)), K(1.24f, KeyframeAnimations.DegreeVec(0f, -45f, 0f)), K(2.04f, KeyframeAnimations.DegreeVec(0f, -60f, 0f)))],
            ["right_gills"] = [Rotation(K(0f, KeyframeAnimations.DegreeVec(0f, 38f, 0f)), K(0.64f, KeyframeAnimations.DegreeVec(0f, 53f, 0f)), K(2.04f, KeyframeAnimations.DegreeVec(0f, 38f, 0f)))],
            ["top_gills"] = [Rotation(K(0f, KeyframeAnimations.DegreeVec(-34f, 0f, 0f)), K(1.04f, KeyframeAnimations.DegreeVec(-41.5f, 0f, 0f)), K(2.04f, KeyframeAnimations.DegreeVec(-34f, 0f, 0f)))]
        });

    private static AnimationDefinition BuildPlayDead() =>
        new(0.04f, false, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["body"] =
            [
                Rotation(K(0f, KeyframeAnimations.DegreeVec(0f, 0f, 30f))),
                Position(KL(0f, KeyframeAnimations.PosVec(0f, 0f, 0f))),
                Scale(KL(0f, KeyframeAnimations.ScaleVec(1f, 1f, 1f)))
            ],
            ["right_front_leg"] =
            [
                Rotation(K(0f, KeyframeAnimations.DegreeVec(202.35f, -30.33f, -40.82f))),
                Position(KL(0f, KeyframeAnimations.PosVec(0.2f, 0f, 0f))),
                Scale(KL(0f, KeyframeAnimations.ScaleVec(1f, 1f, 1f)))
            ],
            ["right_hind_leg"] =
            [
                Rotation(K(0f, KeyframeAnimations.DegreeVec(418f, -50f, 35.8f))),
                Position(KL(0f, KeyframeAnimations.PosVec(0f, 0f, 0f))),
                Scale(KL(0f, KeyframeAnimations.ScaleVec(1f, 1f, 1f)))
            ],
            ["left_front_leg"] =
            [
                Rotation(K(0f, KeyframeAnimations.DegreeVec(177f, 22.76f, 15.9f))),
                Position(KL(0f, KeyframeAnimations.PosVec(-0.2f, 0f, 0f))),
                Scale(KL(0f, KeyframeAnimations.ScaleVec(1f, 1f, 1f)))
            ],
            ["left_hind_leg"] =
            [
                Rotation(K(0f, KeyframeAnimations.DegreeVec(16.4287f, -37.6467f, 16.9822f))),
                Position(KL(0f, KeyframeAnimations.PosVec(0f, 0f, 0f))),
                Scale(KL(0f, KeyframeAnimations.ScaleVec(1f, 1f, 1f)))
            ],
            ["tail"] =
            [
                Rotation(KL(0f, KeyframeAnimations.DegreeVec(0f, -18.67f, 0f))),
                Position(KL(0f, KeyframeAnimations.PosVec(0f, 0f, 0f))),
                Scale(KL(0f, KeyframeAnimations.ScaleVec(1f, 1f, 1f)))
            ],
            ["head"] =
            [
                Rotation(KL(0f, KeyframeAnimations.DegreeVec(-4.21f, -0.95f, -6.33f))),
                Position(KL(0f, KeyframeAnimations.PosVec(0f, 0f, 0f))),
                Scale(KL(0f, KeyframeAnimations.ScaleVec(1f, 1f, 1f)))
            ],
            ["left_gills"] =
            [
                Rotation(K(0f, KeyframeAnimations.DegreeVec(0f, 43.21f, 0f))),
                Position(KL(0f, KeyframeAnimations.PosVec(0f, 0f, 0f))),
                Scale(KL(0f, KeyframeAnimations.ScaleVec(1f, 1f, 1f)))
            ],
            ["right_gills"] =
            [
                Rotation(K(0f, KeyframeAnimations.DegreeVec(0f, -55.87f, 0f))),
                Position(KL(0f, KeyframeAnimations.PosVec(0f, 0f, 0f))),
                Scale(KL(0f, KeyframeAnimations.ScaleVec(1f, 1f, 1f)))
            ],
            ["top_gills"] =
            [
                Rotation(K(0f, KeyframeAnimations.DegreeVec(43f, 0f, 0f))),
                Position(KL(0f, KeyframeAnimations.PosVec(0f, 0f, 0f))),
                Scale(KL(0f, KeyframeAnimations.ScaleVec(1f, 1f, 1f)))
            ]
        });


    /// <summary>
    /// Adult axolotl <c>setupAnim</c> behaviour: four blended factor-driven states plus mirrored leg rotations.
    /// The reference reads these factors and angles from <c>AxolotlRenderState</c>; they are passed explicitly here
    /// because <see cref="Sharpcraft.Client.World.EntityRenderState"/> has no equivalent fields yet (see report).
    /// </summary>
    /// <param name="ageInTicks">Reference <c>ageInTicks</c> (managed by the caller).</param>
    /// <param name="bodyYawDegrees">Reference <c>state.yRot</c> added to the body yaw.</param>
    /// <param name="pitchDegrees">Reference <c>state.xRot</c> used by the swimming state.</param>
    /// <param name="playingDeadFactor">Blend weight of the play-dead state in [0, 1].</param>
    /// <param name="inWaterFactor">Blend weight of being in water in [0, 1].</param>
    /// <param name="onGroundFactor">Blend weight of being on the ground in [0, 1].</param>
    /// <param name="movingFactor">Blend weight of horizontal movement in [0, 1].</param>
    public static void ApplyAdult(
        EntityPose pose,
        float ageInTicks,
        float bodyYawDegrees,
        float pitchDegrees,
        float playingDeadFactor,
        float inWaterFactor,
        float onGroundFactor,
        float movingFactor)
    {
        var notMovingFactor = 1f - movingFactor;
        var mirroredLegsFactor = 1f - MathF.Min(onGroundFactor, movingFactor);
        AddRotation(pose, "body", 0f, bodyYawDegrees * (MathF.PI / 180f), 0f);
        SetupSwimming(pose, ageInTicks, pitchDegrees, MathF.Min(movingFactor, inWaterFactor));
        SetupWaterHovering(pose, ageInTicks, MathF.Min(notMovingFactor, inWaterFactor));
        SetupGroundCrawling(pose, ageInTicks, MathF.Min(movingFactor, onGroundFactor));
        SetupLayStillOnGround(pose, ageInTicks, MathF.Min(notMovingFactor, onGroundFactor));
        SetupPlayDead(pose, playingDeadFactor);
        ApplyMirrorLegRotations(pose, mirroredLegsFactor);
    }

    private static void SetupSwimming(EntityPose pose, float ageInTicks, float pitchDegrees, float factor)
    {
        if (factor <= 1e-5f)
        {
            return;
        }

        var animMoveSpeed = ageInTicks * 0.33f;
        var sineSway = MathF.Sin(animMoveSpeed);
        var cosineSway = MathF.Cos(animMoveSpeed);
        var bodySway = 0.13f * sineSway;
        AddRotation(pose, "body", (pitchDegrees * (MathF.PI / 180f) + bodySway) * factor, 0f, 0f);
        AddRotation(pose, "head", -bodySway * 1.8f * factor, 0f, 0f);
        AddPosition(pose, "body", 0f, -0.45f * cosineSway * factor, 0f);
        AddRotation(pose, "top_gills", (-0.5f * sineSway - 0.8f) * factor, 0f, 0f);
        var gillYRot = (0.3f * sineSway + 0.9f) * factor;
        AddRotation(pose, "left_gills", 0f, gillYRot, 0f);
        AddRotation(pose, "right_gills", 0f, -gillYRot, 0f);
        AddRotation(pose, "tail", 0f, 0.3f * MathF.Cos(animMoveSpeed * 0.9f) * factor, 0f);
        AddRotation(pose, "left_hind_leg", 1.8849558f * factor, -0.4f * sineSway * factor, (MathF.PI / 2f) * factor);
        AddRotation(pose, "left_front_leg", 1.8849558f * factor, (-0.2f * cosineSway - 0.1f) * factor, (MathF.PI / 2f) * factor);
    }

    private static void SetupWaterHovering(EntityPose pose, float ageInTicks, float factor)
    {
        if (factor <= 1e-5f)
        {
            return;
        }

        var animMoveSpeed = ageInTicks * 0.075f;
        var cosineSway = MathF.Cos(animMoveSpeed);
        var sineSway = MathF.Sin(animMoveSpeed) * 0.15f;
        var bodyXRot = (-0.15f + 0.075f * cosineSway) * factor;
        AddRotation(pose, "body", bodyXRot, 0f, 0f);
        AddPosition(pose, "body", 0f, -sineSway * factor, 0f);
        AddRotation(pose, "head", -bodyXRot, 0f, 0f);
        AddRotation(pose, "top_gills", 0.2f * cosineSway * factor, 0f, 0f);
        var gillYRot = (-0.3f * cosineSway - 0.19f) * factor;
        AddRotation(pose, "left_gills", 0f, gillYRot, 0f);
        AddRotation(pose, "right_gills", 0f, -gillYRot, 0f);
        AddRotation(pose, "left_hind_leg", ((MathF.PI * 3f / 4f) - cosineSway * 0.11f) * factor, 0.47123894f * factor, 1.7278761f * factor);
        AddRotation(pose, "left_front_leg", ((MathF.PI / 4f) - cosineSway * 0.2f) * factor, 2.042035f * factor, 0f);
        AddRotation(pose, "tail", 0f, 0.5f * cosineSway * factor, 0f);
    }

    private static void SetupGroundCrawling(EntityPose pose, float ageInTicks, float factor)
    {
        if (factor <= 1e-5f)
        {
            return;
        }

        var animMoveSpeed = ageInTicks * 0.11f;
        var cosineSway = MathF.Cos(animMoveSpeed);
        var hindLegYRotSway = (cosineSway * cosineSway - 2f * cosineSway) / 5f;
        var frontLegYRotSway = 0.7f * cosineSway;
        var headAndTailYRot = 0.09f * cosineSway * factor;
        AddRotation(pose, "head", 0f, headAndTailYRot, 0f);
        AddRotation(pose, "tail", 0f, headAndTailYRot, 0f);
        var gillAngle = (0.6f - 0.08f * (cosineSway * cosineSway + 2f * MathF.Sin(animMoveSpeed))) * factor;
        AddRotation(pose, "top_gills", gillAngle, 0f, 0f);
        AddRotation(pose, "left_gills", 0f, -gillAngle, 0f);
        AddRotation(pose, "right_gills", 0f, gillAngle, 0f);
        var hindLegXRot = 0.9424779f * factor;
        var frontLegXRot = 1.0995574f * factor;
        AddRotation(pose, "left_hind_leg", hindLegXRot, (1.5f - hindLegYRotSway) * factor, -0.1f * factor);
        AddRotation(pose, "left_front_leg", frontLegXRot, ((MathF.PI / 2f) - frontLegYRotSway) * factor, 0f);
        AddRotation(pose, "right_hind_leg", hindLegXRot, (-1.0f - hindLegYRotSway) * factor, 0f);
        AddRotation(pose, "right_front_leg", frontLegXRot, (-MathF.PI / 2f - frontLegYRotSway) * factor, 0f);
    }

    private static void SetupLayStillOnGround(EntityPose pose, float ageInTicks, float factor)
    {
        if (factor <= 1e-5f)
        {
            return;
        }

        var animMoveSpeed = ageInTicks * 0.09f;
        var sineSway = MathF.Sin(animMoveSpeed);
        var cosineSway = MathF.Cos(animMoveSpeed);
        var movement = sineSway * sineSway - 2f * sineSway;
        var movement2 = cosineSway * cosineSway - 3f * sineSway;
        AddRotation(pose, "head", -0.09f * movement * factor, 0f, -0.2f * factor);
        AddRotation(pose, "tail", 0f, (-0.1f + 0.1f * movement) * factor, 0f);
        var gillAngle = (0.6f + 0.05f * movement2) * factor;
        AddRotation(pose, "top_gills", gillAngle, 0f, 0f);
        AddRotation(pose, "left_gills", 0f, -gillAngle, 0f);
        AddRotation(pose, "right_gills", 0f, gillAngle, 0f);
        AddRotation(pose, "left_hind_leg", 1.1f * factor, 1.0f * factor, 0f);
        AddRotation(pose, "left_front_leg", 0.8f * factor, 2.3f * factor, -0.5f * factor);
    }

    private static void SetupPlayDead(EntityPose pose, float factor)
    {
        if (factor <= 1e-5f)
        {
            return;
        }

        AddRotation(pose, "left_hind_leg", 1.4137167f * factor, 1.0995574f * factor, (MathF.PI / 4f) * factor);
        AddRotation(pose, "left_front_leg", (MathF.PI / 4f) * factor, 2.042035f * factor, 0f);
        AddRotation(pose, "body", -0.15f * factor, 0f, 0.35f * factor);
    }

    private static void ApplyMirrorLegRotations(EntityPose pose, float factor)
    {
        if (factor <= 1e-5f)
        {
            return;
        }

        var leftHind = pose.Get("left_hind_leg").Rotation;
        AddRotation(pose, "right_hind_leg", leftHind.X * factor, -leftHind.Y * factor, -leftHind.Z * factor);
        var leftFront = pose.Get("left_front_leg").Rotation;
        AddRotation(pose, "right_front_leg", leftFront.X * factor, -leftFront.Y * factor, -leftFront.Z * factor);
    }

    private static void AddRotation(EntityPose pose, string part, float x, float y, float z) =>
        pose.Add(part, new PoseOffset(Vector3.Zero, new Vector3(x, y, z), Vector3.Zero));

    private static void AddPosition(EntityPose pose, string part, float x, float y, float z) =>
        pose.Add(part, new PoseOffset(new Vector3(x, y, z), Vector3.Zero, Vector3.Zero));

}
