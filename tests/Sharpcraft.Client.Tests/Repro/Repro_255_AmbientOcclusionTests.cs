// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Imaging;
using Sharpcraft.Common.Network;
using Sharpcraft.Common.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Repro;

/// <summary>
/// Issue 255: ambient occlusion and smooth light must follow the reference block model lighter rather than the
/// pre-1.15 "average four cells then darken" rule. These tests drive <see cref="ChunkMesher.PrepareQuadLighting"/>
/// directly with hand-built occlusion and light layouts and compare against values computed here from the reference
/// formulas, so a wrong corner order, a missing diagonal rule or a dropped weight shows up as a number, not a hunch.
/// </summary>
public sealed class Repro_255_AmbientOcclusionTests
{
    private const int BlockX = 8;
    private const int BlockY = 8;
    private const int BlockZ = 8;

    // Direction constants, restated here so a renumbering in the mesher cannot silently re-point these tests.
    private const int West = 0;
    private const int East = 1;
    private const int Down = 2;
    private const int Up = 3;
    private const int North = 4;
    private const int South = 5;

    private static readonly (int X, int Y, int Z)[] Offsets =
    [
        (-1, 0, 0), // west
        (1, 0, 0),  // east
        (0, -1, 0), // down
        (0, 1, 0),  // up
        (0, 0, -1), // north
        (0, 0, 1)   // south
    ];

    /// <summary>A scene with one chunk whose cells can be made solid and given per-cell light.</summary>
    private sealed class Scene
    {
        public Scene()
        {
            World = new ClientWorld(minSectionY: -1, sectionCount: 3);
            Chunk = World.AddChunk(0, 0);
            Section = new ChunkSection(0, 0);
            Chunk.SetSection(Section);

            _sky = new byte[2048];
            Array.Fill(_sky, (byte)0xFF);
            _block = new byte[2048];
            Array.Fill(_block, (byte)0x00);
        }

        private readonly byte[] _sky;
        private readonly byte[] _block;

        public ClientWorld World { get; }
        public LevelChunk Chunk { get; }
        public ChunkSection Section { get; }

        /// <summary>Makes a cell solid; the state defaults to stone and can name another block in the layout.</summary>
        public Scene Solid(int x, int y, int z, Sharpcraft.Common.Identifiers.Identifier? block = null)
        {
            Section.Set(x, y, z, StateOf(block));
            return this;
        }

        private static int StateOf(Sharpcraft.Common.Identifiers.Identifier? block)
        {
            if (block is null)
            {
                return 1;
            }

            var layout = Layout();
            return layout.TryGetBaseId(block.Value, out var id) ? id : throw new InvalidOperationException($"unknown block {block}");
        }

        public Scene Light(int x, int y, int z, byte sky, byte block)
        {
            SetNibble(_sky, x, y, z, sky);
            SetNibble(_block, x, y, z, block);
            return this;
        }

        public Scene Build()
        {
            var lightSection = 0 - World.MinSectionY + 1;
            Chunk.Light.SetSkyLight(lightSection, _sky);
            Chunk.Light.SetBlockLight(lightSection, _block);
            Chunk.MarkLoaded();
            return this;
        }

        private static void SetNibble(byte[] data, int x, int y, int z, byte level)
        {
            var index = (y << 8) | (z << 4) | x;
            var at = index >> 1;
            data[at] = (index & 1) == 0
                ? (byte)((data[at] & 0xF0) | (level & 0xF))
                : (byte)((data[at] & 0x0F) | ((level & 0xF) << 4));
        }
    }

    private static BlockSpriteMap Sprites()
    {
        var data = new byte[16 * 16 * 4];
        Array.Fill(data, (byte)255);
        var entries = new List<(Sharpcraft.Common.Identifiers.Identifier Location, RgbaImage Image, int Frames)>
        {
            (SpriteAtlas.MissingSprite, new RgbaImage(16, 16, data), 1)
        };

        return new BlockSpriteMap(SpriteAtlasBuilder.Stitch(entries));
    }

