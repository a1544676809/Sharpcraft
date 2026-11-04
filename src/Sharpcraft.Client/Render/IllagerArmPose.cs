// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Shared arm-pose ordinals for Illager-style humanoids. Value 0 must be <see cref="Crossed"/> because the mesh uses it
/// to decide whether the crossed-arm group or the separate arms are visible
/// (docs/decisions/0023-client-armed-presentation-state.md).
/// </summary>
public enum IllagerArmPose
{
    Crossed = 0,
    Attacking = 1,
    Spellcasting = 2,
    BowAndArrow = 3,
    CrossbowHold = 4,
    CrossbowCharge = 5,
    Celebrating = 6,
    Neutral = 7
}
