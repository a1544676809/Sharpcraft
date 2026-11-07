// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// T-693: proves the DSL-baked Parrot/Turtle/ZombieNautilus parts are value-identical to the legacy records and that
/// variants, coral visibility and metadata remain unchanged.
/// </summary>
public sealed class ParrotTurtleZombieNautilusModelDefinitionTests
{
    [Fact]
    public void ParrotPartsMatchLegacyRecords()
    {
        var expected = new[]
        {
            LegacyModelPartFactory.FromVanilla("body", 0f, 16.5f, -3f, -1.5f, 0f, -1.5f, 3f, 6f, 3f, 2, 8, rotationX: -0.4937f),
            LegacyModelPartFactory.FromVanilla("tail", 0f, 21.07f, 1.16f, -1.5f, -1f, -1f, 3f, 4f, 1f, 22, 1, rotationX: -1.015f),
            LegacyModelPartFactory.FromVanilla("left_wing", 1.5f, 16.94f, -2.76f, -0.5f, 0f, -1.5f, 1f, 5f, 3f, 19, 8, rotationX: 0.6981f, rotationY: MathF.PI),
            LegacyModelPartFactory.FromVanilla("right_wing", -1.5f, 16.94f, -2.76f, -0.5f, 0f, -1.5f, 1f, 5f, 3f, 19, 8, rotationX: 0.6981f, rotationY: MathF.PI),
            LegacyModelPartFactory.FromVanilla("head", 0f, 15.69f, -2.76f, -1f, -1.5f, -1f, 2f, 3f, 2f, 2, 2),
            LegacyModelPartFactory.FromVanillaChild("head2", "head", 0f, -2f, -1f, -1f, -0.5f, -2f, 2f, 1f, 4f, 10, 0),
            LegacyModelPartFactory.FromVanillaChild("beak1", "head", 0f, -0.5f, -1.5f, -0.5f, -1f, -0.5f, 1f, 2f, 1f, 11, 7),
            LegacyModelPartFactory.FromVanillaChild("beak2", "head", 0f, -1.75f, -2.45f, -0.5f, 0f, -0.5f, 1f, 2f, 1f, 16, 7),
            LegacyModelPartFactory.FromVanillaChild("feather", "head", 0f, -2.15f, 0.15f, 0f, -4f, -2f, 0f, 5f, 4f, 2, 18, rotationX: -0.2214f),
            LegacyModelPartFactory.FromVanilla("left_leg", 1f, 22f, -1.05f, -0.5f, 0f, -0.5f, 1f, 2f, 1f, 14, 18, rotationX: 0.0299f),
            LegacyModelPartFactory.FromVanilla("right_leg", -1f, 22f, -1.05f, -0.5f, 0f, -0.5f, 1f, 2f, 1f, 14, 18, rotationX: 0.0299f)
        };
        AssertPartsMatch(expected, ParrotModelDefinition.Definition.Parts);
        foreach (var key in ParrotModelDefinition.VariantKeys)
        {
            AssertPartsMatch(expected, ParrotModelDefinition.Variants[key].Parts);
        }
    }

    [Fact]
    public void TurtlePartsMatchLegacyRecords()
    {
        var expected = new[]
        {
            LegacyModelPartFactory.FromVanilla("head", 0f, 19f, -10f, -3f, -1f, -3f, 6f, 5f, 6f, 3, 0),
            LegacyModelPartFactory.FromVanilla("body", 0f, 11f, -10f, -9.5f, 3f, -10f, 19f, 20f, 6f, 7, 37, rotationX: -MathF.PI / 2f),
            LegacyModelPartFactory.FromVanilla("belly", 0f, 11f, -10f, -5.5f, 3f, -13f, 11f, 18f, 3f, 31, 1, rotationX: -MathF.PI / 2f),
            LegacyModelPartFactory.FromVanilla("egg_belly", 0f, 11f, -10f, -4.5f, 3f, -14f, 9f, 18f, 1f, 70, 33, rotationX: -MathF.PI / 2f),
            LegacyModelPartFactory.FromVanilla("right_hind_leg", -3.5f, 22f, 11f, -2f, 0f, 0f, 4f, 1f, 10f, 1, 23),
            LegacyModelPartFactory.FromVanilla("left_hind_leg", 3.5f, 22f, 11f, -2f, 0f, 0f, 4f, 1f, 10f, 1, 12),
            LegacyModelPartFactory.FromVanilla("right_front_leg", -5f, 21f, -4f, -13f, 0f, -2f, 13f, 1f, 5f, 27, 30),
            LegacyModelPartFactory.FromVanilla("left_front_leg", 5f, 21f, -4f, 0f, 0f, -2f, 13f, 1f, 5f, 27, 24)
        };
        AssertPartsMatch(expected, TurtleModelDefinition.Definition.Parts);
    }

