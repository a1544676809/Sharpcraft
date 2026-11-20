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

        public Scene Solid(int x, int y, int z)
        {
            Section.Set(x, y, z, 1);
            return this;
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

    private static Sharpcraft.Common.Registries.BlockStateLayout Layout() =>
        new(
        [
            (Sharpcraft.Common.Identifiers.Identifier.Parse("minecraft:air"), 1),
            (Sharpcraft.Common.Identifiers.Identifier.Parse("minecraft:stone"), 1)
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

    /// <summary>The reference's smoothBlend, restated on (block, sky) nibbles: this is the oracle, not the subject.</summary>
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

        return
        [
            ((c3.Shade + c0.Shade + d03.Shade + center.Shade) * 0.25f, Blend(L(c3), L(c0), L(d03), L(center))),
            ((c2.Shade + c0.Shade + d02.Shade + center.Shade) * 0.25f, Blend(L(c2), L(c0), L(d02), L(center))),
            ((c2.Shade + c1.Shade + d12.Shade + center.Shade) * 0.25f, Blend(L(c2), L(c1), L(d12), L(center))),
            ((c3.Shade + c1.Shade + d13.Shade + center.Shade) * 0.25f, Blend(L(c3), L(c1), L(d13), L(center)))
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
    /// The two-sided rule: when both sides of a diagonal are opaque the diagonal is not sampled and corner zero's
    /// value is reused, so a bright diagonal cell must not leak into the result.
    /// </summary>
    [Fact]
    public void AnOpaqueDiagonalPairReusesCornerZeroInsteadOfSampling()
    {
        // Up face: corners are east(0), west(1), north(2), south(3); the 02 diagonal sits at east+north.
        // The reference decides whether to sample a diagonal by probing one cell further along the face normal, so the
        // two sides have to be two blocks tall for the pair rule to fire.
        var scene = new Scene();
        scene.Solid(BlockX + 1, BlockY + 1, BlockZ)          // east corner
             .Solid(BlockX, BlockY + 1, BlockZ - 1)          // north corner
             .Solid(BlockX + 1, BlockY + 1, BlockZ - 1)      // the 02 diagonal itself
             .Solid(BlockX + 1, BlockY + 2, BlockZ)          // east corner, probe cell
             .Solid(BlockX, BlockY + 2, BlockZ - 1)          // north corner, probe cell
             .Light(BlockX + 1, BlockY + 1, BlockZ, 15, 4)   // corner zero: block light 4
             .Light(BlockX, BlockY + 1, BlockZ - 1, 15, 4)   // north corner
             .Light(BlockX + 1, BlockY + 1, BlockZ - 1, 15, 15)  // diagonal is bright but must be ignored
             .Light(BlockX, BlockY + 1, BlockZ, 15, 15);     // centre
        scene.Build();

        // Shades: east and north blocks are opaque; the diagonal cell is opaque too, but the pair rule replaces it
        // with corner zero's sample (shade 0.2, light 4), never with its own light 15.
        var east = (Shade: 0.2f, Block: 4 * 16, Sky: 15 * 16);
        var north = (Shade: 0.2f, Block: 4 * 16, Sky: 15 * 16);
        var west = (Shade: 1f, Block: 0, Sky: 15 * 16);
        var south = (Shade: 1f, Block: 0, Sky: 15 * 16);
        var center = (Shade: 1f, Block: 15 * 16, Sky: 15 * 16);
        var corner02 = east; // the pair rule
        var corner03 = (Shade: 1f, Block: 0, Sky: 15 * 16);
        var corner12 = (Shade: 1f, Block: 0, Sky: 15 * 16);
        var corner13 = (Shade: 1f, Block: 0, Sky: 15 * 16);

        var expected = CubeFace(east, west, north, south, corner02, corner03, corner12, corner13, center);
        var slots = SlotOfComputedVertex(Up);
        var actual = Run(scene, Up);

        for (var vertex = 0; vertex < 4; vertex++)
        {
            var slot = slots[vertex];
            Assert.Equal(expected[vertex].Brightness, actual[slot].Brightness, 5);
            Assert.Equal(expected[vertex].Light.Block / 240f, actual[slot].BlockLight, 5);
            Assert.Equal(expected[vertex].Light.Sky / 240f, actual[slot].SkyLight, 5);
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

        // The blend must differ from a naive average of the quarter's four cells, which is what this replaces.
        var naive = (15 + 3 + 12 + 9) / 4;
        Assert.NotEqual(naive / 15f, actual[slots[1]].BlockLight, 3);
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
}
