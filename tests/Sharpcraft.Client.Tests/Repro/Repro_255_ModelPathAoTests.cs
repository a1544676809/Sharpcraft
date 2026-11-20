// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Imaging;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Registries;
using Sharpcraft.Common.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Repro;

/// <summary>
/// Issue 255, model path: a block whose model is built from elements has to receive the same ambient occlusion as a
/// cube, because grass, leaves and plants all take this path. These tests drive the production entry point
/// (<see cref="ChunkMesher.MeshChunk"/>) with a real model and an occluded corner, so removing the per-quad samples from
/// <c>EmitSectionModels</c> turns them red.
/// </summary>
public sealed class Repro_255_ModelPathAoTests
{
    /// <summary>The six faces of the test model, all sharing one texture and no tint index.</summary>
    private const string FaceList =
        "\"up\":{\"texture\":\"#all\"},"
        + "\"down\":{\"texture\":\"#all\"},"
        + "\"north\":{\"texture\":\"#all\"},"
        + "\"south\":{\"texture\":\"#all\"},"
        + "\"west\":{\"texture\":\"#all\"},"
        + "\"east\":{\"texture\":\"#all\"}";

    private const int BlockState = 1;
    private const int BlockX = 8;
    private const int BlockY = 8;
    private const int BlockZ = 8;

    /// <summary>Neighbour offsets in vanilla's convention, which is what the scene places blocks by.</summary>
    private static readonly Dictionary<string, (int X, int Y, int Z)> Neighbour = new(StringComparer.Ordinal)
    {
        ["up"] = (0, 1, 0),
        ["down"] = (0, -1, 0),
        ["north"] = (0, 0, -1),
        ["south"] = (0, 0, 1),
        ["west"] = (-1, 0, 0),
        ["east"] = (1, 0, 0)
    };

    private static byte[] Filled(int length, byte value)
    {
        var data = new byte[length];
        Array.Fill(data, value);
        return data;
    }

    private static BlockSpriteMap Sprites()
    {
        var entries = new List<(Identifier Location, RgbaImage Image, int Frames)>
        {
            (Identifier.Parse("minecraft:block/test_face"), new RgbaImage(16, 16, Filled(16 * 16 * 4, 200)), 1),
            (SpriteAtlas.MissingSprite, new RgbaImage(16, 16, Filled(16 * 16 * 4, 255)), 1)
        };

        return new BlockSpriteMap(SpriteAtlasBuilder.Stitch(entries));
    }

    /// <summary>A plain cube model with no tint index anywhere, so the vertex colour is shade times brightness alone.</summary>
    private static BlockModelSet Models(BlockStateLayout layout) => Models(layout, string.Empty);

    /// <summary>The same cube, optionally with element rotation or extra boxes, for the geometry tests.</summary>
    private static BlockModelSet Models(BlockStateLayout layout, string extra)
    {
        const string faces =
            "\"up\":{\"texture\":\"#all\"},"
            + "\"down\":{\"texture\":\"#all\"},"
            + "\"north\":{\"texture\":\"#all\"},"
            + "\"south\":{\"texture\":\"#all\"},"
            + "\"west\":{\"texture\":\"#all\"},"
            + "\"east\":{\"texture\":\"#all\"}";

        var json = $"{{\"elements\":[{{\"from\":[0,0,0],\"to\":[16,16,16]{extra},\"faces\":{{{faces}}}}}]}}";
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);

        var library = new BlockModelLibrary(
            id => id.Path == "air" ? null : Identifier.Parse("minecraft:block/test_model"),
            new BlockModelElements(_ => bytes),
            (_, _) => Identifier.Parse("minecraft:block/test_face"));

