// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Imaging;
using Sharpcraft.Common.World;
using Sharpcraft.Common.Registries;
using Xunit;

namespace Sharpcraft.Client.Tests;

/// <summary>
/// Checks the sight matrices the mesher records, which is the input the occlusion walk consumes (T-833).
/// </summary>
/// <remarks>
/// The flood fill and the traversal are each covered on their own; what these cover is the step in between — turning a
/// section's real blocks into the blockage map. That step is where a surface section could be misread as solid rock and
/// make the walk cut away terrain the camera can plainly see.
/// </remarks>
public sealed class MeshedSectionVisibilityTests
{
    private const int West = 0;
    private const int Down = 2;
    private const int Up = 3;

    [Fact]
    public void ASurfaceSectionSeesThroughItsOpenSky()
    {
        var world = GroundAndSky(sprites: true);

        var map = SpriteMap();
        var chunk = world.GetChunk(0, 0)!;

        // The real client meshes through this entry with both an atlas and a layout; the world-level Mesh overload has
        // neither, so going through it would only ever produce the "nothing known" answer.
        using var buffer = new MeshBuffer(512);
        ChunkMesher.MeshChunk(world, chunk, airState: 0, buffer, maxBlocks: 2_000_000, sprites: map, layout: Layout());
        var mesh = buffer.ToMeshData(0, chunk.MinSectionY);

        Assert.NotNull(mesh.SectionVisibility);
        var sight = mesh.SectionVisibility![0];

        Assert.NotEqual(SectionVisibility.NoneVisible, sight);
        Assert.True(sight.CanSee(Up, West), $"up cannot see west; bits 0x{sight.Bits:X}");
        Assert.True(sight.CanSee(West, 1), $"west cannot see east; bits 0x{sight.Bits:X}");
        Assert.False(sight.CanSee(Down, Up), $"down should be covered; bits 0x{sight.Bits:X}");
    }

    [Fact]
    public void WithoutAnAtlasTheSectionIsNotTreatedAsRock()
    {
        // The occlusion predicate answers "solid" for everything when it cannot read alpha, so a section of ordinary
        // blocks would look like solid rock and the walk would cull the whole world. The safe answer is "nothing is
        // known", which must come back as all visible.
        var world = GroundAndSky(sprites: false);

        var mesh = ChunkMesher.Mesh(world);

        Assert.NotNull(mesh.SectionVisibility);
        Assert.Equal(SectionVisibility.AllVisible, mesh.SectionVisibility![0]);
    }

    private static ClientWorld GroundAndSky(bool sprites)
    {
        _ = sprites;

        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        var chunk = world.AddChunk(0, 0);
        chunk.SetSection(new ChunkSection(singleState: 0, sectionY: 0));

        // The shape of a real surface section: half ground, half open sky.
        for (var y = 0; y < 8; y++)
        {
            for (var z = 0; z < 16; z++)
            {
                for (var x = 0; x < 16; x++)
                {
                    chunk.SetBlockState(x, y, z, 1);
                }
            }
        }

        return world;
    }

    private static BlockStateLayout Layout() => new(
    [
        (new Identifier("minecraft", "air"), 1),
        (new Identifier("minecraft", "stone"), 1)
    ]);

    private static BlockSpriteMap SpriteMap()
    {
        var atlas = SpriteAtlasBuilder.Stitch(
        [
            (new Identifier("minecraft", "block/stone"), Solid(16, 16, 143, 143, 143), 1),
            (SpriteAtlas.MissingSprite, Solid(16, 16, 255, 0, 255), 1)
        ]);

        var layout = new BlockStateLayout(
        [
            (new Identifier("minecraft", "air"), 1),
            (new Identifier("minecraft", "stone"), 1)
        ]);

        return new BlockSpriteMap(atlas, layout);
    }

    private static RgbaImage Solid(int width, int height, byte r, byte g, byte b)
    {
        var pixels = new byte[width * height * RgbaImage.BytesPerPixel];

        for (var index = 0; index < width * height; index++)
        {
            pixels[(index * RgbaImage.BytesPerPixel) + 0] = r;
            pixels[(index * RgbaImage.BytesPerPixel) + 1] = g;
            pixels[(index * RgbaImage.BytesPerPixel) + 2] = b;
            pixels[(index * RgbaImage.BytesPerPixel) + 3] = 255;
        }

        return new RgbaImage(width, height, pixels);
    }
}