    [Fact]
    public void ZombieNautilusCoralMatchesLegacyRecords()
    {
        var warm = ZombieNautilusModelDefinition.Variants["warm"];
        Assert.Equal(NautilusModelDefinition.Definition.Parts.Count + 12, warm.Parts.Count);
        for (var i = 0; i < NautilusModelDefinition.Definition.Parts.Count; i++)
        {
            ModelPartAssertions.AssertEqual(NautilusModelDefinition.Definition.Parts[i], warm.Parts[i]);
        }

        var expectedCoral = ExpectedCoralParts();
        for (var i = 0; i < expectedCoral.Length; i++)
        {
            var actual = warm.Parts[NautilusModelDefinition.Definition.Parts.Count + i];
            ModelPartAssertions.AssertEqual(expectedCoral[i], actual);
            Assert.NotNull(actual.VisibleWhen);
            Assert.True(actual.VisibleWhen!(new Sharpcraft.Client.World.EntityRenderState(1, 1, 0f, 0f, 0f, 0f, 0f)));
        }

        Assert.Equal("zombie_nautilus", ZombieNautilusModelDefinition.Definition.Name);
        Assert.Equal("entity/nautilus/zombie_nautilus_coral", warm.TexturePath);
    }

    private static EntityModelPart[] ExpectedCoralParts()
    {
        var parts = new List<EntityModelPart>
        {
            LegacyModelPartFactory.FromVanillaChild("corals", "shell", 8f, 4.5f, -8f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            LegacyModelPartFactory.FromVanillaChild("yellow_coral", "corals", 0f, -11f, 11f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            LegacyModelPartFactory.FromVanillaChild("yellow_coral_second", "yellow_coral", 0f, 0f, 2f, -4.5f, -3.5f, 0f, 6f, 8f, 0f, 0, 85, rotationY: -0.7854f),
            LegacyModelPartFactory.FromVanillaChild("yellow_coral_first", "yellow_coral", 0f, 0f, 0f, -4.5f, -3.5f, 0f, 6f, 8f, 0f, 0, 85, rotationY: 0.7854f),
            LegacyModelPartFactory.FromVanillaChild("pink_coral", "corals", -12.5f, -18f, 11f, -4.5f, 4.5f, 0f, 6f, 0f, 8f, -8, 94),
            LegacyModelPartFactory.FromVanillaChild("pink_coral_second", "pink_coral", -1.5f, 4.5f, 4f, -3f, 0f, -4f, 6f, 0f, 8f, -8, 94, rotationZ: 1.5708f),
            LegacyModelPartFactory.FromVanillaChild("blue_coral", "corals", -14f, 0f, 5.5f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            LegacyModelPartFactory.FromVanillaChild("blue_second", "blue_coral", 0f, 0f, -2f, -3.5f, -5.5f, 0f, 5f, 10f, 0f, 0, 102, rotationY: 0.7854f),
            LegacyModelPartFactory.FromVanillaChild("blue_first", "blue_coral", 0f, 0f, 0f, -3.5f, -5.5f, 0f, 5f, 10f, 0f, 0, 102, rotationY: -0.7854f),
            LegacyModelPartFactory.FromVanillaChild("red_coral", "corals", 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            LegacyModelPartFactory.FromVanillaChild("red_coral_second", "red_coral", -0.5f, -1f, 1.5f, -2.5f, -5.5f, 0f, 4f, 10f, 0f, 0, 112, rotationY: -0.829f),
            LegacyModelPartFactory.FromVanillaChild("red_coral_first", "red_coral", 0f, 0f, 0f, -4.5f, -5.5f, 0f, 6f, 10f, 0f, 0, 112, rotationY: 0.7854f)
        };
        for (var i = 0; i < parts.Count; i++)
        {
            parts[i] = parts[i] with { VisibleWhen = static state => state.Equipment[6] <= 0 };
        }

        return [.. parts];
    }

    private static void AssertPartsMatch(EntityModelPart[] expected, IReadOnlyList<EntityModelPart> actual)
    {
        Assert.Equal(expected.Length, actual.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            ModelPartAssertions.AssertEqual(expected[i], actual[i]);
        }
    }
}
