// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.World;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// task-218: per-cube VisibleSides face mask on the raw emission path. A masked cube must emit exactly the
/// selected source faces -- same vertices/UVs as the corresponding faces of the unmasked cube -- in source
/// polygon order (DOWN, UP, WEST, NORTH, EAST, SOUTH). Legacy frames ignore the mask (stays All).
/// </summary>
public sealed class VisibleSidesTests
{
    private static readonly (EntityModelVisibleSides Mask, int Polygons)[] Cases =
    [
        (EntityModelVisibleSides.All, 6),
        (EntityModelVisibleSides.North, 1),
        (EntityModelVisibleSides.Down | EntityModelVisibleSides.Up, 2),
        (EntityModelVisibleSides.West | EntityModelVisibleSides.North | EntityModelVisibleSides.East, 3),
        (EntityModelVisibleSides.North | EntityModelVisibleSides.South, 2)
    ];

    // Source polygon order after ref ModelPart.Cube: DOWN=0, UP=1, WEST=2, NORTH=3, EAST=4, SOUTH=5.
    private static int PolygonIndex(EntityModelVisibleSides side) => side switch
    {
        EntityModelVisibleSides.Down => 0,
        EntityModelVisibleSides.Up => 1,
        EntityModelVisibleSides.West => 2,
        EntityModelVisibleSides.North => 3,
        EntityModelVisibleSides.East => 4,
        _ => 5
    };

    [Theory]
    [MemberData(nameof(MaskCases))]
    public void MaskedFacesMatchTheUnmaskedCubeKeyed(EntityModelVisibleSides mask, int polygons)
    {
        var full = Geometry(EntityModelVisibleSides.All);
        var masked = Geometry(mask);
        var stride = ChunkMesher.FloatsPerVertex;

        Assert.Equal(6 * 4, full.VertexFloats / stride);
        Assert.Equal(6 * 6, full.IndexCount);
        Assert.Equal(polygons * 4, masked.VertexFloats / stride);
        Assert.Equal(polygons * 6, masked.IndexCount);

        // Selected faces, in source order, must be byte-identical (position + UV) to the unmasked cube's
        // corresponding faces. This pins both which faces are emitted and their order/UV, not just counts.
        var outVertex = 0;
        foreach (var side in new[]
        {
            EntityModelVisibleSides.Down, EntityModelVisibleSides.Up, EntityModelVisibleSides.West,
            EntityModelVisibleSides.North, EntityModelVisibleSides.East, EntityModelVisibleSides.South
        })
        {
            if ((mask & side) == 0) continue;
            var sourceVertex = PolygonIndex(side) * 4;
            for (var v = 0; v < 4; v++)
            {
                var s = (sourceVertex + v) * stride;
                var o = (outVertex + v) * stride;
                for (var f = 0; f < stride; f++)
                    Assert.Equal(full.Vertices[s + f], masked.Vertices[o + f]);
            }
            outVertex += 4;
        }
    }

    [Fact]
    public void MirroredCubeSubsetMatchesUnmaskedMirroredFace()
    {
        // A mirrored cube reflects X and reverses polygon vertices (R-027/#156). A face subset must keep
        // that exact source face geometry/UV, not re-derive it.
        var full = Geometry(EntityModelVisibleSides.All, mirror: true);
        var north = Geometry(EntityModelVisibleSides.North, mirror: true);
        var stride = ChunkMesher.FloatsPerVertex;
        Assert.Equal(24, full.VertexFloats / stride);
        Assert.Equal(4, north.VertexFloats / stride);
        var sourceVertex = PolygonIndex(EntityModelVisibleSides.North) * 4;
        for (var v = 0; v < 4; v++)
            for (var f = 0; f < stride; f++)
                Assert.Equal(full.Vertices[(sourceVertex + v) * stride + f], north.Vertices[v * stride + f]);
    }

    [Theory]
    [InlineData(EntityModelVisibleSides.All, 6)]
    [InlineData(EntityModelVisibleSides.North, 1)]
    [InlineData(EntityModelVisibleSides.Down | EntityModelVisibleSides.Up, 2)]
    public void CompiledTreePathHonoursVisibleSides(EntityModelVisibleSides mask, int polygons)
    {
        // Production path: FromTree always builds a Compiled tree; BuildModel goes through compiled Emit,
        // which must forward cube.VisibleSides onto the EntityModelPart passed to AddPart. The DSL sets the
        // mask explicitly (no auto-inference), so this covers the path DecoratedPot/Chest would use.
        var definition = CompiledDefinition(mask, mirror: false);
        var geometry = new EntityModelMesher().BuildModel(definition, State(), Sprite(), 64, 64, modelScale: 1f);
        var stride = ChunkMesher.FloatsPerVertex;
        Assert.True(definition.Compiled is not null);
        Assert.Equal(polygons * 4, geometry.VertexFloats / stride);
        Assert.Equal(polygons * 6, geometry.IndexCount);

        // Compare against the flat-path geometry: same mask must produce byte-identical keyed vertices/UV.
        var flat = Geometry(mask);
        for (var i = 0; i < geometry.VertexFloats; i++)
            Assert.Equal(flat.Vertices[i], geometry.Vertices[i]);
    }

