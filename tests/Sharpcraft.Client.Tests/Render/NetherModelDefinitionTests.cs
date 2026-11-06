// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// T-681: proves the DSL-baked Hoglin/Strider/Blaze/Ghast/HappyGhast parts are value-identical to the legacy
/// FromVanilla/FromVanillaChild records (computed rods, seeded tentacles, rotations, mirror flags), that the cold /
/// shooting selectors, additional texture paths and scales are unchanged, and that nested order/parent links are kept.
/// </summary>
public sealed class NetherModelDefinitionTests
{
    [Fact]
    public void HoglinPartsMatchLegacyFromVanillaRecords()
    {
        AssertPartsMatch(ExpectedHoglinParts(), HoglinModelDefinition.Definition.Parts);
        AssertPartsMatch(ExpectedHoglinParts(), HoglinModelDefinition.Zoglin.Parts);
    }

    [Fact]
    public void StriderPartsMatchLegacyFromVanillaRecords()
    {
        AssertPartsMatch(ExpectedStriderParts(), StriderModelDefinition.Definition.Parts);
    }

    [Fact]
    public void BlazePartsMatchLegacyFromVanillaRecords()
    {
        AssertPartsMatch(ExpectedBlazeParts(), BlazeModelDefinition.Definition.Parts);
    }

    [Fact]
    public void GhastPartsMatchLegacyFromVanillaRecords()
    {
        AssertPartsMatch(ExpectedGhastParts(), GhastModelDefinition.Definition.Parts);
    }

    [Fact]
    public void HappyGhastPartsMatchLegacyFromVanillaRecords()
    {
        AssertPartsMatch(ExpectedHappyGhastParts(), HappyGhastModelDefinition.Definition.Parts);
    }

    [Fact]
    public void SelectorsScalesAndDefinitionMetadataAreUnchanged()
    {
        // Strider: cold texture selector + additional path.
        var strider = StriderModelDefinition.Definition;
        Assert.Equal("strider", strider.Name);
        Assert.Equal(64, strider.TextureWidth);
        Assert.Equal(128, strider.TextureHeight);
        Assert.Equal("entity/strider/strider", strider.TexturePath);
        Assert.NotNull(strider.TextureSelector);
        Assert.Equal("entity/strider/strider_cold", strider.TextureSelector!(StriderState(isCold: true)));
        Assert.Equal("entity/strider/strider", strider.TextureSelector!(StriderState(isCold: false)));
        var coldPath = Assert.Single(strider.AdditionalTexturePaths);
        Assert.Equal("entity/strider/strider_cold", coldPath);

        // Ghast: 4.5 scale, charging selector + additional path.
        var ghast = GhastModelDefinition.Definition;
        Assert.Equal("ghast", ghast.Name);
        Assert.Equal(64, ghast.TextureWidth);
        Assert.Equal(32, ghast.TextureHeight);
        Assert.Equal("entity/ghast/ghast", ghast.TexturePath);
        Assert.Equal(4.5f, ghast.Scale);
        Assert.NotNull(ghast.TextureSelector);
        Assert.Equal("entity/ghast/ghast_shooting", ghast.TextureSelector!(GhastState(isCharging: true)));
        Assert.Equal("entity/ghast/ghast", ghast.TextureSelector!(GhastState(isCharging: false)));
        var shootingPath = Assert.Single(ghast.AdditionalTexturePaths);
        Assert.Equal("entity/ghast/ghast_shooting", shootingPath);

        // HappyGhast: 4f scale.
        Assert.Equal("happy_ghast", HappyGhastModelDefinition.Definition.Name);
        Assert.Equal(64, HappyGhastModelDefinition.Definition.TextureWidth);
        Assert.Equal(64, HappyGhastModelDefinition.Definition.TextureHeight);
        Assert.Equal("entity/ghast/happy_ghast", HappyGhastModelDefinition.Definition.TexturePath);
        Assert.Equal(4f, HappyGhastModelDefinition.Definition.Scale);

        // Hoglin / Zoglin share the 11-part geometry.
        Assert.Equal("hoglin", HoglinModelDefinition.Definition.Name);
        Assert.Equal(128, HoglinModelDefinition.Definition.TextureWidth);
        Assert.Equal(64, HoglinModelDefinition.Definition.TextureHeight);
        Assert.Equal("entity/hoglin/hoglin", HoglinModelDefinition.Definition.TexturePath);
        Assert.Equal("zoglin", HoglinModelDefinition.Zoglin.Name);
        Assert.Equal("entity/hoglin/zoglin", HoglinModelDefinition.Zoglin.TexturePath);
        Assert.Equal(11, HoglinModelDefinition.Definition.Parts.Count);
        Assert.Equal(11, HoglinModelDefinition.Zoglin.Parts.Count);

        // Blaze.
        Assert.Equal("blaze", BlazeModelDefinition.Definition.Name);
        Assert.Equal(64, BlazeModelDefinition.Definition.TextureWidth);
        Assert.Equal(32, BlazeModelDefinition.Definition.TextureHeight);
        Assert.Equal("entity/blaze/blaze", BlazeModelDefinition.Definition.TexturePath);
        Assert.Equal(13, BlazeModelDefinition.Definition.Parts.Count);
    }

