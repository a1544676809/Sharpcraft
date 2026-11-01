// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Xunit;

namespace Sharpcraft.Client.Tests.Assets;

/// <summary>Celestial body sprite identifiers match the 26.3 layout (docs/specs/render/sky-celestial.md 3).</summary>
public sealed class CelestialSpritesTests
{
    [Fact]
    public void SunAndMoonSpritesLiveUnderEnvironmentCelestial()
    {
        Assert.Equal("environment/celestial/sun", CelestialSprites.Sun.Path);
        Assert.Equal("environment/celestial/moon/full_moon", CelestialSprites.MoonPhase(0).Path);
        Assert.Equal("environment/celestial/moon/waxing_gibbous", CelestialSprites.MoonPhase(7).Path);
    }

    [Fact]
    public void MoonPhaseNamesMatchTheVanillaOrder()
    {
        Assert.Equal("full_moon", CelestialSprites.MoonPhaseName(0));
        Assert.Equal("new_moon", CelestialSprites.MoonPhaseName(4));
        Assert.Throws<ArgumentOutOfRangeException>(() => CelestialSprites.MoonPhaseName(8));
    }
}
