// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.World;

/// <summary>
/// Polar bear stand ease clock, ported from the reference <c>PolarBear.clientSideStandAnimation</c>: each tick the
/// counter moves ±1 toward 0..6 and the renderer samples <c>lerp(partial, previous, current) / 6</c>.
/// </summary>
public sealed class PolarBearStandAnimationState
{
    private float _previous;
    private float _current;

    /// <summary>Advances one tick toward the synced standing flag.</summary>
    public void Tick(bool isStanding)
    {
        _previous = _current;
        _current = isStanding
            ? Math.Clamp(_current + 1f, 0f, 6f)
            : Math.Clamp(_current - 1f, 0f, 6f);
    }

    /// <summary>Reference <c>getStandingAnimationScale(partial)</c>.</summary>
    public float Scale(float partialTicks) =>
        (_previous + ((_current - _previous) * partialTicks)) / 6f;
}
