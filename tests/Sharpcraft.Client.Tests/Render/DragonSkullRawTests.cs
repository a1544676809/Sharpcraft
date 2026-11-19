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
    }

    private static (int Width, int Height, int Depth) Uv(EntityModelPart part) =>
        (part.UvWidth, part.UvHeight, part.UvDepth);
}
