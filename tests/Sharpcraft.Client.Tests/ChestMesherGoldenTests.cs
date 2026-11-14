// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Common.World;

namespace Sharpcraft.Client.Tests;

/// <summary>
/// Pins the chest mesher to the constants recorded in
/// <c>docs/specs/render/golden-vectors/chest.md</c>, which in turn come from the reference
/// model. These are truth values, not values read back from the implementation.
/// </summary>
public sealed class ChestMesherGoldenTests
{
    private const float Tolerance = 1e-6f;

    /// <summary>A closed chest has a level lid.</summary>
    [Fact]
    public void ClosedChestHasNoRotation()
    {
        Assert.Equal(0f, ChestMesher.OpenAngle(0f), Tolerance);
    }

    /// <summary>A fully open chest has the lid standing straight up.</summary>
    [Fact]
    public void OpenChestTurnsAQuarterTurn()
    {
        Assert.Equal(MathF.PI / 2f, ChestMesher.OpenAngle(1f), Tolerance);
    }

    /// <summary>
    /// Half open is not half a quarter turn: the reference applies a cubic ease first, so the angle
    /// is one minus an eighth of the quarter turn.
    /// </summary>
    [Fact]
    public void HalfOpenUsesTheCubicEase()
    {
        Assert.Equal((1f - 0.125f) * (MathF.PI / 2f), ChestMesher.OpenAngle(0.5f), Tolerance);
    }

    /// <summary>The model constants are the reference's pixels over sixteen.</summary>
    [Fact]
    public void ModelConstantsMatchTheReference()
    {
        Assert.Equal(14f / 16f, ChestMesher.BodyWidth, Tolerance);
        Assert.Equal(10f / 16f, ChestMesher.BodyHeight, Tolerance);
        Assert.Equal(14f / 16f, ChestMesher.LidWidth, Tolerance);
        Assert.Equal(5f / 16f, ChestMesher.LidHeight, Tolerance);
        Assert.Equal(2f / 16f, ChestMesher.LockWidth, Tolerance);
        Assert.Equal(4f / 16f, ChestMesher.LockHeight, Tolerance);
        Assert.Equal(1f / 16f, ChestMesher.LockDepth, Tolerance);
        Assert.Equal(9f / 16f, ChestMesher.PivotY, Tolerance);
        Assert.Equal(1f / 16f, ChestMesher.PivotZ, Tolerance);
    }

    /// <summary>Three boxes come out: the body, the lid and the latch.</summary>
    [Fact]
    public void OneChestEmitsThreeBoxes()
    {
        var mesher = new ChestMesher();
        BlockEntityRenderState[] entities =
        [
            new(0f, 0f, 0f, VanillaBlockEntityTypes.Chest)
        ];
        var sprite = new SpriteRect(0, 0, 16, 16, 1);

        var geometry = mesher.Build(entities, sprite);

        const int verticesPerBox = 24;
        const int indicesPerBox = 36;
        Assert.Equal(1, mesher.LastEntityCount);
        Assert.Equal(3 * verticesPerBox * ChunkMesher.FloatsPerVertex, geometry.VertexFloats);
        Assert.Equal(3 * indicesPerBox, geometry.IndexCount);
    }

    /// <summary>Entities that are not chest-like are skipped rather than drawn as chests.</summary>
    [Fact]
    public void NonChestEntitiesAreSkipped()
    {
        var mesher = new ChestMesher();
        BlockEntityRenderState[] entities =
        [
            new(0f, 0f, 0f, VanillaBlockEntityTypes.Furnace)
        ];

        var geometry = mesher.Build(entities, new SpriteRect(0, 0, 16, 16, 1));

        Assert.Equal(0, mesher.LastEntityCount);
        Assert.Equal(0, geometry.VertexFloats);
        Assert.Equal(0, geometry.IndexCount);
    }
}
