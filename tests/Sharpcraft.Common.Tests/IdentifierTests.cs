// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Identifiers;
using Xunit;

namespace Sharpcraft.Common.Tests;

/// <summary>
/// Behavioural tests for <see cref="Identifier"/>, derived from
/// docs/analysis/common/identifier.md (T-005 in docs/traceability.md).
/// </summary>
public sealed class IdentifierTests
{
    [Theory]
    [InlineData("minecraft:stone", "minecraft", "stone")]
    [InlineData("stone", "minecraft", "stone")]
    [InlineData(":stone", "minecraft", "stone")]
    [InlineData(":", "minecraft", "")]
    [InlineData("mod:", "mod", "")]
    [InlineData("mod:path/to/thing", "mod", "path/to/thing")]
    [InlineData("mod:path.with-dots_and-dashes", "mod", "path.with-dots_and-dashes")]
    public void ParseAcceptsValidForms(string input, string expectedNamespace, string expectedPath)
    {
        var identifier = Identifier.Parse(input);

        Assert.Equal(expectedNamespace, identifier.Namespace);
        Assert.Equal(expectedPath, identifier.Path);
    }

    [Theory]
    [InlineData("Mod:stone")]
    [InlineData("minecraft:Stone")]
    [InlineData("mine craft:x")]
    [InlineData("minecraft:sto ne")]
    [InlineData("..:x")]
    [InlineData("a:b:c")]
    [InlineData("mod:path?query")]
    public void ParseRejectsInvalidForms(string input)
    {
        Assert.Throws<IdentifierException>(() => Identifier.Parse(input));
    }

    [Fact]
    public void TryParseReturnsFalseInsteadOfThrowing()
    {
        Assert.False(Identifier.TryParse("BAD:stone", out var invalid));
        Assert.True(invalid.IsDefault);

        Assert.True(Identifier.TryParse("mod:stone", out var valid));
        Assert.Equal("mod:stone", valid.ToString());

        Assert.False(Identifier.TryParse(null, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("..")]
    [InlineData("_-.0123456789abcdefghijklmnopqrstuvwxyz")]
    public void EmptyAndBoundaryPartsFollowVanillaRules(string namespaceValue)
    {
        // Empty namespace is accepted by the explicit constructor (vanilla quirk);
        // the literal ".." is the single rejected namespace value.
        if (namespaceValue == "..")
        {
            Assert.False(Identifier.IsValidNamespace(namespaceValue));
            Assert.Throws<IdentifierException>(() => Identifier.FromNamespaceAndPath(namespaceValue, "x"));
        }
        else
        {
            Assert.True(Identifier.IsValidNamespace(namespaceValue));
            Assert.Equal(namespaceValue, Identifier.FromNamespaceAndPath(namespaceValue, "x").Namespace);
        }

        // Empty path is always legal.
        Assert.True(Identifier.IsValidPath(string.Empty));
        Assert.Equal("mod:", Identifier.FromNamespaceAndPath("mod", string.Empty).ToString());
    }

    [Fact]
    public void ThereIsNoLengthLimit()
    {
        var longPath = new string('a', 100_000);

        var identifier = Identifier.WithDefaultNamespace(longPath);

        Assert.Equal(100_000, identifier.Path.Length);
    }

    [Fact]
    public void CompareToOrdersByPathThenNamespace()
    {
        var first = Identifier.Parse("zzz:aaa");
        var second = Identifier.Parse("aaa:zzz");

        // Path decides first: "aaa" < "zzz", regardless of namespace.
        Assert.True(first.CompareTo(second) < 0);

        // Same path, different namespace: namespace breaks the tie.
        var alpha = Identifier.Parse("aaa:same");
        var beta = Identifier.Parse("bbb:same");
        Assert.True(alpha.CompareTo(beta) < 0);
        Assert.True(beta.CompareTo(alpha) > 0);
        Assert.Equal(0, alpha.CompareTo(Identifier.Parse("aaa:same")));
    }

    [Fact]
    public void SortingAFleetOfIdentifiersUsesPathFirst()
    {
        var identifiers = new[]
        {
            Identifier.Parse("minecraft:zombie"),
            Identifier.Parse("custom:apple"),
            Identifier.Parse("minecraft:apple"),
            Identifier.Parse("aaa:zombie")
        };

        Array.Sort(identifiers);

        Assert.Equal("custom:apple", identifiers[0].ToString());
        Assert.Equal("minecraft:apple", identifiers[1].ToString());
        Assert.Equal("aaa:zombie", identifiers[2].ToString());
        Assert.Equal("minecraft:zombie", identifiers[3].ToString());
    }

    [Fact]
    public void EqualityComparesBothParts()
    {
        var left = Identifier.Parse("minecraft:stone");
        var right = Identifier.Parse("minecraft:stone");
        var differentNamespace = Identifier.Parse("mod:stone");
        var differentPath = Identifier.Parse("minecraft:dirt");

        Assert.True(left == right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
        Assert.False(left == differentNamespace);
        Assert.False(left == differentPath);
        Assert.True(left != differentPath);
    }

    [Fact]
    public void ToStringRoundTripsThroughParse()
    {
        var identifier = Identifier.Parse("mod:path/to/thing");

        Assert.Equal(identifier, Identifier.Parse(identifier.ToString()));
    }

    [Fact]
    public void DerivedStringsFollowVanillaForms()
    {
        var identifier = Identifier.Parse("mod:path/to/thing");

        Assert.Equal("mod_path_to_thing", identifier.ToDebugFileName());
        Assert.Equal("mod.path/to/thing", identifier.ToLanguageKey());
        Assert.Equal("mod.path/to/thing", identifier.ToShortLanguageKey());
        Assert.Equal("mod:path/to/thing", identifier.ToShortString());

        var vanilla = Identifier.Parse("minecraft:block/stone");
        Assert.Equal("block/stone", vanilla.ToShortLanguageKey());
        Assert.Equal("block/stone", vanilla.ToShortString());
    }

    [Fact]
    public void PathMutatorsValidateTheResult()
    {
        var identifier = Identifier.Parse("mod:base");

        Assert.Equal("mod:base/tail", identifier.WithSuffix("/tail").ToString());
        Assert.Equal("mod:head/base", identifier.WithPrefix("head/").ToString());
        Assert.Equal("mod:other", identifier.WithPath("other").ToString());
        Assert.Throws<IdentifierException>(() => identifier.WithPath("Other"));
    }

    [Fact]
    public void ResolveAgainstCombinesNamespaceAndPath()
    {
        var root = Path.Combine(Path.GetTempPath(), "mccsharp-identifier-tests");

        var resolved = Identifier.Parse("minecraft:block/stone").ResolveAgainst(root);

        Assert.Equal(Path.GetFullPath(Path.Combine(root, "minecraft", "block", "stone")), resolved);
    }

    [Fact]
    public void ResolveAgainstRejectsTraversal()
    {
        var root = Path.Combine(Path.GetTempPath(), "mccsharp-identifier-tests");

        var escaping = Identifier.Parse("minecraft:../../outside");

        Assert.Throws<InvalidOperationException>(() => escaping.ResolveAgainst(root));
    }

    [Fact]
    public void DefaultValueIsMarkedAndSafeToRead()
    {
        var value = default(Identifier);

        Assert.True(value.IsDefault);
        Assert.Equal(string.Empty, value.Namespace);
        Assert.Equal(string.Empty, value.Path);
        Assert.Equal(":", value.ToString());
    }
}
