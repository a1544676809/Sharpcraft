// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Common.Identifiers;
using Xunit;

namespace Sharpcraft.Client.Tests.Repro;

/// <summary>
/// Issue 227: blocks whose model resolves are meshed by the element path, and that path drew them untinted
/// (grey grass, white leaves) and unlit (an opaque block's own cell carries no light, so it drew black).
/// Both defects were invisible to geometry tests and to the offscreen frame, so these pin the two behaviours
/// where they are decided: the tint index rule and the per-face light lookup.
/// </summary>
public sealed class Repro_227_ModelPathTintLightTests
{
    private static readonly SpriteRect Sprite = new(0, 0, 16, 16, 1) { RectId = 1 };

    private static TexturedQuad Quad(string direction, int tintIndex) =>
        new(
            new ModelQuad(
                new ModelVertex(new Vector3(0f, 0f, 0f), new Vector2(0f, 0f)),
                new ModelVertex(new Vector3(0f, 0f, 1f), new Vector2(0f, 1f)),
                new ModelVertex(new Vector3(1f, 0f, 1f), new Vector2(1f, 1f)),
                new ModelVertex(new Vector3(1f, 0f, 0f), new Vector2(1f, 0f)),
                direction,
                "#all",
                tintIndex,
                null),
            Identifier.Parse("minecraft:block/stone"));

    /// <summary>Colour bytes of one vertex: red, green, blue from the packed word at offset 3.</summary>
    private static (int R, int G, int B) ColourOf(MeshBuffer buffer, int vertex)
    {
        var word = BitConverter.SingleToUInt32Bits(buffer.Vertices[(vertex * ChunkMesher.FloatsPerVertex) + 3]);
        return ((int)(word & 0xFF), (int)((word >> 8) & 0xFF), (int)((word >> 16) & 0xFF));
    }

    /// <summary>Sky and block light bytes of one vertex from the packed word at offset 5.</summary>
    private static (int Sky, int Block) LightOf(MeshBuffer buffer, int vertex)
    {
        var word = BitConverter.SingleToUInt32Bits(buffer.Vertices[(vertex * ChunkMesher.FloatsPerVertex) + 5]);
        return ((int)((word >> 16) & 0xFF), (int)((word >> 24) & 0xFF));
    }

    private static int Channel(float value) => (int)Math.Clamp((int)MathF.Round(value * 255f), 0, 255);

    /// <summary>
    /// The reference tints exactly the faces whose model declares a tint index. A face without one keeps the
    /// shade-only colour, which is the case that made grass sides and every untinted model draw correctly.
    /// </summary>
    [Fact]
    public void OnlyFacesWithATintIndexTakeTheTint()
    {
        using var buffer = new MeshBuffer();
        var tint = (R: 0.5f, G: 0.25f, B: 0.125f);

        var emitted = BlockModelSet.Emit(
            buffer,
            [Quad("up", -1), Quad("up", 0)],
            x: 0,
            y: 0,
            z: 0,
            light: (1f, 1f),
            _ => Sprite,
            tint: tint);

        Assert.Equal(2, emitted);

        // First quad: no tint index, so white at full upward shade.
        Assert.Equal((255, 255, 255), ColourOf(buffer, 0));

        // Second quad: tint index zero, so the supplied multiplier at full upward shade.
        var tinted = ColourOf(buffer, 4);
        Assert.Equal(Channel(tint.R), tinted.R);
        Assert.Equal(Channel(tint.G), tinted.G);
        Assert.Equal(Channel(tint.B), tinted.B);
    }

    /// <summary>Without a tint the same face stays white, which is what a caller with no biome data expects.</summary>
    [Fact]
    public void AFaceWithATintIndexStaysWhiteWhenNoTintIsSupplied()
    {
        using var buffer = new MeshBuffer();

        BlockModelSet.Emit(
            buffer,
            [Quad("up", 0)],
            x: 0,
            y: 0,
            z: 0,
            light: (1f, 1f),
            _ => Sprite);

        Assert.Equal((255, 255, 255), ColourOf(buffer, 0));
    }

    /// <summary>
    /// A model face is lit from the block it looks at, not from the block it belongs to. Six quads with six
    /// distinct neighbour lights pin the direction mapping: replacing the per-face table with one value collapses
    /// every face onto the same light and fails here.
    /// </summary>
    [Fact]
    public void EachFaceTakesTheLightOfTheNeighbourItLooksAt()
    {
        using var buffer = new MeshBuffer();
        var directions = new[] { "down", "up", "north", "south", "west", "east" };

        // Sky levels 1..6 per face so a wrong direction or a single value is unambiguous; block light stays 0.
        Span<(float Sky, float Block)> faceLights = stackalloc (float, float)[6];
        for (var face = 0; face < 6; face++)
        {
            faceLights[face] = ((face + 1) / 15f, 0f);
        }

        var quads = new TexturedQuad[6];
        for (var face = 0; face < 6; face++)
        {
            quads[face] = Quad(directions[face], -1);
        }

        var emitted = BlockModelSet.Emit(
            buffer,
            quads,
            x: 0,
            y: 0,
            z: 0,
            // The fallback a caller without a neighbourhood passes; it must not win over the per-face table.
            light: (15f / 15f, 0f),
            _ => Sprite,
            faceLights: faceLights);

        Assert.Equal(6, emitted);
        for (var face = 0; face < 6; face++)
        {
            var light = LightOf(buffer, face * 4);
            Assert.Equal(Channel((face + 1) / 15f), light.Sky);
            Assert.Equal(0, light.Block);
        }
    }

    /// <summary>A caller without a neighbourhood still gets the single light it supplied.</summary>
    [Fact]
    public void AFaceFallsBackToTheSingleLightWithoutAPerFaceTable()
    {
        using var buffer = new MeshBuffer();

        BlockModelSet.Emit(
            buffer,
            [Quad("up", -1)],
            x: 0,
            y: 0,
            z: 0,
            light: (0.6f, 0.2f),
            _ => Sprite);

        var light = LightOf(buffer, 0);
        Assert.Equal(Channel(0.6f), light.Sky);
        Assert.Equal(Channel(0.2f), light.Block);
    }

    /// <summary>
    /// The one place that decides whether a block is tinted. Grass and leaves resolve to a multiplier, a block
    /// with no tint rule resolves to null, so the model path cannot silently tint everything or nothing.
    /// </summary>
    [Fact]
    public void TheTintDecisionNamesGrassAndLeavesButNotStone()
    {
        var tint = BiomeTint.FromColormaps(null, null);

        var grass = BlockTintTable.MultiplierFor(tint, Identifier.Parse("minecraft:grass_block"), 0, 64, 0);
        var leaves = BlockTintTable.MultiplierFor(tint, Identifier.Parse("minecraft:oak_leaves"), 0, 64, 0);
        var stone = BlockTintTable.MultiplierFor(tint, Identifier.Parse("minecraft:stone"), 0, 64, 0);

        Assert.NotNull(grass);
        Assert.NotNull(leaves);
        Assert.Null(stone);

        // The reference foliage fallback is what leaves resolve to when the pack has no colormap.
        var expected = BiomeTint.ToMultiplier(BiomeTint.FoliageDefault);
        Assert.Equal(expected.Red, leaves!.Value.R, 6);
        Assert.Equal(expected.Green, leaves.Value.G, 6);
        Assert.Equal(expected.Blue, leaves.Value.B, 6);

        // No tint service at all means no tint, even for grass.
        Assert.Null(BlockTintTable.MultiplierFor(null, Identifier.Parse("minecraft:grass_block"), 0, 64, 0));
    }
}
