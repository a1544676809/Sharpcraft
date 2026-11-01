// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Linq;
using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>Visible cloud faces per the reference buildMesh/buildExtrudedCell rules (docs/modules/clouds.md 2).</summary>
public sealed class CloudMeshBuilderTests
{
    [Fact]
    public void EmptyTextureBuildsNothing()
    {
        var texture = CloudTexture_Empty(3, 3);
        var quads = CloudMeshBuilder.Build(texture, 3, 3, 1, 1, 1, CloudCameraSide.InsideClouds);
        Assert.Empty(quads);
    }

    [Fact]
    public void FastModeBuildsOnlyTheDownFace()
    {
        var texture = CloudTexture_OneCloud(5, 5, 2, 2);
        var quads = CloudMeshBuilder.BuildFlat(texture, 5, 5, 2, 2, 1);

        var only = Assert.Single(quads);
        Assert.Equal(CloudFaceDirection.Down, only.Direction);
        Assert.False(only.InsideFace);
    }

    [Fact]
    public void BelowCloudsEmitsBottomPlusInterior()
    {
        // One cloud at texture cell (1,1); camera cell is that cell, radius 1.
        var texture = CloudTexture_OneCloud(5, 5, 2, 2);
        var quads = CloudMeshBuilder.Build(texture, 5, 5, 2, 2, 1, CloudCameraSide.BelowClouds);

        // Bottom face (no top from below) + six interior faces.
        Assert.Equal(7, quads.Count);
        Assert.Contains(quads, q => q.Direction == CloudFaceDirection.Down && !q.InsideFace);
        Assert.DoesNotContain(quads, q => q.Direction == CloudFaceDirection.Up && !q.InsideFace);
        Assert.Equal(6, quads.Count(q => q.InsideFace));
    }

    [Fact]
    public void InsideCloudsEmitsBothBottomAndTop()
    {
        var texture = CloudTexture_OneCloud(5, 5, 2, 2);
        var quads = CloudMeshBuilder.Build(texture, 5, 5, 2, 2, 1, CloudCameraSide.InsideClouds);

        Assert.Equal(8, quads.Count);
        Assert.Contains(quads, q => q.Direction == CloudFaceDirection.Up && !q.InsideFace);
        Assert.Contains(quads, q => q.Direction == CloudFaceDirection.Down && !q.InsideFace);
    }

    [Fact]
    public void SideFacesOnlyForEmptyNeighboursInMatchingDirections()
    {
        // Cloud at (2,2), camera cell (1,1) so the cloud is the +X/+Z relative cell with empty neighbours.
        var texture = CloudTexture_OneCloud(5, 5, 2, 2);
        var quads = CloudMeshBuilder.Build(texture, 5, 5, 1, 1, 2, CloudCameraSide.BelowClouds);

        var cell = quads.Where(q => q.CellX == 1 && q.CellZ == 1 && !q.InsideFace).ToList();
        Assert.Contains(cell, q => q.Direction == CloudFaceDirection.Down);
        Assert.Contains(cell, q => q.Direction == CloudFaceDirection.North);
        Assert.Contains(cell, q => q.Direction == CloudFaceDirection.West);
        Assert.DoesNotContain(cell, q => q.Direction == CloudFaceDirection.South);
        Assert.DoesNotContain(cell, q => q.Direction == CloudFaceDirection.East);
    }

    private static long[] CloudTexture_Empty(int width, int height) =>
        new long[width * height];

    private static long[] CloudTexture_OneCloud(int width, int height, int cloudX, int cloudZ)
    {
        var pixels = new int[width * height];
        pixels[cloudX + (cloudZ * width)] = unchecked((int)0xFFFFFFFF);
        return Sharpcraft.Client.Assets.CloudTexture.FromPixels(width, height, pixels).Cells;
    }
}
