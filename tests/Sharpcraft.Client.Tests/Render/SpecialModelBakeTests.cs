// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Golden-vector and invariant tests for <see cref="SpecialModelBake"/> and <see cref="SpecialModelDefinitions"/>
/// against Minecraft 26.3 <c>ModelPart.Cube</c> and <c>ChestModel.createSingleBodyLayer</c>.
/// </summary>
public sealed class SpecialModelBakeTests
{
    [Fact]
    public void TheChestBakesExactlyEighteenQuadsForThreeBoxes()
    {
        var quads = new List<SpecialQuad>();
        var chest = SpecialModelDefinitions.Chest();

        SpecialModelBake.Bake(
            chest,
            SpecialModelDefinitions.ChestTextureWidth,
            SpecialModelDefinitions.ChestTextureHeight,
            quads.Add);

        // 3 boxes (bottom, lid, lock) * 6 faces = 18 quads. No face ever crosses u = 64.
        Assert.Equal(18, quads.Count);
        Assert.Equal(18, SpecialModelBake.QuadCount(chest));
    }

    [Fact]
    public void ReferenceCubeUvStripMatchesModelPartCubeFormula()
    {
        // Reference ModelPart.Cube for chest bottom: texOffs(0, 19), size(14, 10, 14) on 64x64 sheet.
        // f4(u0)=0, f5(u1)=14, f6(u2)=28, f7(u22)=42, f8(u3)=42 (NOT 56!), f9(u4)=56 (NOT 70!).
        var strip = ReferenceCubeUvStrip.Compute(0, 19, 14f, 10f, 14f);

        Assert.Equal(0f, strip.U0);
        Assert.Equal(14f, strip.U1);
        Assert.Equal(28f, strip.U2);
        Assert.Equal(42f, strip.U22);
        Assert.Equal(42f, strip.U3);
        Assert.Equal(56f, strip.U4);
        Assert.Equal(19f, strip.V0);
        Assert.Equal(33f, strip.V1);
        Assert.Equal(43f, strip.V2);
    }

    [Fact]
    public void ChestBottomAndLidFacesStayStrictlyInside56PixelStripAndNeverSampleTransparentRightMargin()
    {
        var quads = new List<SpecialQuad>();
        SpecialModelBake.Bake(
            SpecialModelDefinitions.Chest(),
            SpecialModelDefinitions.ChestTextureWidth,
            SpecialModelDefinitions.ChestTextureHeight,
            quads.Add);

        foreach (var quad in quads)
        {
            foreach (var uv in new[] { quad.Uv0, quad.Uv1, quad.Uv2, quad.Uv3 })
            {
                var pixelU = uv.X * SpecialModelDefinitions.ChestTextureWidth;
                var pixelV = uv.Y * SpecialModelDefinitions.ChestTextureHeight;

                // In normal.png (64x64), columns 56..64 are 100% transparent padding.
                // Every chest quad must have 0 <= u <= 56 and 0 <= v <= 43.
                Assert.InRange(pixelU, 0f, 56f);
                Assert.InRange(pixelV, 0f, 43f);
            }
        }
    }

    [Fact]
    public void ChestBottomSixFacesMatchVanillaPixelUvIntervals()
    {
        var quads = new List<SpecialQuad>();
        SpecialModelBake.Bake(
            SpecialModelDefinitions.Chest(),
            SpecialModelDefinitions.ChestTextureWidth,
            SpecialModelDefinitions.ChestTextureHeight,
            quads.Add);

        // First 6 quads are 'bottom' (texOffs(0,19), size(14,10,14)):
        // 0: DOWN  u=[14..28], v=[19..33]
        // 1: UP    u=[28..42], v=[19..33]
        // 2: WEST  u=[0..14],  v=[33..43]
        // 3: NORTH u=[14..28], v=[33..43]
        // 4: EAST  u=[28..42], v=[33..43]
        // 5: SOUTH u=[42..56], v=[33..43] (front face with the lock)
        AssertUvBounds(quads[0], 14f, 28f, 19f, 33f);
        AssertUvBounds(quads[1], 28f, 42f, 19f, 33f);
        AssertUvBounds(quads[2], 0f, 14f, 33f, 43f);
        AssertUvBounds(quads[3], 14f, 28f, 33f, 43f);
        AssertUvBounds(quads[4], 28f, 42f, 33f, 43f);
        AssertUvBounds(quads[5], 42f, 56f, 33f, 43f);
    }

    [Fact]
    public void AppendQuadsEmitsSingleWindingOutwardQuadsWithoutDuplicateBackfaces()
    {
        var output = new List<ItemModelQuad>();
        SpecialModelBake.AppendQuads(
            output,
            SpecialModelDefinitions.Chest(),
            SpecialModelDefinitions.ChestTextureWidth,
            SpecialModelDefinitions.ChestTextureHeight,
            SpecialModelDefinitions.ChestTexture);

        Assert.Equal(18, output.Count);
    }

    private static void AssertUvBounds(SpecialQuad quad, float expectedMinU, float expectedMaxU, float expectedMinV, float expectedMaxV)
    {
        var uvs = new[] { quad.Uv0, quad.Uv1, quad.Uv2, quad.Uv3 };
        var minU = uvs.Min(uv => uv.X) * SpecialModelDefinitions.ChestTextureWidth;
        var maxU = uvs.Max(uv => uv.X) * SpecialModelDefinitions.ChestTextureWidth;
        var minV = uvs.Min(uv => uv.Y) * SpecialModelDefinitions.ChestTextureHeight;
        var maxV = uvs.Max(uv => uv.Y) * SpecialModelDefinitions.ChestTextureHeight;

        Assert.Equal(expectedMinU, minU, 3);
        Assert.Equal(expectedMaxU, maxU, 3);
        Assert.Equal(expectedMinV, minV, 3);
        Assert.Equal(expectedMaxV, maxV, 3);
    }
}
