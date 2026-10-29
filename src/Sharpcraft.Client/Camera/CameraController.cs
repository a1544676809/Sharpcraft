// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Client.Camera;

/// <summary>
/// First-person camera: yaw and pitch driven by relative mouse movement, eye taken from the player.
/// Yaw 0 looks towards −Z and grows clockwise, which is the mirror of the wire convention; LookConvention
/// converts both angles at that boundary (docs/modules/input.md 5).
/// </summary>
public sealed class CameraController
{
    /// <summary>Pitch limit in radians: vanilla stops just short of straight up and down.</summary>
    public const float PitchLimit = 1.5533431f;

    /// <summary>Horizontal facing in radians; 0 looks towards −Z and positive turns right.</summary>
    public float Yaw { get; private set; }

    /// <summary>Vertical facing in radians; positive looks up.</summary>
    public float Pitch { get; private set; }

    /// <summary>Applies mouse movement in pixels at the given sensitivity in degrees per pixel.</summary>
    /// <param name="pixels">Relative movement for this frame.</param>
    /// <param name="degreesPerPixel">Sensitivity.</param>
    public void ApplyLook(Vector2 pixels, float degreesPerPixel)
    {
        const float degreesToRadians = MathF.PI / 180f;

        // Moving the mouse right turns right and moving it down looks down. Both stored angles therefore run the
        // opposite way to the wire convention; LookConvention converts them at that boundary.
        Yaw += pixels.X * degreesPerPixel * degreesToRadians;
        Pitch -= pixels.Y * degreesPerPixel * degreesToRadians;

        Pitch = Math.Clamp(Pitch, -PitchLimit, PitchLimit);

        // Keep yaw bounded so it cannot drift into precision trouble over a long session.
        var twoPi = MathF.PI * 2f;
        Yaw %= twoPi;
    }

    /// <summary>Unit vector the camera looks along.</summary>
    public Vector3 Forward
    {
        get
        {
            var cosPitch = MathF.Cos(Pitch);
            return new Vector3(
                MathF.Sin(Yaw) * cosPitch,
                MathF.Sin(Pitch),
                -MathF.Cos(Yaw) * cosPitch);
        }
    }

    /// <summary>Right vector, on the horizontal plane.</summary>
    public Vector3 Right => new(MathF.Cos(Yaw), 0f, MathF.Sin(Yaw));

    /// <summary>Look-at target a distance in front of the eye.</summary>
    /// <param name="eye">Eye position.</param>
    /// <param name="distance">Distance to aim at.</param>
    public Vector3 Target(Vector3 eye, float distance = 10f) => eye + (Forward * distance);

    /// <summary>Sets the facing directly; used when a session starts.</summary>
    /// <param name="yaw">Yaw in radians.</param>
    /// <param name="pitch">Pitch in radians.</param>
    public void SetLook(float yaw, float pitch)
    {
        Yaw = yaw;
        Pitch = Math.Clamp(pitch, -PitchLimit, PitchLimit);
    }
}
