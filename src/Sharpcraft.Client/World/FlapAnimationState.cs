// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.World;

/// <summary>
/// Reference client-side wing-flap clock used by chickens and parrots (the `flap` / `flapSpeed` / `flapping` triple).
/// </summary>
/// <remarks>
/// Ported from the reference <c>aiStep</c>/<c>calculateFlapping</c>: speed moves toward 1 while airborne and toward 0
/// while grounded at 0.3/tick, the impulse decays by 0.9, and the flap position advances by <c>flapping * 2</c>.
/// The <c>isPassenger</c> term in the reference (a ridden parrot does not flap) is not tracked yet, so callers should
/// treat parrots-in-vehicle as a known deviation until passenger sync exists.
/// </remarks>
public sealed class FlapAnimationState
{
    private float _flap;
    private float _flapSpeed;
    private float _previousFlap;
    private float _previousFlapSpeed;
    private float _flapping = 1f;

    /// <summary>Advances one tick. <paramref name="airborne"/> is the reference's "not on ground and not a passenger".</summary>
    public void Update(bool airborne)
    {
        _previousFlap = _flap;
        _previousFlapSpeed = _flapSpeed;
        _flapSpeed = Math.Clamp(_flapSpeed + (airborne ? 4f : -1f) * 0.3f, 0f, 1f);
        if (airborne && _flapping < 1f)
        {
            _flapping = 1f;
        }

        _flapping *= 0.9f;
        _flap += _flapping * 2f;
    }

    /// <summary>Flap position interpolated by the partial tick.</summary>
    public float Flap(float partialTicks) => _previousFlap + ((_flap - _previousFlap) * partialTicks);

    /// <summary>Flap speed interpolated by the partial tick and clamped to 0..1.</summary>
    public float Speed(float partialTicks) => Math.Clamp(_previousFlapSpeed + ((_flapSpeed - _previousFlapSpeed) * partialTicks), 0f, 1f);
}
