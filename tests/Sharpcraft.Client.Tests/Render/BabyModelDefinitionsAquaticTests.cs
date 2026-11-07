// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>T-696: proves the DSL-baked baby squid/bee/dolphin/axolotl parts match the legacy records.</summary>
public sealed class BabyModelDefinitionsAquaticTests
{
    [Fact]
    public void BabySquidPartsMatchLegacyRecords()
    {
        var expected = new List<EntityModelPart>
        {
            LegacyModelPartFactory.FromVanilla("body", 0f, 13f, 0f, -4f, -5f, -4f, 8f, 10f, 8f, 0, 0)
        };
        for (var i = 0; i < 8; i++)
        {
            var angle = i * MathF.PI * 2f / 8f;
            var x = MathF.Cos(angle) * 3f;
            var z = MathF.Sin(angle) * 3f;
            var yRot = i * MathF.PI * -2f / 8f + (MathF.PI / 2f);
            expected.Add(LegacyModelPartFactory.FromVanilla("tentacle" + i, x, 18.5f, z, -1f, -0.5f, -1f, 2f, 6f, 2f, 0, 18, rotationY: -yRot));
        }

        AssertPartsMatch([.. expected], BabyModelDefinitionsAquatic.Squid.Parts);
    }

    [Fact]
    public void BabyBeePartsMatchLegacyRecords()
    {
        var expected = new[]
        {
            LegacyModelPartFactory.FromVanilla("bone", 0f, 19.6667f, -1.8567f, 1f, -1.6667f, -2.1633f, 1f, 2f, 2f, 6, 12),
            LegacyModelPartFactory.FromVanillaChild("antenna_left", "bone", 0f, 0f, 0f, -2f, -1.6667f, -2.1933f, 1f, 2f, 2f, 0, 12),
            LegacyModelPartFactory.FromVanillaChild("body", "bone", 0f, 1.3333f, 2.3567f, -2f, -2f, -2.5f, 4f, 4f, 5f, 0, 0),
            LegacyModelPartFactory.FromVanillaChild("stinger", "body", 0f, 0.5f, 2.5f, 0f, -0.5f, 0f, 0f, 1f, 1f, 13, 2),
            LegacyModelPartFactory.FromVanillaChild("right_wing", "bone", -1f, -0.6667f, 0.8567f, -3f, 0f, 0f, 3f, 0f, 3f, 3, 9, rotationX: 0.2182f, rotationY: 0.3491f),
            LegacyModelPartFactory.FromVanillaChild("left_wing", "bone", 1f, -0.6667f, 0.8567f, 0f, 0f, 0f, 3f, 0f, 3f, -3, 9, mirror: true, rotationX: 0.2182f, rotationY: -0.3491f),
            LegacyModelPartFactory.FromVanillaChild("front_legs", "bone", 0f, 3.3333f, 1.8567f, -1.5f, 0f, 0f, 3f, 1f, 0f, 13, 0),
            LegacyModelPartFactory.FromVanillaChild("middle_legs", "bone", 0f, 3.3333f, 2.8567f, -1.5f, 0f, 0f, 3f, 1f, 0f, 13, 1),
            LegacyModelPartFactory.FromVanillaChild("back_legs", "bone", 0f, 3.3333f, 3.8567f, -1.5f, 0f, 0f, 3f, 1f, 0f, 13, 2)
        };
        AssertPartsMatch(expected, BabyModelDefinitionsAquatic.Bee.Parts);
        Assert.NotNull(BabyModelDefinitionsAquatic.Bee.TextureSelector);
    }

