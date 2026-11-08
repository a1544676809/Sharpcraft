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
