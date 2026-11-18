// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Equivalence.Audit;
using Xunit;

namespace Sharpcraft.Architecture.Tests;

/// <summary>
/// task-273 / Issue #219: the state matrix is metadata-driven (#219 review round 2). Assertions are derived
/// from the same resolver/layer metadata as the builder: every real layer is covered, every expressible
/// state enters the matrix, no row lies outside the allowed metadata set, and UNKNOWN is not NO_SOURCE.
/// </summary>
[CollectionDefinition("StateMatrixBaseline", DisableParallelization = true)]
public sealed class StateMatrixBaselineCollection;

[Collection("StateMatrixBaseline")]
public sealed class StateMatrixBaselineTests
{
    private static readonly string[] EntityNames = ["allay", "player", "creeper", "skeleton", "cod", "wither", "armor_stand", "armadillo"];

    private static (string Root, IReadOnlyList<StateMatrixBaseline.StateMatrixRow> Rows) Build()
    {
        var root = StateMatrixBaseline.FindRepositoryRoot();
        return (root, StateMatrixBaseline.Build(root));
    }

    [Fact]
    public void SourceRowsResolveRealFilesAndKeysAndNonStrictRowsAreNotStrict()
    {
        var (root, rows) = Build();
        Assert.NotEmpty(rows);
        Assert.Contains(rows, row => row.Status == StateMatrixBaseline.SourceSample && row.TypeKind == "entity");
        Assert.Contains(rows, row => row.Status == StateMatrixBaseline.SourceConstraint && row.StateClass is "energy" or "glint");
        Assert.Contains(rows, row => row.Status == StateMatrixBaseline.NoSource);
        Assert.Contains(rows, row => row.Status == StateMatrixBaseline.Unknown && row.TypeKind == "block");

        foreach (var row in rows.Where(row => row.Strict))
        {
            Assert.NotEqual(string.Empty, row.SampleFile);
            Assert.NotEqual(string.Empty, row.SampleKey);
            Assert.True(File.Exists(Path.Combine(root, row.SampleFile)), row.SampleFile);
        }

        foreach (var row in rows.Where(row => row.Status is StateMatrixBaseline.NoSource or StateMatrixBaseline.Unknown))
        {
            Assert.False(row.Strict);
            Assert.Equal(string.Empty, row.SampleFile);
            Assert.Equal(string.Empty, row.SampleKey);
        }
    }

    [Fact]
    public void UnknownIsNotNoSourceAndBlocksUseBaseOnly()
    {
        var (_, rows) = Build();
        var blocks = rows.Where(row => row.TypeKind == "block").ToArray();
        Assert.NotEmpty(blocks);
        Assert.All(blocks, row => Assert.Equal("base", row.StateClass));
        Assert.Contains(blocks, row => row.Status == StateMatrixBaseline.Unknown);
        Assert.DoesNotContain(blocks, row => row.Status == StateMatrixBaseline.NoSource);
        Assert.NotEqual(StateMatrixBaseline.NoSource, StateMatrixBaseline.Unknown);
    }

    [Fact]
    public void EveryEmittedEntityRowIsMetadataRelevantAndInAllowedSet()
    {
        var (_, rows) = Build();
        var entityRows = rows.Where(row => row.TypeKind == "entity").ToArray();
        Assert.NotEmpty(entityRows);
        // SourceRelevant is computed per pair, not a constant: every emitted row must be in its type's
        // metadata-derived allowed set, and a representative disallowed pair must be false (#219 review).
        foreach (var row in entityRows)
        {
            Assert.True(row.SourceRelevant, $"{row.TypeName}/{row.StateClass}/{row.LayerClass}");
            Assert.Contains((row.StateClass, row.LayerClass), StateMatrixBaseline.AllowedCombinations(row.TypeId));
        }

        Assert.False(StateMatrixBaseline.IsSourceRelevant(EntityModelResolver.FindTypeId("allay")!.Value, "powered", "base"));
        Assert.False(StateMatrixBaseline.IsSourceRelevant(EntityModelResolver.FindTypeId("cod")!.Value, "armor", "base"));
    }

    [Theory]
    [InlineData("allay", "elytra")]
    [InlineData("allay", "powered")]
    [InlineData("allay", "baby")]
    [InlineData("cod", "armor")]
    [InlineData("endermite", "glint")]
    [InlineData("bat", "energy")]
    [InlineData("armadillo", "powered")]
    public void NoCrossProductPseudoRows(string typeName, string stateOrLayer)
    {
        var (_, rows) = Build();
        Assert.DoesNotContain(rows, row =>
            row.TypeName == typeName
            && (row.StateClass == stateOrLayer || row.LayerClass == stateOrLayer));
    }

    [Fact]
    public void EveryRealRenderLayerIsCoveredAtLeastOnce()
    {
        var (_, rows) = Build();
        foreach (var name in EntityNames)
        {
            var typeId = EntityModelResolver.FindTypeId(name);
            Assert.NotNull(typeId);
            var realLayers = StateMatrixBaseline.LayersFor(typeId.Value);
            Assert.NotEmpty(realLayers);
            var coveredLayers = rows.Where(row => row.TypeId == typeId.Value)
                .Select(row => row.LayerClass).ToHashSet(StringComparer.Ordinal);
            foreach (var layer in realLayers)
            {
                Assert.Contains(layer, coveredLayers);
            }
        }
    }

    [Fact]
    public void EveryExpressibleStateEntersTheMatrix()
    {
        var (_, rows) = Build();
        foreach (var name in EntityNames)
        {
            var typeId = EntityModelResolver.FindTypeId(name);
            Assert.NotNull(typeId);
            var capabilities = StateMatrixBaseline.CapabilitiesFor(typeId.Value);
            var coveredStates = rows.Where(row => row.TypeId == typeId.Value)
                .Select(row => row.StateClass).ToHashSet(StringComparer.Ordinal);
            foreach (var state in capabilities.States())
            {
                Assert.Contains(state, coveredStates);
            }
        }
    }

    [Fact]
    public void MatrixIsMetadataDrivenAndCompact()
    {
        var (_, rows) = Build();
        // The cross-product generator emitted ~1,880 rows; a metadata-driven matrix must be compact (#219).
        Assert.InRange(rows.Count, 200, 700);
        Assert.All(rows, row => Assert.True(row.SourceRelevant));
    }
}
