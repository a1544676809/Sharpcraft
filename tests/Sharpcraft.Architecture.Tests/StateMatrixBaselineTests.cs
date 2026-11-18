// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Equivalence.Audit;
using Xunit;

namespace Sharpcraft.Architecture.Tests;

/// <summary>
/// task-273 / Issue #219: the state matrix is metadata-driven (#219 round 4). Coverage is proven by
/// comparing the pairs actually emitted by Build against AllowedCombinations over the FULL per-type
/// candidate domain — no IsSourceRelevant self-proof, no hand-picked type samples.
/// </summary>
[CollectionDefinition("StateMatrixBaseline", DisableParallelization = true)]
public sealed class StateMatrixBaselineCollection;

[Collection("StateMatrixBaseline")]
public sealed class StateMatrixBaselineTests
{
    private static (string Root, IReadOnlyList<StateMatrixBaseline.StateMatrixRow> Rows) Build()
    {
        var root = StateMatrixBaseline.FindRepositoryRoot();
        return (root, StateMatrixBaseline.Build(root));
    }

    private static HashSet<(string State, string Layer)> EmittedPairs(
        IReadOnlyList<StateMatrixBaseline.StateMatrixRow> rows, int typeId) =>
        rows.Where(row => row.TypeKind == "entity" && row.TypeId == typeId)
            .Select(row => (row.StateClass, row.LayerClass))
            .ToHashSet();

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
    public void UnknownIsNotNoSourceAndEveryBlockTypeIsEmittedWithBaseOnly()
    {
        var (_, rows) = Build();
        Assert.NotEqual(StateMatrixBaseline.NoSource, StateMatrixBaseline.Unknown);
        var blockRows = rows.Where(row => row.TypeKind == "block").ToArray();
        Assert.All(blockRows, row => Assert.Equal("base", row.StateClass));
        Assert.Contains(blockRows, row => row.Status == StateMatrixBaseline.Unknown);
        Assert.DoesNotContain(blockRows, row => row.Status == StateMatrixBaseline.NoSource);

        foreach (var typeId in StateMatrixBaseline.AllBlockTypeIds())
        {
            var emitted = blockRows.Where(row => row.TypeId == typeId).Select(row => row.LayerClass)
                .ToHashSet(StringComparer.Ordinal);
            Assert.NotEmpty(emitted);
            foreach (var layer in StateMatrixBaseline.BlockLayersFor(typeId))
            {
                Assert.Contains(layer, emitted);
            }
        }
    }

    [Fact]
    public void EveryEntityTypeEmittedPairsEqualAllowedCombinations()
    {
        var (_, rows) = Build();
        var entityTypeIds = StateMatrixBaseline.AllEntityTypeIds();
        Assert.NotEmpty(entityTypeIds);

        // For EVERY type, the (state,layer) pairs actually emitted by Build must be set-equal to the
        // metadata-derived AllowedCombinations. This compares real output to metadata, not a helper to itself.
        foreach (var typeId in entityTypeIds)
        {
            var emitted = EmittedPairs(rows, typeId);
            var allowed = StateMatrixBaseline.AllowedCombinations(typeId);
            Assert.True(allowed.SetEquals(emitted),
                $"type {typeId}: allowed={{{string.Join(",", allowed.Select(p => p.State + "/" + p.Layer))}}} "
                + $"emitted={{{string.Join(",", emitted.Select(p => p.State + "/" + p.Layer))}}}");
        }
    }

    [Fact]
    public void EveryEntityTypeFullCandidateDomainMatchesAllowedAndEmitted()
    {
        var (_, rows) = Build();
        // Full candidate domain per type = all known states × that type's real layers. For each pair,
        // membership in AllowedCombinations must equal membership in emitted rows (both directions), and
        // the computed relevance must agree. Disallowed pairs are checked for every type, not just allay.
        foreach (var typeId in StateMatrixBaseline.AllEntityTypeIds())
        {
            var allowed = StateMatrixBaseline.AllowedCombinations(typeId);
            var emitted = EmittedPairs(rows, typeId);
            var layers = StateMatrixBaseline.LayersFor(typeId);
            foreach (var state in StateMatrixBaseline.AllStateClasses)
            {
                foreach (var layer in layers)
                {
                    var pair = (state, layer);
                    var isAllowed = allowed.Contains(pair);
                    var isEmitted = emitted.Contains(pair);
                    Assert.True(isAllowed == isEmitted, $"type {typeId} pair {state}/{layer}: allowed={isAllowed} emitted={isEmitted}");
                    Assert.True(isAllowed == StateMatrixBaseline.IsSourceRelevant(typeId, state, layer),
                        $"type {typeId} pair {state}/{layer}: relevance disagrees with allowed");
                }
            }
        }
    }

    [Fact]
    public void EveryEntityTypeHasAllRealLayersCovered()
    {
        var (_, rows) = Build();
        foreach (var typeId in StateMatrixBaseline.AllEntityTypeIds())
        {
            var coveredLayers = EmittedPairs(rows, typeId).Select(pair => pair.Layer).ToHashSet(StringComparer.Ordinal);
            Assert.NotEmpty(coveredLayers);
            foreach (var layer in StateMatrixBaseline.LayersFor(typeId))
            {
                Assert.True(coveredLayers.Contains(layer), $"type {typeId} missing real layer {layer}");
            }
        }
    }

    [Fact]
    public void EveryEntityTypeHasAllExpressibleStatesCovered()
    {
        var (_, rows) = Build();
        foreach (var typeId in StateMatrixBaseline.AllEntityTypeIds())
        {
            var coveredStates = EmittedPairs(rows, typeId).Select(pair => pair.State).ToHashSet(StringComparer.Ordinal);
            Assert.NotEmpty(coveredStates);
            foreach (var state in StateMatrixBaseline.CapabilitiesFor(typeId).States())
            {
                Assert.True(coveredStates.Contains(state), $"type {typeId} missing expressible state {state}");
            }
        }
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
    public void MatrixIsMetadataDrivenAndCompact()
    {
        var (_, rows) = Build();
        // The cross-product generator emitted ~1,880 rows; a metadata-driven matrix must be compact (#219).
        Assert.InRange(rows.Count, 200, 700);
        // Compactness is real filtering: the full candidate domain exceeds the emitted set for at least one type.
        var anyFiltered = StateMatrixBaseline.AllEntityTypeIds().Any(typeId =>
            StateMatrixBaseline.AllStateClasses.Count * StateMatrixBaseline.LayersFor(typeId).Count
            > EmittedPairs(rows, typeId).Count);
        Assert.True(anyFiltered, "metadata filtering must drop cross-product pairs");
    }
}
