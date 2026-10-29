// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Network;

/// <summary>
/// A server-side correction of the player's position, sent as one packet. The relative bitset says which
/// components are offsets from the current value rather than absolute coordinates; treating it as absolute
/// teleports the player into the wrong place (docs/specs/network/play-movement.md 2).
/// </summary>
/// <param name="TeleportId">Id that must be echoed back in the accept packet.</param>
/// <param name="X">X coordinate, absolute or relative depending on <paramref name="RelativeFlags"/>.</param>
/// <param name="Y">Y coordinate.</param>
/// <param name="Z">Z coordinate.</param>
/// <param name="DeltaX">Velocity x.</param>
/// <param name="DeltaY">Velocity y.</param>
/// <param name="DeltaZ">Velocity z.</param>
/// <param name="Yaw">Yaw in degrees.</param>
/// <param name="Pitch">Pitch in degrees.</param>
/// <param name="RelativeFlags">Bitset of relative components.</param>
public sealed record PlayerPositionInfo(
    int TeleportId,
    double X,
    double Y,
    double Z,
    double DeltaX,
    double DeltaY,
    double DeltaZ,
    float Yaw,
    float Pitch,
    int RelativeFlags)
{
    /// <summary>True when the position components are offsets rather than absolute.</summary>
    public bool IsPositionRelative => (RelativeFlags & 1) != 0;
}
