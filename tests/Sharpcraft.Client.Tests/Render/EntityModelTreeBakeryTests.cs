// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// T-713: the central tree bakery preserves reference part identity (including zero-cube groups) and stores each part's
/// cubes as one node, unlike the legacy one-cube-per-EntityModelPart path.
/// </summary>
public sealed class EntityModelTreeBakeryTests
{
    [Fact]
    public void BreezeKeepsMultiCubePartsAsOneNode()
    {
        var compiled = VanillaMeshBakery.Build(BreezeModelDefinition.Mesh, 32, 32);
        Assert.Equal(11, compiled.Parts.Count);
        Assert.Equal(14, compiled.Cubes.Count);

        var head = Assert.IsType<EntityModelPartNode>(compiled.Find("head"));
        Assert.Equal(2, head.Cubes.Count);
        Assert.Contains(compiled.Find("eyes"), head.Children);

        var eyes = Assert.IsType<EntityModelPartNode>(compiled.Find("eyes"));
        Assert.Equal(2, eyes.Cubes.Count);

        var windMid = Assert.IsType<EntityModelPartNode>(compiled.Find("wind_mid"));
        Assert.Equal(3, windMid.Cubes.Count);

        var windTop = Assert.IsType<EntityModelPartNode>(compiled.Find("wind_top"));
        Assert.Equal(3, windTop.Cubes.Count);
    }

    [Fact]
    public void ArmadilloKeepsZeroCubeGroupNodes()
    {
        var compiled = VanillaMeshBakery.Build(ArmadilloModelDefinition.Mesh, 64, 64);
        Assert.Equal(14, compiled.Parts.Count);

        var head = Assert.IsType<EntityModelPartNode>(compiled.Find("head"));
        Assert.Empty(head.Cubes);
        Assert.Equal("body", head.Parent);
        Assert.Contains(compiled.Find("head_cube"), head.Children);

        var body = Assert.IsType<EntityModelPartNode>(compiled.Find("body"));
        Assert.Single(body.Cubes);
    }
}
