// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
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
