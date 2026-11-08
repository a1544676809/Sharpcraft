// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// The held-item anchor used to be a fixed part offset with no rotation, so a held chest rendered a full block tall and
/// unrotated. The reference <c>ItemInHandLayer</c> instead rotates XP by -90, YP by 180, offsets by (±1, 2, -10)/16 and
/// then applies the item model's own display transform (chest thirdperson_righthand scale is 0.375).
/// </summary>
public sealed class HeldItemAnchorTests
{
    private static readonly ItemDisplayTransform ChestThirdPerson = new(
        new Vector3(75f, 315f, 0f),
        new Vector3(0f, 2.5f, 0f),
        new Vector3(0.375f, 0.375f, 0.375f));

    [Fact]
    public void TheAnchorAppliesTheDisplayScale()
    {
        var resolution = Resolution(ChestThirdPerson);
        var anchor = Anchor(resolution);
        Assert.Equal(0.375f, UniformScale(anchor), 4);
    }

    [Fact]
    public void WithoutADisplayTransformTheAnchorKeepsUnitScale()
    {
        var anchor = Anchor(Resolution(transform: null));
        Assert.Equal(1f, UniformScale(anchor), 4);
    }

    [Fact]
    public void TheOffHandAnchorDiffersFromTheMainHandOne()
    {
        // The reference offsets the hand by (±1, 2, -10)/16 and the rotations in front of it re-orient that offset, so
        // the composed translations are not a plain mirror; the anchors must simply not coincide, with equal scale.
        var resolution = Resolution(ChestThirdPerson);
        var main = EntityModelResolver.AnchorFor(resolution, offhand: false, baby: false);
        var off = EntityModelResolver.AnchorFor(resolution, offhand: true, baby: false);

        Assert.NotEqual(main, off);
        Assert.Equal(UniformScale(main), UniformScale(off), 5);
    }

    [Fact]
    public void BabiesUseTheSmallerOffset()
    {
        var resolution = Resolution(ChestThirdPerson);
        var adult = EntityModelResolver.AnchorFor(resolution, offhand: false, baby: false);
        var baby = EntityModelResolver.AnchorFor(resolution, offhand: false, baby: true);
        Assert.NotEqual(adult.Translation, baby.Translation);
    }

    private static ItemModelResolution Resolution(ItemDisplayTransform? transform)
    {
        var display = new Dictionary<ItemDisplayContext, ItemDisplayTransform>();
        if (transform is not null)
        {
            display[ItemDisplayContext.ThirdPersonRightHand] = transform.Value;
            display[ItemDisplayContext.ThirdPersonLeftHand] = transform.Value;
        }

        return new ItemModelResolution("minecraft:item/chest", new Dictionary<string, string>(), display);
    }

    private static Matrix4x4 Anchor(ItemModelResolution resolution) =>
        EntityModelResolver.AnchorFor(resolution, offhand: false, baby: false);

    private static float UniformScale(Matrix4x4 matrix)
    {
        var x = Vector3.TransformNormal(Vector3.UnitX, matrix).Length();
        var y = Vector3.TransformNormal(Vector3.UnitY, matrix).Length();
        var z = Vector3.TransformNormal(Vector3.UnitZ, matrix).Length();
        Assert.Equal(x, y, 4);
        Assert.Equal(x, z, 4);
        return x;
    }
}
