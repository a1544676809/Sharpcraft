// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>T-696: proves the DSL-baked baby squid/bee/dolphin/axolotl parts match the legacy records.</summary>
public sealed class BabyModelDefinitionsAquaticTests
{

    [Fact]
    public void BabyBeePartsMatchSourceFrameRecords()
    {
        var expected = new[]
        {
            RawModelPartFactory.FromVanilla("bone", 0f, 19.6667f, -1.8567f, 1f, -1.6667f, -2.1633f, 1f, 2f, 2f, 6, 12),
            RawModelPartFactory.FromVanillaChild("antenna_left", "bone", 0f, 0f, 0f, -2f, -1.6667f, -2.1933f, 1f, 2f, 2f, 0, 12),
            RawModelPartFactory.FromVanillaChild("body", "bone", 0f, 1.3333f, 2.3567f, -2f, -2f, -2.5f, 4f, 4f, 5f, 0, 0),
            RawModelPartFactory.FromVanillaChild("stinger", "body", 0f, 0.5f, 2.5f, 0f, -0.5f, 0f, 0f, 1f, 1f, 13, 2),
            RawModelPartFactory.FromVanillaChild("right_wing", "bone", -1f, -0.6667f, 0.8567f, -3f, 0f, 0f, 3f, 0f, 3f, 3, 9, rotationX: 0.2182f, rotationY: 0.3491f),
            RawModelPartFactory.FromVanillaChild("left_wing", "bone", 1f, -0.6667f, 0.8567f, 0f, 0f, 0f, 3f, 0f, 3f, -3, 9, mirror: true, rotationX: 0.2182f, rotationY: -0.3491f),
            RawModelPartFactory.FromVanillaChild("front_legs", "bone", 0f, 3.3333f, 1.8567f, -1.5f, 0f, 0f, 3f, 1f, 0f, 13, 0),
            RawModelPartFactory.FromVanillaChild("middle_legs", "bone", 0f, 3.3333f, 2.8567f, -1.5f, 0f, 0f, 3f, 1f, 0f, 13, 1),
            RawModelPartFactory.FromVanillaChild("back_legs", "bone", 0f, 3.3333f, 3.8567f, -1.5f, 0f, 0f, 3f, 1f, 0f, 13, 2)
        };
        AssertPartsMatch(expected, BabyModelDefinitionsAquatic.Bee.Parts);
        Assert.NotNull(BabyModelDefinitionsAquatic.Bee.TextureSelector);
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
