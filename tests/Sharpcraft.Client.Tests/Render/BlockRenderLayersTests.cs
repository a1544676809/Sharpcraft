// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Registries;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Translucent layer classification (docs/specs/render/pipeline.md 15, slice S1): water and glass belong in the blended
/// layer, stone does not, and the classifier must work from a state id as well as a name.
/// </summary>
public sealed class BlockRenderLayersTests
{
    [Theory]
    [InlineData("minecraft:water")]
    [InlineData("minecraft:glass")]
    [InlineData("minecraft:glass_pane")]
    [InlineData("minecraft:ice")]
    [InlineData("minecraft:red_stained_glass")]
    public void TranslucentBlocksAreClassified(string name)
    {
        Assert.True(BlockRenderLayers.IsTranslucent(Id(name)));
    }

    [Theory]
    [InlineData("minecraft:stone")]
    [InlineData("minecraft:dirt")]
    [InlineData("minecraft:oak_leaves")]
    public void OpaqueBlocksAreNotClassified(string name)
    {
        Assert.False(BlockRenderLayers.IsTranslucent(Id(name)));
    }

    [Fact]
    public void AStateIdIsResolvedThroughTheLayout()
    {
        var layout = new BlockStateLayout([(new Identifier("minecraft", "stone"), 1), (new Identifier("minecraft", "water"), 2)]);

        Assert.True(BlockRenderLayers.IsTranslucent(1, layout));
        Assert.False(BlockRenderLayers.IsTranslucent(0, layout));
    }

    private static Identifier Id(string text)
    {
        var parts = text.Split(':', 2);
        return new Identifier(parts[0], parts[1]);
    }
}
