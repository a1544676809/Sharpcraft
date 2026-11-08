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
