// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using Brew.Wgpu;
using Brew.Wgpu.Native;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Framework.Tests;

/// <summary>
/// The vertex layout is shared by the offscreen and windowed pipelines. Both once declared their own
/// copies, and when texture coordinates were added only one copy was updated: windowed rendering then
/// failed pipeline validation because the shader consumed a location nothing provided
/// (docs/modules/render.md 6.1).
/// </summary>
public sealed class VertexLayoutTests
{
    [Fact]
    public void TheLayoutMatchesTheVertexStride()
    {
        Assert.Equal(ClipSpaceTransform.VertexStride, VertexLayout.Stride);

        var attributes = VertexLayout.Attributes();

        // position, light colour, tile uv, sprite rect: 4 + 3 + 2 + 4 floats.
        Assert.Equal(4 + 3 + 2 + 4, VertexLayout.Stride / sizeof(float));
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
    public void TheShaderConsumesPositionColourUvAndTheSpriteRect()
    {
        var attributes = VertexLayout.Attributes();

        Assert.Equal(WGPUVertexFormat.Float32x4, attributes[0].Format);
        Assert.Equal(WGPUVertexFormat.Float32x3, attributes[1].Format);
        Assert.Equal(WGPUVertexFormat.Float32x2, attributes[2].Format);
        Assert.Equal(WGPUVertexFormat.Float32x4, attributes[3].Format);
    }

    private static ulong SizeOf(WGPUVertexFormat format) => format switch
    {
        WGPUVertexFormat.Float32x2 => 8,
        WGPUVertexFormat.Float32x3 => 12,
        WGPUVertexFormat.Float32x4 => 16,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "unexpected format")
    };
}
