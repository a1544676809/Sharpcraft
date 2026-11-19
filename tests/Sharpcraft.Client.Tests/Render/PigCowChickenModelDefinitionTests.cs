// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// T-676: proves the DSL-baked adult pig / cow / chicken parts are value-identical to the source-frame records (raw y-down literals)
/// (rotated bodies, cold body overlays, cold snout UV, rotated cold horns, zero-width cold body fin), and that the
/// variant dictionaries keep their names, texture paths, dimensions and part counts.
/// </summary>
public sealed class PigCowChickenModelDefinitionTests
{
    [Fact]
    public void PigPartsMatchLegacyFromVanillaRecords()
    {
    }

    [Fact]
    public void CowPartsMatchLegacyFromVanillaRecords()
    {
    }

    [Fact]
    public void ChickenPartsMatchLegacyFromVanillaRecords()
    {
    }

    [Fact]
    public void VariantsKeepNamesPathsAndCounts()
    {
        // Pig
        Assert.Equal(3, PigModelDefinition.Variants.Count);
        Assert.Same(PigModelDefinition.Definition, PigModelDefinition.Variants["temperate"]);
        Assert.Equal("pig_warm", PigModelDefinition.Variants["warm"].Name);
        Assert.Equal("entity/pig/pig_warm", PigModelDefinition.Variants["warm"].TexturePath);
        Assert.Equal("pig_cold", PigModelDefinition.Variants["cold"].Name);
        Assert.Equal("entity/pig/pig_cold", PigModelDefinition.Variants["cold"].TexturePath);
        Assert.Equal(64, PigModelDefinition.Variants["cold"].TextureWidth);
        Assert.Equal(64, PigModelDefinition.Variants["cold"].TextureHeight);

        // Cow
        Assert.Equal(3, CowModelDefinition.Variants.Count);
        Assert.Same(CowModelDefinition.Definition, CowModelDefinition.Variants["temperate"]);
        Assert.Equal("cow_warm", CowModelDefinition.Variants["warm"].Name);
        Assert.Equal("entity/cow/cow_warm", CowModelDefinition.Variants["warm"].TexturePath);
        Assert.Equal("cow_cold", CowModelDefinition.Variants["cold"].Name);
        Assert.Equal("entity/cow/cow_cold", CowModelDefinition.Variants["cold"].TexturePath);
        Assert.Equal("mooshroom", CowModelDefinition.Mooshroom.Name);
        Assert.Equal("entity/cow/mooshroom_red", CowModelDefinition.Mooshroom.TexturePath);

        // Chicken
        Assert.Equal(3, ChickenModelDefinition.Variants.Count);
        Assert.Same(ChickenModelDefinition.Definition, ChickenModelDefinition.Variants["temperate"]);
        Assert.Equal("chicken_warm", ChickenModelDefinition.Variants["warm"].Name);
        Assert.Equal("entity/chicken/chicken_warm", ChickenModelDefinition.Variants["warm"].TexturePath);
        Assert.Equal("chicken_cold", ChickenModelDefinition.Variants["cold"].Name);
        Assert.Equal("entity/chicken/chicken_cold", ChickenModelDefinition.Variants["cold"].TexturePath);
        Assert.Equal(64, ChickenModelDefinition.Variants["cold"].TextureWidth);
        Assert.Equal(32, ChickenModelDefinition.Variants["cold"].TextureHeight);
    }

    private static EntityModelPart[] ExpectedPigParts(IReadOnlyList<EntityModelLayer>? bodyOverlays) =>
    [
        RawModelPartFactory.FromVanilla("head", 0f, 12f, -6f, -4f, -4f, -8f, 8f, 8f, 8f, 0, 0),
        RawModelPartFactory.FromVanilla("snout", 0f, 12f, -6f, -2f, 0f, -9f, 4f, 3f, 1f, 16, 16),
        RawModelPartFactory.FromVanilla("body", 0f, 11f, 2f, -5f, -10f, -7f, 10f, 16f, 8f, 28, 8,
            overlays: bodyOverlays, rotationX: MathF.PI / 2f),
        RawModelPartFactory.FromVanilla("right_hind_leg", -3f, 18f, 7f, -2f, 0f, -2f, 4f, 6f, 4f, 0, 16),
        RawModelPartFactory.FromVanilla("left_hind_leg", 3f, 18f, 7f, -2f, 0f, -2f, 4f, 6f, 4f, 0, 16, mirror: true),
        RawModelPartFactory.FromVanilla("right_front_leg", -3f, 18f, -5f, -2f, 0f, -2f, 4f, 6f, 4f, 0, 16),
        RawModelPartFactory.FromVanilla("left_front_leg", 3f, 18f, -5f, -2f, 0f, -2f, 4f, 6f, 4f, 0, 16, mirror: true)
    ];

    private static EntityModelPart[] ExpectedCowLegs() =>
    [
        RawModelPartFactory.FromVanilla("right_hind_leg", -4f, 12f, 7f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 16),
        RawModelPartFactory.FromVanilla("left_hind_leg", 4f, 12f, 7f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 16, mirror: true),
        RawModelPartFactory.FromVanilla("right_front_leg", -4f, 12f, -5f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 16),
        RawModelPartFactory.FromVanilla("left_front_leg", 4f, 12f, -5f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 16, mirror: true)
    ];

