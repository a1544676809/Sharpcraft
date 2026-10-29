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
    /// <summary>Bit marking the X component (and its velocity) as relative.</summary>
    public const int RelativeX = 1;

    /// <summary>Bit marking the Y component as relative.</summary>
    public const int RelativeY = 1 << 1;

    /// <summary>Bit marking the Z component as relative.</summary>
    public const int RelativeZ = 1 << 2;

    /// <summary>Bit marking yaw as relative.</summary>
    public const int RelativeYaw = 1 << 3;

    /// <summary>Bit marking pitch as relative.</summary>
    public const int RelativePitch = 1 << 4;

    /// <summary>Bit marking the x velocity as relative.</summary>
    public const int RelativeDeltaX = 1 << 5;

    /// <summary>Bit marking the y velocity as relative.</summary>
    public const int RelativeDeltaY = 1 << 6;

    /// <summary>Bit marking the z velocity as relative.</summary>
    public const int RelativeDeltaZ = 1 << 7;

    /// <summary>Any of the position bits set.</summary>
    public bool IsPositionRelative => (RelativeFlags & (RelativeX | RelativeY | RelativeZ)) != 0;

    /// <summary>True when the given bit is set.</summary>
    /// <param name="bit">One of the <c>Relative*</c> constants.</param>
    public bool Has(int bit) => (RelativeFlags & bit) != 0;
}
