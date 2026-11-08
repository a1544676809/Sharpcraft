// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Element faces become quads with the reference <c>FaceInfo</c> corner order and the reference UV index mapping, so the
/// expectations below are the reference tables written out for a full unit cube.
/// </summary>
public sealed class ItemModelGeometryTests
{
    private static ItemModelElement FullCube()
    {
        var faces = new Dictionary<ModelFaceDirection, ItemModelFace>();
        foreach (var direction in Enum.GetValues<ModelFaceDirection>())
        {
            // A full cube with auto UV is 0..16 on every face; give each direction a distinct range so the mapping is
            // visible in the assertions.
            faces[direction] = new ItemModelFace(
                "block/test",
                new Vector4(0f, 0f, 16f, 16f),
                TintIndex: 0,
                CullFace: direction);
        }

        return new ItemModelElement(new Vector3(0f, 0f, 0f), new Vector3(16f, 16f, 16f), faces);
    }

    [Fact]
    public void AFullCubeProducesOneQuadPerFace()
    {
        var quads = ItemModelGeometry.Build([FullCube()]);
        Assert.Equal(6, quads.Count);
        Assert.All(quads, quad => Assert.Equal("block/test", quad.Texture));
        Assert.All(quads, quad => Assert.Equal(0, quad.TintIndex));
    }

    [Fact]
    public void DownFaceCornersFollowTheReferenceOrder()
    {
        var quads = ItemModelGeometry.Build([FullCube()]);
        var down = Assert.Single(quads, quad => quad.Direction == ModelFaceDirection.Down);

        // FaceInfo.DOWN: (minX,minY,maxZ), (minX,minY,minZ), (maxX,minY,minZ), (maxX,minY,maxZ)
        Assert.Equal(new Vector3(0f, 0f, 1f), down.Corner0);
        Assert.Equal(new Vector3(0f, 0f, 0f), down.Corner1);
        Assert.Equal(new Vector3(1f, 0f, 0f), down.Corner2);
        Assert.Equal(new Vector3(1f, 0f, 1f), down.Corner3);
    }

    [Fact]
    public void UpAndNorthFacesFollowTheReferenceOrder()
    {
        var quads = ItemModelGeometry.Build([FullCube()]);

        // FaceInfo.UP: (minX,maxY,minZ), (minX,maxY,maxZ), (maxX,maxY,maxZ), (maxX,maxY,minZ)
        var up = Assert.Single(quads, quad => quad.Direction == ModelFaceDirection.Up);
        Assert.Equal(new Vector3(0f, 1f, 0f), up.Corner0);
        Assert.Equal(new Vector3(0f, 1f, 1f), up.Corner1);
        Assert.Equal(new Vector3(1f, 1f, 1f), up.Corner2);
        Assert.Equal(new Vector3(1f, 1f, 0f), up.Corner3);

        // FaceInfo.NORTH starts at (maxX,maxY,minZ).
        var north = Assert.Single(quads, quad => quad.Direction == ModelFaceDirection.North);
        Assert.Equal(new Vector3(1f, 1f, 0f), north.Corner0);
        Assert.Equal(new Vector3(1f, 0f, 0f), north.Corner1);
        Assert.Equal(new Vector3(0f, 0f, 0f), north.Corner2);
        Assert.Equal(new Vector3(0f, 1f, 0f), north.Corner3);
    }

    [Fact]
    public void UvIndexMappingMatchesTheReferenceTables()
    {
        // UVs.getVertexU: 0/1 take minU, 2/3 take maxU. UVs.getVertexV: 0/3 take minV, 1/2 take maxV.
        var faces = new Dictionary<ModelFaceDirection, ItemModelFace>
        {
            [ModelFaceDirection.South] = new("block/test", new Vector4(2f, 4f, 10f, 12f))
        };
        var element = new ItemModelElement(new Vector3(0f, 0f, 0f), new Vector3(16f, 16f, 16f), faces);

        var quad = Assert.Single(ItemModelGeometry.Build([element]));
        Assert.Equal(new Vector2(2f / 16f, 4f / 16f), quad.Uv0);
        Assert.Equal(new Vector2(2f / 16f, 12f / 16f), quad.Uv1);
        Assert.Equal(new Vector2(10f / 16f, 12f / 16f), quad.Uv2);
        Assert.Equal(new Vector2(10f / 16f, 4f / 16f), quad.Uv3);
    }

    [Fact]
    public void UnlistedFacesAreNotEmitted()
    {
        var faces = new Dictionary<ModelFaceDirection, ItemModelFace>
        {
            [ModelFaceDirection.Up] = new("block/test", new Vector4(0f, 0f, 16f, 16f))
        };
        var element = new ItemModelElement(new Vector3(4f, 0f, 4f), new Vector3(12f, 8f, 12f), faces);

        var quad = Assert.Single(ItemModelGeometry.Build([element]));
        Assert.Equal(ModelFaceDirection.Up, quad.Direction);
        // Only four of sixteen pixels: the element's own bounds drive the corners.
        Assert.Equal(new Vector3(0.25f, 0.5f, 0.25f), quad.Corner0);
        Assert.Equal(new Vector3(0.75f, 0.5f, 0.75f), quad.Corner2);
    }
}
