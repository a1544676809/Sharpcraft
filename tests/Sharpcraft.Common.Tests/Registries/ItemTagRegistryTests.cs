// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Registries;
using Xunit;

namespace Sharpcraft.Common.Tests.Registries;

/// <summary>Item tag parsing: direct values, nested <c>#tag</c> references, object entries and missing refs.</summary>
public sealed class ItemTagRegistryTests
{
    private static Identifier Id(string value)
    {
        var colon = value.IndexOf(':');
        return colon < 0 ? new Identifier("minecraft", value) : new Identifier(value[..colon], value[(colon + 1)..]);
    }

    [Fact]
    public void ResolvesDirectItemsAndNestedTags()
    {
        var registry = ItemTagRegistry.Parse(
        [
            (Id("minecraft:planks"), """{"values":["minecraft:oak_planks","#minecraft:wooden_tool_materials"]}"""),
            (Id("minecraft:wooden_tool_materials"), """{"values":["minecraft:stick"]}""")
        ]);

        Assert.True(registry.IsInTag(Id("minecraft:planks"), Id("minecraft:oak_planks")));
        Assert.True(registry.IsInTag(Id("minecraft:planks"), Id("minecraft:stick")));
        Assert.False(registry.IsInTag(Id("minecraft:planks"), Id("minecraft:stone")));
        Assert.False(registry.IsInTag(Id("minecraft:missing"), Id("minecraft:stick")));
    }

    [Fact]
    public void ReadsObjectEntriesAndIgnoresMissingNestedTags()
    {
        var registry = ItemTagRegistry.Parse(
        [
            (Id("minecraft:piglin_loved"), """{"values":[{"id":"minecraft:gold_ingot","required":false},"#minecraft:missing_tag"]}""")
        ]);

        Assert.True(registry.IsInTag(Id("minecraft:piglin_loved"), Id("minecraft:gold_ingot")));
        Assert.Empty(registry.Values(Id("minecraft:missing_tag")));
        Assert.Single(registry.Values(Id("minecraft:piglin_loved")));
    }

    [Fact]
    public void ReportsItsTags()
    {
        var registry = ItemTagRegistry.Parse(
        [
            (Id("minecraft:planks"), """{"values":[]}"""),
            (Id("minecraft:logs"), """{"values":[]}""")
        ]);

        Assert.Equal(2, registry.Tags.Count);
    }
}
