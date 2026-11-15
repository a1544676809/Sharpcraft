// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Common.Identifiers;

namespace Sharpcraft.Client.Tests.Repro;

/// <summary>
/// Issue 8: directional shading was never measured on its own. A flat cube has to come out with
/// visibly different brightness per face, and the ordering has to be the reference's.
/// </summary>
public sealed class Repro_008_DirectionalShadingTests
{
    private static readonly Identifier Texture = Identifier.Parse("minecraft:block/stone");

    [Theory]
    [InlineData("up", 1f)]
    [InlineData("down", 0.5f)]
    [InlineData("north", 0.8f)]
    [InlineData("south", 0.8f)]
    [InlineData("west", 0.6f)]
    [InlineData("east", 0.6f)]
    public void TheFactorsAreTheReferences(string direction, float expected)
    {
        Assert.Equal(expected, BlockModelSet.Shade(direction));
    }

    [Fact]
    public void TheSpreadBetweenTheBrightestAndDarkestFaceIsHalfTheBrightness()
    {
        string[] directions = ["down", "up", "north", "south", "west", "east"];
        var factors = directions.Select(BlockModelSet.Shade).ToArray();

        // Without shading every face would be 1, so the observable effect is this spread.
        Assert.Equal(0.5f, factors.Max() - factors.Min(), 6);
    }

    [Fact]
    public void ACubeComesOutWithDifferentColourPerFace()
    {
        using var buffer = new MeshBuffer();

        var quads = BlockModelGeometry.Build([Cube()])
            .Select(quad => new TexturedQuad(quad, Texture))
            .ToArray();

        var emitted = BlockModelSet.Emit(
            buffer,
            quads,
            x: 0,
            y: 0,
            z: 0,
            light: (1f, 0f),
            spriteFor: _ => new SpriteRect(0, 0, 16, 16, 1));

        Assert.Equal(6, emitted);

        // Every vertex carries a packed colour at float index 3 of the vertex stride.
        var colours = new HashSet<uint>();
        for (var vertex = 0; vertex < buffer.VertexCount; vertex++)
        {
            colours.Add(BitConverter.SingleToUInt32Bits(buffer.Vertices[(vertex * ChunkMesher.FloatsPerVertex) + 3]));
        }

        Assert.True(colours.Count >= 3, $"expected at least three distinct face colours, got {colours.Count}");
    }

    private static ModelElement Cube() =>
        new(
            new ModelBox(0, 0, 0, 16, 16, 16),
            new Dictionary<string, ModelFace>(StringComparer.Ordinal)
            {
                ["down"] = new ModelFace("#all", null, null, -1),
                ["up"] = new ModelFace("#all", null, null, -1),
                ["north"] = new ModelFace("#all", null, null, -1),
                ["south"] = new ModelFace("#all", null, null, -1),
                ["west"] = new ModelFace("#all", null, null, -1),
                ["east"] = new ModelFace("#all", null, null, -1)
            },
            null);
}
