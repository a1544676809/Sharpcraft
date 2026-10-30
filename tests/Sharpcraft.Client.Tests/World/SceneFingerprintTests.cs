// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.World;
using Sharpcraft.Common.World;
using Xunit;

namespace Sharpcraft.Client.Tests.World;

/// <summary>
/// Scene fingerprints (docs/specs/render/occlusion-culling.md 5): the same world must always hash the same and a single
/// changed block must change it, otherwise the number cannot be used to prove two runs share a scene.
/// </summary>
public sealed class SceneFingerprintTests
{
    [Fact]
    public void TheSameWorldHashesTheSameTwice()
    {
        var world = Build();

        Assert.Equal(SceneFingerprint.Of(world), SceneFingerprint.Of(world));
    }

    [Fact]
    public void ADifferentSceneChangesTheHash()
    {
        var first = Build();
        var second = new ClientWorld(0, 1);
        second.AddChunk(Chunk(0, 0));

        second.SetBlockState(0, 0, 0, 0, 0);

        Assert.NotEqual(SceneFingerprint.Of(first), SceneFingerprint.Of(second));
    }

    private static ClientWorld Build()
    {
        var world = new ClientWorld(0, 1);
        world.AddChunk(Chunk(0, 0));
        world.AddChunk(Chunk(1, 0));
        return world;
    }

    private static LevelChunk Chunk(int x, int z) => new(x, z, 0, 1);
}
