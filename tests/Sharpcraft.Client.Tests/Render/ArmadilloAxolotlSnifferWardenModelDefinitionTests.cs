// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// T-686: proves the DSL-baked Armadillo/Axolotl/Sniffer/Warden parts (including Warden emissive layers) are
/// value-identical to the legacy FromVanilla/FromVanillaChild records, and that their nested order/parent links and
/// model metadata remain unchanged.
/// </summary>
public sealed class ArmadilloAxolotlSnifferWardenModelDefinitionTests
{
    [Fact]
    public void ArmadilloPartsMatchLegacyRecords()
    {
        var expected = new[]
        {
            LegacyModelPartFactory.FromVanilla("body", 0f, 21f, 4f, -4f, -7f, -10f, 8f, 8f, 12f, 0, 40) with { VisibleWhen = Unrolled },
            LegacyModelPartFactory.FromVanillaChild("body_outer", "body", 0f, 0f, 0f, -4f, -7f, -10f, 8f, 8f, 12f, 0, 20, grow: 0.3f) with { VisibleWhen = Unrolled },
            LegacyModelPartFactory.FromVanillaChild("tail", "body", 0f, -3f, 1f, -0.5f, -0.0865f, 0.0933f, 1f, 6f, 1f, 44, 53, rotationX: 0.5061f) with { VisibleWhen = Unrolled },
            LegacyModelPartFactory.FromVanillaChild("head", "body", 0f, -2f, -11f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0) with { VisibleWhen = Unrolled },
            LegacyModelPartFactory.FromVanillaChild("head_cube", "head", 0f, 0f, 0f, -1.5f, -1f, -1f, 3f, 5f, 2f, 43, 15, rotationX: -0.3927f),
            LegacyModelPartFactory.FromVanillaChild("right_ear", "head", -1f, -1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0) with { VisibleWhen = Unrolled },
            LegacyModelPartFactory.FromVanillaChild("right_ear_cube", "right_ear", -0.5f, 0f, -0.6f, -2f, -3f, 0f, 2f, 5f, 0f, 43, 10,
                rotationX: 0.1886f, rotationY: -0.3864f, rotationZ: -0.0718f),
            LegacyModelPartFactory.FromVanillaChild("left_ear", "head", 1f, -2f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0) with { VisibleWhen = Unrolled },
            LegacyModelPartFactory.FromVanillaChild("left_ear_cube", "left_ear", 0.5f, 1f, -0.6f, 0f, -3f, 0f, 2f, 5f, 0f, 47, 10,
                rotationX: 0.1886f, rotationY: 0.3864f, rotationZ: 0.0718f),
            LegacyModelPartFactory.FromVanilla("right_hind_leg", -2f, 21f, 4f, -1f, 0f, -1f, 2f, 3f, 2f, 51, 31) with { VisibleWhen = Unrolled },
            LegacyModelPartFactory.FromVanilla("left_hind_leg", 2f, 21f, 4f, -1f, 0f, -1f, 2f, 3f, 2f, 42, 31) with { VisibleWhen = Unrolled },
            LegacyModelPartFactory.FromVanilla("right_front_leg", -2f, 21f, -4f, -1f, 0f, -1f, 2f, 3f, 2f, 51, 43) with { VisibleWhen = Unrolled },
            LegacyModelPartFactory.FromVanilla("left_front_leg", 2f, 21f, -4f, -1f, 0f, -1f, 2f, 3f, 2f, 42, 43) with { VisibleWhen = Unrolled },
            LegacyModelPartFactory.FromVanilla("cube", 0f, 24f, 0f, -5f, -10f, -6f, 10f, 10f, 10f, 0, 0) with { VisibleWhen = Rolled }
        };
        AssertPartsMatch(expected, ArmadilloModelDefinition.Definition.Parts);

        var unrolled = new Sharpcraft.Client.World.EntityRenderState(1, 1, 0f, 0f, 0f, 0f, 0f);
        var rolled = new Sharpcraft.Client.World.EntityRenderState(1, 1, 0f, 0f, 0f, 0f, 0f, IsHidingInShell: true);
        Assert.True(ArmadilloModelDefinition.Definition.Parts[0].VisibleWhen!(unrolled));
        Assert.False(ArmadilloModelDefinition.Definition.Parts[0].VisibleWhen!(rolled));
        Assert.False(ArmadilloModelDefinition.Definition.Parts[^1].VisibleWhen!(unrolled));
        Assert.True(ArmadilloModelDefinition.Definition.Parts[^1].VisibleWhen!(rolled));
    }

