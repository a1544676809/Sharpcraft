// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using MinecraftCSharp.Common.Network;
using MinecraftCSharp.Common.World;
using Xunit;

namespace MinecraftCSharp.Common.Tests;

/// <summary>
/// Light storage in the chunk model: nibble decoding and the mapping from the wire masks to light
/// sections (docs/specs/network/protocol-phases.md §5.3.1).
/// </summary>
public sealed class ChunkLightTests
{
    [Fact]
    public void NibblesDecodeLowSampleFirst()
    {
        var chunk = new LevelChunk(0, 0, minSectionY: 0, sectionCount: 1);
        var data = new byte[ChunkLight.BytesPerSection];

        // Byte 0 holds samples 0 (low nibble) and 1 (high nibble).
        data[0] = 0xA5;

        chunk.Light.SetSkyLight(1, data);

        Assert.Equal(5, chunk.GetSkyLight(0, 0, 0));
        Assert.Equal(10, chunk.GetSkyLight(1, 0, 0));
        Assert.Equal(0, chunk.GetSkyLight(2, 0, 0));
    }

    [Fact]
    public void IndexPacksYThenZThenX()
    {
        var chunk = new LevelChunk(0, 0, minSectionY: 0, sectionCount: 1);
        var data = new byte[ChunkLight.BytesPerSection];

        // Sample index = (y << 8) | (z << 4) | x; the low nibble is the even index.
        // index 1 -> high nibble of byte 0, index 16 -> low nibble of byte 8,
        // index 256 -> low nibble of byte 128.
        data[0] = 0xF0;
        data[8] = 0x0F;
        data[128] = 0x0F;

        chunk.Light.SetSkyLight(1, data);

        Assert.Equal(15, chunk.GetSkyLight(1, 0, 0));
        Assert.Equal(15, chunk.GetSkyLight(0, 0, 1));
        Assert.Equal(15, chunk.GetSkyLight(0, 1, 0));
        Assert.Equal(0, chunk.GetSkyLight(0, 0, 0));
    }

    [Fact]
    public void MissingArraysReadAsZero()
    {
        var chunk = new LevelChunk(0, 0, minSectionY: -4, sectionCount: 24);

        Assert.Equal(0, chunk.GetSkyLight(3, -64, 7));
        Assert.Equal(0, chunk.GetBlockLight(3, 320, 7));
        Assert.False(chunk.Light.HasSkyLight);
        Assert.Equal(26, chunk.Light.LightSectionCount);
    }

    [Fact]
    public void FillSkyLightMakesEverySectionBright()
    {
        var chunk = new LevelChunk(0, 0, minSectionY: 0, sectionCount: 2);
        chunk.Light.FillSkyLight(15);

        Assert.True(chunk.Light.HasSkyLight);
        Assert.Equal(15, chunk.GetSkyLight(0, 0, 0));
        Assert.Equal(15, chunk.GetSkyLight(15, 31, 15));
        Assert.Equal(0, chunk.GetBlockLight(0, 0, 0));
    }

    [Fact]
    public void ApplyLightFollowsTheMaskOrderNotTheBitIndex()
    {
        var chunk = new LevelChunk(0, 0, minSectionY: 0, sectionCount: 2);

        // Light sections 1 and 3 carry sky light; the arrays arrive in ascending mask order.
        var sectionOne = Pack(3);
        var sectionThree = Pack(9);
        byte[] skyMask = [0b0000_1010];

        var light = new LightData(
            SkyMask: skyMask,
            BlockMask: [],
            EmptySkyMask: [],
            EmptyBlockMask: [],
            SkyUpdates: [sectionOne, sectionThree],
            BlockUpdates: []);

        chunk.ApplyLight(light);

        // y = 0 lands in light section 1, y = 32 in light section 3.
        Assert.Equal(3, chunk.GetSkyLight(0, 0, 0));
        Assert.Equal(9, chunk.GetSkyLight(0, 32, 0));
    }

    [Fact]
    public void ArraysOfTheWrongSizeAreRejected()
    {
        var light = new ChunkLight(0, 1);

        Assert.Throws<ArgumentException>(() => light.SetSkyLight(0, new byte[16]));
        Assert.Throws<ArgumentException>(() => light.SetBlockLight(0, new byte[16]));
    }

    private static byte[] Pack(byte level)
    {
        var data = new byte[ChunkLight.BytesPerSection];
        Array.Fill(data, (byte)((level & 0xF) * 0x11));
        return data;
    }
}
