// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>T-697: proves the DSL-baked baby horse/llama/camel/donkey parts match the legacy records.</summary>
public sealed class BabyModelDefinitionsEquineTests
{

    [Fact]
    public void BabyLlamaPartsMatchSourceFrameRecords()
    {
        var expected = new[]
        {
            RawModelPartFactory.FromVanilla("head", 0f, 12f, -4f, -3f, -9f, -4f, 6f, 11f, 4f, 0, 0),
            RawModelPartFactory.FromVanillaChild("snout", "head", 0f, 0f, 0f, -1.5f, -7f, -7f, 3f, 3f, 3f, 0, 15),
            RawModelPartFactory.FromVanillaChild("ear_right", "head", 0f, 0f, 0f, 0.5f, -11f, -3f, 2f, 2f, 2f, 20, 4),
            RawModelPartFactory.FromVanillaChild("ear_left", "head", 0f, 0f, 0f, -2.5f, -11f, -3f, 2f, 2f, 2f, 20, 0),
            RawModelPartFactory.FromVanilla("right_hind_leg", -2.5f, 16.5f, 4.5f, -1.4f, -0.5f, -1.5f, 3f, 8f, 3f, 0, 45),
            RawModelPartFactory.FromVanilla("left_hind_leg", 2.5f, 16.5f, 4.5f, -1.6f, -0.5f, -1.5f, 3f, 8f, 3f, 12, 45),
            RawModelPartFactory.FromVanilla("right_front_leg", -2.5f, 16.5f, -3.5f, -1.4f, -0.5f, -1.5f, 3f, 8f, 3f, 0, 34),
            RawModelPartFactory.FromVanilla("left_front_leg", 2.5f, 16.5f, -3.5f, -1.6f, -0.5f, -1.5f, 3f, 8f, 3f, 12, 34),
            RawModelPartFactory.FromVanilla("body", 0f, 14f, 2.5f, -4f, -3f, -8.5f, 8f, 6f, 13f, 0, 15),
            RawModelPartFactory.FromVanilla("right_chest", -8.5f, 4f, 3f, -3f, 0f, 0f, 8f, 8f, 3f, 45, 28, rotationY: MathF.PI / 2f),
            RawModelPartFactory.FromVanilla("left_chest", 5.5f, 4f, 3f, -3f, 0f, 0f, 8f, 8f, 3f, 45, 41, rotationY: MathF.PI / 2f)
        };
        AssertPartsMatch(expected, BabyModelDefinitionsEquine.Llama.Parts);
        // task-158: every baby llama coat variant must render in the raw source frame.
        foreach (var key in LlamaModelDefinition.VariantKeys)
        {
            var def = BabyModelDefinitionsEquine.Variants[(LlamaModelDefinition.LlamaEntityTypeId, key)];
            AssertPartsMatch(expected, def.Parts);
            Assert.True(def.RawSourceFrame, $"baby_llama variant {key}: must be rawSourceFrame");
            Assert.Equal(0f, def.BaselinePx);
            Assert.Null(def.FaceTransforms is { Count: > 0 } ? def.FaceTransforms : null);
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
