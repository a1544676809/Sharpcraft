// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Camera;

/// <summary>
/// Converts between the camera's internal pitch and the pitch written on the wire.
/// </summary>
/// <remarks>
/// The two conventions point opposite ways. Vanilla's <c>xRot</c> grows downwards: a positive <c>xRot</c> means
/// looking down, which follows from its view vector taking a negative Y component from the sine of the pitch.
/// The camera here grows upwards, because that is what the mouse mapping and the view matrix want.
/// <para>
/// Skipping the conversion is not a subtle error: everyone else sees the player look up when they look down.
/// Yaw needs no conversion, both sides measuring it the same way round.
/// </para>
/// </remarks>
public static class LookConvention
{
    private const float RadiansToDegrees = 180f / MathF.PI;
    private const float DegreesToRadians = MathF.PI / 180f;

    /// <summary>Camera pitch in radians to the degrees written on the wire.</summary>
    /// <param name="cameraPitchRadians">Positive means looking up.</param>
    public static float ToWirePitchDegrees(float cameraPitchRadians) => -cameraPitchRadians * RadiansToDegrees;

    /// <summary>Wire pitch in degrees to the camera's pitch in radians.</summary>
    /// <param name="wirePitchDegrees">Positive means looking down.</param>
    public static float FromWirePitchDegrees(float wirePitchDegrees) => -wirePitchDegrees * DegreesToRadians;
}
