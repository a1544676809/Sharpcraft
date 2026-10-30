// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// The shared build policy (docs/specs/quality-attributes.md): the numbers live in one place, and both the interactive and
/// bulk paths go through the same overload so a caller cannot quietly invent its own budget.
/// </summary>
public sealed class MeshBuildPolicyTests
{
    [Fact]
    public void TheInteractivePolicyKeepsPassesShort()
    {
        Assert.Equal(8, MeshBuildPolicy.Interactive.MaxChunksPerPass);
        Assert.True(MeshBuildPolicy.Interactive.MaxMillisecondsPerPass <= 250d);
    }

    [Fact]
    public void TheCatchUpPolicyIsUnboundedInChunks()
    {
        Assert.Equal(int.MaxValue, MeshBuildPolicy.CatchUp.MaxChunksPerPass);
        Assert.True(MeshBuildPolicy.CatchUp.MaxMillisecondsPerPass >= 1_000d);
    }

    [Fact]
    public void ThePolicyOverloadMatchesTheExplicitCallOnAnEmptyWorld()
    {
        var world = new ClientWorld(0, 1);
        var cache = new ChunkMeshCache();

        var byPolicy = cache.Build(world, null, MeshBuildPolicy.Interactive);
        var byArguments = cache.Build(world, null, maxChunksPerBuild: 8, maxMillisecondsPerBuild: 250d);

        Assert.Equal(byArguments.Vertices.Length, byPolicy.Vertices.Length);
        Assert.Equal(0, cache.LastTotalChunks);
    }
}
