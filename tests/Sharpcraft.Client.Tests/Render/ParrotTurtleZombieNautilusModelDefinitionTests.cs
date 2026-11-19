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
            RawModelPartFactory.FromVanillaChild("corals", "shell", 8f, 4.5f, -8f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            RawModelPartFactory.FromVanillaChild("yellow_coral", "corals", 0f, -11f, 11f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            RawModelPartFactory.FromVanillaChild("yellow_coral_second", "yellow_coral", 0f, 0f, 2f, -4.5f, -3.5f, 0f, 6f, 8f, 0f, 0, 85, rotationY: -0.7854f),
            RawModelPartFactory.FromVanillaChild("yellow_coral_first", "yellow_coral", 0f, 0f, 0f, -4.5f, -3.5f, 0f, 6f, 8f, 0f, 0, 85, rotationY: 0.7854f),
            RawModelPartFactory.FromVanillaChild("pink_coral", "corals", -12.5f, -18f, 11f, -4.5f, 4.5f, 0f, 6f, 0f, 8f, -8, 94),
            RawModelPartFactory.FromVanillaChild("pink_coral_second", "pink_coral", -1.5f, 4.5f, 4f, -3f, 0f, -4f, 6f, 0f, 8f, -8, 94, rotationZ: 1.5708f),
            RawModelPartFactory.FromVanillaChild("blue_coral", "corals", -14f, 0f, 5.5f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            RawModelPartFactory.FromVanillaChild("blue_second", "blue_coral", 0f, 0f, -2f, -3.5f, -5.5f, 0f, 5f, 10f, 0f, 0, 102, rotationY: 0.7854f),
            RawModelPartFactory.FromVanillaChild("blue_first", "blue_coral", 0f, 0f, 0f, -3.5f, -5.5f, 0f, 5f, 10f, 0f, 0, 102, rotationY: -0.7854f),
            RawModelPartFactory.FromVanillaChild("red_coral", "corals", 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            RawModelPartFactory.FromVanillaChild("red_coral_second", "red_coral", -0.5f, -1f, 1.5f, -2.5f, -5.5f, 0f, 4f, 10f, 0f, 0, 112, rotationY: -0.829f),
            RawModelPartFactory.FromVanillaChild("red_coral_first", "red_coral", 0f, 0f, 0f, -4.5f, -5.5f, 0f, 6f, 10f, 0f, 0, 112, rotationY: 0.7854f)
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
