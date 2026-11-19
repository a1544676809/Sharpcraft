// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Allay/bat/bee definition structural invariants: child order and parent links are preserved, allay translucency,
/// the bee texture selector and additional texture paths, and definition names/dims/paths are unchanged.
/// The former flat <c>AssertPartsMatch</c> value checks were removed (#231): jar source-sample strict equivalence for
/// these families is provided by <see cref="LivingRawReferenceSampleTests.RawBakeMatchesSourceVertices"/> (keyed,
/// 1e-5 + UV half) through the compiled mesher, which is a stronger primary-evidence check.
/// </summary>
public sealed class AllayBatBeeModelDefinitionTests
{

    [Fact]
    public void BatAndBeeKeepChildOrderAndParentLinks()
    {
        AssertNamesAndParents(BatModelDefinition.Definition.StructuralParts(),
            ["body", "feet", "right_wing", "right_wing_tip", "left_wing", "left_wing_tip", "head", "right_ear", "left_ear"],
            [null, "body", "body", "right_wing", "body", "left_wing", null, "head", "head"]);
        AssertNamesAndParents(BeeModelDefinition.Definition.StructuralParts(),
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

        Assert.Equal("bat", BatModelDefinition.Definition.Name);
        Assert.Equal(32, BatModelDefinition.Definition.TextureWidth);
        Assert.Equal(32, BatModelDefinition.Definition.TextureHeight);
        Assert.Equal("entity/bat/bat", BatModelDefinition.Definition.TexturePath);

        Assert.Equal("bee", BeeModelDefinition.Definition.Name);
        Assert.Equal(64, BeeModelDefinition.Definition.TextureWidth);
        Assert.Equal(64, BeeModelDefinition.Definition.TextureHeight);
        Assert.Equal("entity/bee/bee", BeeModelDefinition.Definition.TexturePath);
    }

    private static EntityRenderState State(bool isAngry, bool hasNectar) =>
        new(1, BeeModelDefinition.BeeEntityTypeId, 0f, 0f, 0f, 0f, 0f, IsAngry: isAngry, HasNectar: hasNectar);

    private static EntityModelPart[] ExpectedAllayParts()
    {
        var parts = new[]
        {
            RawModelPartFactory.FromVanilla("root", 0f, 23.5f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            RawModelPartFactory.FromVanillaChild("head", "root", 0f, -3.99f, 0f, -2.5f, -5f, -2.5f, 5f, 5f, 5f, 0, 0),
            RawModelPartFactory.FromVanillaChild("body", "root", 0f, -4f, 0f, -1.5f, 0f, -1f, 3f, 4f, 2f, 0, 10),
            RawModelPartFactory.FromVanillaChild("body_inner", "body", 0f, 0f, 0f, -1.5f, 0f, -1f, 3f, 5f, 2f, 0, 16, grow: -0.2f),
            RawModelPartFactory.FromVanillaChild("right_arm", "body", -1.75f, 0.5f, 0f, -0.75f, -0.5f, -1f, 1f, 4f, 2f, 23, 0, grow: -0.01f),
            RawModelPartFactory.FromVanillaChild("left_arm", "body", 1.75f, 0.5f, 0f, -0.25f, -0.5f, -1f, 1f, 4f, 2f, 23, 6, grow: -0.01f),
            RawModelPartFactory.FromVanillaChild("right_wing", "body", -0.5f, 0f, 0.6f, 0f, 1f, 0f, 0f, 5f, 8f, 16, 14),
            RawModelPartFactory.FromVanillaChild("left_wing", "body", 0.5f, 0f, 0.6f, 0f, 1f, 0f, 0f, 5f, 8f, 16, 14)
        };

        for (var i = 0; i < parts.Length; i++)
        {
            parts[i] = parts[i] with { Kind = EntityGeometryLayerKind.Translucent };
        }

        return parts;
    }

    private static EntityModelPart[] ExpectedBatParts() =>
    [
        RawModelPartFactory.FromVanilla("body", 0f, 17f, 0f, -1.5f, 0f, -1f, 3f, 5f, 2f, 0, 0),
        RawModelPartFactory.FromVanillaChild("feet", "body", 0f, 5f, 0f, -1.5f, 0f, 0f, 3f, 2f, 0f, 16, 16),
        RawModelPartFactory.FromVanillaChild("right_wing", "body", -1.5f, 0f, 0f, -2f, -2f, 0f, 2f, 7f, 0f, 12, 0),
        RawModelPartFactory.FromVanillaChild("right_wing_tip", "right_wing", -2f, 0f, 0f, -6f, -2f, 0f, 6f, 8f, 0f, 16, 0),
        RawModelPartFactory.FromVanillaChild("left_wing", "body", 1.5f, 0f, 0f, 0f, -2f, 0f, 2f, 7f, 0f, 12, 7),
        RawModelPartFactory.FromVanillaChild("left_wing_tip", "left_wing", 2f, 0f, 0f, 0f, -2f, 0f, 6f, 8f, 0f, 16, 8),
        RawModelPartFactory.FromVanilla("head", 0f, 17f, 0f, -2f, -3f, -1f, 4f, 3f, 2f, 0, 7),
        RawModelPartFactory.FromVanillaChild("right_ear", "head", -1.5f, -2f, 0f, -2.5f, -4f, 0f, 3f, 5f, 0f, 1, 15),
        RawModelPartFactory.FromVanillaChild("left_ear", "head", 1.1f, -3f, 0f, -0.1f, -3f, 0f, 3f, 5f, 0f, 8, 15)
    ];

    private static EntityModelPart[] ExpectedBeeParts() =>
    [
        RawModelPartFactory.FromVanilla("bone", 0f, 19f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
        RawModelPartFactory.FromVanillaChild("body", "bone", 0f, 0f, 0f, -3.5f, -4f, -5f, 7f, 7f, 10f, 0, 0),
        RawModelPartFactory.FromVanillaChild("stinger", "body", 0f, 0f, 0f, 0f, -1f, 5f, 0f, 1f, 2f, 26, 7),
        RawModelPartFactory.FromVanillaChild("left_antenna", "body", 0f, -2f, -5f, 1.5f, -2f, -3f, 1f, 2f, 3f, 2, 0),
        RawModelPartFactory.FromVanillaChild("right_antenna", "body", 0f, -2f, -5f, -2.5f, -2f, -3f, 1f, 2f, 3f, 2, 3),
        RawModelPartFactory.FromVanillaChild("right_wing", "bone", -1.5f, -4f, -3f, -9f, 0f, 0f, 9f, 0f, 6f, 0, 18,
            rotationY: -0.2618f, grow: 0.001f),
        RawModelPartFactory.FromVanillaChild("left_wing", "bone", 1.5f, -4f, -3f, 0f, 0f, 0f, 9f, 0f, 6f, 0, 18,
            mirror: true, rotationY: 0.2618f, grow: 0.001f),
        RawModelPartFactory.FromVanillaChild("front_legs", "bone", 1.5f, 3f, -2f, -5f, 0f, 0f, 7f, 2f, 0f, 26, 1),
        RawModelPartFactory.FromVanillaChild("middle_legs", "bone", 1.5f, 3f, 0f, -5f, 0f, 0f, 7f, 2f, 0f, 26, 3),
        RawModelPartFactory.FromVanillaChild("back_legs", "bone", 1.5f, 3f, 2f, -5f, 0f, 0f, 7f, 2f, 0f, 26, 5)
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
