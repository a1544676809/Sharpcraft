// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests;

/// <summary>
/// Pins the two index bases the occlusion state juggles (T-833).
/// </summary>
/// <remarks>
/// A bit position is counted from the world's lowest section because that is how the renderer names a section; a
/// mesh's matrix array is counted from that chunk's lowest section because that is how the mesher filled it. Mixing
/// them up shifts every lookup and shows up as terrain the camera can see being culled, which is hard to trace from
/// the outside, so they are pinned here.
/// </remarks>
public sealed class SectionOcclusionStateTests
{
    private const int MinimumY = -4;
    private const int MaximumY = 19;

    [Fact]
    public void ABitIsCountedFromTheWorldsLowestSection()
    {
        var state = Closed(Mesh(SectionVisibility.AllVisible, messMinY: MinimumY));
        var camera = new SectionCoord(0, 5, 0);

        Assert.True(state.Refresh(camera));

        // Section Y = 5 sits nine sections above the world floor, and that is the bit the renderer will ask about.
        Assert.True(state.Test(ClientWorld.Pack(0, 0), 5 - MinimumY));
        Assert.True(state.Test(ClientWorld.Pack(0, 0), 6 - MinimumY));
    }

    [Fact]
    public void TheCameraSectionItselfIsAlwaysKept()
    {
        var state = Closed(Mesh(SectionVisibility.AllVisible, messMinY: MinimumY));
        var camera = new SectionCoord(0, 5, 0);

        state.Refresh(camera);

        Assert.True(state.Test(ClientWorld.Pack(camera.X, camera.Z), camera.Y - MinimumY));
    }

    [Fact]
    public void SectionsTheWalkNeverReachedAreNotKept()
    {
        var state = Closed(Mesh(SectionVisibility.AllVisible, messMinY: MinimumY));

        state.Refresh(new SectionCoord(0, 5, 0));

        // A chunk that was never visited has no entry at all.
        Assert.False(state.Test(ClientWorld.Pack(100, 100), 0));
    }

    [Fact]
    public void ASolidSectionStopsTheWalkBehindIt()
    {
        // Everything solid: the neighbours are still reached, but nothing beyond them.
        var state = Closed(Mesh(SectionVisibility.NoneVisible, messMinY: MinimumY));
        var camera = new SectionCoord(0, 5, 0);

        state.Refresh(camera);

        Assert.True(state.Test(ClientWorld.Pack(0, 0), 5 - MinimumY));
        Assert.True(state.Test(ClientWorld.Pack(0, 0), 6 - MinimumY));
        Assert.False(state.Test(ClientWorld.Pack(0, 0), 9 - MinimumY));
    }

    [Fact]
    public void AMissingMeshDoesNotStopTheWalk()
    {
        var state = new SectionOcclusionState(
            2,
            MinimumY,
            MaximumY,
            _ => true,
            _ => null,
            () => new SectionCoord(0, 5, 0),
            () => 0);

        state.Refresh(new SectionCoord(0, 5, 0));

        // Without geometry there is nothing proven solid, so the far section behind it stays reachable.
        Assert.True(state.Test(ClientWorld.Pack(0, 0), 9 - MinimumY));
    }

    [Fact]
    public void RebuildingIsSkippedWhenTheCameraHasNotMoved()
    {
        var state = Closed(Mesh(SectionVisibility.AllVisible, messMinY: MinimumY));
        var camera = new SectionCoord(0, 5, 0);

        Assert.True(state.Refresh(camera));
        Assert.False(state.Refresh(camera));
    }

    private static SectionOcclusionState Closed(MeshData mesh) => new(
        2,
        MinimumY,
        MaximumY,
        _ => true,
        _ => mesh,
        () => new SectionCoord(0, 5, 0),
        () => 0);

    private static MeshData Mesh(SectionVisibility sight, int messMinY)
    {
        var matrices = new SectionVisibility[MaximumY - MinimumY + 1];
        Array.Fill(matrices, sight);
        return new MeshData([], [], 0, SectionStarts: null, SectionVisibility: matrices, SectionMinY: messMinY);
    }
}
