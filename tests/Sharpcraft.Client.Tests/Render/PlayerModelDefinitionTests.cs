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
        // Source-frame bakes emit the 6 base parts plus their 6 independent inflated overlay children.
        Assert.Equal(12, parts.Length);
        AssertPart(parts, "head", 0f, -8f, 8f, 8f, 8f);
        AssertPart(parts, "body", 0f, 0f, 8f, 12f, 4f);
        AssertPart(parts, "left_arm", 6f, 0f, 4f, 12f, 4f);
        AssertPart(parts, "right_arm", -6f, 0f, 4f, 12f, 4f);
        AssertPart(parts, "left_leg", 1.9f, 12f, 4f, 12f, 4f);
        // Raw bake rounds the 1.9 pivot to 4 decimals; compare with tolerance.
        var rightLeg = Assert.Single(parts, candidate => candidate.Name == "right_leg");
        Assert.True(MathF.Abs(rightLeg.CentreX - (-1.9f)) < 1e-5f, $"right_leg CentreX={rightLeg.CentreX}");
        Assert.Equal(12f, rightLeg.Bottom);
        Assert.Equal(4f, rightLeg.Width);
        foreach (var name in new[] { "hat", "jacket", "left_sleeve", "right_sleeve", "left_pants", "right_pants" })
        {
            Assert.Contains(parts, candidate => candidate.Name == name);
        }
    }

    [Fact]
    public void TheModelIsThirtyTwoPixelsTall()
    {
        // The raw source frame keeps y-down model pixels; the head spans y [-8, 0] around its pivot,
        // and the model's full height is the declared 32px constant. Legacy y-up had bottom 24.
        Assert.Equal(32f, PlayerModelDefinition.ModelPixels);
        var head = PlayerModelDefinition.AdultParts.Single(p => p.Name == "head");
        Assert.Equal(-8f, head.Bottom);
        Assert.Equal(0f, head.Bottom + head.Height);
    }

    [Fact]
    public void AdultPartsMatchTheSourceFrameRecords()
    {
        // Independent source literals (RawModelPartFactory): 6 base parts with their independent
        // inflated overlay children, never the C# bake output.
        var expected = new[]
        {
            RawModelPartFactory.FromVanilla("head", 0f, 0f, 0f, -4f, -8f, -4f, 8f, 8f, 8f, 0, 0),
            RawModelPartFactory.FromVanillaChild("hat", "head", 0f, 0f, 0f, -4f, -8f, -4f, 8f, 8f, 8f, 32, 0, grow: 0.5f, kind: EntityGeometryLayerKind.Translucent),
            RawModelPartFactory.FromVanilla("body", 0f, 0f, 0f, -4f, 0f, -2f, 8f, 12f, 4f, 16, 16),
            RawModelPartFactory.FromVanillaChild("jacket", "body", 0f, 0f, 0f, -4f, 0f, -2f, 8f, 12f, 4f, 16, 32, grow: 0.25f, kind: EntityGeometryLayerKind.Translucent),
            RawModelPartFactory.FromVanilla("left_arm", 5f, 2f, 0f, -1f, -2f, -2f, 4f, 12f, 4f, 32, 48),
            RawModelPartFactory.FromVanillaChild("left_sleeve", "left_arm", 0f, 0f, 0f, -1f, -2f, -2f, 4f, 12f, 4f, 48, 48, grow: 0.25f, kind: EntityGeometryLayerKind.Translucent),
            RawModelPartFactory.FromVanilla("right_arm", -5f, 2f, 0f, -3f, -2f, -2f, 4f, 12f, 4f, 40, 16),
            RawModelPartFactory.FromVanillaChild("right_sleeve", "right_arm", 0f, 0f, 0f, -3f, -2f, -2f, 4f, 12f, 4f, 40, 32, grow: 0.25f, kind: EntityGeometryLayerKind.Translucent),
            RawModelPartFactory.FromVanilla("left_leg", 1.9f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 16, 48),
            RawModelPartFactory.FromVanillaChild("left_pants", "left_leg", 0f, 0f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 48, grow: 0.25f, kind: EntityGeometryLayerKind.Translucent),
            RawModelPartFactory.FromVanilla("right_leg", -1.9f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 16),
            RawModelPartFactory.FromVanillaChild("right_pants", "right_arm", 0f, 0f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 32, grow: 0.25f, kind: EntityGeometryLayerKind.Translucent)
        };
        // Fix right_pants parent (source: child of right_leg).
        expected[11] = RawModelPartFactory.FromVanillaChild("right_pants", "right_leg", 0f, 0f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 32, grow: 0.25f, kind: EntityGeometryLayerKind.Translucent);

        var actual = PlayerModelDefinition.AdultParts;
        Assert.Equal(expected.Length, actual.Length);
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
