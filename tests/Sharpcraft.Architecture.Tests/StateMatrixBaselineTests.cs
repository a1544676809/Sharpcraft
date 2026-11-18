// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Equivalence.Audit;
using Xunit;

namespace Sharpcraft.Architecture.Tests;

/// <summary>
/// task-273 / Issue #219: the state matrix is metadata-driven (no cross-product pseudo rows).
/// Every source row resolves to a real sample file and key; UNKNOWN is distinct from NO_SOURCE.
/// </summary>
[CollectionDefinition("StateMatrixBaseline", DisableParallelization = true)]
public sealed class StateMatrixBaselineCollection;

[Collection("StateMatrixBaseline")]
public sealed class StateMatrixBaselineTests
{
    [Fact]
    public void SourceRowsResolveRealFilesAndKeysAndNonStrictRowsAreNotStrict()
    {
        var root = StateMatrixBaseline.FindRepositoryRoot();
        var rows = StateMatrixBaseline.Build(root);
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
        var rows = StateMatrixBaseline.Build(StateMatrixBaseline.FindRepositoryRoot());
        Assert.DoesNotContain(rows, row =>
            row.TypeName == typeName
            && (row.StateClass == stateOrLayer || row.LayerClass == stateOrLayer));
    }

    [Fact]
    public void BlockRowsUseOnlyBaseStateAndAreUnknownWithoutSample()
    {
        var rows = StateMatrixBaseline.Build(StateMatrixBaseline.FindRepositoryRoot());
        var blockRows = rows.Where(row => row.TypeKind == "block").ToArray();
        Assert.NotEmpty(blockRows);
        Assert.All(blockRows, row => Assert.Equal("base", row.StateClass));
        // A block row without a keyed sample is UNKNOWN (source behaviour unconfirmed), never a silent NO_SOURCE.
        Assert.Contains(blockRows, row => row.Status == StateMatrixBaseline.Unknown);
        Assert.DoesNotContain(blockRows, row => row.Status == StateMatrixBaseline.NoSource);
    }

    [Fact]
    public void MatrixIsMetadataDrivenAndCompact()
    {
        var rows = StateMatrixBaseline.Build(StateMatrixBaseline.FindRepositoryRoot());
        // The cross-product generator emitted ~1,880 rows; a metadata-driven matrix must be compact (#219).
        Assert.InRange(rows.Count, 200, 700);
        Assert.All(rows, row => Assert.True(row.SourceRelevant));
    }
}
