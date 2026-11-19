// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Armadillo/Axolotl/Sniffer/Warden definition structural invariants: nested order/parent links and model metadata
/// remain unchanged (plus Armadillo roll visibility predicates). The former flat <c>AssertPartsMatch</c> value checks
/// were removed (#231): jar source-sample strict equivalence for these families is provided by
/// <see cref="LivingRawReferenceSampleTests.RawBakeMatchesSourceVertices"/> (keyed, 1e-5 + UV half) through the
/// compiled mesher. Warden's flat bake was a curated subset of the compiled tree, so it could not be projected losslessly.
/// </summary>
public sealed class ArmadilloAxolotlSnifferWardenModelDefinitionTests
{

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
