// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// T-764 S2: the special model bake must place reference pixel geometry in item model space.
/// </summary>
/// <remarks>
/// The reference divides every <c>ModelPart</c> vertex by 16 (<c>ModelPart.Vertex.worldX</c>) and renders special models
/// inside the item pipeline without touching the pose, so a chest box spanning pixels 1..15 must land at 1/16..15/16 in
/// the item's unit cube.
/// </remarks>
public sealed class SpecialModelBakeTests
{
    [Fact]
    public void TheChestBakesThreeBoxes()
    {
        // Three boxes, six faces each, plus the two faces whose U runs past the 64-wide sheet (the SOUTH face of
        // bottom and of lid), which the bake splits at the seam to reproduce GL REPEAT.
        var quads = new List<SpecialQuad>();
        SpecialModelBake.Bake(SpecialModelDefinitions.Chest(), SpecialModelDefinitions.ChestTextureWidth, SpecialModelDefinitions.ChestTextureHeight, quads.Add);
        Assert.Equal(20, quads.Count);
    }

    [Fact]
    public void TheChestStaysInsideTheItemCube()
    {
        var quads = new List<SpecialQuad>();
        SpecialModelBake.Bake(SpecialModelDefinitions.Chest(), SpecialModelDefinitions.ChestTextureWidth, SpecialModelDefinitions.ChestTextureHeight, quads.Add);

        Assert.Equal(20, quads.Count);
        foreach (var quad in quads)
        {
            foreach (var corner in new[] { quad.P0, quad.P1, quad.P2, quad.P3 })
            {
                // Chest pixels reach 15/16 horizontally and 14/16 vertically (lid closed), all inside the unit cube.
                Assert.InRange(corner.X, -0.001f, 1.001f);
                Assert.InRange(corner.Y, -0.001f, 1.001f);
                Assert.InRange(corner.Z, -0.001f, 1.001f);
            }
        }
    }

    [Fact]
    public void TheBottomBoxSpansTheReferencePixels()
    {
        var quads = new List<SpecialQuad>();
        SpecialModelBake.Bake(SpecialModelDefinitions.Chest(), SpecialModelDefinitions.ChestTextureWidth, SpecialModelDefinitions.ChestTextureHeight, quads.Add);

        // The bottom box is pixels (1,0,1)..(15,10,15): its corners must be those divided by 16.
        var bottomCorners = quads
            .Take(6)
            .SelectMany(quad => new[] { quad.P0, quad.P1, quad.P2, quad.P3 })
            .ToList();

        Assert.Equal(1f / 16f, bottomCorners.Min(corner => corner.X), 4);
        Assert.Equal(15f / 16f, bottomCorners.Max(corner => corner.X), 4);
        Assert.Equal(0f, bottomCorners.Min(corner => corner.Y), 4);
        Assert.Equal(10f / 16f, bottomCorners.Max(corner => corner.Y), 4);
        Assert.Equal(1f / 16f, bottomCorners.Min(corner => corner.Z), 4);
        Assert.Equal(15f / 16f, bottomCorners.Max(corner => corner.Z), 4);
    }

    [Fact]
    public void TextureCoordinatesAreNormalizedToTheModelTexture()
    {
        var quads = new List<SpecialQuad>();
        SpecialModelBake.Bake(SpecialModelDefinitions.Chest(), SpecialModelDefinitions.ChestTextureWidth, SpecialModelDefinitions.ChestTextureHeight, quads.Add);

        // The reference chest model authors boxes that run past the declared 64x64 texture (the bottom box reaches
        // 70/64 on U) and relies on REPEAT sampling; the bake wraps those coordinates so every emitted UV stays inside
        // the sprite rectangle.
        foreach (var quad in quads)
        {
            foreach (var uv in new[] { quad.Uv0, quad.Uv1, quad.Uv2, quad.Uv3 })
            {
                Assert.InRange(uv.X, 0f, 1.2f);
                Assert.InRange(uv.Y, 0f, 1f);
            }
        }
    }

    [Fact]
    public void TheChestBakesIntoItemModelQuadsForOneTexture()
    {
        var quads = new List<ItemModelQuad>();
        SpecialModelBake.AppendQuads(
            quads,
            SpecialModelDefinitions.Chest(),
            SpecialModelDefinitions.ChestTextureWidth,
            SpecialModelDefinitions.ChestTextureHeight,
            SpecialModelDefinitions.ChestTexture);

        // Each of the twenty faces (eighteen plus two split halves) is emitted twice for the two windings.
        Assert.Equal(40, quads.Count);
        Assert.All(quads, quad => Assert.Equal(SpecialModelDefinitions.ChestTexture, quad.Texture));
        Assert.All(quads, quad => Assert.Equal(0, quad.TintIndex));

        // The mapped quads must carry the same corners and UVs the bake produced.
        var baked = new List<SpecialQuad>();
        SpecialModelBake.Bake(SpecialModelDefinitions.Chest(), 64, 64, baked.Add);
        // ItemModelQuad expects its first corner at (minU,minV), so the reference ring is rotated by one.
        Assert.Equal(baked[0].P1, quads[0].Corner0);
        Assert.Equal(baked[0].Uv1, quads[0].Uv0);
        Assert.Equal(baked[0].P0, quads[1].Corner0);
    }
    [Fact]
    public void TheLidRotatesAroundItsPivot()
    {
        var closed = new List<SpecialQuad>();
        var open = new List<SpecialQuad>();
        SpecialModelBake.Bake(SpecialModelDefinitions.Chest(), 64, 64, closed.Add);
        SpecialModelBake.Bake(SpecialModelDefinitions.Chest(lidRotationDegrees: -90f), 64, 64, open.Add);

        // The bottom box (first six quads) is not driven by the lid pose, so it must not move.
        Assert.Equal(closed[0], open[0]);

        // The lid is the next six quads; a -90 degree rotation lifts its far edge, changing the height.
        var closedTop = closed.Skip(6).Take(6).SelectMany(q => new[] { q.P1.Y, q.P2.Y }).Max();
        var openTop = open.Skip(6).Take(6).SelectMany(q => new[] { q.P1.Y, q.P2.Y }).Max();
        Assert.True(openTop > closedTop, $"the lid should rise when opened (closed {closedTop:F4}, open {openTop:F4})");
    }
}