    [Fact]
    public void BabyDolphinPartsMatchLegacyRecords()
    {
        var expected = new[]
        {
            LegacyModelPartFactory.FromVanilla("body", 0f, 21.5f, 0f, -3f, -2.5f, -4f, 6f, 5f, 8f, 20, 0),
            LegacyModelPartFactory.FromVanillaChild("head", "body", 0f, 1f, -4f, -3f, -3.5f, -4f, 6f, 5f, 4f, 0, 0),
            LegacyModelPartFactory.FromVanillaChild("nose", "head", 0f, 0.5f, -4f, -1f, -1f, -2f, 2f, 2f, 2f, 0, 9),
            LegacyModelPartFactory.FromVanillaChild("left_fin", "body", 1.8f, 0.85f, -2.6f, -0.5f, -1.5f, -0.5f, 1f, 3f, 6f, 34, 18, rotationX: 0.8727f, rotationZ: 1.7017f),
            LegacyModelPartFactory.FromVanillaChild("right_fin", "body", -1.8f, 0.85f, -2.6f, -0.5f, -1.5f, -0.5f, 1f, 3f, 6f, 48, 18, mirror: true, rotationX: 0.8727f, rotationZ: -1.7017f),
            LegacyModelPartFactory.FromVanillaChild("tail", "body", 0f, 1f, 4f, -2f, -1.5f, 0f, 4f, 3f, 7f, 0, 13),
            LegacyModelPartFactory.FromVanillaChild("tail_fin", "tail", 0f, 0f, 6f, -4f, -0.5f, -1f, 8f, 1f, 4f, 22, 13),
            LegacyModelPartFactory.FromVanillaChild("back_fin", "body", 0f, -1f, -2.7f, -0.5f, -1f, 1f, 1f, 3f, 4f, 42, 0, rotationX: 0.8727f)
        };
        AssertPartsMatch(expected, BabyModelDefinitionsAquatic.Dolphin.Parts);
    }

    [Fact]
    public void BabyAxolotlPartsMatchLegacyRecords()
    {
        var expected = new[]
        {
            LegacyModelPartFactory.FromVanilla("root", 0f, 24f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            LegacyModelPartFactory.FromVanillaChild("body", "root", 0f, -1.25f, 1.75f, -2f, -0.75f, -2.75f, 4f, 2f, 6f, 0, 0),
            LegacyModelPartFactory.FromVanillaChild("body_fin", "body", 0f, 0f, 0f, 0f, -1.75f, -2.75f, 0f, 3f, 5f, 0, 12),
            LegacyModelPartFactory.FromVanillaChild("right_front_leg", "body", -2f, 0.25f, -1.25f, -3f, 0f, -0.5f, 3f, 0f, 1f, 20, 16),
            LegacyModelPartFactory.FromVanillaChild("right_hind_leg", "body", -2f, 0.25f, 1.75f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0, rotationY: 1.5708f, rotationZ: 1.5708f),
            LegacyModelPartFactory.FromVanillaChild("right_leg_r1", "right_hind_leg", 0f, 0f, 0f, 0f, 0f, -0.5f, 3f, 0f, 1f, 20, 14, rotationX: -1.5708f, rotationZ: 1.5708f),
            LegacyModelPartFactory.FromVanillaChild("left_front_leg", "body", 2f, 0.25f, -1.25f, 0f, 0f, -0.5f, 3f, 0f, 1f, 20, 13),
            LegacyModelPartFactory.FromVanillaChild("left_hind_leg", "body", 2f, 0.25f, 1.75f, 0f, 0f, -0.5f, 3f, 0f, 1f, 20, 14),
            LegacyModelPartFactory.FromVanillaChild("tail", "body", 0f, -0.25f, 3.25f, 0f, -1.5f, -1f, 0f, 3f, 8f, 10, 9),
            LegacyModelPartFactory.FromVanillaChild("head", "body", 0f, 0.25f, -2.75f, -3f, -2f, -4f, 6f, 3f, 4f, 0, 8),
            LegacyModelPartFactory.FromVanillaChild("left_gills", "head", 3f, -0.5f, -2f, 0f, -3.5f, 0f, 3f, 5f, 0f, 20, 8),
            LegacyModelPartFactory.FromVanillaChild("right_gills", "head", -3f, -0.5f, -2f, -3f, -3.5f, 0f, 3f, 5f, 0f, 20, 3),
            LegacyModelPartFactory.FromVanillaChild("top_gills", "head", 0f, -2f, -2f, -3f, -3f, 0f, 6f, 3f, 0f, 20, 0)
        };
        AssertPartsMatch(expected, BabyModelDefinitionsAquatic.Axolotl.Parts);
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
