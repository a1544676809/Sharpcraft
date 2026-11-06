// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.World;
using Xunit;

namespace Sharpcraft.Common.Tests.World;

public class OcclusionShapeTests
{
    // Vector 1: an empty occluder never hides a face.
    [Fact]
    public void EmptyOccluder_DoesNotOcclude()
    {
        Assert.False(OcclusionShape.IsFaceOccludedByState(BlockFace.North, 0.8888889f, OcclusionShape.Empty));
    }

    // Vector 2: a full block occludes side and bottom faces.
    [Fact]
    public void FullBlock_OccludesSideAndDown()
    {
        Assert.True(OcclusionShape.IsFaceOccludedByState(BlockFace.North, 0.8888889f, OcclusionShape.FullBlock));
        Assert.True(OcclusionShape.IsFaceOccludedByState(BlockFace.Down, 0.8888889f, OcclusionShape.FullBlock));
    }

    // Vector 3: a full block occludes the top face only for a full-height fluid.
    [Fact]
    public void FullBlock_OccludesUpOnlyForFullHeightFluid()
    {
        Assert.False(OcclusionShape.IsFaceOccludedByState(BlockFace.Up, 0.8888889f, OcclusionShape.FullBlock));
        Assert.True(OcclusionShape.IsFaceOccludedByState(BlockFace.Up, 1.0f, OcclusionShape.FullBlock));
    }

    // Vector 4: bottom slab side-face occlusion depends on fluid height.
    [Fact]
    public void BottomSlab_FluidSideOcclusion()
    {
        Assert.True(OcclusionShape.IsFaceOccludedByState(BlockFace.North, 0.4f, OcclusionShape.BottomSlab));
        Assert.False(OcclusionShape.IsFaceOccludedByState(BlockFace.North, 0.8888889f, OcclusionShape.BottomSlab));
    }

    // Vector 4: bottom slab does not touch the upper block bottom, but covers a full lower fluid top.
    [Fact]
    public void BottomSlab_DownAndUp()
    {
        Assert.False(OcclusionShape.IsFaceOccludedByState(BlockFace.Down, 0.8888889f, OcclusionShape.BottomSlab));
        Assert.True(OcclusionShape.IsFaceOccludedByState(BlockFace.Up, 1.0f, OcclusionShape.BottomSlab));
    }

    // Vector 5: top slab touches the upper block bottom but leaves the lower side visible.
    [Fact]
    public void TopSlab_DownAndNorth()
    {
        Assert.True(OcclusionShape.IsFaceOccludedByState(BlockFace.Down, 0.8888889f, OcclusionShape.TopSlab));
        Assert.False(OcclusionShape.IsFaceOccludedByState(BlockFace.North, 0.8888889f, OcclusionShape.TopSlab));
    }

    [Fact]
    public void Opposite_ReturnsTheFacingFace()
    {
        Assert.Equal(BlockFace.Up, BlockFace.Down.Opposite());
        Assert.Equal(BlockFace.Down, BlockFace.Up.Opposite());
        Assert.Equal(BlockFace.South, BlockFace.North.Opposite());
        Assert.Equal(BlockFace.North, BlockFace.South.Opposite());
        Assert.Equal(BlockFace.East, BlockFace.West.Opposite());
        Assert.Equal(BlockFace.West, BlockFace.East.Opposite());
    }

    [Fact]
    public void ShapeProperties_ExposeEmptyAndFullBlock()
    {
        Assert.True(OcclusionShape.Empty.IsEmpty);
        Assert.False(OcclusionShape.Empty.IsFullBlock);
        Assert.True(OcclusionShape.FullBlock.IsFullBlock);
        Assert.False(OcclusionShape.BottomSlab.IsFullBlock);
        Assert.True(OcclusionShape.Box(0, 0, 0, 1, 0, 1).IsEmpty);
    }
}
