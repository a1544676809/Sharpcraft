// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.World;
using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Client.Tests.World;

/// <summary>
/// Client weather state matches the reference Level.getRainLevel/getThunderLevel semantics
/// (docs/modules/weather-and-sky-color.md 6.3): level changes set previous and current together; thunder scales by rain.
/// </summary>
public sealed class ClientWorldWeatherTests
{
    [Fact]
    public void RainLevelChangeSetsTheRainLevel()
    {
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        world.ApplyWeather(new GameEventPacket(GameEventPacketCodec.RainLevelChange, 0.5f));

        Assert.Equal(0.5f, world.RainLevel(1f), 4);
        Assert.Equal(0f, world.ThunderLevel(1f), 4); // no thunder yet
    }

    [Fact]
    public void ThunderIsScaledByRain()
    {
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        world.ApplyWeather(new GameEventPacket(GameEventPacketCodec.RainLevelChange, 0.5f));
        world.ApplyWeather(new GameEventPacket(GameEventPacketCodec.ThunderLevelChange, 0.4f));

        // getThunderLevel = thunder * rain = 0.4 * 0.5 = 0.2.
        Assert.Equal(0.2f, world.ThunderLevel(1f), 4);
    }

    [Fact]
    public void StartAndStopMirrorTheReferenceQuirk()
    {
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);

        world.ApplyWeather(new GameEventPacket(GameEventPacketCodec.StartRaining, 0f));
        Assert.Equal(0f, world.RainLevel(1f), 4);

        world.ApplyWeather(new GameEventPacket(GameEventPacketCodec.StopRaining, 0f));
        Assert.Equal(1f, world.RainLevel(1f), 4);
    }

    [Fact]
    public void LevelsAreClamped()
    {
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        world.ApplyWeather(new GameEventPacket(GameEventPacketCodec.RainLevelChange, 1.5f));
        Assert.Equal(1f, world.RainLevel(1f), 4);
    }
}
