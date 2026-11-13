// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests;

/// <summary>
/// Checks the section face matrix against the behaviours the specification states
/// (docs/specs/render/section-occlusion.md).
/// </summary>
public sealed class SectionVisibilityTests
{
    private const int West = 0;
    private const int East = 1;
    private const int Down = 2;
    private const int Up = 3;
    private const int North = 4;
    private const int South = 5;

    [Fact]
    public void AnEmptySectionLetsEveryFaceSeeEveryOther()
    {
        var visibility = Compute(_ => false);

        Assert.Equal(SectionVisibility.AllVisible, visibility);
    }

    [Fact]
    public void ASectionUnderTheBlockingThresholdIsTreatedAsEmpty()
    {
        // One short of a full face: still cannot separate anything, so the answer is the cheap one.
        var visibility = Compute(cell => cell < SectionVisibilityCompute.MinimumBlockingCells - 1);

        Assert.Equal(SectionVisibility.AllVisible, visibility);
    }

    [Fact]
    public void ASolidSectionHidesEveryFaceFromEveryOther()
    {
        var visibility = Compute(_ => true);

        Assert.Equal(SectionVisibility.NoneVisible, visibility);
    }

    [Fact]
    public void ASolidFloorStopsTheFaceItCoversFromSeeingAnything()
    {
        // A complete layer at y = 0 fills 256 cells, which is exactly the threshold, so the flood fill runs and finds
        // that the open space above never touches the down face.
        var visibility = Compute(cell => (cell >> 8) == 0);

        Assert.False(visibility.CanSee(Down, Up));
        Assert.False(visibility.CanSee(Up, Down));
        Assert.False(visibility.CanSee(Down, West));

        // Everything above the floor is one cavity, so those faces all see each other.
        Assert.True(visibility.CanSee(Up, West));
        Assert.True(visibility.CanSee(West, East));
        Assert.True(visibility.CanSee(North, South));
    }

    [Fact]
    public void AStandardSurfaceSectionConnectsEverythingButDown()
    {
        // The shape of a real surface section: the bottom half is ground and the top half is open sky. The open space
        // reaches all four sides and the top, so those five faces must all see each other; the down face is covered by
        // the ground and can see nothing. If this fails, the flood fill is wrong and no amount of traversal work will
        // make the occlusion walk trustworthy (T-833).
        var visibility = Compute(cell => (cell >> 8) < 8);

        Assert.True(visibility.CanSee(Up, West), "up cannot see west");
        Assert.True(visibility.CanSee(West, East), "west cannot see east");
        Assert.True(visibility.CanSee(North, South), "north cannot see south");
        Assert.True(visibility.CanSee(Up, North), "up cannot see north");
        Assert.False(visibility.CanSee(Down, Up), "down should be covered");
        Assert.False(visibility.CanSee(Down, West), "down should be covered");
    }
    [Fact]
    public void TheMatrixIsSymmetric()
    {
        var visibility = Compute(cell => (cell >> 8) == 0);

        for (var from = 0; from < 6; from++)
        {
            for (var to = 0; to < 6; to++)
            {
                Assert.Equal(visibility.CanSee(from, to), visibility.CanSee(to, from));
            }
        }
    }

    [Fact]
    public void OpenSpaceThatWrapsAPillarStillConnectsEveryFace()
    {
        // A single column in the middle does not divide the section, so the flood fill reaches all six faces from one
        // cavity and reports the whole matrix.
        var visibility = Compute(cell =>
        {
            var x = cell & 15;
            var z = (cell >> 4) & 15;
            return x == 8 && z == 8;
        });

        Assert.Equal(SectionVisibility.AllVisible, visibility);
    }

    [Fact]
    public void TheScratchBuffersAreReusedWithoutAllocating()
    {
        Span<ulong> blocking = stackalloc ulong[SectionVisibilityCompute.CellWordCount];
        Span<ulong> visited = stackalloc ulong[SectionVisibilityCompute.CellWordCount];
        Span<ushort> queue = stackalloc ushort[SectionVisibilityCompute.CellCount];

        blocking.Clear();
        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var iteration = 0; iteration < 32; iteration++)
        {
            blocking.Clear();
            blocking[0] = ulong.MaxValue;
            SectionVisibilityCompute.Compute(blocking, visited, queue);
        }

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

    private static SectionVisibility Compute(Func<int, bool> blocks)
    {
        Span<ulong> blocking = stackalloc ulong[SectionVisibilityCompute.CellWordCount];
        Span<ulong> visited = stackalloc ulong[SectionVisibilityCompute.CellWordCount];
        Span<ushort> queue = stackalloc ushort[SectionVisibilityCompute.CellCount];

        blocking.Clear();
        for (var cell = 0; cell < SectionVisibilityCompute.CellCount; cell++)
        {
            if (blocks(cell))
            {
                blocking[cell >> 6] |= 1UL << (cell & 63);
            }
        }

        return SectionVisibilityCompute.Compute(blocking, visited, queue);
    }
}