    /// <summary>
    /// Air, an opaque full block, and two blocks whose sprite is not opaque but whose collision shape is a full block:
    /// glass and leaves. The reference shades both of them 0.2 and lets light through them, so a renderer that judges by
    /// the texture gets them backwards.
    /// </summary>
    private static Sharpcraft.Common.Registries.BlockStateLayout Layout() =>
        new(
        [
            (Sharpcraft.Common.Identifiers.Identifier.Parse("minecraft:air"), 1),
            (Sharpcraft.Common.Identifiers.Identifier.Parse("minecraft:stone"), 1),
            (Sharpcraft.Common.Identifiers.Identifier.Parse("minecraft:glass"), 1),
            (Sharpcraft.Common.Identifiers.Identifier.Parse("minecraft:oak_leaves"), 1)
        ]);

    /// <summary>Runs the reference pass for one face and returns the four slots.</summary>
    private static ChunkMesher.VertexLighting[] Run(
        Scene scene,
        int direction,
        int x = BlockX,
        int y = BlockY,
        int z = BlockZ,
        (float MinX, float MaxX, float MinY, float MaxY, float MinZ, float MaxZ)? bounds = null,
        bool ambientOcclusion = true)
    {
        var sprites = Sprites();
        var layout = Layout();
        var neighbourhood = new ChunkMesher.Neighbourhood(scene.World, scene.Chunk);
        var slots = new ChunkMesher.VertexLighting[4];
        ChunkMesher.PrepareQuadLighting(
            neighbourhood,
            direction,
            (x, y, z),
            ambientOcclusion,
            sprites,
            layout,
            bounds ?? ChunkMesher.CubeFaceBoundsOf(direction),
            slots);
        return slots;
    }

    // ---- independent oracle ------------------------------------------------------------------------------------

    /// <summary>
    /// The reference blend, restated on (block, sky) pairs on the 0..240 scale. A whole-face vertex keeps only the
    /// four-bit level, which is what <see cref="Cut"/> applies; the vector probe pins that truncation.
    /// </summary>
    private static (int Block, int Sky) Blend((int Block, int Sky) a, (int Block, int Sky) b, (int Block, int Sky) c, (int Block, int Sky) center)
    {
        if (center.Sky > 2 || center.Block > 2)
        {
            a = Fix(a, center);
            b = Fix(b, center);
            c = Fix(c, center);
        }

        return ((a.Block + b.Block + c.Block + center.Block) >> 2, (a.Sky + b.Sky + c.Sky + center.Sky) >> 2);

        static (int Block, int Sky) Fix((int Block, int Sky) n, (int Block, int Sky) center) =>
            n is (0, 0) ? center : n.Sky == 0 ? (n.Block, center.Sky) : n;


    }

    /// <summary>The four corner values of a whole cube face, in the reference's computed order.</summary>
    private static (float Brightness, (int Block, int Sky) Light)[] CubeFace(
        (float Shade, int Block, int Sky) c0, (float Shade, int Block, int Sky) c1,
        (float Shade, int Block, int Sky) c2, (float Shade, int Block, int Sky) c3,
        (float Shade, int Block, int Sky) d02, (float Shade, int Block, int Sky) d03,
        (float Shade, int Block, int Sky) d12, (float Shade, int Block, int Sky) d13,
        (float Shade, int Block, int Sky) center)
    {
        static (int, int) L((float Shade, int Block, int Sky) v) => (v.Block, v.Sky);

        // A whole-face vertex keeps only the four-bit level, so the fraction of a blend is dropped here; the partial
        // face branch feeds the same blends into the weighted sum and keeps the fraction.
        static (int Block, int Sky) Cut((int Block, int Sky) v) => ((v.Block >> 4) << 4, (v.Sky >> 4) << 4);

        return
        [
            ((c3.Shade + c0.Shade + d03.Shade + center.Shade) * 0.25f, Cut(Blend(L(c3), L(c0), L(d03), L(center)))),
            ((c2.Shade + c0.Shade + d02.Shade + center.Shade) * 0.25f, Cut(Blend(L(c2), L(c0), L(d02), L(center)))),
            ((c2.Shade + c1.Shade + d12.Shade + center.Shade) * 0.25f, Cut(Blend(L(c2), L(c1), L(d12), L(center)))),
            ((c3.Shade + c1.Shade + d13.Shade + center.Shade) * 0.25f, Cut(Blend(L(c3), L(c1), L(d13), L(center))))
        ];
    }

