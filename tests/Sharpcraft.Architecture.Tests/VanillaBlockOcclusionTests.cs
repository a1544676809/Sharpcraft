// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Registries;
using Xunit;

namespace Sharpcraft.Architecture.Tests;

/// <summary>
/// Anchors the generated occlusion table so drift cannot pass silently.
/// </summary>
/// <remarks>
/// AGENTS.md 3.6 asks for the same rule to exist as a test, not only as a script: CI runs the test suite, and the
/// generator's own <c>-Check</c> needs the analysis workspace, which a normal build does not have. So the count and a
/// handful of representative blocks are pinned here from the probe output, and <c>-Check</c> stays the full drift check
/// to run against the workspace.
/// </remarks>
public sealed class VanillaBlockOcclusionTests
{
    /// <summary>The reference registers this many blocks in 26.3 (probe output, 1286 data rows).</summary>
    [Fact]
    public void TheTableCoversEveryVanillaBlock()
    {
        Assert.Equal(1286, VanillaBlockOcclusion.Count);
    }

    /// <summary>
    /// Representative blocks, each value taken from
    /// ref://mc-26.3-analysis/vectors/block-occlusion.tsv
    /// and written here so a regeneration that loses a predicate fails.
    /// </summary>
    /// <remarks>
    /// The code packs bit 0 collision-full, bit 1 solid-render and bits 2..5 light dampening. The chosen blocks are the
    /// ones where the three predicates disagree, which is why a single texture-based proxy got them wrong.
    /// </remarks>
    [Theory]
    // ref://mc-26.3-analysis/vectors/block-occlusion.tsv -- minecraft:glass, true/false/0
    [InlineData("minecraft:glass", 1)]
    // minecraft:ice, true/false/1
    [InlineData("minecraft:ice", 5)]
    // minecraft:oak_leaves, true/false/1: a full collision shape with an empty occlusion shape
    [InlineData("minecraft:oak_leaves", 5)]
    // minecraft:stone, true/true/15
    [InlineData("minecraft:stone", 63)]
    // minecraft:dirt, true/true/15
    [InlineData("minecraft:dirt", 63)]
    // minecraft:water, false/false/1
    [InlineData("minecraft:water", 4)]
    // minecraft:stone_slab, false/false/0: occludes without being collision full
    [InlineData("minecraft:stone_slab", 0)]
    // minecraft:short_grass, false/false/0
    [InlineData("minecraft:short_grass", 0)]
    public void TheTableMatchesTheProbe(string blockName, int expectedCode)
    {
        Assert.True(VanillaBlockOcclusion.TryGet(Identifier.Parse(blockName), out var info), $"{blockName} is not in the table");

        var code = (info.CollisionFullBlock ? 1 : 0) | (info.SolidRender ? 2 : 0) | (info.LightDampening << 2);
        Assert.Equal(expectedCode, code);
    }

    /// <summary>The predicates the ambient occlusion pass reads, spelled out for the three interesting blocks.</summary>
    [Fact]
    public void ThePredicatesDisagreeWhereTheReferenceSaysTheyDo()
    {
        Assert.True(VanillaBlockOcclusion.TryGet(Identifier.Parse("minecraft:glass"), out var glass));
        Assert.True(glass.CollisionFullBlock, "glass has a full collision shape, so a corner next to it shades 0.2");
        Assert.False(glass.SolidRender, "glass has an empty occlusion shape");
        Assert.True(glass.LightPermeable, "glass lets light through");

        Assert.True(VanillaBlockOcclusion.TryGet(Identifier.Parse("minecraft:oak_leaves"), out var leaves));
        Assert.True(leaves.CollisionFullBlock);
        Assert.True(leaves.LightPermeable);

        Assert.True(VanillaBlockOcclusion.TryGet(Identifier.Parse("minecraft:stone"), out var stone));
        Assert.True(stone.CollisionFullBlock);
        Assert.True(stone.SolidRender);
        Assert.False(stone.LightPermeable);
    }

    /// <summary>Another namespace must not borrow a vanilla entry, and the miss has to be countable.</summary>
    [Fact]
    public void AModdedNamespaceDoesNotMatchAVanillaPath()
    {
        var before = VanillaBlockOcclusion.FallbackCount;

        Assert.False(VanillaBlockOcclusion.TryGet(new Identifier("examplemod", "glass"), out _));
        Assert.Equal(before + 1, VanillaBlockOcclusion.FallbackCount);

        Assert.True(VanillaBlockOcclusion.TryGet(new Identifier("minecraft", "glass"), out _));
        Assert.Equal(before + 1, VanillaBlockOcclusion.FallbackCount);
    }

    /// <summary>The generator has to stay in the repository, or the table becomes hand-edited.</summary>
    [Fact]
    public void TheGeneratorScriptIsPresent()
    {
        var root = RepositoryRoot();
        var script = Path.Combine(root, "tools", "blocks", "generate-block-occlusion-table.ps1");
        Assert.True(File.Exists(script), $"missing generator: {script}");

        var text = File.ReadAllText(script);
        Assert.Contains("block-occlusion.tsv", text, StringComparison.Ordinal);
        Assert.Contains("-Check", text, StringComparison.Ordinal);
    }

    /// <summary>The probe that produces the generator's input lives in the analysis workspace, so only document it.</summary>
    [Fact]
    public void TheGeneratedFileSaysWhereItCameFrom()
    {
        var root = RepositoryRoot();
        var generated = File.ReadAllText(Path.Combine(root, "src", "Sharpcraft.Common", "Registries", "VanillaBlockOcclusion.cs"));
        Assert.Contains("BlockOcclusionVectors.java", generated, StringComparison.Ordinal);
        Assert.Contains("auto-generated", generated, StringComparison.Ordinal);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Sharpcraft.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
