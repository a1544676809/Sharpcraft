// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// T-682: proves the DSL-baked piglin head tree (cube, snout, nostrils, ears) is value-identical to the legacy
/// FromVanilla/FromVanillaChild records with the exact order and parent links, that each definition's leading parts are
/// the adult player parts minus the head, and that the Definition/Brute/Zombified names, paths and counts are unchanged.
/// </summary>
public sealed class PiglinModelDefinitionTests
{
    [Fact]
    public void PiglinHeadTreeMatchesLegacyFromVanillaRecords()
    {
        var head = ExpectedHeadParts();
        foreach (var definition in Definitions())
        {
            Assert.Equal(11, definition.Parts.Count);
            for (var i = 0; i < head.Length; i++)
            {
                var actual = definition.Parts[5 + i];
                ModelPartAssertions.AssertEqual(head[i], actual);
                // The head cube is a root part; every other head part is a child of "head".
                Assert.Equal(head[i].Name, actual.Name);
                Assert.Equal(i == 0 ? null : "head", actual.Parent);
            }
        }
    }

    [Fact]
    public void HumanoidPrefixEqualsAdultPartsMinusHead()
    {
        var expected = new List<EntityModelPart>();
        foreach (var part in PlayerModelDefinition.AdultParts)
        {
            if (part.Name != "head")
            {
                expected.Add(part);
            }
        }

        Assert.Equal(5, expected.Count);
        foreach (var definition in Definitions())
        {
            Assert.Equal(11, definition.Parts.Count);
            for (var i = 0; i < expected.Count; i++)
            {
                ModelPartAssertions.AssertEqual(expected[i], definition.Parts[i]);
            }
        }
    }

    [Fact]
    public void DefinitionBruteZombifiedKeepNamesPathsAndCounts()
    {
        var definition = PiglinModelDefinition.Definition;
        Assert.Equal("piglin", definition.Name);
        Assert.Equal(64, definition.TextureWidth);
        Assert.Equal(64, definition.TextureHeight);
        Assert.Equal("entity/piglin/piglin", definition.TexturePath);
        Assert.Equal(11, definition.Parts.Count);

        var brute = PiglinModelDefinition.Brute;
        Assert.Equal("piglin_brute", brute.Name);
        Assert.Equal(64, brute.TextureWidth);
        Assert.Equal(64, brute.TextureHeight);
        Assert.Equal("entity/piglin/piglin_brute", brute.TexturePath);
        Assert.Equal(11, brute.Parts.Count);

        var zombified = PiglinModelDefinition.Zombified;
        Assert.Equal("zombified_piglin", zombified.Name);
        Assert.Equal(64, zombified.TextureWidth);
        Assert.Equal(64, zombified.TextureHeight);
        Assert.Equal("entity/piglin/zombified_piglin", zombified.TexturePath);
        Assert.Equal(11, zombified.Parts.Count);
    }

    private static IReadOnlyList<EntityModelDefinition> Definitions() =>
        [PiglinModelDefinition.Definition, PiglinModelDefinition.Brute, PiglinModelDefinition.Zombified];

    private static EntityModelPart[] ExpectedHeadParts() =>
    [
        PlayerModelDefinition.FromVanilla("head", 0f, 0f, 0f, -5f, -8f, -4f, 10f, 8f, 8f, 0, 0),
        PlayerModelDefinition.FromVanillaChild("head_nose", "head", 0f, 0f, 0f, -2f, -4f, -5f, 4f, 4f, 1f, 31, 1),
        PlayerModelDefinition.FromVanillaChild("head_nostril_right", "head", 0f, 0f, 0f, 2f, -2f, -5f, 1f, 2f, 1f, 2, 4),
        PlayerModelDefinition.FromVanillaChild("head_nostril_left", "head", 0f, 0f, 0f, -3f, -2f, -5f, 1f, 2f, 1f, 2, 0),
        PlayerModelDefinition.FromVanillaChild("left_ear", "head", 4.5f, -6f, 0f, 0f, 0f, -2f, 1f, 5f, 4f, 51, 6,
            rotationZ: -MathF.PI / 6f),
        PlayerModelDefinition.FromVanillaChild("right_ear", "head", -4.5f, -6f, 0f, -1f, 0f, -2f, 1f, 5f, 4f, 39, 6,
            rotationZ: MathF.PI / 6f)
    ];
}