    /// <summary>Where each computed corner of a face lands in our quad corner order.</summary>
    private static int[] SlotOfComputedVertex(int direction) =>
        [.. ChunkMesher.VertexRemapOf(direction).ToArray().Select(v => ChunkMesher.QuadCornerOrderOf(direction)[v])];

    // ---- table structure ---------------------------------------------------------------------------------------

    /// <summary>
    /// The corner tables name the reference's own neighbour directions. For the up face the reference lists east,
    /// west, north, south (ref BlockModelLighter.java:334), and every table row is four entries.
    /// </summary>
    [Fact]
    public void TheCornerTablesNameTheReferenceDirections()
    {
        Assert.Equal([East, West, North, South], ChunkMesher.CornerDirectionsOf(Up).ToArray());
        Assert.Equal([West, East, North, South], ChunkMesher.CornerDirectionsOf(Down).ToArray());
        Assert.Equal([Up, Down, East, West], ChunkMesher.CornerDirectionsOf(North).ToArray());
        Assert.Equal([West, East, Down, Up], ChunkMesher.CornerDirectionsOf(South).ToArray());
        Assert.Equal([Up, Down, North, South], ChunkMesher.CornerDirectionsOf(West).ToArray());
        Assert.Equal([Down, Up, North, South], ChunkMesher.CornerDirectionsOf(East).ToArray());

        for (var direction = 0; direction < 6; direction++)
        {
            Assert.Equal(4, ChunkMesher.CornerDirectionsOf(direction).Length);
            Assert.Equal(4, ChunkMesher.VertexRemapOf(direction).Length);
            Assert.Equal(4, ChunkMesher.QuadCornerOrderOf(direction).Length);
            for (var vertex = 0; vertex < 4; vertex++)
            {
                Assert.Equal(8, ChunkMesher.VertexWeightsOf(direction, vertex).Length);
            }
        }
    }

    /// <summary>
    /// The remap and our corner order compose to a permutation: every computed corner reaches exactly one of our
    /// four slots, so no corner is written twice and none is dropped.
    /// </summary>
    [Fact]
    public void TheVertexRemapIsAPermutationForEveryFace()
    {
        for (var direction = 0; direction < 6; direction++)
        {
            var slots = SlotOfComputedVertex(direction);
            Assert.Equal(4, slots.Distinct().Count());
            Assert.All(slots, slot => Assert.InRange(slot, 0, 3));
        }
    }

    /// <summary>All six faces carry the reference's non-cubic weight flag (ref BlockModelLighter.java:291,336,381,426,471,516).</summary>
    [Fact]
    public void EveryFaceCarriesTheNonCubicWeightFlag()
    {
        for (var direction = 0; direction < 6; direction++)
        {
            Assert.True(ChunkMesher.NonCubicWeight(direction));
        }
    }

    /// <summary>The partial-face test is the reference's: anything narrower than the block in either in-plane axis.</summary>
    [Fact]
    public void ThePartialFaceTestMatchesTheReferenceEpsilon()
    {
        Assert.False(ChunkMesher.IsPartialFace(Up, ChunkMesher.CubeFaceBoundsOf(Up)));
        Assert.False(ChunkMesher.IsPartialFace(East, ChunkMesher.CubeFaceBoundsOf(East)));
        Assert.True(ChunkMesher.IsPartialFace(Up, (0.5f, 1f, 1f, 1f, 0f, 1f)));
        Assert.True(ChunkMesher.IsPartialFace(East, (1f, 1f, 0f, 1f, 0.5f, 1f)));
        Assert.True(ChunkMesher.IsPartialFace(Up, (0f, 1f, 1f, 1f, 0.0002f, 1f)));
    }

