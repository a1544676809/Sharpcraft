// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// T-678: proves the DSL-baked allay / bat / bee parts are value-identical to the legacy
/// FromVanilla/FromVanillaChild records (nested chains, zero-width parts, wing grow/rotations, allay translucency), that
/// the child order and parent links are preserved, and that the bee texture selector and additional texture paths and
/// all definition names/dims/paths are unchanged.
/// </summary>
public sealed class AllayBatBeeModelDefinitionTests
{
    [Fact]
    public void AllayPartsMatchLegacyFromVanillaRecords()
    {
        AssertPartsMatch(ExpectedAllayParts(), AllayModelDefinition.Definition.Parts);
    }

    [Fact]
    public void BatPartsMatchLegacyFromVanillaRecords()
    {
        AssertPartsMatch(ExpectedBatParts(), BatModelDefinition.Definition.Parts);
    }

    [Fact]
    public void BeePartsMatchLegacyFromVanillaRecords()
    {
        AssertPartsMatch(ExpectedBeeParts(), BeeModelDefinition.Definition.Parts);
    }

    [Fact]
    public void AllayKeepsChildOrderParentLinksAndTranslucentKind()
    {
        var parts = AllayModelDefinition.Definition.Parts;
        AssertNamesAndParents(parts,
            ["root", "head", "body", "body_inner", "right_arm", "left_arm", "right_wing", "left_wing"],
            [null, "root", "root", "body", "body", "body", "body", "body"]);
        foreach (var part in parts)
        {
            Assert.Equal(EntityGeometryLayerKind.Translucent, part.Kind);
        }
    }

    [Fact]
    public void BatAndBeeKeepChildOrderAndParentLinks()
    {
        AssertNamesAndParents(BatModelDefinition.Definition.Parts,
            ["body", "feet", "right_wing", "right_wing_tip", "left_wing", "left_wing_tip", "head", "right_ear", "left_ear"],
            [null, "body", "body", "right_wing", "body", "left_wing", null, "head", "head"]);
        AssertNamesAndParents(BeeModelDefinition.Definition.Parts,
            ["bone", "body", "stinger", "left_antenna", "right_antenna", "right_wing", "left_wing", "front_legs", "middle_legs", "back_legs"],
            [null, "bone", "body", "body", "body", "bone", "bone", "bone", "bone", "bone"]);
    }

    [Fact]
    public void BeeKeepsTextureSelectorAndAdditionalPaths()
    {
        var definition = BeeModelDefinition.Definition;
        Assert.NotNull(definition.TextureSelector);
        Assert.Equal("entity/bee/bee_angry_nectar", definition.TextureSelector!(State(isAngry: true, hasNectar: true)));
        Assert.Equal("entity/bee/bee_angry", definition.TextureSelector!(State(isAngry: true, hasNectar: false)));
        Assert.Equal("entity/bee/bee_nectar", definition.TextureSelector!(State(isAngry: false, hasNectar: true)));
        Assert.Equal("entity/bee/bee", definition.TextureSelector!(State(isAngry: false, hasNectar: false)));
        var paths = definition.AdditionalTexturePaths;
        Assert.Equal(3, paths.Count);
        Assert.Equal("entity/bee/bee_angry", paths[0]);
        Assert.Equal("entity/bee/bee_nectar", paths[1]);
        Assert.Equal("entity/bee/bee_angry_nectar", paths[2]);
    }

    [Fact]
    public void DefinitionsKeepNamesDimsPathsAndCounts()
    {
        Assert.Equal("allay", AllayModelDefinition.Definition.Name);
        Assert.Equal(32, AllayModelDefinition.Definition.TextureWidth);
        Assert.Equal(32, AllayModelDefinition.Definition.TextureHeight);
        Assert.Equal("entity/allay/allay", AllayModelDefinition.Definition.TexturePath);
        Assert.Equal(8, AllayModelDefinition.Definition.Parts.Count);

        Assert.Equal("bat", BatModelDefinition.Definition.Name);
        Assert.Equal(32, BatModelDefinition.Definition.TextureWidth);
        Assert.Equal(32, BatModelDefinition.Definition.TextureHeight);
        Assert.Equal("entity/bat/bat", BatModelDefinition.Definition.TexturePath);
        Assert.Equal(9, BatModelDefinition.Definition.Parts.Count);

        Assert.Equal("bee", BeeModelDefinition.Definition.Name);
        Assert.Equal(64, BeeModelDefinition.Definition.TextureWidth);
        Assert.Equal(64, BeeModelDefinition.Definition.TextureHeight);
        Assert.Equal("entity/bee/bee", BeeModelDefinition.Definition.TexturePath);
        Assert.Equal(10, BeeModelDefinition.Definition.Parts.Count);
    }

    private static EntityRenderState State(bool isAngry, bool hasNectar) =>
        new(1, BeeModelDefinition.BeeEntityTypeId, 0f, 0f, 0f, 0f, 0f, IsAngry: isAngry, HasNectar: hasNectar);

