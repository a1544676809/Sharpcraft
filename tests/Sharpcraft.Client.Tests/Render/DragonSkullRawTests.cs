// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>task-203: dragon skull raw source-frame definition, scaled-head UV pinning.</summary>
public sealed class DragonSkullRawTests
{
    [Fact]
    public void DragonDefinitionUsesRawSourceFrame()
    {
        var def = SkullModelDefinition.DragonDefinition;
        Assert.True(def.RawSourceFrame);
        Assert.Equal(0f, def.BaselinePx);
        Assert.True(def.FaceTransforms is null || def.FaceTransforms.Count == 0);
        // Dragon skull is now produced by the compiled-only production mesher (#229); its compiled tree
        // carries the same scaled-head UV pins as the flat parts (verified by the keyed source sample and
        // DragonPartsPinPreScaleUvDimensions).
        Assert.NotNull(def.Compiled);
        Assert.Equal(7, def.Parts.Count);
    }

    [Fact]
    public void DragonPartsPinPreScaleUvDimensions()
    {
        var parts = SkullModelDefinition.DragonDefinition.Parts.ToDictionary(p => p.Name, StringComparer.Ordinal);
        Assert.Equal((12, 5, 16), Uv(parts["head"]));
        Assert.Equal((16, 16, 16), Uv(parts["upper_head"]));
        Assert.Equal((2, 4, 6), Uv(parts["scale_left"]));
        Assert.Equal((2, 2, 4), Uv(parts["nostril_right"]));
        Assert.Equal((12, 4, 16), Uv(parts["jaw"]));
    }

    [Fact]
    public void DragonScaledHeadKeepsSourceBoxOrder()
    {
        var names = SkullModelDefinition.DragonDefinition.Parts.Select(p => p.Name).ToArray();
        Assert.Equal(["head", "upper_head", "scale_left", "nostril_left", "scale_right", "nostril_right", "jaw"], names);
        Assert.Equal("head", SkullModelDefinition.DragonDefinition.Parts[^1].Parent);
    }

    private static (int Width, int Height, int Depth) Uv(EntityModelPart part) =>
        (part.UvWidth, part.UvHeight, part.UvDepth);
}