    // ---- whole cube face ---------------------------------------------------------------------------------------

    /// <summary>
    /// One occluder east of an up face darkens the two corners that touch +X. A wrong corner table, a wrong vertex
    /// remap or a flipped diagonal all move the dark corners.
    /// </summary>
    /// <remarks>
    /// Our quad corner order for an up face is 0 = (-X,-Z), 1 = (-X,+Z), 2 = (+X,+Z), 3 = (+X,-Z), so an occluder on
    /// +X darkens slots 2 and 3 and one on -Z darkens slots 0 and 3.
    /// </remarks>
    [Theory]
    [InlineData(East, 2, 3)]
    [InlineData(West, 0, 1)]
    [InlineData(North, 0, 3)]
    [InlineData(South, 1, 2)]
    public void OneOccluderDarkensExactlyTheCornersItTouches(int occluderDirection, int darkA, int darkB)
    {
        var (ox, oy, oz) = Offsets[occluderDirection];
        var scene = new Scene();
        scene.Solid(BlockX + ox, BlockY + 1 + oy, BlockZ + oz);
        scene.Light(BlockX, BlockY + 1, BlockZ, 15, 0);
        scene.Build();

        var slots = Run(scene, Up);

        // Corner shades are 0.2 next to the occluder and 1.0 elsewhere, so the two corners that touch it come out at
        // 0.8 and the other two stay at 1.0.
        for (var slot = 0; slot < 4; slot++)
        {
            var expected = slot == darkA || slot == darkB ? 0.8f : 1f;
            Assert.Equal(expected, slots[slot].Brightness, 5);
        }
    }

    /// <summary>
    /// A three-cell light staircase: the blended light per corner must be the reference average of the corner, its
    /// diagonal partner and the centre, not a plain average of the four cells.
    /// </summary>
    [Fact]
    public void SmoothLightBlendsTheCornerWithItsDiagonalAndTheCentre()
    {
        var scene = new Scene();
        // Nothing solid: every shade is 1.0, so only the light blend is under test.
        scene.Light(BlockX + 1, BlockY + 1, BlockZ, 15, 15)         // east corner
             .Light(BlockX, BlockY + 1, BlockZ - 1, 15, 3)          // north corner
             .Light(BlockX + 1, BlockY + 1, BlockZ - 1, 15, 12)     // east+north diagonal
             .Light(BlockX, BlockY + 1, BlockZ, 15, 9)              // centre
             .Light(BlockX - 1, BlockY + 1, BlockZ, 15, 1)          // west corner
             .Light(BlockX, BlockY + 1, BlockZ + 1, 15, 2)          // south corner
             .Light(BlockX - 1, BlockY + 1, BlockZ - 1, 15, 5)      // west+north
             .Light(BlockX - 1, BlockY + 1, BlockZ + 1, 15, 6)      // west+south
             .Light(BlockX + 1, BlockY + 1, BlockZ + 1, 15, 7);     // east+south
        scene.Build();

        var east = (Shade: 1f, Block: 15 * 16, Sky: 15 * 16);
        var west = (Shade: 1f, Block: 1 * 16, Sky: 15 * 16);
        var north = (Shade: 1f, Block: 3 * 16, Sky: 15 * 16);
        var south = (Shade: 1f, Block: 2 * 16, Sky: 15 * 16);
        var center = (Shade: 1f, Block: 9 * 16, Sky: 15 * 16);
        var d02 = (Shade: 1f, Block: 12 * 16, Sky: 15 * 16);
        var d03 = (Shade: 1f, Block: 7 * 16, Sky: 15 * 16);
        var d12 = (Shade: 1f, Block: 5 * 16, Sky: 15 * 16);
        var d13 = (Shade: 1f, Block: 6 * 16, Sky: 15 * 16);

        var expected = CubeFace(east, west, north, south, d02, d03, d12, d13, center);
        var slots = SlotOfComputedVertex(Up);
        var actual = Run(scene, Up);

        for (var vertex = 0; vertex < 4; vertex++)
        {
            var slot = slots[vertex];
            Assert.Equal(expected[vertex].Light.Block / 240f, actual[slot].BlockLight, 5);
            Assert.Equal(1f, actual[slot].Brightness, 5);
        }

    }