    private static EntityModelPart[] ExpectedAllayParts()
    {
        var parts = new[]
        {
            PlayerModelDefinition.FromVanilla("root", 0f, 23.5f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("head", "root", 0f, -3.99f, 0f, -2.5f, -5f, -2.5f, 5f, 5f, 5f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("body", "root", 0f, -4f, 0f, -1.5f, 0f, -1f, 3f, 4f, 2f, 0, 10),
            PlayerModelDefinition.FromVanillaChild("body_inner", "body", 0f, 0f, 0f, -1.5f, 0f, -1f, 3f, 5f, 2f, 0, 16, grow: -0.2f),
            PlayerModelDefinition.FromVanillaChild("right_arm", "body", -1.75f, 0.5f, 0f, -0.75f, -0.5f, -1f, 1f, 4f, 2f, 23, 0, grow: -0.01f),
            PlayerModelDefinition.FromVanillaChild("left_arm", "body", 1.75f, 0.5f, 0f, -0.25f, -0.5f, -1f, 1f, 4f, 2f, 23, 6, grow: -0.01f),
            PlayerModelDefinition.FromVanillaChild("right_wing", "body", -0.5f, 0f, 0.6f, 0f, 1f, 0f, 0f, 5f, 8f, 16, 14),
            PlayerModelDefinition.FromVanillaChild("left_wing", "body", 0.5f, 0f, 0.6f, 0f, 1f, 0f, 0f, 5f, 8f, 16, 14)
        };

        for (var i = 0; i < parts.Length; i++)
        {
            parts[i] = parts[i] with { Kind = EntityGeometryLayerKind.Translucent };
        }

        return parts;
    }

    private static EntityModelPart[] ExpectedBatParts() =>
    [
        PlayerModelDefinition.FromVanilla("body", 0f, 17f, 0f, -1.5f, 0f, -1f, 3f, 5f, 2f, 0, 0),
        PlayerModelDefinition.FromVanillaChild("feet", "body", 0f, 5f, 0f, -1.5f, 0f, 0f, 3f, 2f, 0f, 16, 16),
        PlayerModelDefinition.FromVanillaChild("right_wing", "body", -1.5f, 0f, 0f, -2f, -2f, 0f, 2f, 7f, 0f, 12, 0),
        PlayerModelDefinition.FromVanillaChild("right_wing_tip", "right_wing", -2f, 0f, 0f, -6f, -2f, 0f, 6f, 8f, 0f, 16, 0),
        PlayerModelDefinition.FromVanillaChild("left_wing", "body", 1.5f, 0f, 0f, 0f, -2f, 0f, 2f, 7f, 0f, 12, 7),
        PlayerModelDefinition.FromVanillaChild("left_wing_tip", "left_wing", 2f, 0f, 0f, 0f, -2f, 0f, 6f, 8f, 0f, 16, 8),
        PlayerModelDefinition.FromVanilla("head", 0f, 17f, 0f, -2f, -3f, -1f, 4f, 3f, 2f, 0, 7),
        PlayerModelDefinition.FromVanillaChild("right_ear", "head", -1.5f, -2f, 0f, -2.5f, -4f, 0f, 3f, 5f, 0f, 1, 15),
        PlayerModelDefinition.FromVanillaChild("left_ear", "head", 1.1f, -3f, 0f, -0.1f, -3f, 0f, 3f, 5f, 0f, 8, 15)
    ];

    private static EntityModelPart[] ExpectedBeeParts() =>
    [
        PlayerModelDefinition.FromVanilla("bone", 0f, 19f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
        PlayerModelDefinition.FromVanillaChild("body", "bone", 0f, 0f, 0f, -3.5f, -4f, -5f, 7f, 7f, 10f, 0, 0),
        PlayerModelDefinition.FromVanillaChild("stinger", "body", 0f, 0f, 0f, 0f, -1f, 5f, 0f, 1f, 2f, 26, 7),
        PlayerModelDefinition.FromVanillaChild("left_antenna", "body", 0f, -2f, -5f, 1.5f, -2f, -3f, 1f, 2f, 3f, 2, 0),
        PlayerModelDefinition.FromVanillaChild("right_antenna", "body", 0f, -2f, -5f, -2.5f, -2f, -3f, 1f, 2f, 3f, 2, 3),
        PlayerModelDefinition.FromVanillaChild("right_wing", "bone", -1.5f, -4f, -3f, -9f, 0f, 0f, 9f, 0f, 6f, 0, 18,
            rotationY: -0.2618f, grow: 0.001f),
        PlayerModelDefinition.FromVanillaChild("left_wing", "bone", 1.5f, -4f, -3f, 0f, 0f, 0f, 9f, 0f, 6f, 0, 18,
            mirror: true, rotationY: 0.2618f, grow: 0.001f),
        PlayerModelDefinition.FromVanillaChild("front_legs", "bone", 1.5f, 3f, -2f, -5f, 0f, 0f, 7f, 2f, 0f, 26, 1),
        PlayerModelDefinition.FromVanillaChild("middle_legs", "bone", 1.5f, 3f, 0f, -5f, 0f, 0f, 7f, 2f, 0f, 26, 3),
        PlayerModelDefinition.FromVanillaChild("back_legs", "bone", 1.5f, 3f, 2f, -5f, 0f, 0f, 7f, 2f, 0f, 26, 5)
    ];

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
