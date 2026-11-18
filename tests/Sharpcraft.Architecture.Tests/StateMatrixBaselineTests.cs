// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Equivalence.Audit;
using Xunit;

namespace Sharpcraft.Architecture.Tests;

/// <summary>
/// task-273 / Issue #219: the state matrix is metadata-driven (#219 round 3). Coverage assertions iterate
/// the FULL machine enumeration of entity and block entity types — no hand-picked type samples.
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

        // Every block entity type from the machine registry must be emitted with all its declared layers.
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
    public void EveryEntityTypeHasAllRealLayersCovered()
    {
        var (_, rows) = Build();
        var entityTypeIds = StateMatrixBaseline.AllEntityTypeIds();
        Assert.NotEmpty(entityTypeIds);

        // Iterate EVERY entity type from the machine resolver enumeration; no hand-picked sample set.
        foreach (var typeId in entityTypeIds)
        {
            var coveredLayers = rows.Where(row => row.TypeKind == "entity" && row.TypeId == typeId)
                .Select(row => row.LayerClass).ToHashSet(StringComparer.Ordinal);
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
        var entityTypeIds = StateMatrixBaseline.AllEntityTypeIds();
        Assert.NotEmpty(entityTypeIds);

        foreach (var typeId in entityTypeIds)
        {
            var coveredStates = rows.Where(row => row.TypeKind == "entity" && row.TypeId == typeId)
                .Select(row => row.StateClass).ToHashSet(StringComparer.Ordinal);
            Assert.NotEmpty(coveredStates);
            foreach (var state in StateMatrixBaseline.CapabilitiesFor(typeId).States())
            {
                Assert.True(coveredStates.Contains(state), $"type {typeId} missing expressible state {state}");
            }
        }
    }

    [Fact]
    public void EveryTypeAllowedAndDisallowedPairMatchesComputedSourceRelevant()
    {
        var (_, rows) = Build();
        var allayRow = rows.First(row => row.TypeName == "allay");
        // For EVERY type, verify SourceRelevant is a computed value over allowed/disallowed pairs:
        // each emitted row is allowed/relevant, and representative disallowed pairs are false.
        foreach (var typeId in StateMatrixBaseline.AllEntityTypeIds())
        {
            var allowed = StateMatrixBaseline.AllowedCombinations(typeId);
            var typeRows = rows.Where(row => row.TypeKind == "entity" && row.TypeId == typeId).ToArray();
            foreach (var row in typeRows)
            {
                Assert.True(row.SourceRelevant, $"type {typeId} row {row.StateClass}/{row.LayerClass}");
                Assert.Contains((row.StateClass, row.LayerClass), allowed);
            }

            // The allowed set itself must agree with the public computed relevance for each pair.
            foreach (var (state, layer) in allowed)
            {
                Assert.True(StateMatrixBaseline.IsSourceRelevant(typeId, state, layer));
            }
        }

        // Disallowed pairs from non-expressible types must be false (computed, not constant true).
        Assert.False(StateMatrixBaseline.IsSourceRelevant(allayRow.TypeId, "powered", "base"));
        Assert.False(StateMatrixBaseline.IsSourceRelevant(allayRow.TypeId, "energy", "energy"));
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
        Assert.All(rows, row => Assert.True(row.SourceRelevant));
    }
}