    /// <summary>With occlusion off the brightness is one while the light still smooths.</summary>
    [Fact]
    public void TurningOcclusionOffKeepsTheSmoothLight()
    {
        var scene = new Scene();
        scene.Solid(BlockX + 1, BlockY + 1, BlockZ)
             .Light(BlockX + 1, BlockY + 1, BlockZ, 15, 15)
             .Light(BlockX, BlockY + 1, BlockZ, 15, 9);
        scene.Build();

        var withOcclusion = Run(scene, Up);
        var without = Run(scene, Up, ambientOcclusion: false);

        Assert.Contains(withOcclusion, slot => slot.Brightness < 0.99f);
        Assert.All(without, slot => Assert.Equal(1f, slot.Brightness, 5));
        for (var slot = 0; slot < 4; slot++)
        {
            Assert.Equal(withOcclusion[slot].BlockLight, without[slot].BlockLight, 5);
        }
    }

    // ---- partial face ------------------------------------------------------------------------------------------


    // ---- partial face ------------------------------------------------------------------------------------------

    /// <summary>
    /// A partial face weights the four corner shades by the model's shape entries. The numbers below are worked out by
    /// hand from the reference tables: for the up face, computed vertex 0 weights entries (5,3,5,9,11,9,11,3) and
    /// vertex 2 weights (4,2,4,8,10,8,10,2). With the quad covering the block along X (0..1) and half of Z (0..0.5) the
    /// shape entries are up=1, south=0.5, flip-north=1, flip-south=0.5, flip-west=1, flip-east=0, the rest zero, so
    /// vertex 0 covers shade corners 0 and 1 at half each and vertex 2 covers corner shade 2 alone.
    /// </summary>
    [Fact]
    public void APartialFaceWeightsTheCornerShades()
    {
        var scene = new Scene();
        // A single occluder to the south: shade[3] (the corner pair 3+1 and 3+0) is the only one below one.
        scene.Solid(BlockX, BlockY + 1, BlockZ + 1)
             .Light(BlockX, BlockY + 1, BlockZ, 15, 9)
             .Light(BlockX, BlockY + 1, BlockZ + 1, 15, 9);
        scene.Build();

        var bounds = (MinX: 0f, MaxX: 1f, MinY: 1f, MaxY: 1f, MinZ: 0f, MaxZ: 0.5f);
        Assert.True(ChunkMesher.IsPartialFace(Up, bounds));

        var partial = Run(scene, Up, bounds: bounds);
        var cube = Run(scene, Up);

        // Corner shades: c3 (south) is 0.2, the others 1.0, and no diagonal is behind an opaque pair.
        //   shade0 = (c3 + c0 + d03 + center) / 4 = (0.2 + 1 + 1 + 1) / 4 = 0.8
        //   shade1 = (c2 + c0 + d02 + center) / 4 = 1.0
        //   shade2 = (c2 + c1 + d12 + center) / 4 = 1.0
        //   shade3 = (c3 + c1 + d13 + center) / 4 = 0.8
        // Partial weights for vertex 0: 0.5 * shade0 + 0.5 * shade1 = 0.9 (the cube branch would say 0.8).
        // Partial weights for vertex 2: 1.0 * shade2 = 1.0.
        // Partial weights for vertex 3: 0.5 * shade2 + 0.5 * shade3 = 0.9.
        var expected = new[] { 0.9f, 1f, 1f, 0.9f };
        var slots = SlotOfComputedVertex(Up);
        for (var vertex = 0; vertex < 4; vertex++)
        {
            Assert.Equal(expected[vertex], partial[slots[vertex]].Brightness, 4);
        }

        // The cube branch keeps the raw corner values, which is why the two differ.
        Assert.Equal(0.8f, cube[slots[0]].Brightness, 4);
        Assert.Equal(0.8f, cube[slots[3]].Brightness, 4);
    }

