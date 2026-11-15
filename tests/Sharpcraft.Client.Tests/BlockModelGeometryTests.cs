// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Assets;

namespace Sharpcraft.Client.Tests;

/// <summary>
/// Pins the quad builder: a full cube must reproduce the plain cube the mesher already draws, every
/// face must wind towards its own direction, and a face without an explicit texture rectangle must
/// derive it from the box the way the reference does.
/// </summary>
public sealed class BlockModelGeometryTests
{
    private const float Tolerance = 1e-5f;

    private static ModelElement Box(float fromX, float fromY, float fromZ, float toX, float toY, float toZ, params string[] directions)
    {
        var faces = new Dictionary<string, ModelFace>(StringComparer.Ordinal);
        foreach (var direction in directions)
        {
            faces[direction] = new ModelFace("#all", null, null, -1);
        }

        return new ModelElement(new ModelBox(fromX, fromY, fromZ, toX, toY, toZ), faces, null);
    }

    private static ModelQuad Face(ModelElement element, string direction) =>
        BlockModelGeometry.Build([element]).Single(quad => quad.Direction == direction);

    /// <summary>A full cube has six faces and unit corners.</summary>
    [Fact]
    public void AFullCubeHasSixUnitFaces()
    {
        var quads = BlockModelGeometry.Build([Box(0, 0, 0, 16, 16, 16, "down", "up", "north", "south", "west", "east")]);

        Assert.Equal(6, quads.Count);
        foreach (var quad in quads)
        {
            foreach (var vertex in new[] { quad.V0, quad.V1, quad.V2, quad.V3 })
            {
                Assert.InRange(vertex.Position.X, 0f, 1f);
                Assert.InRange(vertex.Position.Y, 0f, 1f);
                Assert.InRange(vertex.Position.Z, 0f, 1f);
                Assert.InRange(vertex.Uv.X, 0f, 1f);
                Assert.InRange(vertex.Uv.Y, 0f, 1f);
            }
        }
    }

    /// <summary>Every face winds towards the direction it belongs to.</summary>
    [Theory]
    [InlineData("down", 0f, -1f, 0f)]
    [InlineData("up", 0f, 1f, 0f)]
    [InlineData("north", 0f, 0f, -1f)]
    [InlineData("south", 0f, 0f, 1f)]
    [InlineData("west", -1f, 0f, 0f)]
    [InlineData("east", 1f, 0f, 0f)]
    public void EachFaceWindsOutwards(string direction, float nx, float ny, float nz)
    {
        var quad = Face(Box(0, 0, 0, 16, 16, 16, direction), direction);

        var edge1 = quad.V1.Position - quad.V0.Position;
        var edge2 = quad.V2.Position - quad.V0.Position;
        var normal = Vector3.Normalize(Vector3.Cross(edge1, edge2));

        Assert.Equal(nx, normal.X, 1e-4f);
        Assert.Equal(ny, normal.Y, 1e-4f);
        Assert.Equal(nz, normal.Z, 1e-4f);
    }

    /// <summary>The up face keeps the reference's corner order and full texture rectangle.</summary>
    [Fact]
    public void TheUpFaceKeepsTheReferenceCorners()
    {
        var quad = Face(Box(0, 0, 0, 16, 16, 16, "up"), "up");

        Assert.Equal(new Vector3(0f, 1f, 0f), quad.V0.Position);
        Assert.Equal(new Vector3(0f, 1f, 1f), quad.V1.Position);
        Assert.Equal(new Vector3(1f, 1f, 1f), quad.V2.Position);
        Assert.Equal(new Vector3(1f, 1f, 0f), quad.V3.Position);

        Assert.Equal(new Vector2(0f, 0f), quad.V0.Uv);
        Assert.Equal(new Vector2(0f, 1f), quad.V1.Uv);
        Assert.Equal(new Vector2(1f, 1f), quad.V2.Uv);
        Assert.Equal(new Vector2(1f, 0f), quad.V3.Uv);
    }

    /// <summary>An omitted texture rectangle is derived from the box extent on the face plane.</summary>
    [Theory]
    // The horizontal faces use the box footprint directly; the vertical ones flip the height, so a box
    // that stops short of the top runs to the bottom of the sprite.
    [InlineData("down", new[] { 4f, 4f, 12f, 12f })]
    [InlineData("up", new[] { 4f, 4f, 12f, 12f })]
    [InlineData("north", new[] { 4f, 4f, 12f, 16f })]
    [InlineData("south", new[] { 4f, 4f, 12f, 16f })]
    [InlineData("west", new[] { 4f, 4f, 12f, 16f })]
    [InlineData("east", new[] { 4f, 4f, 12f, 16f })]
    public void TheDefaultRectangleFollowsTheBox(string direction, float[] expected)
    {
        var uv = BlockModelGeometry.DefaultUv(direction, new ModelBox(4f, 0f, 4f, 12f, 12f, 12f));

        Assert.Equal(expected, uv);
    }

    /// <summary>A box that does not fill the block still projects onto the right texture rectangle.</summary>
    [Fact]
    public void AnInsetBoxProjectsItsRectangle()
    {
        // A box sitting high and shallow: its top face uses the box's own footprint.
        var uv = BlockModelGeometry.DefaultUv("up", new ModelBox(2f, 14f, 3f, 10f, 16f, 9f));

        Assert.Equal([2f, 3f, 10f, 9f], uv);
    }

    /// <summary>An explicit rectangle wins over the derived one.</summary>
    [Fact]
    public void AnExplicitRectangleWins()
    {
        var element = new ModelElement(
            new ModelBox(0, 0, 0, 16, 16, 16),
            new Dictionary<string, ModelFace>(StringComparer.Ordinal)
            {
                ["up"] = new ModelFace("#all", [0f, 8f, 8f, 16f], null, 2)
            },
            null);

        var quad = BlockModelGeometry.Build([element]).Single();

        Assert.Equal(new Vector2(0f, 0.5f), quad.V0.Uv);
        Assert.Equal(new Vector2(0.5f, 1f), quad.V2.Uv);
        Assert.Equal(2, quad.TintIndex);
    }

    /// <summary>Rotation turns the corners about the element origin.</summary>
    [Fact]
    public void RotationTurnsTheCorners()
    {
        var rotated = new ModelElement(
            new ModelBox(0, 0, 0, 16, 16, 16),
            new Dictionary<string, ModelFace>(StringComparer.Ordinal)
            {
                ["up"] = new ModelFace("#all", null, null, -1)
            },
            new ModelRotation(90f, "y", 8f, 8f, 8f, false));

        var quad = BlockModelGeometry.Build([rotated]).Single();

        // The corner that started at the origin turns a quarter around the middle of the block.
        Assert.Equal(0f, quad.V0.Position.X, 1e-4f);
        Assert.Equal(1f, quad.V0.Position.Z, 1e-4f);
        Assert.Equal(1f, quad.V0.Position.Y, 1e-4f);
    }

    /// <summary>An unknown face name is skipped rather than drawn.</summary>
    [Fact]
    public void AnUnknownFaceIsSkipped()
    {
        var element = Box(0, 0, 0, 16, 16, 16, "up");
        var withUnknown = new ModelElement(
            element.Box,
            new Dictionary<string, ModelFace>(StringComparer.Ordinal)
            {
                ["up"] = new ModelFace("#all", null, null, -1),
                ["sideways"] = new ModelFace("#all", null, null, -1)
            },
            null);

        Assert.Single(BlockModelGeometry.Build([withUnknown]));
    }
}
