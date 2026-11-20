// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Imaging;
using Sharpcraft.Common.Network;
using Sharpcraft.Common.Registries;
using Sharpcraft.Common.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Repro;

/// <summary>
/// Issue 227 review: the element path is only correct if the mesher hands the tint and the per-face lights to
/// <see cref="BlockModelSet"/>. A test that calls <c>Emit</c> directly cannot see that wiring, so this drives the real
/// entry point, <see cref="ChunkMesher.MeshChunk"/>, with one model block surrounded by air and light levels that differ
/// per direction.
/// </summary>
/// <remarks>
/// Every expected value here is derived from the reference rules independently of the code under test: the shade
/// table, the face a vertex plane belongs to, and the direction offsets are written out literally rather than read back
/// from <see cref="BlockModelSet"/>.
/// </remarks>
public sealed class Repro_227_ModelPathWiringTests
{
    private const int BlockState = 1;
    private const int BlockX = 8;
    private const int BlockY = 8;
    private const int BlockZ = 8;

    /// <summary>The reference's per-face brightness, written out so a swapped direction cannot hide.</summary>
    private static readonly Dictionary<string, float> Shade = new(StringComparer.Ordinal)
    {
        ["up"] = 1.0f,
        ["down"] = 0.5f,
        ["north"] = 0.8f,
        ["south"] = 0.8f,
        ["west"] = 0.6f,
        ["east"] = 0.6f
    };

    /// <summary>Neighbour cell of each face, in vanilla's convention: north is -Z and south is +Z.</summary>
    private static readonly Dictionary<string, (int X, int Y, int Z)> Neighbour = new(StringComparer.Ordinal)
    {
        ["up"] = (0, 1, 0),
        ["down"] = (0, -1, 0),
        ["north"] = (0, 0, -1),
        ["south"] = (0, 0, 1),
        ["west"] = (-1, 0, 0),
        ["east"] = (1, 0, 0)
    };

    private static int Channel(float value) => (int)Math.Clamp((int)MathF.Round(value * 255f), 0, 255);

    private static byte[] Filled(int length, byte value)
    {
        var data = new byte[length];
        Array.Fill(data, value);
        return data;
    }

    /// <summary>A one pixel atlas holding the model's face texture and the missing sprite.</summary>
    private static BlockSpriteMap Sprites()
    {
        var entries = new List<(Identifier Location, RgbaImage Image, int Frames)>
        {
            (Identifier.Parse("minecraft:block/test_face"), new RgbaImage(16, 16, Filled(16 * 16 * 4, 200)), 1),
            (SpriteAtlas.MissingSprite, new RgbaImage(16, 16, Filled(16 * 16 * 4, 255)), 1)
        };

        return new BlockSpriteMap(SpriteAtlasBuilder.Stitch(entries));
    }

    /// <summary>
    /// A single cube whose up face carries <c>tintindex 0</c> and whose other five carry none, which is the shape
    /// grass and leaves have.
    /// </summary>
    private static BlockModelSet Models(BlockStateLayout layout)
    {
        const string faces =
            "\"up\":{\"texture\":\"#all\",\"tintindex\":0},"
            + "\"down\":{\"texture\":\"#all\"},"
            + "\"north\":{\"texture\":\"#all\"},"
            + "\"south\":{\"texture\":\"#all\"},"
            + "\"west\":{\"texture\":\"#all\"},"
            + "\"east\":{\"texture\":\"#all\"}";

        var json = $"{{\"elements\":[{{\"from\":[0,0,0],\"to\":[16,16,16],\"faces\":{{{faces}}}}}]}}";
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);

        var library = new BlockModelLibrary(
            id => id.Path == "air" ? null : Identifier.Parse("minecraft:block/test_model"),
            new BlockModelElements(_ => bytes),
            (_, _) => Identifier.Parse("minecraft:block/test_face"));

