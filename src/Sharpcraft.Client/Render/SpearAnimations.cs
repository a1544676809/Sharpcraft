// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>SpearAnimations</c> easing helpers and the third-person STAB item offset. The formulas are ported
/// value-for-value from the 26.3 reference: <c>progress</c> clamps an inverse lerp, <c>inQuad</c> is <c>x²</c> and
/// <c>inOutExpo</c> is the two-branch exponential. <c>thirdPersonAttackItem</c> rotates the held item about -X by
/// <c>70° · (attack − retract)</c> around the pivot <c>(0, −0.125, 0.125)</c> and translates by the weapon jet.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.effects.SpearAnimations", "ref://mc-26.3-analysis/src/net/minecraft/client/model/effects/SpearAnimations.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "progress/inQuad/inOutExpo helpers and thirdPersonAttackItem offset only; hand/use-item animations live elsewhere")]
public static class SpearAnimations
{
    /// <summary>Reference <c>itemInHandDepth</c>: the depth the item sits at inside the hand.</summary>
    public const float ItemInHandDepth = 0.125f;

    /// <summary>Reference pivot the STAB rotation is applied around, in hand space.</summary>
    public static readonly System.Numerics.Vector3 AttackPivot = new(0f, -0.125f, 0.125f);

    /// <summary>
    /// Reference <c>thirdPersonAttackItem</c> as a row-vector matrix: a rotation of <c>-degrees</c> about X around
    /// <see cref="AttackPivot"/>, followed by the weapon jet translation.
    /// </summary>
    /// <remarks>
    /// Solved against <c>StabProbe.java</c>: at <c>swingAnimation = 0.5</c> the reference world matrix yields
    /// <c>Rx(-69.655°)</c> with translation <c>(0, -0.19874, -0.03566)</c>, which is <c>pivot - pivot·R</c> for that
    /// rotation — exactly what the rotate-around-pivot product below produces.
    /// </remarks>
    public static System.Numerics.Matrix4x4 ThirdPersonAttackItemMatrix(float swingAnimation, float jetForward)
    {
        var (rotationDegrees, yTranslation) = ThirdPersonAttackItem(swingAnimation, jetForward);
        if (rotationDegrees == 0f && yTranslation == 0f)
        {
            return System.Numerics.Matrix4x4.Identity;
        }

        var pivot = AttackPivot;
        return System.Numerics.Matrix4x4.CreateTranslation(-pivot)
            * System.Numerics.Matrix4x4.CreateRotationX(-rotationDegrees * (MathF.PI / 180f))
            * System.Numerics.Matrix4x4.CreateTranslation(pivot)
            * System.Numerics.Matrix4x4.CreateTranslation(0f, yTranslation, 0f);
    }

    /// <summary>Reference <c>progress</c>: inverse lerp clamped to 0..1.</summary>
    public static float Progress(float time, float start, float end) =>
        Math.Clamp((time - start) / (end - start), 0f, 1f);

    /// <summary>Reference <c>Ease.inQuad</c>.</summary>
    public static float InQuad(float x) => x * x;

    /// <summary>Reference <c>Ease.inOutExpo</c>.</summary>
    public static float InOutExpo(float x) =>
        x < 0.5f
            ? x == 0f ? 0f : (float)(Math.Pow(2d, (20d * x) - 10d) / 2d)
            : x == 1f ? 1f : (float)((2d - Math.Pow(2d, (-20d * x) + 10d)) / 2d);

    /// <summary>
    /// Reference <c>SpearAnimations.thirdPersonAttackItem</c>: the STAB swing offset applied to the held item.
    /// Returns the rotation about -X in degrees and the Y translation contributed by the weapon's jet.
    /// </summary>
    public static (float RotationDegrees, float YTranslation) ThirdPersonAttackItem(float swingAnimation, float jetForward)
    {
        if (swingAnimation <= 0f)
        {
            return (0f, 0f);
        }

        var attack = InQuad(Progress(swingAnimation, 0.05f, 0.2f));
        var retract = InOutExpo(Progress(swingAnimation, 0.4f, 1f));
        return (70f * (attack - retract), jetForward * (attack - retract));
    }

