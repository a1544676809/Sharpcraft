// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>T-702: proves the DSL-baked baby humanoid armor mesh matches the legacy records.</summary>
public sealed class BabyHumanoidArmorPartsTests
{
    [Fact]
    public void BabyArmorPartsMatchLegacyRecords()
    {
        var expected = new[]
        {
            LegacyModelPartFactory.FromVanilla("head", 0f, 15f, 0f, -4.5f, -7f, -4.5f, 9f, 8f, 8f, 0, 0),
            LegacyModelPartFactory.FromVanilla("body", 0f, 18f, 0f, -3f, -3f, -1.5f, 6f, 5f, 3f, 0, 17),
            LegacyModelPartFactory.FromVanilla("waist", 0f, 19f, 0f, -3f, -1.2f, -1.49f, 5.9f, 2f, 2.9f, 0, 36),
            LegacyModelPartFactory.FromVanilla("right_arm", -3.5f, 15.5f, 0f, -1f, 0f, -1.53f, 2f, 5f, 3f, 30, 25),
            LegacyModelPartFactory.FromVanilla("left_arm", 3.5f, 15.5f, 0f, -1f, 0f, -1.53f, 2f, 5f, 3f, 30, 17),
            LegacyModelPartFactory.FromVanilla("left_leg", 1.5f, 20f, 0.5f, -2f, -0.2f, -2f, 3f, 4f, 3f, 18, 24),
            LegacyModelPartFactory.FromVanilla("right_leg", -1.5f, 20f, 0.5f, -1f, -0.2f, -2f, 3f, 4f, 3f, 18, 17),
            LegacyModelPartFactory.FromVanilla("right_foot", 1.5f, 20f, 0.5f, -2f, 2.9f, -2f, 3f, 1f, 3f, 0, 25),
            LegacyModelPartFactory.FromVanilla("left_foot", -1.5f, 20f, 0.5f, -1f, 2.9f, -2f, 3f, 1f, 3f, 0, 29, mirror: true)
        };
        AssertPartsMatch(expected, BabyHumanoidArmorParts.Parts);
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