    [Fact]
    public void CompiledMirrorNorthMatchesFlatMirrorNorth()
    {
        var compiled = new EntityModelMesher().BuildModel(
            CompiledDefinition(EntityModelVisibleSides.North, mirror: true), State(), Sprite(), 64, 64, modelScale: 1f);
        var flat = Geometry(EntityModelVisibleSides.North, mirror: true);
        Assert.Equal(flat.VertexFloats, compiled.VertexFloats);
        for (var i = 0; i < flat.VertexFloats; i++)
            Assert.Equal(flat.Vertices[i], compiled.Vertices[i]);
    }

    [Fact]
    public void DefaultVisibleSidesIsAll()
    {
        Assert.Equal(0x3F, (byte)EntityModelVisibleSides.All);
        var part = new EntityModelMesher().BuildModel(
            Definition(EntityModelVisibleSides.All), State(), Sprite(), 64, 64, modelScale: 1f);
        Assert.Equal(24, part.VertexFloats / ChunkMesher.FloatsPerVertex);
    }

    public static IEnumerable<object[]> MaskCases() =>
        Cases.Select(c => new object[] { c.Mask, c.Polygons });

    private static DynamicGeometry Geometry(EntityModelVisibleSides mask, bool mirror = false)
    {
        var definition = Definition(mask, mirror);
        return new EntityModelMesher().BuildModel(definition, State(), Sprite(), 64, 64, modelScale: 1f);
    }

    private static EntityModelDefinition Definition(EntityModelVisibleSides mask, bool mirror = false)
    {
        // Build a raw definition from a simple DSL cube, then stamp the mask onto the baked flat part.
        // No DSL auto-inference: the API is applied explicitly at the baked part level.
        var mesh = new VanillaMeshDefinition(new VanillaPartDefinition("root", VanillaPartPose.Zero,
            children:
            [
                new VanillaPartDefinition("cube", VanillaPartPose.Zero,
                    cubes:
                    [
                        new VanillaCubeDefinition("cube", 0, 0, -4f, -8f, -4f, 8f, 8f, 8f, Mirror: mirror)
                    ])
            ]));
        var baked = EntityModelDefinition.FromTree("mask_cube", 64, 64, mesh,
            faceTransforms: null, "entity/test", 1f, baselinePx: 0f, rawSourceFrame: true);
        var part = baked.Parts.Single(p => p.Name == "cube") with { VisibleSides = mask };
        return new EntityModelDefinition("mask_cube", 64, 64, [part],
            faceTransforms: null, "entity/test", 1f, baselinePx: 0f, rawSourceFrame: true);
    }

    private static EntityModelDefinition CompiledDefinition(EntityModelVisibleSides mask, bool mirror = false)
    {
        // Explicit per-cube mask on the DSL; FromTree bakes it into the compiled EntityModelCube.
        var mesh = new VanillaMeshDefinition(new VanillaPartDefinition("root", VanillaPartPose.Zero,
            children:
            [
                new VanillaPartDefinition("cube", VanillaPartPose.Zero,
                    cubes:
                    [
                        new VanillaCubeDefinition("cube", 0, 0, -4f, -8f, -4f, 8f, 8f, 8f,
                            Mirror: mirror, VisibleSides: mask)
                    ])
            ]));
        return EntityModelDefinition.FromTree("mask_cube_compiled", 64, 64, mesh,
            faceTransforms: null, "entity/test", 1f, baselinePx: 0f, rawSourceFrame: true);
    }

    private static EntityRenderState State() =>
        new(Id: 0, TypeId: 0, X: 0f, Y: 0f, Z: 0f, Yaw: 180f, Pitch: 0f);

    private static SpriteRect Sprite() =>
        new(X: 0, Y: 0, Width: 64, Height: 64, FrameCount: 1) { RectId = 1, U0 = 0f, V0 = 0f, U1 = 1f, V1 = 1f };
}