    /// <summary>Reference <c>Ease.inOutSine</c>.</summary>
    public static float InOutSine(float x) => -(MathF.Cos(MathF.PI * x) - 1f) / 2f;

    /// <summary>Reference <c>Ease.outBack</c>.</summary>
    public static float OutBack(float x) =>
        1f + (2.70158f * Cube(x - 1f)) + (1.70158f * (x - 1f) * (x - 1f));

    /// <summary>Reference <c>Ease.outCubic</c>.</summary>
    public static float OutCubic(float x) => 1f - Cube(1f - x);

    /// <summary>Reference <c>Ease.outCirc</c>.</summary>
    public static float OutCirc(float x) => MathF.Sqrt(MathF.Max(0f, 1f - ((x - 1f) * (x - 1f))));

    /// <summary>Reference <c>Ease.inOutElastic</c>.</summary>
    public static float InOutElastic(float x)
    {
        if (x == 0f)
        {
            return 0f;
        }

        if (x == 1f)
        {
            return 1f;
        }

        var sin = MathF.Sin((20f * x - 11.125f) * MathF.PI * 4f / 9f);
        return x < 0.5f
            ? (float)(-(Math.Pow(2d, (20d * x) - 10d) * sin) / 2d)
            : (float)((Math.Pow(2d, (-20d * x) + 10d) * sin / 2d) + 1d);
    }

    private static float Cube(float x) => x * x * x;

