// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Registries;
using Xunit;

namespace Sharpcraft.Common.Tests;

/// <summary>
/// Ordering and id-assignment tests for <see cref="Registry{T}"/> and
/// <see cref="IdMapper{T}"/>, derived from docs/analysis/common/registry-order.md (T-007).
/// </summary>
public sealed class RegistryOrderTests
{
    private sealed class Element(string name)
    {
        public string Name { get; } = name;
    }

    [Fact]
    public void IterationFollowsRegistrationOrderNotIdentifierOrder()
    {
        var registry = new Registry<Element>();

        // Deliberately not alphabetical: "zulu" registered first must stay first.
        registry.Register(Identifier.Parse("minecraft:zulu"), new Element("zulu"));
        registry.Register(Identifier.Parse("minecraft:alpha"), new Element("alpha"));
        registry.Register(Identifier.Parse("custom:alpha"), new Element("custom-alpha"));

        Assert.Equal(
            ["minecraft:zulu", "minecraft:alpha", "custom:alpha"],
            registry.Entries.Select(entry => entry.Id.ToString()));
    }

    [Fact]
    public void NumericIdsMatchRegistrationPositions()
    {
        var registry = new Registry<Element>();
        var first = new Element("first");
        var second = new Element("second");

        registry.Register(Identifier.Parse("minecraft:first"), first);
        registry.Register(Identifier.Parse("minecraft:second"), second);

        Assert.Equal(0, registry.GetId(first));
        Assert.Equal(1, registry.GetId(second));
        Assert.Same(first, registry.ById(0));
        Assert.Same(second, registry.ById(1));
        Assert.Null(registry.ById(2));
    }

    [Fact]
    public void DuplicateIdentifiersAreRejected()
    {
        var registry = new Registry<Element>();
        registry.Register(Identifier.Parse("minecraft:stone"), new Element("stone"));

        Assert.Throws<InvalidOperationException>(
            () => registry.Register(Identifier.Parse("minecraft:stone"), new Element("other")));
    }

    [Fact]
    public void FrozenRegistryRejectsWritesButKeepsReads()
    {
        var registry = new Registry<Element>();
        var element = new Element("stone");
        registry.Register(Identifier.Parse("minecraft:stone"), element);
        registry.Freeze();

        Assert.Throws<InvalidOperationException>(
            () => registry.Register(Identifier.Parse("minecraft:dirt"), new Element("dirt")));
        Assert.True(registry.TryGet(Identifier.Parse("minecraft:stone"), out var found));
        Assert.Same(element, found);
    }

    [Fact]
    public void LookupByIdentifierIsIndependentOfOrder()
    {
        var registry = new Registry<Element>();
        var dirt = new Element("dirt");
        registry.Register(Identifier.Parse("minecraft:stone"), new Element("stone"));
        registry.Register(Identifier.Parse("minecraft:dirt"), dirt);

        Assert.Same(dirt, registry.Get(Identifier.Parse("minecraft:dirt")));
        Assert.Throws<KeyNotFoundException>(() => registry.Get(Identifier.Parse("minecraft:gravel")));
        Assert.False(registry.TryGet(Identifier.Parse("minecraft:gravel"), out _));
    }

    [Fact]
    public void PathPrefixFilteringPreservesRegistrationOrder()
    {
        var registry = new Registry<Element>();
        registry.Register(Identifier.Parse("minecraft:block/stone"), new Element("stone"));
        registry.Register(Identifier.Parse("minecraft:item/apple"), new Element("apple"));
        registry.Register(Identifier.Parse("minecraft:block/dirt"), new Element("dirt"));

        Assert.Equal(
            ["minecraft:block/stone", "minecraft:block/dirt"],
            registry.WithPathPrefix("block/").Select(entry => entry.Id.ToString()));
    }

    [Fact]
    public void IdMapperAssignsSequentialIdsAndIsReferenceBased()
    {
        var mapper = new IdMapper<Element>();
        var first = new Element("a");
        var second = new Element("b");

        Assert.Equal(0, mapper.Add(first));
        Assert.Equal(1, mapper.Add(second));
        Assert.Equal(2, mapper.Count);
        Assert.Equal(1, mapper.GetId(second));
        Assert.Same(first, mapper.ById(0));
        Assert.Null(mapper.ById(99));

        // A structurally identical but distinct instance is unmapped (identity semantics).
        Assert.Equal(-1, mapper.GetId(new Element("a")));
    }

    [Fact]
    public void IdMapperRejectsRemapping()
    {
        var mapper = new IdMapper<Element>();
        var value = new Element("a");
        mapper.Add(value);

        Assert.Throws<InvalidOperationException>(() => mapper.AddMapping(value, 5));
        Assert.Throws<InvalidOperationException>(() => mapper.AddMapping(new Element("b"), 0));
    }

    [Fact]
    public void IdMapperSupportsSparseExplicitIds()
    {
        var mapper = new IdMapper<Element>();
        var value = new Element("sparse");

        mapper.AddMapping(value, 7);

        Assert.Equal(7, mapper.GetId(value));
        Assert.Equal(8, mapper.Count);
        Assert.Null(mapper.ById(3));
        Assert.Same(value, mapper.ById(7));
    }

    /// <summary>
    /// Pins the vanilla block registration order (exported from the official jar) into the
    /// registry contract: ids follow registration order, and that order is not alphabetical.
    /// </summary>
    [Fact]
    public void VanillaBlockOrderMatchesRegistrationSemantics()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "vanilla-block-order.txt");
        Assert.True(File.Exists(path), $"order snapshot missing from test output: {path}");

        var identifiers = File.ReadAllLines(path)
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => line.Split('\t'))
            .Select(parts => (Index: int.Parse(parts[0], CultureInfo.InvariantCulture), Id: Identifier.Parse(parts[1])))
            .ToList();

        Assert.Equal(1286, identifiers.Count);
        Assert.Equal("minecraft:air", identifiers[0].Id.ToString());
        Assert.Equal("minecraft:firefly_bush", identifiers[^1].Id.ToString());

        var registry = new Registry<Element>();
        var byIndex = new List<Element>(identifiers.Count);
        foreach (var (index, id) in identifiers)
        {
            var element = new Element(id.ToString());
            byIndex.Add(element);
            registry.Register(id, element);
            Assert.Equal(index, registry.GetId(element));
        }

        registry.Freeze();

        // Registration order is not lexicographic: stone precedes granite in vanilla.
        var stoneIndex = registry.Entries.ToList().FindIndex(entry => entry.Id.ToString() == "minecraft:stone");
        var graniteIndex = registry.Entries.ToList().FindIndex(entry => entry.Id.ToString() == "minecraft:granite");
        Assert.True(stoneIndex < graniteIndex, "vanilla order should not be sorted alphabetically");
    }

}
