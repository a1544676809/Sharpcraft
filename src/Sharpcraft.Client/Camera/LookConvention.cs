// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Camera;

/// <summary>
/// Converts the camera's internal yaw and pitch to the angles written on the wire.
/// </summary>
/// <remarks>
/// Both angles differ by a sign, and neither difference is cosmetic.
/// <para>
/// Pitch: vanilla's <c>xRot</c> grows downwards, a positive value meaning looking down, which follows from its
/// view vector taking a negative vertical component from the sine of the pitch. The camera here grows upwards, so
/// the sign is flipped. Get this wrong and everyone else sees the player look up when they look down.
/// </para>
/// <para>
/// Yaw: both sides increase clockwise seen from above, but they disagree about where zero points. Vanilla's zero
/// faces +Z, while the camera's zero faces −Z, so the two are half a turn apart. Get this wrong and the body
/// faces backwards: the player appears to be looking away from whatever they are actually looking at.
/// </para>
/// <para>
/// Both rules were derived by equating the two forward vectors: camera <c>(sin y, sin p, −cos y)</c> against
/// vanilla <c>(−sin y, −sin p, cos y)</c>. That equation is satisfied by <c>y + 180</c> and <c>−p</c>, not by
/// negating the yaw, which is the intuitive but wrong guess. A test pins the equivalence over a grid of angles
/// rather than trusting the arithmetic.
/// </para>
/// </remarks>
public static class LookConvention
{
    private const float RadiansToDegrees = 180f / MathF.PI;
    private const float DegreesToRadians = MathF.PI / 180f;

    /// <summary>Camera yaw in radians to the degrees written on the wire.</summary>
    /// <param name="cameraYawRadians">Zero faces −Z and positive turns right.</param>
    public static float ToWireYawDegrees(float cameraYawRadians) => WrapDegrees((cameraYawRadians * RadiansToDegrees) + 180f);

    /// <summary>Wire yaw in degrees to the camera's yaw in radians.</summary>
    /// <param name="wireYawDegrees">Zero faces +Z.</param>
    /// <remarks>Half a turn is its own inverse, so the same offset converts back.</remarks>
    public static float FromWireYawDegrees(float wireYawDegrees) =>
        WrapDegrees(wireYawDegrees + 180f) * DegreesToRadians;

    /// <summary>Wraps an angle into half open interval from minus one eighty to one eighty.</summary>
    private static float WrapDegrees(float degrees)
    {
        var wrapped = degrees % 360f;

        if (wrapped >= 180f)
        {
            wrapped -= 360f;
        }
        else if (wrapped < -180f)
        {
            wrapped += 360f;
        }

        return wrapped;
    }

    /// <summary>Camera pitch in radians to the degrees written on the wire.</summary>
    /// <param name="cameraPitchRadians">Positive means looking up.</param>
    public static float ToWirePitchDegrees(float cameraPitchRadians) => -cameraPitchRadians * RadiansToDegrees;

    /// <summary>Wire pitch in degrees to the camera's pitch in radians.</summary>
    /// <param name="wirePitchDegrees">Positive means looking down.</param>
    public static float FromWirePitchDegrees(float wirePitchDegrees) => -wirePitchDegrees * DegreesToRadians;
}
