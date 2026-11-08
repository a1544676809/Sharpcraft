// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// L2 prerequisite: attaching extra geometry to a posed part needs that part's world matrix, computed by the very same
/// frame maths the geometry walk uses (<see cref="EntityModelMesher.PartWorldMatrix"/>). These tests pin the properties
/// the held-item pass depends on.
/// </summary>
public sealed class PartWorldMatrixTests
{
    private static EntityRenderState Entity(int typeId, int variant = 0) =>
        new(1, typeId, 0f, 0f, 0f, 0f, 0f, VariantId: variant);

    [Fact]
    public void AnUnknownPartResolvesToIdentity()
    {
        var matrix = EntityModelMesher.PartWorldMatrix(
            HeldItemModelDefinition.Definition,
            Entity(HumanoidMobModelDefinition.ZombieEntityTypeId),
            "no_such_part");
        Assert.Equal(Matrix4x4.Identity, matrix);
    }

    [Fact]
    public void TheHandFrameMovesWithTheArmPose()
    {
        // The held-item definition roots at the arm, so posing the arm must move the hand frame. A provider-driven pose
        // with a swung arm is the cheapest reproducible difference.
        var entity = Entity(HumanoidMobModelDefinition.ZombieEntityTypeId);
        var rest = EntityModelMesher.PartWorldMatrix(HeldItemModelDefinition.Definition, entity, "held_item");

        var pose = new EntityPose();
        pose.Set("right_arm", new PoseOffset(Vector3.Zero, new Vector3(0.5f, 0f, 0f), Vector3.Zero));
        var swung = EntityModelMesher.PartWorldMatrix(HeldItemModelDefinition.Definition, entity, "held_item", pose);

        Assert.NotEqual(rest, swung);
    }

    [Fact]
    public void TheHandFrameCarriesTheHeldItemAnchor()
    {
        // The held cube sets a TransformSelector, and the bakery lifts it onto the node, so the hand frame already
        // includes the ItemInHandLayer anchor (XP -90, YP 180, offset). That is why the frame translation is not simply
        // the composed pivots: the anchor rotates and shifts it.
        var definition = HeldItemModelDefinition.Definition;
        var entity = Entity(HumanoidMobModelDefinition.ZombieEntityTypeId);
        var arm = definition.Compiled!.Find("right_arm")!;
        var held = definition.Compiled.Find("held_item")!;
        var matrix = EntityModelMesher.PartWorldMatrix(definition, entity, "held_item");
        var px = EntityPlaceholderMesher.ModelScale / 16f * definition.Scale;
        var naiveY = (arm.PivotY + held.PivotY) * px;

        Assert.NotEqual(naiveY, matrix.Translation.Y, 4);
    }
    [Fact]
    public void TheLeftHandFrameMirrorsTheRightOne()
    {
        var entity = Entity(HumanoidMobModelDefinition.ZombieEntityTypeId);
        var right = EntityModelMesher.PartWorldMatrix(HeldItemModelDefinition.Definition, entity, "held_item");
        var left = EntityModelMesher.PartWorldMatrix(HeldItemModelDefinition.LeftDefinition, entity, "held_item");

        Assert.Equal(right.Translation.X, -left.Translation.X, 4);
        Assert.Equal(right.Translation.Y, left.Translation.Y, 4);
    }
}
