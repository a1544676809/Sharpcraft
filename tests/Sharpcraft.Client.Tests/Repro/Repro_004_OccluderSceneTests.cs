// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Game;

namespace Sharpcraft.Client.Tests.Repro;

/// <summary>
/// Issue 4: the occlusion scene rendered empty because nothing ever called the builder. The scene
/// itself has to contain solid geometry before a cull rate can mean anything.
/// </summary>
public sealed class Repro_004_OccluderSceneTests
{
    [Fact]
    public void TheSceneContainsTheWallAndTheChunksHiddenBehindIt()
    {
        var world = FrameCommand.BuildOccluderWorld();

        // Seven chunks of wall plus three wide by four deep behind it.
        Assert.Equal(20, world.LoadedChunkCount);
    }

    [Fact]
    public void EverySceneChunkCarriesSolidSections()
    {
        var world = FrameCommand.BuildOccluderWorld();

        var solidSections = 0;
        foreach (var chunk in world.GetChunks())
        {
            Assert.True(chunk.Loaded, "a chunk that is not loaded is skipped by the mesher");

            for (var section = -1; section <= 1; section++)
            {
                if (chunk.GetSection(section) is not null)
                {
                    solidSections++;
                }
            }
        }

        // Nineteen solid chunks at three sections each; the twentieth holds only the chest block entity.
        Assert.Equal(19 * 3, solidSections);

        // A block inside the wall reads back as the solid state the scene filled every section with.
        Assert.Equal(1, world.GetBlockState(0, 0, 0));
    }
}
