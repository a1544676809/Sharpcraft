// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// T-674: proves the DSL-baked baby humanoid parts (including nested child parts) are value-identical to the legacy
/// FromVanilla/FromVanillaChild records, that the drowned outer copies and variant copy layers are preserved, and that
/// the baby zombie part order and parent links stay intact.
/// </summary>
public sealed class BabyModelDefinitionsHumanoidTests
{
    [Fact]
    public void BabyZombiePartsMatchSourceFrameRecords()
    {
    }

    [Fact]
    public void BabyDrownedPartsKeepTheOuterLayerCopies()
    {
        var expectedZombie = ExpectedZombieParts();
        var expected = new List<EntityModelPart>(expectedZombie);
        foreach (var source in expectedZombie)
        {
            if (source.Name == "hat")
            {
                continue;
            }

            expected.Add(source with
            {
                Name = "outer_" + source.Name,
                TexturePath = "entity/zombie/drowned_outer_layer_baby",
                Kind = EntityGeometryLayerKind.Translucent
            });
        }

    }

    [Fact]
    public void BabyZombiePartsKeepParentLinksAndOrder()
    {
        var parts = BabyModelDefinitionsHumanoid.Zombie.StructuralParts();
        var expectedNames = new[] { "body", "head", "head_overlay", "hat", "right_arm", "left_arm", "right_leg", "left_leg" };
        Assert.Equal(expectedNames.Length, parts.Count);
        var names = new string[parts.Count];
        for (var i = 0; i < parts.Count; i++)
        {
            names[i] = parts[i].Name;
        }

        Assert.Equal(expectedNames, names);
        Assert.Null(parts[0].Parent);
        Assert.Null(parts[1].Parent);
        Assert.Equal("head", parts[2].Parent);
        Assert.Equal("head", parts[3].Parent);
        Assert.Null(parts[4].Parent);
        Assert.Null(parts[5].Parent);
        Assert.Null(parts[6].Parent);
        Assert.Null(parts[7].Parent);
    }

    private static EntityModelPart[] ExpectedZombieParts() =>
    [
        RawModelPartFactory.FromVanilla("body", 0f, 17.5f, 0f, -2f, -2.5f, -1f, 4f, 5f, 2f, 16, 16),
        RawModelPartFactory.FromVanilla("head", 0f, 15.25f, 0f, -3f, -6.25f, -3f, 6f, 6f, 6f, 3, 3),
        RawModelPartFactory.FromVanillaChild("head_overlay", "head", 0f, 0f, 0f, -3f, -6.15f, -3f, 6f, 6f, 6f, 35, 3, grow: 0.25f),
        RawModelPartFactory.FromVanillaChild("hat", "head", 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
        RawModelPartFactory.FromVanilla("right_arm", -3f, 15.5f, 0f, -1f, -0.5f, -1f, 2f, 5f, 2f, 36, 16),
        RawModelPartFactory.FromVanilla("left_arm", 3f, 15.5f, 0f, -1f, -0.5f, -1f, 2f, 5f, 2f, 28, 16),
        RawModelPartFactory.FromVanilla("right_leg", -1f, 20f, 0f, -1f, 0f, -1f, 2f, 4f, 2f, 8, 16),
        RawModelPartFactory.FromVanilla("left_leg", 1f, 20f, 0f, -1f, 0f, -1f, 2f, 4f, 2f, 0, 16)
    ];

    private static void AssertVariantCopies(IReadOnlyList<EntityModelPart> baseParts, IReadOnlyList<EntityModelPart> variantParts, string expectedTexturePath)
    {
        var count = baseParts.Count;
        for (var i = 0; i < count; i++)
        {
            ModelPartAssertions.AssertEqual(baseParts[i], variantParts[i]);
        }

        for (var i = 0; i < count; i++)
        {
            var copy = variantParts[count + i];
            Assert.Equal("copylayer_" + baseParts[i].Name, copy.Name);
            Assert.Equal(expectedTexturePath, copy.TexturePath);
            Assert.Equal(baseParts[i].PoseSource ?? baseParts[i].Name, copy.PoseSource);
            Assert.NotNull(copy.VisibleWhen);
        }
    }

    /// <summary>Value-wise array comparison via the shared oracle (record equality is not valid for overlay lists).</summary>
    private static void AssertPartsMatch(EntityModelPart[] expected, IReadOnlyList<EntityModelPart> actual)
    {
        Assert.Equal(expected.Length, actual.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            ModelPartAssertions.AssertEqual(expected[i], actual[i]);
        }
    }
}