    /// <summary>
    /// The light of a partial face is the reference's weighted blend of the four blended samples, truncated per channel,
    /// not the plain average the cube branch would use.
    /// </summary>
    [Fact]
    public void APartialFaceWeightsTheBlendedLight()
    {
        var scene = new Scene();
        scene.Light(BlockX, BlockY + 1, BlockZ, 15, 9)
             .Light(BlockX, BlockY + 1, BlockZ + 1, 15, 8)
             .Light(BlockX, BlockY + 1, BlockZ - 1, 15, 4)
             .Light(BlockX + 1, BlockY + 1, BlockZ, 15, 12)
             .Light(BlockX - 1, BlockY + 1, BlockZ, 15, 2);
        scene.Build();

        var bounds = (MinX: 0f, MaxX: 1f, MinY: 1f, MaxY: 1f, MinZ: 0f, MaxZ: 0.5f);
        var partial = Run(scene, Up, bounds: bounds);
        var cube = Run(scene, Up);

        // Corner samples, in 0..240 space: east 12, west 2, north 4, south 8, centre 9, and 0 for the three
        // diagonals whose cells are unlit. Vertex 0 weights 0.5 (blend0) and 0.5 (blend1) and nothing else.
        var east = (Block: 192, Sky: 240);
        var north = (Block: 64, Sky: 240);
        var south = (Block: 128, Sky: 240);
        var center = (Block: 144, Sky: 240);
        var zero = (Block: 0, Sky: 240);

        var blend0 = Blend(south, east, zero, center);
        var blend1 = Blend(north, east, zero, center);
        var expectedBlock = (int)((blend0.Block * 0.5f) + (blend1.Block * 0.5f));

        var slots = SlotOfComputedVertex(Up);
        Assert.Equal(expectedBlock / 240f, partial[slots[0]].BlockLight, 5);
        Assert.NotEqual(expectedBlock / 240f, cube[slots[0]].BlockLight, 5);
    }

