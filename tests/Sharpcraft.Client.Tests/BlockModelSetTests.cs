// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Common.Identifiers;

namespace Sharpcraft.Client.Tests;

/// <summary>
/// Covers emitting model quads into a mesh buffer: a quad with a resolvable texture becomes four
/// vertices and two triangles at its block position, and one whose texture the atlas lacks is
/// dropped instead of drawn unsampled.
/// </summary>
public sealed class BlockModelSetTests
{
    private static readonly SpriteRect Sprite = new(0, 0, 16, 16, 1);

    private static TexturedQuad Quad(Identifier texture) =>
        new(
            new ModelQuad(
                new ModelVertex(new Vector3(0f, 0f, 0f), new Vector2(0f, 0f)),
                new ModelVertex(new Vector3(0f, 0f, 1f), new Vector2(0f, 1f)),
                new ModelVertex(new Vector3(1f, 0f, 1f), new Vector2(1f, 1f)),
                new ModelVertex(new Vector3(1f, 0f, 0f), new Vector2(1f, 0f)),
                "down",
                "#all",
                -1,
                null),
            texture);

    /// <summary>A resolvable quad becomes one quad of geometry.</summary>
    [Fact]
    public void AResolvableQuadIsEmitted()
    {
        using var buffer = new MeshBuffer();

        var emitted = BlockModelSet.Emit(
            buffer,
            [Quad(Identifier.Parse("minecraft:block/stone"))],
            x: 3,
            y: 70,
            z: -2,
            light: (1f, 1f),
            _ => Sprite);

        Assert.Equal(1, emitted);
        Assert.Equal(4, buffer.VertexCount);
        Assert.Equal(6, buffer.IndexCount);
    }

    /// <summary>A quad whose texture the atlas does not hold is dropped.</summary>
    [Fact]
    public void AnUnresolvableTextureIsDropped()
    {
        using var buffer = new MeshBuffer();

        var emitted = BlockModelSet.Emit(
            buffer,
            [Quad(Identifier.Parse("minecraft:block/missing"))],
            x: 0,
            y: 0,
            z: 0,
            light: (1f, 1f),
            _ => null);

        Assert.Equal(0, emitted);
        Assert.Equal(0, buffer.VertexCount);
    }

    /// <summary>Every quad of a multi quad model is emitted.</summary>
    [Fact]
    public void EveryQuadOfAModelIsEmitted()
    {
        using var buffer = new MeshBuffer();
        var texture = Identifier.Parse("minecraft:block/torch");

        var emitted = BlockModelSet.Emit(
            buffer,
            [Quad(texture), Quad(texture), Quad(texture)],
            x: 0,
            y: 0,
            z: 0,
            light: (1f, 1f),
            _ => Sprite);

        Assert.Equal(3, emitted);
        Assert.Equal(12, buffer.VertexCount);
        Assert.Equal(18, buffer.IndexCount);
    }

    /// <summary>The reference gives each face direction its own brightness.</summary>
    [Theory]
    [InlineData("down", 0.5f)]
    [InlineData("up", 1f)]
    [InlineData("north", 0.8f)]
    [InlineData("south", 0.8f)]
    [InlineData("west", 0.6f)]
    [InlineData("east", 0.6f)]
    public void EachDirectionHasTheReferenceShade(string direction, float expected)
    {
        Assert.Equal(expected, BlockModelSet.Shade(direction));
    }
}
