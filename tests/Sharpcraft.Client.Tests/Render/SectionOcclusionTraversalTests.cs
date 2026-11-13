// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests;

/// <summary>
/// Pins the traversal rules stated in docs/specs/render/section-occlusion.md.
/// </summary>
public sealed class SectionOcclusionTraversalTests
{
    private const int Radius = 3;
    private const int MinimumY = -1;
    private const int MaximumY = 4;

    [Fact]
    public void AnOpenWorldReturnsEverythingInRange()
    {
        var visible = Run(_ => SectionVisibility.AllVisible, out var count);

        // Every section in the box except those the monotone rule and the frustum can reach is returned; the walk must
        // at least cover the camera section and its horizontal neighbours.
        Assert.True(count > 1);
        Assert.Contains(new SectionCoord(0, 0, 0), visible[..count].ToArray());
        Assert.Contains(new SectionCoord(1, 0, 0), visible[..count].ToArray());
        Assert.Contains(new SectionCoord(-1, 0, 0), visible[..count].ToArray());
    }

    [Fact]
    public void ASolidWorldStillShowsTheCameraSectionAndItsNeighbours()
    {
        // Everything solid means nothing can be walked through, but the six neighbours are still reachable from the
        // camera section itself: the walk marks a section visible when it arrives, before asking whether it may pass.
        var visible = Run(_ => SectionVisibility.NoneVisible, out var count);

        Assert.Equal(7, count);
        Assert.Equal(new SectionCoord(0, 0, 0), visible[0]);
    }

    [Fact]
    public void ASolidSectionStopsTheStraightPathIntoIt()
    {
        // The face matrix of that one section says its faces cannot see each other, so the walk cannot pass through it.
        // It can still go around: a rock you can see past is not a wall, and the earlier rule that forbade every
        // direction already taken made exactly that mistake, keeping only seventeen of six hundred columns in game.
        var visible = Run(
            coord => coord == new SectionCoord(0, 1, 0) ? SectionVisibility.NoneVisible : SectionVisibility.AllVisible,
            out var count);

        var reported = visible[..count].ToArray();

        // Reached on arrival, before the walk asks whether it may pass.
        Assert.Contains(new SectionCoord(0, 1, 0), reported);

        // Nothing may be entered through it, but going around the column is allowed.
        Assert.Contains(new SectionCoord(1, 2, 0), reported);
    }

    [Fact]
    public void SectionsOutsideTheRadiusAreNotReported()
    {
        var visible = Run(_ => SectionVisibility.AllVisible, out var count);

        foreach (var coord in visible[..count].ToArray())
        {
            Assert.True(Math.Abs(coord.X) <= Radius, $"x {coord.X} outside radius");
            Assert.True(Math.Abs(coord.Z) <= Radius, $"z {coord.Z} outside radius");
        }
    }

    [Fact]
    public void TheWalkDoesNotAllocate()
    {
        var queue = new SectionOcclusionTraversal.TraversalNode[Volume() * SectionOcclusionTraversal.EntriesPerSection];
        var visited = new byte[Volume()];
        var output = new SectionCoord[Volume()];

        // Warm up so any first-call statics are behind us before the measurement starts.
        SectionOcclusionTraversal.Traverse(
            new SectionCoord(0, 0, 0), Radius, MinimumY, MaximumY,
            _ => true, _ => SectionVisibility.AllVisible, _ => true,
            queue, visited, output, out _);

        Func<SectionCoord, bool> loaded = _ => true;
        Func<SectionCoord, SectionVisibility> open = _ => SectionVisibility.AllVisible;
        Func<SectionCoord, bool> inFrustum = _ => true;
        var origin = new SectionCoord(0, 0, 0);
        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var iteration = 0; iteration < 16; iteration++)
        {
            SectionOcclusionTraversal.Traverse(
                origin, Radius, MinimumY, MaximumY, loaded, open, inFrustum, queue, visited, output, out _);
        }

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

    private static int Volume() => ((2 * Radius) + 1) * ((2 * Radius) + 1) * (MaximumY - MinimumY + 1);

    private static SectionCoord[] Run(Func<SectionCoord, SectionVisibility> visibility, out int count)
    {
        var queue = new SectionOcclusionTraversal.TraversalNode[Volume() * SectionOcclusionTraversal.EntriesPerSection];
        var visited = new byte[Volume()];
        var output = new SectionCoord[Volume()];

        SectionOcclusionTraversal.Traverse(
            new SectionCoord(0, 0, 0),
            Radius,
            MinimumY,
            MaximumY,
            _ => true,
            visibility,
            _ => true,
            queue,
            visited,
            output,
            out count);

        return output;
    }
}