    [Fact]
    public void AxolotlPartsMatchLegacyRecords()
    {
        var expected = new[]
        {
            LegacyModelPartFactory.FromVanilla("body", 0f, 19.5f, 5f, -4f, -2f, -9f, 8f, 4f, 10f, 0, 11),
            LegacyModelPartFactory.FromVanillaChild("body_layer", "body", 0f, 0f, 0f, 0f, -3f, -8f, 0f, 5f, 9f, 2, 17),
            LegacyModelPartFactory.FromVanillaChild("head", "body", 0f, 0f, -9f, -4f, -3f, -5f, 8f, 5f, 5f, 0, 1, grow: 0.001f),
            LegacyModelPartFactory.FromVanillaChild("top_gills", "head", 0f, -3f, -1f, -4f, -3f, 0f, 8f, 3f, 0f, 3, 37, grow: 0.001f),
            LegacyModelPartFactory.FromVanillaChild("left_gills", "head", -4f, 0f, -1f, -3f, -5f, 0f, 3f, 7f, 0f, 0, 40, grow: 0.001f),
            LegacyModelPartFactory.FromVanillaChild("right_gills", "head", 4f, 0f, -1f, 0f, -5f, 0f, 3f, 7f, 0f, 11, 40, grow: 0.001f),
            LegacyModelPartFactory.FromVanillaChild("right_hind_leg", "body", -3.5f, 1f, -1f, -2f, 0f, 0f, 3f, 5f, 0f, 2, 13, grow: 0.001f),
            LegacyModelPartFactory.FromVanillaChild("left_hind_leg", "body", 3.5f, 1f, -1f, -1f, 0f, 0f, 3f, 5f, 0f, 2, 13, grow: 0.001f),
            LegacyModelPartFactory.FromVanillaChild("right_front_leg", "body", -3.5f, 1f, -8f, -2f, 0f, 0f, 3f, 5f, 0f, 2, 13, grow: 0.001f),
            LegacyModelPartFactory.FromVanillaChild("left_front_leg", "body", 3.5f, 1f, -8f, -1f, 0f, 0f, 3f, 5f, 0f, 2, 13, grow: 0.001f),
            LegacyModelPartFactory.FromVanillaChild("tail", "body", 0f, 0f, 1f, 0f, -3f, 0f, 0f, 5f, 12f, 2, 19)
        };
        AssertPartsMatch(expected, AxolotlModelDefinition.Definition.Parts);
        foreach (var key in AxolotlModelDefinition.VariantKeys)
        {
            AssertPartsMatch(expected, AxolotlModelDefinition.Variants[key].Parts);
        }
    }

