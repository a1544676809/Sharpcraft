// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// T-689: proves the DSL-baked Guardian/Squid parts are value-identical to the legacy records and that ElderGuardian/
/// GlowSquid scale and texture metadata remain unchanged.
/// </summary>
public sealed class GuardianSquidModelDefinitionTests
{
    [Fact]
    public void GuardianPartsMatchLegacyRecords()
    {
        var parts = new List<EntityModelPart>
        {
            LegacyModelPartFactory.FromVanilla("head", 0f, 0f, 0f, -6f, 10f, -8f, 12f, 12f, 16f, 0, 0),
            LegacyModelPartFactory.FromVanillaChild("head_left", "head", 0f, 0f, 0f, -8f, 10f, -6f, 2f, 12f, 12f, 0, 28),
            LegacyModelPartFactory.FromVanillaChild("head_right", "head", 0f, 0f, 0f, 6f, 10f, -6f, 2f, 12f, 12f, 0, 28, mirror: true),
            LegacyModelPartFactory.FromVanillaChild("head_top", "head", 0f, 0f, 0f, -6f, 8f, -6f, 12f, 2f, 12f, 16, 40),
            LegacyModelPartFactory.FromVanillaChild("head_bottom", "head", 0f, 0f, 0f, -6f, 22f, -6f, 12f, 2f, 12f, 16, 40)
        };
        float[] xRot = [1.75f, 0.25f, 0f, 0f, 0.5f, 0.5f, 0.5f, 0.5f, 1.25f, 0.75f, 0f, 0f];
        float[] yRot = [0f, 0f, 0f, 0f, 0.25f, 1.75f, 1.25f, 0.75f, 0f, 0f, 0f, 0f];
        float[] zRot = [0f, 0f, 0.25f, 1.75f, 0f, 0f, 0f, 0f, 0f, 0f, 0.75f, 1.25f];
        float[] x = [0f, 0f, 8f, -8f, -8f, 8f, 8f, -8f, 0f, 0f, 8f, -8f];
        float[] y = [-8f, -8f, -8f, -8f, 0f, 0f, 0f, 0f, 8f, 8f, 8f, 8f];
        float[] z = [8f, -8f, 0f, 0f, -8f, -8f, 8f, 8f, 8f, -8f, 0f, 0f];
        for (var i = 0; i < 12; i++)
        {
            parts.Add(LegacyModelPartFactory.FromVanillaChild("spike" + i, "head", x[i], 16f + y[i], z[i],
                -1f, -4.5f, -1f, 2f, 9f, 2f, 0, 0,
                rotationX: MathF.PI * xRot[i], rotationY: MathF.PI * yRot[i], rotationZ: MathF.PI * zRot[i]));
        }

        parts.Add(LegacyModelPartFactory.FromVanillaChild("eye", "head", 0f, 0f, -8.25f, -1f, 15f, 0f, 2f, 2f, 1f, 8, 0));
        parts.Add(LegacyModelPartFactory.FromVanillaChild("tail0", "head", 0f, 0f, 0f, -2f, 14f, 7f, 4f, 4f, 8f, 40, 0));
        parts.Add(LegacyModelPartFactory.FromVanillaChild("tail1", "tail0", -1.5f, 0.5f, 14f, 0f, 14f, 0f, 3f, 3f, 7f, 0, 54));
        parts.Add(LegacyModelPartFactory.FromVanillaChild("tail2", "tail1", 0.5f, 0.5f, 6f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0));
        parts.Add(LegacyModelPartFactory.FromVanillaChild("tail2_body", "tail2", 0f, 0f, 0f, 0f, 14f, 0f, 2f, 2f, 6f, 41, 32));
        parts.Add(LegacyModelPartFactory.FromVanillaChild("tail2_fin", "tail2", 0f, 0f, 0f, 1f, 10.5f, 3f, 1f, 9f, 9f, 25, 19));

        AssertPartsMatch([.. parts], GuardianModelDefinition.Definition.Parts);
        AssertPartsMatch([.. parts], GuardianModelDefinition.Elder.Parts);
        Assert.Equal(2.35f, GuardianModelDefinition.Elder.Scale);
    }

    [Fact]
    public void SquidPartsMatchLegacyRecords()
    {
        var expected = new[]
        {
            LegacyModelPartFactory.FromVanilla("body", 0f, 8f, 0f, -6f, -8f, -6f, 12f, 16f, 12f, 0, 0, grow: 0.02f),
            Tentacle(0, 5f, 0f, MathF.PI / 2f),
            Tentacle(1, 3.535f, 3.535f, MathF.PI / 4f),
            Tentacle(2, 0f, 5f, 0f),
            Tentacle(3, -3.535f, 3.535f, -MathF.PI / 4f),
            Tentacle(4, -5f, 0f, -MathF.PI / 2f),
            Tentacle(5, -3.535f, -3.535f, -3f * MathF.PI / 4f),
            Tentacle(6, 0f, -5f, MathF.PI),
            Tentacle(7, 3.535f, -3.535f, 3f * MathF.PI / 4f)
        };
        AssertPartsMatch(expected, SquidModelDefinition.Definition.Parts);
        AssertPartsMatch(expected, SquidModelDefinition.GlowSquid.Parts);
        Assert.Equal("entity/squid/glow_squid", SquidModelDefinition.GlowSquid.TexturePath);
    }

    [Fact]
    public void NamesAndCountsRemain()
    {
        AssertNames(GuardianModelDefinition.Definition.Parts,
            ["head", "head_left", "head_right", "head_top", "head_bottom", "spike0", "spike1", "spike2", "spike3", "spike4", "spike5", "spike6", "spike7", "spike8", "spike9", "spike10", "spike11", "eye", "tail0", "tail1", "tail2", "tail2_body", "tail2_fin"]);
        AssertNames(SquidModelDefinition.Definition.Parts,
            ["body", "tentacle0", "tentacle1", "tentacle2", "tentacle3", "tentacle4", "tentacle5", "tentacle6", "tentacle7"]);
    }

    private static EntityModelPart Tentacle(int index, float x, float z, float yRot) =>
        LegacyModelPartFactory.FromVanilla("tentacle" + index, x, 15f, z, -1f, 0f, -1f, 2f, 18f, 2f, 48, 0, rotationY: yRot);

    private static void AssertNames(IReadOnlyList<EntityModelPart> parts, string[] names)
    {
        Assert.Equal(names.Length, parts.Count);
        for (var i = 0; i < names.Length; i++)
        {
            Assert.Equal(names[i], parts[i].Name);
        }
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
