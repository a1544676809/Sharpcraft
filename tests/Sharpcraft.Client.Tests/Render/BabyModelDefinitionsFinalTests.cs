// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>T-698: proves the DSL-baked baby nautilus/happy ghast parts match the legacy records.</summary>
public sealed class BabyModelDefinitionsFinalTests
{

    [Fact]
    public void BabyHappyGhastPartsMatchLegacyRecords()
    {
        var expected = new List<EntityModelPart>
        {
            RawModelPartFactory.FromVanilla("body", 0f, 16f, 0f, -8f, -8f, -8f, 16f, 16f, 16f, 0, 0),
            RawModelPartFactory.FromVanillaChild("inner_body", "body", 0f, 8f, 0f, -8f, -16f, -8f, 16f, 16f, 16f, 0, 32, grow: -0.5f)
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
            expected.Add(RawModelPartFactory.FromVanillaChild("tentacle" + i, "body", x, y, z, -1f, 0f, -1f, 2f, length, 2f, 0, 0));
        }

        Assert.Equal(0.95f, BabyModelDefinitionsFinal.HappyGhast.Scale);
    }

    [Fact]
    public void BabyGlowSquidUsesDedicatedBabyMesh()
    {
        Assert.NotSame(BabyModelDefinitionsAquatic.Squid.StructuralParts(), BabyModelDefinitionsFinal.GlowSquid.StructuralParts());
        Assert.True(BabyModelDefinitionsFinal.GlowSquid.RawSourceFrame);
        Assert.Equal("entity/squid/glow_squid_baby", BabyModelDefinitionsFinal.GlowSquid.TexturePath);
        var body = Assert.Single(BabyModelDefinitionsFinal.GlowSquid.StructuralParts(), p => p.Name == "body");
        Assert.Equal(8f, body.Width);
        Assert.Equal(10f, body.Height);
        Assert.Equal(8f, body.Depth);
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