    [Fact]
    public void SnifferPartsMatchLegacyRecords()
    {
        var expected = new[]
        {
            LegacyModelPartFactory.FromVanilla("bone", 0f, 5f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            LegacyModelPartFactory.FromVanillaChild("body", "bone", 0f, 0f, 0f, -12.5f, -14f, -20f, 25f, 29f, 40f, 62, 68),
            LegacyModelPartFactory.FromVanillaChild("body_outer", "body", 0f, 0f, 0f, -12.5f, -14f, -20f, 25f, 24f, 40f, 62, 0, grow: 0.5f),
            LegacyModelPartFactory.FromVanillaChild("body_bottom", "body", 0f, 0f, 0f, -12.5f, 12f, -20f, 25f, 0f, 40f, 87, 68),
            LegacyModelPartFactory.FromVanillaChild("right_front_leg", "bone", -7.5f, 10f, -15f, -3.5f, -1f, -4f, 7f, 10f, 8f, 32, 87),
            LegacyModelPartFactory.FromVanillaChild("right_mid_leg", "bone", -7.5f, 10f, 0f, -3.5f, -1f, -4f, 7f, 10f, 8f, 32, 105),
            LegacyModelPartFactory.FromVanillaChild("right_hind_leg", "bone", -7.5f, 10f, 15f, -3.5f, -1f, -4f, 7f, 10f, 8f, 32, 123),
            LegacyModelPartFactory.FromVanillaChild("left_front_leg", "bone", 7.5f, 10f, -15f, -3.5f, -1f, -4f, 7f, 10f, 8f, 0, 87),
            LegacyModelPartFactory.FromVanillaChild("left_mid_leg", "bone", 7.5f, 10f, 0f, -3.5f, -1f, -4f, 7f, 10f, 8f, 0, 105),
            LegacyModelPartFactory.FromVanillaChild("left_hind_leg", "bone", 7.5f, 10f, 15f, -3.5f, -1f, -4f, 7f, 10f, 8f, 0, 123),
            LegacyModelPartFactory.FromVanillaChild("head", "body", 0f, 6.5f, -19.48f, -6.5f, -7.5f, -11.5f, 13f, 18f, 11f, 8, 15),
            LegacyModelPartFactory.FromVanillaChild("head_flat", "head", 0f, 0f, 0f, -6.5f, 7.5f, -11.5f, 13f, 0f, 11f, 8, 4),
            LegacyModelPartFactory.FromVanillaChild("left_ear", "head", 6.51f, -7.5f, -4.51f, 0f, 0f, -3f, 1f, 19f, 7f, 2, 0),
            LegacyModelPartFactory.FromVanillaChild("right_ear", "head", -6.51f, -7.5f, -4.51f, -1f, 0f, -3f, 1f, 19f, 7f, 48, 0),
            LegacyModelPartFactory.FromVanillaChild("nose", "head", 0f, -4.5f, -11.5f, -6.5f, -2f, -9f, 13f, 2f, 9f, 10, 45),
            LegacyModelPartFactory.FromVanillaChild("lower_beak", "head", 0f, 2.5f, -12.5f, -6.5f, -7f, -8f, 13f, 12f, 9f, 10, 57)
        };
        AssertPartsMatch(expected, SnifferModelDefinition.Definition.Parts);
    }

    [Fact]
    public void WardenPartsAndEmissiveLayersMatchLegacyRecords()
    {
        var baseParts = new[]
        {
            LegacyModelPartFactory.FromVanilla("bone", 0f, 24f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            LegacyModelPartFactory.FromVanillaChild("body", "bone", 0f, -21f, 0f, -9f, -13f, -4f, 18f, 21f, 11f, 0, 0),
            LegacyModelPartFactory.FromVanillaChild("right_ribcage", "body", -7f, -2f, -4f, -2f, -11f, -0.1f, 9f, 21f, 0f, 90, 11),
            LegacyModelPartFactory.FromVanillaChild("left_ribcage", "body", 7f, -2f, -4f, -7f, -11f, -0.1f, 9f, 21f, 0f, 90, 11),
            LegacyModelPartFactory.FromVanillaChild("head", "body", 0f, -13f, 0f, -8f, -16f, -5f, 16f, 16f, 10f, 0, 32),
            LegacyModelPartFactory.FromVanillaChild("right_tendril", "head", -8f, -12f, 0f, -16f, -13f, 0f, 16f, 16f, 0f, 52, 32),
            LegacyModelPartFactory.FromVanillaChild("left_tendril", "head", 8f, -12f, 0f, 0f, -13f, 0f, 16f, 16f, 0f, 58, 0),
            LegacyModelPartFactory.FromVanillaChild("right_arm", "body", -13f, -13f, 1f, -4f, 0f, -4f, 8f, 28f, 8f, 44, 50),
            LegacyModelPartFactory.FromVanillaChild("left_arm", "body", 13f, -13f, 1f, -4f, 0f, -4f, 8f, 28f, 8f, 0, 58),
            LegacyModelPartFactory.FromVanillaChild("right_leg", "bone", -5.9f, -13f, 0f, -3.1f, 0f, -3f, 6f, 13f, 6f, 76, 48),
            LegacyModelPartFactory.FromVanillaChild("left_leg", "bone", 5.9f, -13f, 0f, -2.9f, 0f, -3f, 6f, 13f, 6f, 76, 76)
        };
        var expected = new List<EntityModelPart>(baseParts);
        AddWardenLayer(expected, baseParts, "bioluminescent", "entity/warden/warden_bioluminescent_layer", "head", "left_arm", "right_arm", "left_leg", "right_leg");
        AddWardenLayer(expected, baseParts, "spots", "entity/warden/warden_pulsating_spots_1", "body", "head", "left_arm", "right_arm", "left_leg", "right_leg");
        AddWardenLayer(expected, baseParts, "heart", "entity/warden/warden_heart", "body");
        AddWardenLayer(expected, baseParts, "tendril_glow", "entity/warden/warden", "left_tendril", "right_tendril");
        AssertPartsMatch([.. expected], WardenModelDefinition.Definition.Parts);
    }

    [Fact]
    public void NestedOrderAndMetadataRemain()
    {
        AssertNames(ArmadilloModelDefinition.Definition.Parts,
            ["body", "body_outer", "tail", "head", "head_cube", "right_ear", "right_ear_cube", "left_ear", "left_ear_cube", "right_hind_leg", "left_hind_leg", "right_front_leg", "left_front_leg", "cube"]);
        AssertNames(SnifferModelDefinition.Definition.Parts,
            ["bone", "body", "body_outer", "body_bottom", "right_front_leg", "right_mid_leg", "right_hind_leg", "left_front_leg", "left_mid_leg", "left_hind_leg", "head", "head_flat", "left_ear", "right_ear", "nose", "lower_beak"]);
        Assert.Equal(25, WardenModelDefinition.Definition.Parts.Count);
        Assert.Equal(14, ArmadilloModelDefinition.Definition.Parts.Count);
        Assert.Equal(11, AxolotlModelDefinition.Definition.Parts.Count);
        Assert.Equal(16, SnifferModelDefinition.Definition.Parts.Count);
    }

    private static bool Unrolled(Sharpcraft.Client.World.EntityRenderState state) => !state.IsHidingInShell;
    private static bool Rolled(Sharpcraft.Client.World.EntityRenderState state) => state.IsHidingInShell;

    private static void AddWardenLayer(List<EntityModelPart> parts, EntityModelPart[] baseParts, string prefix, string texturePath, params string[] sourceNames)
    {
        foreach (var sourceName in sourceNames)
        {
            var source = baseParts.Single(part => part.Name == sourceName);
            parts.Add(source with
            {
                Name = prefix + "_" + sourceName,
                PoseSource = sourceName,
                EmissiveOnly = true,
                IncludeInEmissive = true,
                EmissiveTexturePath = texturePath,
                Kind = Sharpcraft.Framework.Rendering.EntityGeometryLayerKind.Cutout
            });
        }
    }

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
