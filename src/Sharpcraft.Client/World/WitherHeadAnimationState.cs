// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.World;

/// <summary>One Wither side-head aim input (relative target yaw/pitch in degrees) or no target.</summary>
public readonly record struct WitherHeadAim(bool HasTarget, float TargetYaw, float TargetPitch);

/// <summary>
/// Wither side-head aim clock, ported from <c>WitherBoss.aiStep</c>: with a target the head yaw moves 10°/tick and
/// pitch 40°/tick toward the target; without one, yaw eases back to the body yaw at 10°/tick and pitch holds.
/// </summary>
public sealed class WitherHeadAnimationState
{
    private readonly float[] _yaw = new float[2];
    private readonly float[] _pitch = new float[2];

    public void Tick(float bodyYawDegrees, ReadOnlySpan<WitherHeadAim> aims)
    {
        for (var i = 0; i < 2; i++)
        {
            if (i < aims.Length && aims[i].HasTarget)
            {
                _yaw[i] = RotLerp(_yaw[i], aims[i].TargetYaw, 10f);
                _pitch[i] = RotLerp(_pitch[i], aims[i].TargetPitch, 40f);
            }
            else
            {
                _yaw[i] = RotLerp(_yaw[i], bodyYawDegrees, 10f);
            }
        }
    }

    public float Yaw(int head) => _yaw[head];

    public float Pitch(int head) => _pitch[head];

    private static float RotLerp(float from, float to, float maxStep)
    {
        var diff = WrapDegrees(to - from);
        if (diff > maxStep)
        {
            diff = maxStep;
        }
        else if (diff < -maxStep)
        {
            diff = -maxStep;
        }

        return from + diff;
    }

    private static float WrapDegrees(float value)
    {
        value %= 360f;
        if (value >= 180f)
        {
            value -= 360f;
        }

        if (value < -180f)
        {
            value += 360f;
        }

        return value;
    }
}
