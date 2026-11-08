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

    private static EntityModelDefinition Zombie =>
        EntityModelResolver.Resolve(HumanoidMobModelDefinition.ZombieEntityTypeId);

    [Fact]
    public void TheHandFrameIsTheArmPivotNotTheModelOrigin()
    {
        // PartWorldMatrix must hand back the pivot in world space. Subtracting a root part's pivot — which the geometry
        // path does because its cube vertices are pivot-relative — silently collapsed this to the origin and threw every
        // held item to the entity's feet.
        var definition = Zombie;
        var hand = EntityModelMesher.PartWorldMatrix(definition, Entity(HumanoidMobModelDefinition.ZombieEntityTypeId), "right_arm");
        var arm = definition.Compiled!.Find("right_arm")!;
        var px = EntityPlaceholderMesher.ModelScale / 16f * definition.Scale;

        Assert.Equal(arm.PivotX * px, hand.Translation.X, 4);
        Assert.Equal(arm.PivotY * px, hand.Translation.Y, 4);
        Assert.Equal(arm.PivotZ * px, hand.Translation.Z, 4);
        Assert.NotEqual(0f, hand.Translation.Length());
    }

    [Fact]
    public void TheAnchorPlacesTheItemAtTheHand()
    {
        // Reference placement, expressed from the arm pivot: 1/16 outward, 9/16 down and 5/16 forward, all in model
        // pixels. This is the regression that pins the held-item position after the frame fix above.
        var resolution = new ItemModelResolution(
            "minecraft:item/probe",
            new Dictionary<string, string>(),
            new Dictionary<ItemDisplayContext, ItemDisplayTransform>
            {
                [ItemDisplayContext.ThirdPersonRightHand] = new ItemDisplayTransform(Vector3.Zero, Vector3.Zero, Vector3.One)
            });

        var anchor = EntityModelResolver.AnchorFor(resolution, offhand: false, baby: false);

        // The probe model is NO_TRANSFORM, whose display matrix only centres the item at the origin; the anchor must then
        // land that centre on the measured hand offset.
        var centre = Vector3.Transform(new Vector3(0.5f, 0.5f, 0.5f), anchor);

        Assert.Equal(1f / 16f, centre.X, 4);
        Assert.Equal(-9f / 16f, centre.Y, 4);
        Assert.Equal(-5f / 16f, centre.Z, 4);
    }
    [Fact]
    public void AnUnknownPartResolvesToIdentity()
    {
        var matrix = EntityModelMesher.PartWorldMatrix(
            Zombie,
            Entity(HumanoidMobModelDefinition.ZombieEntityTypeId),
            "no_such_part");
        Assert.Equal(Matrix4x4.Identity, matrix);
    }

    [Fact]
    public void TheArmFrameMovesWithTheArmPose()
    {
        var entity = Entity(HumanoidMobModelDefinition.ZombieEntityTypeId);
        var rest = EntityModelMesher.PartWorldMatrix(Zombie, entity, "right_arm");

        var pose = new EntityPose();
        pose.Set("right_arm", new PoseOffset(Vector3.Zero, new Vector3(0.5f, 0f, 0f), Vector3.Zero));
        var swung = EntityModelMesher.PartWorldMatrix(Zombie, entity, "right_arm", pose);

        Assert.NotEqual(rest, swung);
    }

    [Fact]
    public void TheLeftArmMirrorsTheRightArm()
    {
        var entity = Entity(HumanoidMobModelDefinition.ZombieEntityTypeId);
        var right = EntityModelMesher.PartWorldMatrix(Zombie, entity, "right_arm");
        var left = EntityModelMesher.PartWorldMatrix(Zombie, entity, "left_arm");

        Assert.Equal(-right.Translation.X, left.Translation.X, 4);
        Assert.Equal(right.Translation.Y, left.Translation.Y, 4);
    }

}
