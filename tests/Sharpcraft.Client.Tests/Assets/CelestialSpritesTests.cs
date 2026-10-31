// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Xunit;

namespace Sharpcraft.Client.Tests.Assets;

/// <summary>Moon-phase sheet slicing for the celestial pass (docs/specs/render/sky-celestial.md 3).</summary>
public sealed class CelestialSpritesTests
{
    [Fact]
    public void MoonPhasesSliceIntoARowMajorFourByTwoGrid()
    {
        var phases = CelestialSprites.SliceMoonPhases(64, 32);

        Assert.Equal(8, phases.Length);
        Assert.Equal(new CelestialSprite(0, 0, 16, 16), phases[0]);
        Assert.Equal(new CelestialSprite(48, 0, 16, 16), phases[3]);
        Assert.Equal(new CelestialSprite(0, 16, 16, 16), phases[4]);
        Assert.Equal(new CelestialSprite(48, 16, 16, 16), phases[7]);
    }

    [Fact]
    public void AnImageThatDoesNotTileIsRejected()
    {
        Assert.Throws<ArgumentException>(() => CelestialSprites.SliceMoonPhases(63, 32));
    }
}
