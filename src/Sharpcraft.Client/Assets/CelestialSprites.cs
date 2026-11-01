// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Identifiers;

namespace Sharpcraft.Client.Assets;

/// <summary>
/// Celestial body sprites. In 26.3 the sun and the eight moon phases are individual textures under
/// <c>textures/environment/celestial/</c> (the reference loads them into its celestials atlas), not a 4x2 moon sheet
/// (docs/specs/render/sky-celestial.md 3, ref://mc-26.3-analysis/src/net/minecraft/client/renderer/SkyRenderer.java).
/// </summary>
public static class CelestialSprites
{
    /// <summary>Sun texture (<c>textures/environment/celestial/sun.png</c>).</summary>
    public static readonly Identifier Sun = new("minecraft", "environment/celestial/sun");

    /// <summary>Number of moon phases (<c>MoonPhase.COUNT</c>).</summary>
    public const int MoonPhaseCount = 8;

    // MoonPhase.values() order from net.minecraft.world.level.MoonPhase: index -> serialized name.
    private static readonly string[] MoonPhaseNames =
    [
        "full_moon", "waning_gibbous", "third_quarter", "waning_crescent",
        "new_moon", "waxing_crescent", "first_quarter", "waxing_gibbous"
    ];

    /// <summary>Serialized name of a moon phase (0..7), matching <c>MoonPhase.getSerializedName()</c>.</summary>
    public static string MoonPhaseName(int index)
    {
        if ((uint)index >= MoonPhaseCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "moon phase index must be 0..7");
        }

        return MoonPhaseNames[index];
    }

    /// <summary>
    /// Texture identifier of a moon phase (0..7): <c>textures/environment/celestial/moon/&lt;name&gt;.png</c>.
    /// </summary>
    public static Identifier MoonPhase(int index) =>
        new("minecraft", "environment/celestial/moon/" + MoonPhaseName(index));
}
