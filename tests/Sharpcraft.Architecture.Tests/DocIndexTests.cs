// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Docs.Indexer;
using Xunit;

namespace Sharpcraft.Architecture.Tests;

/// <summary>
/// Documentation gates (AGENTS.md principle 12). The generator is the single source of truth for
/// the directory indexes, and every source module must either have a doc or sit in the shrinking
/// baseline (docs/modules/doc-indexer.md).
/// </summary>
public sealed class DocIndexTests
{
    [Fact]
    public void GeneratedIndexesAreCurrent()
    {
        var stale = DocIndexer.Check(RepositoryLocator.Root);

        Assert.True(stale.Count == 0,
            "generated indexes are stale; run: dotnet run --project tools/Sharpcraft.Docs.Indexer -- --write\n"
            + string.Join("\n", stale));
    }

    [Fact]
    public void EverySourceModuleIsDocumentedOrBaselined()
    {
        var violations = SourceModules.Check(RepositoryLocator.Root);

        Assert.True(violations.Count == 0,
            "source module coverage is out of date:\n" + string.Join("\n", violations));
    }
}