    /// <summary>
    /// Reference <c>SpearAnimations.UseParams</c>: the ten progress values the spear use animation reads.
    /// </summary>
    [Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.effects.SpearAnimations", "ref://mc-26.3-analysis/src/net/minecraft/client/model/effects/SpearAnimations.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Full, Note = "SpearAnimations.UseParams progress values")]
    public readonly record struct UseParams(
        float RaiseProgress,
        float RaiseProgressStart,
        float RaiseProgressMiddle,
        float RaiseProgressEnd,
        float SwayProgress,
        float LowerProgress,
        float RaiseBackProgress,
        float SwayIntensity,
        float SwayScaleSlow,
        float SwayScaleFast)
    {
        /// <summary>Reference <c>UseParams.fromKineticWeapon(kineticWeapon, time)</c>.</summary>
        public static UseParams FromKineticWeapon(in KineticWeapon weapon, float time)
        {
            var finishRaisingTick = weapon.DelayTicks;
            var finishSwayingTick = weapon.DismountMaxTicks + finishRaisingTick;
            var startSwayingTick = finishSwayingTick - 20;
            var finishLoweringTick = weapon.KnockbackMaxTicks + finishRaisingTick;
            var startLoweringTick = finishLoweringTick - 40;
            var finishRaisingBackTick = weapon.DamageMaxTicks + finishRaisingTick;

            var raiseProgress = Progress(time, 0f, finishRaisingTick);
            var swayProgress = Progress(time, startSwayingTick, finishSwayingTick);
            var lowerProgress = OutCubic(InOutElastic(Progress(time - 20f, startLoweringTick, finishLoweringTick)));
            var raiseBackProgress = Progress(time, finishRaisingBackTick - 5, finishRaisingBackTick);
            var swayIntensity = (2f * OutCirc(swayProgress)) - (2f * OutCirc(raiseBackProgress));

            return new UseParams(
                raiseProgress,
                Progress(raiseProgress, 0f, 0.5f),
                Progress(raiseProgress, 0.5f, 0.8f),
                Progress(raiseProgress, 0.8f, 1f),
                swayProgress,
                lowerProgress,
                raiseBackProgress,
                swayIntensity,
                MathF.Sin(time * 19f * (MathF.PI / 180f)) * swayIntensity,
                MathF.Sin(time * 30f * (MathF.PI / 180f)) * swayIntensity);
        }
    }
    /// <summary>
    /// Reference <c>SpearAnimations.thirdPersonUseItem</c> as a row-vector matrix: a translate, then a rotation about -X
    /// around <c>(0, -0.03125, 0.125)</c>, then a rotation about +Y around <c>(0, 0, 0.125)</c>. <paramref name="invert"/>
    /// is +1 for the right arm and -1 for the left, matching the reference's <c>invert</c>.
    /// </summary>
    public static System.Numerics.Matrix4x4 ThirdPersonUseItemMatrix(
        in UseParams parameters,
        in KineticWeapon weapon,
        float swingAnimation,
        int invert)
    {
        var attack = InQuad(Progress(swingAnimation, 0.05f, 0.2f));
        var retract = InOutExpo(Progress(swingAnimation, 0.4f, 1f));
        var raiseProgressModified = 1f - OutBack(1f - parameters.RaiseProgress);

        var translate = System.Numerics.Matrix4x4.CreateTranslation(
            0f,
            0f,
            -weapon.ForwardMovement * (raiseProgressModified - parameters.RaiseBackProgress));

        var rotationXDegrees = (70f * (parameters.RaiseProgress - parameters.RaiseBackProgress))
            - (40f * (attack - retract));
        var rotationYDegrees = invert * 90f
            * (parameters.RaiseProgress - parameters.SwayProgress + (3f * retract) + attack);

        var rotateX = AroundPivot(
            System.Numerics.Matrix4x4.CreateRotationX(-rotationXDegrees * (MathF.PI / 180f)),
            new System.Numerics.Vector3(0f, -0.03125f, 0.125f));
        var rotateY = AroundPivot(
            System.Numerics.Matrix4x4.CreateRotationY(rotationYDegrees * (MathF.PI / 180f)),
            new System.Numerics.Vector3(0f, 0f, 0.125f));

        // Reference order: translate, then the X rotation, then the Y rotation.
        return rotateY * rotateX * translate;
    }

    private static System.Numerics.Matrix4x4 AroundPivot(
        in System.Numerics.Matrix4x4 rotation,
        System.Numerics.Vector3 pivot) =>
        System.Numerics.Matrix4x4.CreateTranslation(-pivot)
        * rotation
        * System.Numerics.Matrix4x4.CreateTranslation(pivot);
    /// <summary>Reference <c>SwingAnimationType.STAB</c> id.</summary>
    public const int StabSwingType = 2;

    /// <summary>
    /// Applies the STAB offset to one held-item part. The reference rotates about <c>Axis.XN</c>; the mesher folds a pose
    /// offset in as <c>rotation.X - offset.X</c>, so a rotation of <c>+a</c> about −X is supplied as <c>+a</c> degrees
    /// worth of radians in the offset. <paramref name="jetForward"/> is zero whenever the stack has no kinetic weapon
    /// component, which is exactly what the reference falls back to.
    /// </summary>
    public static void ApplyThirdPersonAttackItem(
        float swingAnimation,
        int swingType,
        float jetForward,
        EntityPose pose,
        string partName = "held_item")
    {
        ArgumentNullException.ThrowIfNull(pose);
        if (swingType != StabSwingType)
        {
            return;
        }

        var (rotationDegrees, yTranslation) = ThirdPersonAttackItem(swingAnimation, jetForward);
        if (rotationDegrees == 0f && yTranslation == 0f)
        {
            return;
        }

        pose.Add(partName, new PoseOffset(
            Position: new System.Numerics.Vector3(0f, yTranslation, 0f),
            Rotation: new System.Numerics.Vector3(rotationDegrees * (MathF.PI / 180f), 0f, 0f),
            Scale: System.Numerics.Vector3.Zero));
    }
}