    /// <summary>
    /// The two-sided rule for all four diagonals: when both sides of a diagonal are opaque one cell along the normal, the
    /// diagonal is not sampled and corner zero is reused. The 12 and 13 pairs reuse <em>corner zero</em>, not their own
    /// partner, which is what the reference does and what a "tidy" rewrite gets wrong.
    /// </summary>
    /// <remarks>
    /// For an up face the four computed vertices take the diagonals in the order: vertex 0 = corners 3+0,
    /// vertex 1 = corners 2+0, vertex 2 = corners 2+1, vertex 3 = corners 3+1. Corner zero is east, so every case keeps
    /// the east corner light (4) and gives the diagonal itself a bright, distinct light (15) that must be ignored.
    /// </remarks>
    [Theory]
    [InlineData(North, East, 1)]
    [InlineData(North, West, 2)]
    [InlineData(South, East, 0)]
    [InlineData(South, West, 3)]
    public void AnOpaqueDiagonalPairReusesCornerZeroInsteadOfSampling(int sideA, int sideB, int vertex)
    {
        var (ax, ay, az) = Offsets[sideA];
        var (bx, by, bz) = Offsets[sideB];
        var scene = new Scene();
        scene.Solid(BlockX + ax, BlockY + 1 + ay, BlockZ + az)          // side A corner
             .Solid(BlockX + bx, BlockY + 1 + by, BlockZ + bz)          // side B corner
             .Solid(BlockX + ax + bx, BlockY + 1 + ay + by, BlockZ + az + bz)   // the diagonal itself
             .Solid(BlockX + ax, BlockY + 2 + ay, BlockZ + az)          // side A probe cell
             .Solid(BlockX + bx, BlockY + 2 + by, BlockZ + bz)          // side B probe cell
             .Light(BlockX + 1, BlockY + 1, BlockZ, 15, 4)              // corner zero (east): block light 4
             .Light(BlockX + ax + bx, BlockY + 1 + ay + by, BlockZ + az + bz, 15, 15)  // bright diagonal
             .Light(BlockX, BlockY + 1, BlockZ, 15, 15);                // centre
        scene.Build();

        // Oracle: corner zero's sample replaces the diagonal, so the diagonal entries of the blend are the east corner.
        var east = (Shade: 0.2f, Block: 4 * 16, Sky: 15 * 16);
        var dark = (Shade: 0.2f, Block: 0, Sky: 15 * 16);
        var clear = (Shade: 1f, Block: 0, Sky: 15 * 16);
        var center = (Shade: 1f, Block: 15 * 16, Sky: 15 * 16);
        var c2 = sideA == North || sideB == North ? dark : clear;
        var c3 = sideA == South || sideB == South ? dark : clear;
        var c1 = sideA == West || sideB == West ? dark : clear;

        var expected = CubeFace(east, c1, c2, c3, east, east, east, east, center);
        var slots = SlotOfComputedVertex(Up);
        var actual = Run(scene, Up);

        Assert.Equal(expected[vertex].Light.Block / 240f, actual[slots[vertex]].BlockLight, 5);

        // Control: the same scene with the two probe cells left empty must sample the bright diagonal instead.
        var openScene = new Scene();
        openScene.Solid(BlockX + ax, BlockY + 1 + ay, BlockZ + az)
                 .Solid(BlockX + bx, BlockY + 1 + by, BlockZ + bz)
                 .Solid(BlockX + ax + bx, BlockY + 1 + ay + by, BlockZ + az + bz)
                 .Light(BlockX + 1, BlockY + 1, BlockZ, 15, 4)
                 .Light(BlockX + ax + bx, BlockY + 1 + ay + by, BlockZ + az + bz, 15, 15)
                 .Light(BlockX, BlockY + 1, BlockZ, 15, 15);
        openScene.Build();

        var open = Run(openScene, Up);
        Assert.NotEqual(actual[slots[vertex]].BlockLight, open[slots[vertex]].BlockLight);
    }

    /// <summary>
    /// The geometric corner match (used by the element model path, whose vertices come from the model file in any order)
    /// agrees with the merged path's table for all six faces: for every corner of every face, the baked vertex derived
    /// from the position inside the quad must be the one the hand-written table names.
    /// </summary>
    /// <remarks>
    /// Our corner slots are written here as (uMax, vMax) sign pairs, which is what the merged path builds when it assigns
    /// origin, a, b and c; the baked quad's order comes from FaceInfo.
    /// </remarks>
    [Fact]
    public void TheGeometricCornerMatchAgreesWithTheMergedPathTable()
    {
        // Slot order per face: 0 = (-u,-v), 1 = (+u,-v), 2 = (+u,+v), 3 = (-u,+v) except the faces whose V span starts
        // from the high end, which is why these are written out rather than derived.
        int[][] slotSigns =
        [
            [0, 1, 3, 2],   // -X west
            [0, 2, 3, 1],   // +X east
            [0, 1, 3, 2],   // -Y down
            [0, 2, 3, 1],   // +Y up
            [0, 1, 3, 2],   // -Z north
            [0, 1, 3, 2]    // +Z south
        ];

        for (var direction = 0; direction < 6; direction++)
        {
            var table = ChunkMesher.QuadCornerOrderOf(direction).ToArray();
            for (var slot = 0; slot < 4; slot++)
            {
                var code = slotSigns[direction][slot];
                var baked = ChunkMesher.BakedVertexAt(direction, (code & 1) != 0, (code & 2) != 0);
                Assert.Equal(slot, table[baked]);
            }
        }
    }

    // ---- the three reference predicates ------------------------------------------------------------------------