        return BlockModelSet.Build(library, layout)!;
    }

    private static BlockStateLayout Layout() =>
        new(
        [
            (Identifier.Parse("minecraft:air"), 1),
            (Identifier.Parse("minecraft:grass_block"), 1)
        ]);

    private static void SetNibble(byte[] data, int x, int y, int z, byte level)
    {
        var index = (y << 8) | (z << 4) | x;
        var at = index >> 1;
        data[at] = (index & 1) == 0
            ? (byte)((data[at] & 0xF0) | (level & 0xF))
            : (byte)((data[at] & 0x0F) | ((level & 0xF) << 4));
    }

    /// <summary>
    /// A grass block whose model resolves, plus the occluders around one corner of its up face: nothing else, so every
    /// vertex of the up face is lit identically and only the occlusion can separate them.
    /// </summary>
    /// <param name="occludeEastNorth">Whether the east and north sides of the up face's corner are blocked.</param>
    /// <summary>Temporary: exposes the mesh for a geometry probe.</summary>
    internal static MeshBuffer MeshForProbe(string extra) =>
        Mesh(occludeEastNorth: false, modelsOverride: Models(Layout(), extra));

    private static MeshBuffer Mesh(bool occludeEastNorth, bool ambientOcclusion = true, BlockModelSet? modelsOverride = null)
    {
        var layout = Layout();
        var world = new ClientWorld(minSectionY: -1, sectionCount: 3);
        var chunk = world.AddChunk(0, 0);

        var section = new ChunkSection(0, 0);
        section.Set(BlockX, BlockY, BlockZ, BlockState);

        if (occludeEastNorth)
        {
            // The two sides of the up face's (-Z, +X) corner, plus their probe cells one further up, which is what the
            // reference reads to decide a diagonal is hidden.
            section.Set(BlockX + 1, BlockY + 1, BlockZ, BlockState);
            section.Set(BlockX, BlockY + 1, BlockZ - 1, BlockState);
            section.Set(BlockX + 1, BlockY + 1, BlockZ - 1, BlockState);
            section.Set(BlockX + 1, BlockY + 2, BlockZ, BlockState);
            section.Set(BlockX, BlockY + 2, BlockZ - 1, BlockState);
        }

        chunk.SetSection(section);

        var sky = new byte[2048];
        Array.Fill(sky, (byte)0xFF);
        var blockLight = new byte[2048];
        SetNibble(blockLight, BlockX, BlockY + 1, BlockZ, 15);
        var lightSection = 0 - world.MinSectionY + 1;
        chunk.Light.SetSkyLight(lightSection, sky);
        chunk.Light.SetBlockLight(lightSection, blockLight);
        chunk.MarkLoaded();

        var buffer = new MeshBuffer();
        ChunkMesher.MeshChunk(
            world,
            chunk,
            airState: 0,
            buffer,
            maxBlocks: 2_000_000,
            sprites: Sprites(),
            layout: layout,
            tint: null,
            models: modelsOverride ?? Models(layout),
            ambientOcclusion: ambientOcclusion);
        return buffer;
    }

    /// <summary>The four vertices of the up (+Y) quad, in the order the model emitted them.</summary>
    private static int UpFaceStart(ReadOnlySpan<float> vertices)
    {
        for (var quad = 0; quad < 6; quad++)
        {
            var first = quad * 4;
            var up = true;
            for (var vertex = 0; vertex < 4; vertex++)
            {
                up &= vertices[((first + vertex) * ChunkMesher.FloatsPerVertex) + 1] == BlockY + 1;
            }

            if (up)
            {
                return first;
            }
        }

        throw new InvalidOperationException("no up face was emitted");
    }

    /// <summary>Red channel of a vertex, which the buffer stores as a packed colour word.</summary>
    private static int Red(ReadOnlySpan<float> vertices, int vertex)
    {
        var word = BitConverter.SingleToUInt32Bits(vertices[(vertex * ChunkMesher.FloatsPerVertex) + 3]);
        return (int)(word & 0xFF);
    }

    /// <summary>
    /// The blocked corner of the up face is the (+X, -Z) one. Its brightness must be the reference 0.8 of the open
    /// corners, and the open corners must agree with each other: a corner mix-up, a missing weighted branch or a
    /// dropped sample all break one of the two.
    /// </summary>
    [Fact]
    public void TheModelPathDarkensTheOccludedCorner()
    {
        using var open = Mesh(occludeEastNorth: false);
        using var blocked = Mesh(occludeEastNorth: true);

        var openStart = UpFaceStart(open.Vertices);
        var blockedStart = UpFaceStart(blocked.Vertices);

        // Without occluders every corner of the face is equally bright.
        var reference = Red(open.Vertices, openStart);
        for (var vertex = 1; vertex < 4; vertex++)
        {
            Assert.Equal(reference, Red(open.Vertices, openStart + vertex));
        }

        // With them, the boxed corner drops to the reference quarter average of three opaque samples and one open
        // centre: (0.2 + 0.2 + 0.2 + 1.0) / 4 = 0.4, which on a full brightness face is channel round(255 * 0.4) = 102.
        // Three samples are opaque because the diagonal cell is boxed as well, and the two-sided rule replaces the
        // diagonal with the east corner rather than sampling it.
        var values = new[] { Red(blocked.Vertices, blockedStart), Red(blocked.Vertices, blockedStart + 1),
                             Red(blocked.Vertices, blockedStart + 2), Red(blocked.Vertices, blockedStart + 3) };
        var darkest = values.Min();
        var brightest = values.Max();

        Assert.True(darkest < brightest, $"every corner of the up face is {brightest}: no occlusion reached the model path");
        Assert.Equal(brightest, reference);
        Assert.Equal(102, darkest);
        Assert.Equal(255, brightest);
    }

    /// <summary>Turning occlusion off leaves the model path at full brightness while the light still smooths.</summary>
    [Fact]
    public void TheModelPathHonoursTheOcclusionSwitch()
    {
        using var blocked = Mesh(occludeEastNorth: true, ambientOcclusion: false);
        var start = UpFaceStart(blocked.Vertices);
        var reference = Red(blocked.Vertices, start);

        for (var vertex = 1; vertex < 4; vertex++)
        {
            Assert.Equal(reference, Red(blocked.Vertices, start + vertex));
        }
    }

    /// <summary>
    /// The corner each vertex value lands on is decided by the vertex position inside the quad, not by our merged
    /// path's corner order, which is what lets a model file order its vertices freely.
    /// </summary>
    [Fact]
    public void TheDarkestVertexIsTheOneNextToTheOccluders()
    {
        using var blocked = Mesh(occludeEastNorth: true);
        var start = UpFaceStart(blocked.Vertices);

        var darkest = -1;
        var darkestValue = int.MaxValue;
        for (var vertex = 0; vertex < 4; vertex++)
        {
            var value = Red(blocked.Vertices, start + vertex);
            if (value < darkestValue)
            {
                darkestValue = value;
                darkest = vertex;
            }
        }

        Assert.InRange(darkest, 0, 3);
        var at = (start + darkest) * ChunkMesher.FloatsPerVertex;
        Assert.Equal(BlockX + 1, blocked.Vertices[at]);      // +X side, where one occluder sits
        Assert.Equal(BlockZ, blocked.Vertices[at + 2]);      // -Z side, where the other one sits
    }

    // ---- geometry: rotation and the quad-count guard ------------------------------------------------------------

    /// <summary>
    /// A model with more quads than the sample buffer keeps the single-light fallback instead of growing the stack.
    /// This pins the boundary; the fallback itself is the documented residual SIM-MODEL-QUAD-COUNT-CAP / R-051.
    /// </summary>
    [Fact]
    public void TheQuadCountCapIsWhereItSaysItIs()
    {
        Assert.True(ChunkMesher.ModelSharesQuadSamples(ChunkMesher.MaxModelQuads));
        Assert.False(ChunkMesher.ModelSharesQuadSamples(ChunkMesher.MaxModelQuads + 1));
        Assert.True(ChunkMesher.MaxModelQuads >= 12);
    }
    /// <summary>Corner brightness of every quad vertex, keyed by face direction and by the corner's sign pair.</summary>
    private static Dictionary<(string Direction, int Code), int> CornerBrightness(BlockModelSet models)
    {
        using var buffer = Mesh(occludeEastNorth: true, modelsOverride: models);
        var vertices = buffer.Vertices;
        var result = new Dictionary<(string, int), int>();

        for (var quad = 0; quad * 4 + 3 < buffer.VertexCount; quad++)
        {
            var first = quad * 4;
            var direction = DirectionNameOf(vertices, first);
            if (direction is null)
            {
                continue;
            }

            var (uAxis, vAxis) = direction switch
            {
                "up" or "down" => (0, 2),
                "north" or "south" => (0, 1),
                _ => (2, 1)
            };

            var minU = float.MaxValue;
            var maxU = float.MinValue;
            var minV = float.MaxValue;
            var maxV = float.MinValue;
            for (var vertex = 0; vertex < 4; vertex++)
            {
                var at = (first + vertex) * ChunkMesher.FloatsPerVertex;
                minU = Math.Min(minU, vertices[at + uAxis]);
                maxU = Math.Max(maxU, vertices[at + uAxis]);
                minV = Math.Min(minV, vertices[at + vAxis]);
                maxV = Math.Max(maxV, vertices[at + vAxis]);
            }

            for (var vertex = 0; vertex < 4; vertex++)
            {
                var at = (first + vertex) * ChunkMesher.FloatsPerVertex;
                var code = 0;
                if (vertices[at + uAxis] > ((minU + maxU) * 0.5f))
                {
                    code |= 1;
                }

                if (vertices[at + vAxis] > ((minV + maxV) * 0.5f))
                {
                    code |= 2;
                }

                result[(direction, code)] = Red(vertices, first + vertex);
            }
        }

        return result;
    }

    /// <summary>The face a quad lies on, or null when the four vertices share no axis plane.</summary>
    private static string? DirectionNameOf(ReadOnlySpan<float> vertices, int first)
    {
        for (var axis = 0; axis < 3; axis++)
        {
            var value = vertices[(first * ChunkMesher.FloatsPerVertex) + axis];
            var same = true;
            for (var vertex = 1; vertex < 4; vertex++)
            {
                same &= vertices[((first + vertex) * ChunkMesher.FloatsPerVertex) + axis] == value;
            }

            if (!same)
            {
                continue;
            }

            // A plane at a block edge is a face; an interior plane came from a rotation and is not one of the six.
            return axis switch
            {
                1 => (int)value == BlockY + 1 ? "up" : (int)value == BlockY ? "down" : null,
                0 => (int)value == BlockX + 1 ? "east" : (int)value == BlockX ? "west" : null,
                _ => (int)value == BlockZ + 1 ? "south" : (int)value == BlockZ ? "north" : null
            };
        }

        return null;
    }
}