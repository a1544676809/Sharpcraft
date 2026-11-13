// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Brew.Wgpu.Native;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Framework.Tests;

public sealed class VertexLayoutTests
{
    [Fact]
    public void TheLayoutMatchesTheVertexStride()
    {
        Assert.Equal(24, VertexLayout.Stride);

        // Six 32-bit slots: world position (3), packed colour (1), half uv (1), sprite index and light (1).
        Assert.Equal(6, VertexLayout.Stride / sizeof(float));

        var attributes = VertexLayout.Attributes();
        Assert.Equal(4, attributes.Length);
    }

    [Fact]
    public void EveryAttributeStartsWhereThePreviousOneEnds()
    {
        var attributes = VertexLayout.Attributes();
        ulong expectedOffset = 0;

        for (var index = 0; index < attributes.Length; index++)
        {
            Assert.Equal(expectedOffset, attributes[index].Offset);
            Assert.Equal((uint)index, attributes[index].ShaderLocation);
            expectedOffset += SizeOf(attributes[index].Format);
        }

        Assert.Equal((ulong)VertexLayout.Stride, expectedOffset);
    }

    [Fact]
    public void TheShaderConsumesPositionColourUvAndThePackedWord()
    {
        var attributes = VertexLayout.Attributes();

        Assert.Equal(WGPUVertexFormat.Float32x3, attributes[0].Format);
        Assert.Equal(WGPUVertexFormat.Unorm8x4, attributes[1].Format);

        // The atlas rectangle is no longer per vertex: it moves to a table the vertex stage indexes with the id packed
        // into this word, which is where the fourteen bytes of saving come from (T-832).
        Assert.Equal(WGPUVertexFormat.Float16x2, attributes[2].Format);
        Assert.Equal(WGPUVertexFormat.Uint32, attributes[3].Format);
    }

    private static ulong SizeOf(WGPUVertexFormat format) => format switch
    {
        WGPUVertexFormat.Float32x3 => 12,
        WGPUVertexFormat.Float32x2 => 8,
        WGPUVertexFormat.Float32x4 => 16,
        WGPUVertexFormat.Float16x2 => 4,
        WGPUVertexFormat.Uint32 => 4,
        WGPUVertexFormat.Unorm8x4 => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "unexpected format")
    };
}
