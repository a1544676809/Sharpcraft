// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>T-698: proves the DSL-baked baby nautilus/happy ghast parts match the legacy records.</summary>
public sealed class BabyModelDefinitionsFinalTests
{
    [Fact]
    public void BabyNautilusPartsMatchLegacyRecords()
    {
        var expected = new[]
        {
            PlayerModelDefinition.FromVanilla("root", -0.5f, 28f, -0.5f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("shell", "root", 3f, -8f, -2f, -6f, -4f, -1f, 7f, 4f, 7f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("shell_mid", "shell", 0f, 0f, 0f, -6f, 0f, -1f, 7f, 4f, 9f, 0, 11),
            PlayerModelDefinition.FromVanillaChild("shell_back", "shell", 0f, 0f, 0f, -6f, 0f, 5f, 7f, 4f, 0f, 23, 11),
            PlayerModelDefinition.FromVanillaChild("body", "root", 0.5f, -5f, 3f, -2.5f, -3.01f, -1f, 5f, 4f, 7f, 0, 24),
            PlayerModelDefinition.FromVanillaChild("body_back", "body", 0f, 0f, 0f, -2.5f, -3.01f, 4.1f, 5f, 4f, 0f, 0, 35),
            PlayerModelDefinition.FromVanillaChild("upper_mouth", "body", 0f, -2.01f, 3.9f, -2.5f, -1f, 0f, 5f, 2f, 2f, 24, 24, grow: -0.001f),
            PlayerModelDefinition.FromVanillaChild("inner_mouth", "body", 0f, -1.01f, 4.9f, -1.5f, -1f, -1f, 3f, 2f, 2f, 24, 32),
            PlayerModelDefinition.FromVanillaChild("lower_mouth", "body", 0f, -0.01f, 3.9f, -2.5f, -1f, 0f, 5f, 2f, 2f, 24, 28, grow: -0.001f)
        };
        AssertPartsMatch(expected, BabyModelDefinitionsFinal.Nautilus.Parts);
    }

    [Fact]
    public void BabyHappyGhastPartsMatchLegacyRecords()
    {
        var expected = new List<EntityModelPart>
        {
            PlayerModelDefinition.FromVanilla("body", 0f, 16f, 0f, -8f, -8f, -8f, 16f, 16f, 16f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("inner_body", "body", 0f, 8f, 0f, -8f, -16f, -8f, 16f, 16f, 16f, 0, 32, grow: -0.5f)
        };
        (float X, float Y, float Z, float Length)[] tentacles =
        [
            (-3.75f, 7f, -5f, 5f), (1.25f, 7f, -5f, 7f), (6.25f, 7f, -5f, 4f),
            (-6.25f, 7f, 0f, 5f), (-1.25f, 7f, 0f, 5f), (3.75f, 7f, 0f, 7f),
            (-3.75f, 7f, 5f, 8f), (1.25f, 7f, 5f, 8f), (6.25f, 7f, 5f, 5f)
        ];
        for (var i = 0; i < tentacles.Length; i++)
        {
            var (x, y, z, length) = tentacles[i];
            expected.Add(PlayerModelDefinition.FromVanillaChild("tentacle" + i, "body", x, y, z, -1f, 0f, -1f, 2f, length, 2f, 0, 0));
        }

        AssertPartsMatch([.. expected], BabyModelDefinitionsFinal.HappyGhast.Parts);
        Assert.Equal(0.95f, BabyModelDefinitionsFinal.HappyGhast.Scale);
    }

    [Fact]
    public void BabyGlowSquidSharesSquidParts()
    {
        Assert.Same(BabyModelDefinitionsAquatic.Squid.Parts, BabyModelDefinitionsFinal.GlowSquid.Parts);
        Assert.Equal("entity/squid/glow_squid_baby", BabyModelDefinitionsFinal.GlowSquid.TexturePath);
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