    [Fact]
    public void HoglinStriderHappyGhastKeepOrderAndParentLinks()
    {
        AssertNamesAndParents(HoglinModelDefinition.Definition.Parts,
            ["body", "mane", "head", "right_ear", "left_ear", "right_horn", "left_horn", "right_front_leg", "left_front_leg", "right_hind_leg", "left_hind_leg"],
            [null, "body", null, "head", "head", "head", "head", null, null, null, null]);
        AssertNamesAndParents(StriderModelDefinition.Definition.Parts,
            ["right_leg", "left_leg", "body", "right_bottom_bristle", "right_middle_bristle", "right_top_bristle", "left_top_bristle", "left_middle_bristle", "left_bottom_bristle"],
            [null, null, null, "body", "body", "body", "body", "body", "body"]);
        AssertNamesAndParents(HappyGhastModelDefinition.Definition.Parts,
            ["body", "tentacle0", "tentacle1", "tentacle2", "tentacle3", "tentacle4", "tentacle5", "tentacle6", "tentacle7", "tentacle8"],
            [null, "body", "body", "body", "body", "body", "body", "body", "body", "body"]);
    }

    private static EntityRenderState StriderState(bool isCold) =>
        new(1, StriderModelDefinition.StriderEntityTypeId, 0f, 0f, 0f, 0f, 0f, IsCold: isCold);

    private static EntityRenderState GhastState(bool isCharging) =>
        new(1, GhastModelDefinition.GhastEntityTypeId, 0f, 0f, 0f, 0f, 0f, IsCharging: isCharging);

    private static EntityModelPart[] ExpectedHoglinParts() =>
    [
        PlayerModelDefinition.FromVanilla("body", 0f, 7f, 0f, -8f, -7f, -13f, 16f, 14f, 26f, 1, 1),
        PlayerModelDefinition.FromVanillaChild("mane", "body", 0f, -14f, -7f, 0f, 0f, -9f, 0f, 10f, 19f, 90, 33, grow: 0.001f),
        PlayerModelDefinition.FromVanilla("head", 0f, 2f, -12f, -7f, -3f, -19f, 14f, 6f, 19f, 61, 1,
            rotationX: -0.87266463f),
        PlayerModelDefinition.FromVanillaChild("right_ear", "head", -6f, -2f, -3f, -6f, -1f, -2f, 6f, 1f, 4f, 1, 1,
            rotationZ: -MathF.PI * 2f / 9f),
        PlayerModelDefinition.FromVanillaChild("left_ear", "head", 6f, -2f, -3f, 0f, -1f, -2f, 6f, 1f, 4f, 1, 6,
            rotationZ: MathF.PI * 2f / 9f),
        PlayerModelDefinition.FromVanillaChild("right_horn", "head", -7f, 2f, -12f, -1f, -11f, -1f, 2f, 11f, 2f, 10, 13),
        PlayerModelDefinition.FromVanillaChild("left_horn", "head", 7f, 2f, -12f, -1f, -11f, -1f, 2f, 11f, 2f, 1, 13),
        PlayerModelDefinition.FromVanilla("right_front_leg", -4f, 10f, -8.5f, -3f, 0f, -3f, 6f, 14f, 6f, 66, 42),
        PlayerModelDefinition.FromVanilla("left_front_leg", 4f, 10f, -8.5f, -3f, 0f, -3f, 6f, 14f, 6f, 41, 42),
        PlayerModelDefinition.FromVanilla("right_hind_leg", -5f, 13f, 10f, -2.5f, 0f, -2.5f, 5f, 11f, 5f, 21, 45),
        PlayerModelDefinition.FromVanilla("left_hind_leg", 5f, 13f, 10f, -2.5f, 0f, -2.5f, 5f, 11f, 5f, 0, 45)
    ];

