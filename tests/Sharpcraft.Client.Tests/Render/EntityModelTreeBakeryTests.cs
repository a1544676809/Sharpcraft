// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
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

    [Fact]
    public void FoxAndRabbitDefinitionsExposeCompiledTrees()
    {
        Assert.NotNull(FoxModelDefinition.Definition.Compiled);
        Assert.NotNull(RabbitModelDefinition.Definition.Compiled);

        var fox = FoxModelDefinition.Definition.Compiled!;
        Assert.Equal(10, fox.Parts.Count);
        Assert.Equal("body", Assert.IsType<EntityModelPartNode>(fox.Find("tail")).Parent);

        var rabbit = RabbitModelDefinition.Definition.Compiled!;
        Assert.Equal(13, rabbit.Parts.Count);
        Assert.Empty(Assert.IsType<EntityModelPartNode>(rabbit.Find("frontlegs")).Cubes);
        Assert.Empty(Assert.IsType<EntityModelPartNode>(rabbit.Find("backlegs")).Cubes);
        Assert.Contains(rabbit.Find("right_front_leg"), rabbit.Find("frontlegs")!.Children);
    }

    [Fact]
    public void FarmModelsExposeCompiledTrees()
    {
        Assert.NotNull(PigModelDefinition.Definition.Compiled);
        Assert.NotNull(PigModelDefinition.Variants["cold"].Compiled);
        Assert.NotNull(CowModelDefinition.Definition.Compiled);
        Assert.NotNull(CowModelDefinition.Variants["warm"].Compiled);
        Assert.NotNull(ChickenModelDefinition.Definition.Compiled);
        Assert.NotNull(ChickenModelDefinition.Variants["cold"].Compiled);
        Assert.NotNull(SheepModelDefinition.Definition.Compiled);
    }

    [Fact]
    public void SimpleModelsExposeCompiledTrees()
    {
        Assert.NotNull(PandaModelDefinition.Definition.Compiled);
        Assert.NotNull(GoatModelDefinition.Definition.Compiled);
        Assert.NotNull(FrogModelDefinition.Definition.Compiled);
        Assert.NotNull(TadpoleModelDefinition.Definition.Compiled);
        Assert.NotNull(BatModelDefinition.Definition.Compiled);
        Assert.NotNull(AllayModelDefinition.Definition.Compiled);
        Assert.NotNull(BeeModelDefinition.Definition.Compiled);
        Assert.NotNull(VexModelDefinition.Definition.Compiled);
    }

    [Fact]
    public void TreeMesherBuildsBreezeGeometry()
    {
        var state = new EntityRenderState(1, BreezeModelDefinition.BreezeEntityTypeId, 0f, 0f, 0f, 0f, 0f);
        var geometry = new EntityModelMesher().Build(
            [state],
            new SpriteRect(0, 0, 32, 32, 1),
            32,
            32);

        Assert.True(geometry.VertexFloats > 0);
        Assert.True(geometry.IndexCount > 0);
    }

    [Fact]
    public void TreeMesherInheritsArmadilloGroupVisibility()
    {
        var unrolled = new EntityRenderState(1, ArmadilloModelDefinition.ArmadilloEntityTypeId, 0f, 0f, 0f, 0f, 0f);
        var rolled = new EntityRenderState(1, ArmadilloModelDefinition.ArmadilloEntityTypeId, 0f, 0f, 0f, 0f, 0f, IsHidingInShell: true);
        Assert.NotNull(ArmadilloModelDefinition.Definition.Compiled);
        Assert.NotNull(EntityModelResolver.Resolve(ArmadilloModelDefinition.ArmadilloEntityTypeId).Compiled);
        Assert.Same(ArmadilloModelDefinition.Definition.Compiled, EntityModelResolver.Resolve(ArmadilloModelDefinition.ArmadilloEntityTypeId).Compiled);
        Assert.Single(ArmadilloModelDefinition.Definition.Compiled!.Find("body")!.Cubes);
        Assert.Equal(6, ArmadilloModelDefinition.Definition.Compiled!.Root.Children.Count);
        var cubeNode = Assert.IsType<EntityModelPartNode>(ArmadilloModelDefinition.Definition.Compiled!.Find("cube"));
        var bodyNode = Assert.IsType<EntityModelPartNode>(ArmadilloModelDefinition.Definition.Compiled!.Find("body"));
        Assert.NotNull(bodyNode.VisibleWhen);
        Assert.True(bodyNode.VisibleWhen!(unrolled));
        Assert.False(bodyNode.VisibleWhen!(rolled));
        Assert.NotNull(cubeNode.VisibleWhen);
        Assert.True(cubeNode.VisibleWhen!(rolled));
        Assert.False(cubeNode.VisibleWhen!(unrolled));
        var unrolledMesher = new EntityModelMesher();
        var rolledMesher = new EntityModelMesher();
        var unrolledGeometry = unrolledMesher.Build([unrolled], new SpriteRect(0, 0, 64, 64, 1), 64, 64);
        var rolledGeometry = rolledMesher.Build([rolled], new SpriteRect(0, 0, 64, 64, 1), 64, 64);

        Assert.True(unrolledGeometry.VertexFloats > 0);
        Assert.True(rolledGeometry.VertexFloats > 0);
        Assert.True(rolledGeometry.VertexFloats < unrolledGeometry.VertexFloats,
            $"rolled={rolledGeometry.VertexFloats}, unrolled={unrolledGeometry.VertexFloats}");
    }
}
