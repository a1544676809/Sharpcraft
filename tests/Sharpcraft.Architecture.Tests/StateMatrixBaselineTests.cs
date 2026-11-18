// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Equivalence.Audit;
using Xunit;

namespace Sharpcraft.Architecture.Tests;

/// <summary>task-273: every state-matrix source row must resolve to a real sample file and key.</summary>
[CollectionDefinition("StateMatrixBaseline", DisableParallelization = true)]
public sealed class StateMatrixBaselineCollection;

[Collection("StateMatrixBaseline")]
public sealed class StateMatrixBaselineTests
{
    [Fact]
    public void SourceRowsResolveRealFilesAndKeysAndNoSourceRowsAreNotStrict()
    {
        var root = StateMatrixBaseline.FindRepositoryRoot();
        var rows = StateMatrixBaseline.Build(root);
        Assert.NotEmpty(rows);
        Assert.Contains(rows, row => row.Status == StateMatrixBaseline.SourceSample && row.TypeKind == "entity");
        Assert.Contains(rows, row => row.Status == StateMatrixBaseline.SourceConstraint && row.StateClass is "energy" or "glint");
        Assert.Contains(rows, row => row.Status == StateMatrixBaseline.NoSource);

        foreach (var row in rows.Where(row => row.Strict))
        {
            Assert.NotEqual(string.Empty, row.SampleFile);
            Assert.NotEqual(string.Empty, row.SampleKey);
            Assert.True(File.Exists(Path.Combine(root, row.SampleFile)), row.SampleFile);
        }

        foreach (var row in rows.Where(row => row.Status == StateMatrixBaseline.NoSource))
        {
            Assert.False(row.Strict);
            Assert.Equal(string.Empty, row.SampleFile);
            Assert.Equal(string.Empty, row.SampleKey);
        }
    }
}