    /// <summary>
    /// Glass and leaves have a full collision shape but an empty occlusion shape, so the reference shades a corner next
    /// to them at 0.2 even though their texture is see-through. Judging by the sprite instead (the proxy this replaced)
    /// left them at 1.0, which is the "leaves have no ambient occlusion" half of issue 255.
    /// </summary>
    [Theory]
    [InlineData("minecraft:glass")]
    [InlineData("minecraft:oak_leaves")]
    public void ATransparentFullBlockShadesItsCornerLikeTheReference(string blockName)
    {
        var scene = new Scene();
        scene.Solid(BlockX + 1, BlockY + 1, BlockZ, Sharpcraft.Common.Identifiers.Identifier.Parse(blockName));
        scene.Light(BlockX, BlockY + 1, BlockZ, 15, 0);
        scene.Build();

        var slots = Run(scene, Up);

        // One occluder east: slots 2 and 3 touch +X and come out at 0.8, the other two stay at 1.0.
        Assert.Equal(0.8f, slots[2].Brightness, 4);
        Assert.Equal(0.8f, slots[3].Brightness, 4);
        Assert.Equal(1f, slots[0].Brightness, 4);
        Assert.Equal(1f, slots[1].Brightness, 4);
    }

    /// <summary>
    /// The same two blocks are light permeable (their occlusion shape is not full), so the two-sided rule must not fire
    /// even with both sides and the probe cells filled: a bright diagonal has to reach the result.
    /// </summary>
    [Theory]
    [InlineData("minecraft:glass")]
    [InlineData("minecraft:oak_leaves")]
    public void ATransparentFullBlockStaysLightPermeable(string blockName)
    {
        var block = Sharpcraft.Common.Identifiers.Identifier.Parse(blockName);

        // East and north corners opaque-ish, plus their probe cells, plus the diagonal itself, but the light is only in
        // the diagonal cell: if the pair rule fired, corner zero (dark) would replace it.
        var permeableScene = new Scene();
        permeableScene.Solid(BlockX + 1, BlockY + 1, BlockZ, block)
                      .Solid(BlockX, BlockY + 1, BlockZ - 1, block)
                      .Solid(BlockX + 1, BlockY + 2, BlockZ, block)
                      .Solid(BlockX, BlockY + 2, BlockZ - 1, block)
                      .Solid(BlockX + 1, BlockY + 1, BlockZ - 1, block)
                      .Light(BlockX + 1, BlockY + 1, BlockZ - 1, 15, 15)
                      .Light(BlockX, BlockY + 1, BlockZ, 15, 0);
        permeableScene.Build();

        // The same scene with the two probe cells replaced by stone, which is not permeable, so the pair rule fires.
        var blockedScene = new Scene();
        var stone = Sharpcraft.Common.Identifiers.Identifier.Parse("minecraft:stone");
        blockedScene.Solid(BlockX + 1, BlockY + 1, BlockZ, block)
                    .Solid(BlockX, BlockY + 1, BlockZ - 1, block)
                    .Solid(BlockX + 1, BlockY + 2, BlockZ, stone)
                    .Solid(BlockX, BlockY + 2, BlockZ - 1, stone)
                    .Solid(BlockX + 1, BlockY + 1, BlockZ - 1, block)
                    .Light(BlockX + 1, BlockY + 1, BlockZ - 1, 15, 15)
                    .Light(BlockX, BlockY + 1, BlockZ, 15, 0);
        blockedScene.Build();

        var slots = SlotOfComputedVertex(Up);
        var permeable = Run(permeableScene, Up);
        var blocked = Run(blockedScene, Up);

        // Vertex 1 is the (north + east) corner, whose diagonal carries the only light in the scene.
        Assert.True(
            permeable[slots[1]].BlockLight > blocked[slots[1]].BlockLight,
            $"a permeable neighbour did not let the bright diagonal through: {permeable[slots[1]].BlockLight} vs {blocked[slots[1]].BlockLight}");
    }
}