    private static EntityModelPart[] ExpectedStriderParts() =>
    [
        PlayerModelDefinition.FromVanilla("right_leg", -4f, 8f, 0f, -2f, 0f, -2f, 4f, 16f, 4f, 0, 32),
        PlayerModelDefinition.FromVanilla("left_leg", 4f, 8f, 0f, -2f, 0f, -2f, 4f, 16f, 4f, 0, 55),
        PlayerModelDefinition.FromVanilla("body", 0f, 1f, 0f, -8f, -6f, -8f, 16f, 14f, 16f, 0, 0),
        PlayerModelDefinition.FromVanillaChild("right_bottom_bristle", "body", -8f, 4f, -8f, -12f, 0f, 0f, 12f, 0f, 16f, 16, 65,
            mirror: true, rotationZ: -1.2217305f),
        PlayerModelDefinition.FromVanillaChild("right_middle_bristle", "body", -8f, -1f, -8f, -12f, 0f, 0f, 12f, 0f, 16f, 16, 49,
            mirror: true, rotationZ: -1.134464f),
        PlayerModelDefinition.FromVanillaChild("right_top_bristle", "body", -8f, -5f, -8f, -12f, 0f, 0f, 12f, 0f, 16f, 16, 33,
            mirror: true, rotationZ: -0.87266463f),
        PlayerModelDefinition.FromVanillaChild("left_top_bristle", "body", 8f, -6f, -8f, 0f, 0f, 0f, 12f, 0f, 16f, 16, 33,
            rotationZ: 0.87266463f),
        PlayerModelDefinition.FromVanillaChild("left_middle_bristle", "body", 8f, -2f, -8f, 0f, 0f, 0f, 12f, 0f, 16f, 16, 49,
            rotationZ: 1.134464f),
        PlayerModelDefinition.FromVanillaChild("left_bottom_bristle", "body", 8f, 3f, -8f, 0f, 0f, 0f, 12f, 0f, 16f, 16, 65,
            rotationZ: 1.2217305f)
    ];

    private static EntityModelPart[] ExpectedBlazeParts()
    {
        var parts = new List<EntityModelPart>
        {
            PlayerModelDefinition.FromVanilla("head", 0f, 0f, 0f, -4f, -4f, -4f, 8f, 8f, 8f, 0, 0)
        };
        var angle = 0f;
        for (var i = 0; i < 4; i++)
        {
            AddRod(parts, i, MathF.Cos(angle) * 9f, -2f + MathF.Cos(i * 2 * 0.25f), MathF.Sin(angle) * 9f);
            angle += MathF.PI / 2f;
        }

        angle = MathF.PI / 4f;
        for (var i = 4; i < 8; i++)
        {
            AddRod(parts, i, MathF.Cos(angle) * 7f, 2f + MathF.Cos(i * 2 * 0.25f), MathF.Sin(angle) * 7f);
            angle += MathF.PI / 2f;
        }

        angle = 0.47123894f;
        for (var i = 8; i < 12; i++)
        {
            AddRod(parts, i, MathF.Cos(angle) * 5f, 11f + MathF.Cos(i * 1.5f * 0.5f), MathF.Sin(angle) * 5f);
            angle += MathF.PI / 2f;
        }

        return [.. parts];
    }

    private static void AddRod(List<EntityModelPart> parts, int index, float x, float y, float z) =>
        parts.Add(PlayerModelDefinition.FromVanilla($"part{index}", x, y, z, 0f, 0f, 0f, 2f, 8f, 2f, 0, 16));

    private static EntityModelPart[] ExpectedGhastParts()
    {
        var lengths = new[] { 8, 13, 9, 11, 11, 10, 12, 9, 12 };
        var parts = new List<EntityModelPart>
        {
            PlayerModelDefinition.FromVanilla("body", 0f, 17.6f, 0f, -8f, -8f, -8f, 16f, 16f, 16f, 0, 0)
        };
        for (var i = 0; i < 9; i++)
        {
            var xo = ((i % 3 - i / 3 % 2 * 0.5f + 0.25f) - 1f) * 5f;
            var yo = (i / 3 / 2 * 2 - 1f) * 5f;
            parts.Add(PlayerModelDefinition.FromVanilla("tentacle" + i, xo, 24.6f, yo, -1f, 0f, -1f, 2f, lengths[i], 2f, 0, 0));
        }

        return [.. parts];
    }

    private static EntityModelPart[] ExpectedHappyGhastParts()
    {
        var tentacles = new (float X, float Y, float Z, float Length)[]
        {
            (-3.75f, 7f, -5f, 5f),
            (1.25f, 7f, -5f, 7f),
            (6.25f, 7f, -5f, 4f),
            (-6.25f, 7f, 0f, 5f),
            (-1.25f, 7f, 0f, 5f),
            (3.75f, 7f, 0f, 7f),
            (-3.75f, 7f, 5f, 8f),
            (1.25f, 7f, 5f, 8f),
            (6.25f, 7f, 5f, 5f)
        };
        var parts = new List<EntityModelPart>
        {
            PlayerModelDefinition.FromVanilla("body", 0f, 16f, 0f, -8f, -8f, -8f, 16f, 16f, 16f, 0, 0)
        };
        for (var i = 0; i < tentacles.Length; i++)
        {
            var (x, y, z, length) = tentacles[i];
            parts.Add(PlayerModelDefinition.FromVanillaChild("tentacle" + i, "body", x, y, z,
                -1f, 0f, -1f, 2f, length, 2f, 0, 0));
        }

        return [.. parts];
    }

    private static void AssertNamesAndParents(IReadOnlyList<EntityModelPart> parts, string[] names, string?[] parents)
    {
        Assert.Equal(names.Length, parts.Count);
        Assert.Equal(parents.Length, parts.Count);
        for (var i = 0; i < names.Length; i++)
        {
            Assert.Equal(names[i], parts[i].Name);
            Assert.Equal(parents[i], parts[i].Parent);
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
