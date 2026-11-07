// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

public sealed class PlayerModelDefinitionTests
{
    [Fact]
    public void TheAdultModelHasTheReferencePartsAndDimensions()
    {
        var parts = PlayerModelDefinition.AdultParts;

        Assert.Equal(6, parts.Length);
        AssertPart(parts, "head", 0f, 24f, 8f, 8f, 8f);
        AssertPart(parts, "body", 0f, 12f, 8f, 12f, 4f);
        AssertPart(parts, "left_arm", -6f, 12f, 4f, 12f, 4f);
        AssertPart(parts, "right_arm", 6f, 12f, 4f, 12f, 4f);
        AssertPart(parts, "left_leg", -1.9f, 0f, 4f, 12f, 4f);
        AssertPart(parts, "right_leg", 1.9f, 0f, 4f, 12f, 4f);
    }

    [Fact]
    public void TheModelIsThirtyTwoPixelsTall()
    {
        var head = PlayerModelDefinition.AdultParts[0];
        Assert.Equal(32f, PlayerModelDefinition.ModelPixels);
        Assert.Equal(32f, head.Bottom + head.Height);
    }

    /// <summary>
    /// T-671: proves the DSL-baked AdultParts are identical to the legacy FromVanilla records, as a first real-model
    /// migration check. Uses the shared value comparer because record equality is not a valid oracle for parts with
    /// reference-typed overlay lists.
    /// </summary>
    [Fact]
    public void AdultPartsMatchTheLegacyFromVanillaRecords()
    {
        var expected = new[]
        {
            LegacyModelPartFactory.FromVanilla("head", 0f, 0f, 0f, -4f, -8f, -4f, 8f, 8f, 8f, 0, 0,
                overlays: [new("hat", 0.5f, 32, 0, Kind: EntityGeometryLayerKind.Translucent)]),
            LegacyModelPartFactory.FromVanilla("body", 0f, 0f, 0f, -4f, 0f, -2f, 8f, 12f, 4f, 16, 16,
                overlays: [new("jacket", 0.25f, 16, 32, Kind: EntityGeometryLayerKind.Translucent)]),
            LegacyModelPartFactory.FromVanilla("left_arm", 5f, 2f, 0f, -1f, -2f, -2f, 4f, 12f, 4f, 32, 48,
                overlays: [new("left_sleeve", 0.25f, 48, 48, Kind: EntityGeometryLayerKind.Translucent)]),
            LegacyModelPartFactory.FromVanilla("right_arm", -5f, 2f, 0f, -3f, -2f, -2f, 4f, 12f, 4f, 40, 16,
                overlays: [new("right_sleeve", 0.25f, 40, 32, Kind: EntityGeometryLayerKind.Translucent)]),
            LegacyModelPartFactory.FromVanilla("left_leg", 1.9f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 16, 48,
                overlays: [new("left_pants", 0.25f, 0, 48, Kind: EntityGeometryLayerKind.Translucent)]),
            LegacyModelPartFactory.FromVanilla("right_leg", -1.9f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 16,
                overlays: [new("right_pants", 0.25f, 0, 32, Kind: EntityGeometryLayerKind.Translucent)])
        };

        var actual = PlayerModelDefinition.AdultParts;
        Assert.Equal(expected.Length, actual.Length);
        // Record equality is not a valid migration oracle (OverlayLayers is reference-typed); compare by value.
        for (var i = 0; i < expected.Length; i++)
        {
            ModelPartAssertions.AssertEqual(expected[i], actual[i]);
        }
    }

    private static void AssertPart(EntityModelPart[] parts, string name, float x, float bottom, float width, float height, float depth)
    {
        var part = Assert.Single(parts, candidate => candidate.Name == name);
        Assert.Equal(x, part.CentreX);
        Assert.Equal(bottom, part.Bottom);
        Assert.Equal(width, part.Width);
        Assert.Equal(height, part.Height);
        Assert.Equal(depth, part.Depth);
    }
}
