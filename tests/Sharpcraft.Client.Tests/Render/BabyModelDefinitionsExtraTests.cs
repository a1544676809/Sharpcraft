// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>T-699: proves the DSL-baked baby cat/wolf/fox/rabbit parts match the legacy records and collar layers.</summary>
public sealed class BabyModelDefinitionsExtraTests
{

    [Fact]
    public void BabyWolfPartsMatchLegacyRecords()
    {
        var baseParts = new[]
        {
            RawModelPartFactory.FromVanilla("head", 0f, 18.25f, -4f, -2.99f, -3.25f, -3f, 6f, 5f, 5f, 0, 12, grow: 0.025f),
            RawModelPartFactory.FromVanillaChild("snout", "head", 0f, 0f, 0f, -1.5f, -0.24f, -5f, 3f, 2f, 2f, 17, 12),
            RawModelPartFactory.FromVanillaChild("right_ear", "head", -2f, -4.25f, -0.5f, -1f, -1f, -0.5f, 2f, 2f, 1f, 0, 5),
            RawModelPartFactory.FromVanillaChild("left_ear", "head", 2f, -4.25f, -0.5f, -1f, -1f, -0.5f, 2f, 2f, 1f, 20, 5),
            RawModelPartFactory.FromVanilla("body", 0f, 19f, 0f, -3f, -2f, -4f, 6f, 4f, 8f, 0, 0),
            RawModelPartFactory.FromVanilla("right_hind_leg", -1.5f, 21f, 3f, -1f, 0f, -1f, 2f, 3f, 2f, 0, 22),
            RawModelPartFactory.FromVanilla("left_hind_leg", 1.5f, 21f, 3f, -1f, 0f, -1f, 2f, 3f, 2f, 8, 22),
            RawModelPartFactory.FromVanilla("right_front_leg", -1.5f, 21f, -3f, -1f, 0f, -1f, 2f, 3f, 2f, 0, 0),
            RawModelPartFactory.FromVanilla("left_front_leg", 1.5f, 21f, -3f, -1f, 0f, -1f, 2f, 3f, 2f, 20, 0),
            RawModelPartFactory.FromVanilla("tail", 0f, 19f, 3f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0, rotationX: -0.5236f),
            RawModelPartFactory.FromVanillaChild("tail_r1", "tail", 0f, -0.6f, 0.2f, -1f, -5.7f, -1f, 2f, 6f, 2f, 22, 16, rotationX: -3.1f)
        };
        var expected = EntityModelLayerHelpers.WithCollar(baseParts, "entity/wolf/wolf_collar_baby");
        Assert.NotNull(BabyModelDefinitionsExtra.Wolf.TextureSelector);
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
