// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.World;

/// <summary>
/// Panda sit / lie-on-back / roll amount clocks, ported from the reference <c>Panda.updateSitAmount</c>,
/// <c>updateOnBackAnimation</c> and <c>updateRollAmount</c>: each amount moves toward the synced flag by +0.15 or
/// -0.19 per tick, clamped 0..1, and the renderer samples <c>lerp(partial, previous, current)</c>.
/// </summary>
public sealed class PandaPoseAnimationState
{
    private float _sit;
    private float _sitO;
    private float _onBack;
    private float _onBackO;
    private float _roll;
    private float _rollO;

    /// <summary>Advances one tick from the synced sitting / on-back / rolling flags.</summary>
    public void Tick(bool isSitting, bool isOnBack, bool isRolling)
    {
        _sitO = _sit;
        _sit = Move(_sit, isSitting);

        _onBackO = _onBack;
        _onBack = Move(_onBack, isOnBack);

        _rollO = _roll;
        _roll = Move(_roll, isRolling);
    }

    private static float Move(float value, bool active) =>
        active ? MathF.Min(1f, value + 0.15f) : MathF.Max(0f, value - 0.19f);

    public float SitAmount(float partialTicks) => _sitO + ((_sit - _sitO) * partialTicks);

    public float LieOnBackAmount(float partialTicks) => _onBackO + ((_onBack - _onBackO) * partialTicks);

    public float RollAmount(float partialTicks) => _rollO + ((_roll - _rollO) * partialTicks);
}