        var models = BlockModelSet.Build(library, layout);
        Assert.NotNull(models);
        return models!;
    }

    private static BlockStateLayout Layout() =>
        new(
        [
            (Identifier.Parse("minecraft:air"), 1),
            (Identifier.Parse("minecraft:grass_block"), 1)
        ]);

    /// <summary>Pokes one sky/block light nibble the way the reference packs light section data.</summary>
    private static void SetNibble(byte[] data, int x, int y, int z, byte level)
    {
        var index = (y << 8) | (z << 4) | x;
        var at = index >> 1;
        data[at] = (index & 1) == 0
            ? (byte)((data[at] & 0xF0) | (level & 0xF))
            : (byte)((data[at] & 0x0F) | ((level & 0xF) << 4));
    }

    /// <summary>
    /// One model block at (8,8,8) in air, with a distinct block light on each of its six neighbours and full sky light
    /// everywhere. The block's own cell is lit too, so the single-value fallback would be 15 everywhere and this test
    /// would fail if the per-face table never arrived.
    /// </summary>
    private static (ClientWorld World, LevelChunk Chunk, BlockSpriteMap Sprites, BlockModelSet Models, BlockStateLayout Layout) Scene(string? liftedDirection = null)
    {
        var layout = Layout();
        var world = new ClientWorld(minSectionY: -1, sectionCount: 3);
        var chunk = world.AddChunk(0, 0);

        var section = new ChunkSection(0, 0);
        section.Set(BlockX, BlockY, BlockZ, BlockState);
        chunk.SetSection(section);

        var sky = new byte[2048];
        Array.Fill(sky, (byte)0xFF);
        var blockLight = new byte[2048];
        Array.Fill(blockLight, (byte)0x00);

        // Full light in the block's own cell: the fallback must not be able to pass for any of the six faces.
        SetNibble(blockLight, BlockX, BlockY, BlockZ, 15);

        // Six distinct neighbour levels, assigned by the vanilla direction convention rather than by the code's table.
        byte level = 1;
        foreach (var (key, offset) in Neighbour.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            SetNibble(blockLight, BlockX + offset.X, BlockY + offset.Y, BlockZ + offset.Z, key == liftedDirection ? (byte)15 : level);
            level++;
        }

        // ChunkLight adds one section below and one above the block sections, so the index shifts by one more.
        var lightSection = 0 - world.MinSectionY + 1;
        chunk.Light.SetSkyLight(lightSection, sky);
        chunk.Light.SetBlockLight(lightSection, blockLight);
        chunk.MarkLoaded();

        return (world, chunk, Sprites(), Models(layout), layout);
    }

    /// <summary>The block light the scene gave the neighbour of a direction, before pack quantisation.</summary>
    private static byte NeighbourLevel(string direction)
    {
        byte level = 1;
        foreach (var key in Neighbour.Keys.OrderBy(key => key, StringComparer.Ordinal))
        {
            if (key == direction)
            {
                return level;
            }

            level++;
        }

        throw new ArgumentOutOfRangeException(nameof(direction));
    }

    /// <summary>Which face a quad is, from the plane its four vertices share.</summary>
    private static string DirectionOf(ReadOnlySpan<float> vertices, int firstVertex)
    {
        var up = true;
        var down = true;
        var east = true;
        var west = true;
        var south = true;
        var north = true;

        for (var vertex = 0; vertex < 4; vertex++)
        {
            var at = (firstVertex + vertex) * ChunkMesher.FloatsPerVertex;
            up &= vertices[at + 1] == BlockY + 1;
            down &= vertices[at + 1] == BlockY;
            east &= vertices[at] == BlockX + 1;
            west &= vertices[at] == BlockX;
            south &= vertices[at + 2] == BlockZ + 1;
            north &= vertices[at + 2] == BlockZ;
        }

        if (up) { return "up"; }
        if (down) { return "down"; }
        if (east) { return "east"; }
        if (west) { return "west"; }
        if (south) { return "south"; }
        if (north) { return "north"; }
        return "unknown";
    }

    private static (int R, int G, int B) ColourOf(ReadOnlySpan<float> vertices, int vertex)
    {
        var word = BitConverter.SingleToUInt32Bits(vertices[(vertex * ChunkMesher.FloatsPerVertex) + 3]);
        return ((int)(word & 0xFF), (int)((word >> 8) & 0xFF), (int)((word >> 16) & 0xFF));
    }

    private static (int Sky, int Block) LightOf(ReadOnlySpan<float> vertices, int vertex)
    {
        var word = BitConverter.SingleToUInt32Bits(vertices[(vertex * ChunkMesher.FloatsPerVertex) + 5]);
        return ((int)((word >> 16) & 0xFF), (int)((word >> 24) & 0xFF));
    }

    private static MeshBuffer Mesh(string? liftedDirection = null)
    {
        var (world, chunk, sprites, models, layout) = Scene(liftedDirection);
        var buffer = new MeshBuffer();
        ChunkMesher.MeshChunk(
            world,
            chunk,
            airState: 0,
            buffer,
            maxBlocks: 2_000_000,
            sprites: sprites,
            layout: layout,
            tint: BiomeTint.FromColormaps(null, null),
            models: models);
        return buffer;
    }

    /// <summary>
    /// The mesher really passes the tint: the model's tinted face comes out multiplied, the others stay shaded white.
    /// Setting <c>tintColour</c> to null in <c>EmitSectionModels</c> fails exactly here.
    /// </summary>
    [Fact]
    public void TheMesherHandsTheTintToTheModelPath()
    {
        using var buffer = Mesh();
        var vertices = buffer.Vertices;
        Assert.Equal(24, buffer.VertexCount);

        // The scene has no colormap, so grass falls back to the reference magenta sentinel: (1, 0, 1).
        for (var quad = 0; quad < 6; quad++)
        {
            var first = quad * 4;
            var direction = DirectionOf(vertices, first);
            Assert.NotEqual("unknown", direction);

            var colour = ColourOf(vertices, first);
            if (direction == "up")
            {
                // Up shade is 1, so the tinted face shows the multiplier unscaled.
                Assert.Equal((255, 0, 255), colour);
            }
            else
            {
                var shade = Channel(Shade[direction]);
                Assert.Equal((shade, shade, shade), colour);
            }
        }
    }

    /// <summary>
    /// The model path lights each face from the cell that face looks at: raising that one neighbour brightens that face
    /// and leaves every other face untouched. A swapped direction table fails here, and so does a path that reads the
    /// block's own (fully lit) cell, because then no neighbour could move a vertex.
    /// </summary>
    /// <remarks>
    /// Issue 255 replaced the single per-face value with the reference's per-corner blend, so this test compares two
    /// scenes instead of one exact level: the property under test is attribution, not the blend itself, which
    /// <c>Repro_255_AmbientOcclusionTests</c> pins numerically.
    /// </remarks>
    [Theory]
    [InlineData("up")]
    [InlineData("down")]
    [InlineData("north")]
    [InlineData("south")]
    [InlineData("west")]
    [InlineData("east")]
    public void TheMesherLightsEachModelFaceFromItsOwnNeighbour(string lifted)
    {
        var baseline = FaceLightAverages(null);
        var raised = FaceLightAverages(lifted);

        Assert.Equal(6, baseline.Count);
        Assert.True(
            raised[lifted] > baseline[lifted],
            $"lifting the {lifted} neighbour did not brighten the {lifted} face: {baseline[lifted]} -> {raised[lifted]}");

        foreach (var (direction, value) in baseline)
        {
            if (direction != lifted)
            {
                Assert.Equal(value, raised[direction]);
            }
        }
    }

    /// <summary>
    /// An absolute level check, not just a difference: every vertex of the up face carries the reference blend of its
    /// own neighbour. In this scene the up neighbour holds block light 5, the in-plane cells around the face hold 0 and
    /// the centre is the neighbour cell itself, so the blend is (0 + 0 + 0 + 5) >> 2 = 1, which packs to 17 of 255.
    /// A face that read another neighbour, or that mixed several neighbours with the wrong weights, misses this number
    /// even when a differential test would still pass.
    /// </summary>
    [Fact]
    public void TheUpFaceCarriesTheReferenceBlendOfItsOwnNeighbour()
    {
        using var buffer = Mesh();
        var vertices = buffer.Vertices;

        for (var quad = 0; quad < 6; quad++)
        {
            var first = quad * 4;
            if (DirectionOf(vertices, first) != "up")
            {
                continue;
            }

            for (var vertex = 0; vertex < 4; vertex++)
            {
                var light = LightOf(vertices, first + vertex);
                Assert.Equal(Channel(15 / 15f), light.Sky);
                Assert.Equal(17, light.Block);
            }

            return;
        }

        Assert.Fail("no up face was emitted");
    }

    /// <summary>Mean block light of each face's four vertices, keyed by the face the vertices lie on.</summary>
    private static Dictionary<string, int> FaceLightAverages(string? liftedDirection)
    {
        using var buffer = Mesh(liftedDirection);
        var vertices = buffer.Vertices;
        var result = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var quad = 0; quad < 6; quad++)
        {
            var first = quad * 4;
            var direction = DirectionOf(vertices, first);
            Assert.NotEqual("unknown", direction);

            var sum = 0;
            for (var vertex = 0; vertex < 4; vertex++)
            {
                sum += LightOf(vertices, first + vertex).Block;
            }

            Assert.True(result.TryAdd(direction, sum / 4), $"direction {direction} was emitted twice");
        }

        return result;
    }

    /// <summary>The direction table itself, written against vanilla's convention rather than against the code.</summary>
    [Theory]
    [InlineData("down", 0, 0, -1, 0)]
    [InlineData("up", 1, 0, 1, 0)]
    [InlineData("north", 2, 0, 0, -1)]
    [InlineData("south", 3, 0, 0, 1)]
    [InlineData("west", 4, -1, 0, 0)]
    [InlineData("east", 5, 1, 0, 0)]
    public void FaceIndexAndOffsetFollowTheReferenceConvention(string direction, int index, int x, int y, int z)
    {
        Assert.Equal(index, BlockModelSet.FaceIndex(direction));
        Assert.Equal((x, y, z), BlockModelSet.FaceOffset(index));
        Assert.Equal((x, y, z), (BlockModelSet.FaceOffset(BlockModelSet.FaceIndex(direction))));
    }

    /// <summary>An unknown direction has no index and no offset, so callers fall back instead of guessing.</summary>
    [Fact]
    public void AnUnknownDirectionHasNoIndex()
    {
        Assert.Equal(-1, BlockModelSet.FaceIndex("sideways"));
        Assert.Equal((0, 0, 0), BlockModelSet.FaceOffset(-1));
    }
}
