// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.ClassMap;
using Xunit;

namespace Sharpcraft.Architecture.Tests;

/// <summary>
/// "Implementation ↔ reference class" map gate (AGENTS.md 3.5). Every public Sharpcraft type must carry a
/// [ReferenceClass] annotation or sit in the shrinking docs/src/.classmap-baseline.txt; the generated page must be current.
/// </summary>
public sealed class ClassMapTests
{
    [Fact]
    public void ClassMapIsCurrent()
    {
        var violations = ClassMapTool.Check(RepositoryLocator.Root);
        Assert.True(violations.Count == 0,
            "class map violations (run: dotnet run --project tools/Sharpcraft.ClassMap -- --write [--update-baseline]):\n"
            + string.Join("\n", violations));
    }
}