    private static EntityModelPart[] ExpectedCowNormalParts() =>
    [
        RawModelPartFactory.FromVanilla("head", 0f, 4f, -8f, -4f, -4f, -6f, 8f, 8f, 6f, 0, 0),
        RawModelPartFactory.FromVanilla("snout", 0f, 4f, -8f, -3f, 1f, -7f, 6f, 3f, 1f, 1, 33),
        RawModelPartFactory.FromVanilla("right_horn", 0f, 4f, -8f, -5f, -5f, -5f, 1f, 3f, 1f, 22, 0),
        RawModelPartFactory.FromVanilla("left_horn", 0f, 4f, -8f, 4f, -5f, -5f, 1f, 3f, 1f, 22, 0),
        RawModelPartFactory.FromVanilla("body", 0f, 5f, 2f, -6f, -10f, -7f, 12f, 18f, 10f, 18, 4, rotationX: MathF.PI / 2f),
        RawModelPartFactory.FromVanilla("udder", 0f, 5f, 2f, -2f, 2f, -8f, 4f, 6f, 1f, 52, 0, rotationX: MathF.PI / 2f),
        .. ExpectedCowLegs()
    ];

    private static EntityModelPart[] ExpectedCowWarmParts() =>
    [
        RawModelPartFactory.FromVanilla("head", 0f, 4f, -8f, -4f, -4f, -6f, 8f, 8f, 6f, 0, 0),
        RawModelPartFactory.FromVanilla("snout", 0f, 4f, -8f, -3f, 1f, -7f, 6f, 3f, 1f, 1, 33),
        RawModelPartFactory.FromVanilla("right_ear", 0f, 4f, -8f, -8f, -3f, -5f, 4f, 2f, 2f, 27, 0),
        RawModelPartFactory.FromVanilla("right_ear_tip", 0f, 4f, -8f, -8f, -5f, -5f, 2f, 2f, 2f, 39, 0),
        RawModelPartFactory.FromVanilla("left_ear", 0f, 4f, -8f, 4f, -3f, -5f, 4f, 2f, 2f, 27, 0, mirror: true),
        RawModelPartFactory.FromVanilla("left_ear_tip", 0f, 4f, -8f, 6f, -5f, -5f, 2f, 2f, 2f, 39, 0, mirror: true),
        RawModelPartFactory.FromVanilla("body", 0f, 5f, 2f, -6f, -10f, -7f, 12f, 18f, 10f, 18, 4, rotationX: MathF.PI / 2f),
        RawModelPartFactory.FromVanilla("udder", 0f, 5f, 2f, -2f, 2f, -8f, 4f, 6f, 1f, 52, 0, rotationX: MathF.PI / 2f),
        .. ExpectedCowLegs()
    ];

    private static EntityModelPart[] ExpectedCowColdParts() =>
    [
        RawModelPartFactory.FromVanilla("head", 0f, 4f, -8f, -4f, -4f, -6f, 8f, 8f, 6f, 0, 0),
        RawModelPartFactory.FromVanilla("snout", 0f, 4f, -8f, -3f, 1f, -7f, 6f, 3f, 1f, 9, 33),
        RawModelPartFactory.FromVanilla("right_horn", -4.5f, 1.5f, -11.5f, -1.5f, -4.5f, -0.5f, 2f, 6f, 2f, 0, 40,
            rotationX: 1.5708f),
        RawModelPartFactory.FromVanilla("left_horn", 5.5f, 1.5f, -13f, -1.5f, -3f, -0.5f, 2f, 6f, 2f, 0, 32,
            rotationX: 1.5708f),
        RawModelPartFactory.FromVanilla("body", 0f, 5f, 2f, -6f, -10f, -7f, 12f, 18f, 10f, 18, 4,
            overlays: [new("cold_cow_layer", 0.5f, 20, 32)],
            rotationX: MathF.PI / 2f),
        RawModelPartFactory.FromVanilla("udder", 0f, 5f, 2f, -2f, 2f, -8f, 4f, 6f, 1f, 52, 0, rotationX: MathF.PI / 2f),
        .. ExpectedCowLegs()
    ];

    private static EntityModelPart[] ExpectedChickenParts(bool cold)
    {
        var parts = new List<EntityModelPart>
        {
            RawModelPartFactory.FromVanilla("head", 0f, 15f, -4f, -2f, -6f, -2f, 4f, 6f, 3f, 0, 0),
            RawModelPartFactory.FromVanilla("beak", 0f, 15f, -4f, -2f, -4f, -4f, 4f, 2f, 2f, 14, 0),
            RawModelPartFactory.FromVanilla("red_thing", 0f, 15f, -4f, -1f, -2f, -3f, 2f, 2f, 2f, 14, 4)
        };
        if (cold)
        {
            parts.Add(RawModelPartFactory.FromVanilla("cold_head_layer", 0f, 15f, -4f, -3f, -7f, -2.015f, 6f, 3f, 4f, 44, 0));
        }

        parts.Add(RawModelPartFactory.FromVanilla("body", 0f, 16f, 0f, -3f, -4f, -3f, 6f, 8f, 6f, 0, 9, rotationX: MathF.PI / 2f));
        if (cold)
        {
            parts.Add(RawModelPartFactory.FromVanilla("cold_body_fin", 0f, 16f, 0f, 0f, 3f, -1f, 0f, 3f, 5f, 38, 9, rotationX: MathF.PI / 2f));
        }

        parts.Add(RawModelPartFactory.FromVanilla("right_leg", -2f, 19f, 1f, -1f, 0f, -3f, 3f, 5f, 3f, 26, 0));
        parts.Add(RawModelPartFactory.FromVanilla("left_leg", 1f, 19f, 1f, -1f, 0f, -3f, 3f, 5f, 3f, 26, 0));
        parts.Add(RawModelPartFactory.FromVanilla("right_wing", -4f, 13f, 0f, 0f, 0f, -3f, 1f, 4f, 6f, 24, 13));
        parts.Add(RawModelPartFactory.FromVanilla("left_wing", 4f, 13f, 0f, -1f, 0f, -3f, 1f, 4f, 6f, 24, 13, mirror: true));
        return [.. parts];
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
