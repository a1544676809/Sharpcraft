// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Reference;

namespace Sharpcraft.Client.Render;

/// <summary>One 16³ section in section coordinates.</summary>
/// <param name="X">Section X.</param>
/// <param name="Y">Section Y.</param>
/// <param name="Z">Section Z.</param>
[ReferenceClass("net.minecraft.core.SectionPos", "ref://mc-26.3-analysis/src/net/minecraft/core/SectionPos.java", Kind = ReferenceKind.Mirror, Status = ReferenceStatus.Partial, Note = "only the coordinate triple and a face step")]
public readonly record struct SectionCoord(int X, int Y, int Z)
{
    /// <summary>Steps one section along a face direction (west, east, down, up, north, south).</summary>
    /// <param name="face">Face index, 0..5.</param>
    public SectionCoord Step(int face) => face switch
    {
        0 => this with { X = X - 1 },
        1 => this with { X = X + 1 },
        2 => this with { Y = Y - 1 },
        3 => this with { Y = Y + 1 },
        4 => this with { Z = Z - 1 },
        _ => this with { Z = Z + 1 }
    };
}

/// <summary>
/// Walks the sections around the camera and keeps only those reachable through open space.
/// </summary>
/// <remarks>
/// A section whose face matrix says the entry and exit faces are separated by rock cannot be crossed, which stops the
/// walk at solid ground instead of descending into everything buried below it.
/// <para>
/// Each path carries the directions it has already taken and refuses their opposites, so a sight line cannot leave the
/// camera, turn, and come back to claim something behind it. That mask travels with the node and is never shared
/// between paths: sharing it is what once cut six hundred columns down to seventeen.
/// </para>
/// <para>
/// A section may be entered more than once, but only through a different face each time. One flag per section is not
/// enough: a hillside reached first through its solid west wall would burn its only mark and lock out the open path
/// down from the sky above it, which fragments the surface. Six bits per section record which faces have been used,
/// and a seventh remembers whether the section has been reported. The queue is sized for the worst case of three
/// entries per section.
/// </para>
/// <para>
/// Nothing is allocated: the queue, the visited marks and the output all belong to the caller. The three delegates are
/// called once per section per walk, so the caller must hold them in fields rather than create them per frame.
/// </para>
/// </remarks>
[ReferenceClass("net.minecraft.client.renderer.SectionOcclusionGraph", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/SectionOcclusionGraph.java", Kind = ReferenceKind.Mirror, Status = ReferenceStatus.Partial, Note = "traversal semantics only; the reference organises sections in a tree and we walk a flat grid")]
public static class SectionOcclusionTraversal
{
    /// <summary>Marks a section the camera stands in: six because it is the sum of all six directions.</summary>
    public const byte AllDirections = 0x3F;

    /// <summary>No face recorded yet; the camera section is entered from nowhere.</summary>
    public const byte NoEntryFace = 0xFF;

    /// <summary>Visited bit that means the section has already been written to the output.</summary>
    public const byte Reported = 0x80;

    /// <summary>How many times one section may be entered: once per axis, so one per face of a hemisphere.</summary>
    public const int EntriesPerSection = 3;

    /// <summary>Walks from the camera section, appending reachable sections to the output.</summary>
    /// <param name="cameraSection">Section the camera occupies.</param>
    /// <param name="radius">Reach in sections along X and Z.</param>
    /// <param name="minimumY">Lowest section Y that exists.</param>
    /// <param name="maximumY">Highest section Y that exists.</param>
    /// <param name="isLoaded">Whether a section has geometry to consider.</param>
    /// <param name="visibility">Face matrix of a section, or <see cref="SectionVisibility.AllVisible"/> if unknown.</param>
    /// <param name="isInFrustum">Whether a section's box intersects the view frustum.</param>
    /// <param name="queue">Scratch queue, at least <see cref="EntriesPerSection"/> times the searched volume.</param>
    /// <param name="visited">Scratch marks, at least the size of the searched volume; cleared here.</param>
    /// <param name="output">Receives the reachable sections.</param>
    /// <param name="outputCount">Number written to <paramref name="output"/>.</param>
    public static void Traverse(
        SectionCoord cameraSection,
        int radius,
        int minimumY,
        int maximumY,
        Func<SectionCoord, bool> isLoaded,
        Func<SectionCoord, SectionVisibility> visibility,
        Func<SectionCoord, bool> isInFrustum,
        Span<TraversalNode> queue,
        Span<byte> visited,
        Span<SectionCoord> output,
        out int outputCount)
    {
        ArgumentNullException.ThrowIfNull(isLoaded);
        ArgumentNullException.ThrowIfNull(visibility);
        ArgumentNullException.ThrowIfNull(isInFrustum);

        outputCount = 0;
        visited.Clear();

        if (radius < 0 || minimumY > maximumY)
        {
            return;
        }

        var volume = Volume(radius, minimumY, maximumY);

        if (visited.Length < volume || queue.Length < volume * EntriesPerSection || output.Length < volume)
        {
            throw new ArgumentException("scratch buffers are too small for the searched volume");
        }

        var head = 0;
        var tail = 0;

        // The camera can sit outside the world's vertical range - high altitude, or the moment a
        // teleport lands - and Index would then compute a slot outside the scratch buffers. Only the
        // start's Y is clamped: the XZ centre stays the camera's, so looking down from above still
        // floods from the nearest real layer instead of culling everything away.
        var startCoord = new SectionCoord(
            cameraSection.X,
            Math.Clamp(cameraSection.Y, minimumY, maximumY),
            cameraSection.Z);
        var startSlot = Index(startCoord, cameraSection, minimumY, radius);
        visited[startSlot] = Reported;
        queue[tail++] = new TraversalNode(startCoord, NoEntryFace, 0);
        output[outputCount++] = startCoord;

        while (head < tail)
        {
            var current = queue[head++];
            var matrix = visibility(current.Coord);

            for (var face = 0; face < 6; face++)
            {
                var enteredBy = face ^ 1;

                // This path has already gone this way and back once; a sight line cannot bend back over itself.
                if ((current.DirectionsTaken & (1 << enteredBy)) != 0)
                {
                    continue;
                }

                // Cross a section only where its own open space connects the face we enter by to the face we leave by.
                if (current.EntryFace != NoEntryFace && !matrix.CanSee(current.EntryFace, face))
                {
                    continue;
                }

                var next = current.Coord.Step(face);

                if (next.Y < minimumY || next.Y > maximumY)
                {
                    continue;
                }

                if (Math.Abs(next.X - cameraSection.X) > radius || Math.Abs(next.Z - cameraSection.Z) > radius)
                {
                    continue;
                }

                if (!isLoaded(next) || !isInFrustum(next))
                {
                    continue;
                }

                var slot = Index(next, cameraSection, minimumY, radius);
                var entryBit = (byte)(1 << enteredBy);

                // Each face may deliver a section once, so a solid approach cannot burn the mark an open one needs.
                if ((visited[slot] & entryBit) != 0)
                {
                    continue;
                }

                if ((visited[slot] & Reported) == 0)
                {
                    visited[slot] |= Reported;

                    if (outputCount < output.Length)
                    {
                        output[outputCount++] = next;
                    }
                }

                visited[slot] |= entryBit;
                queue[tail++] = new TraversalNode(
                    next,
                    (byte)enteredBy,
                    (byte)(current.DirectionsTaken | (1 << face)));
            }
        }
    }

    /// <summary>Number of sections in the searched box.</summary>
    /// <param name="radius">Reach along X and Z.</param>
    /// <param name="minimumY">Lowest section Y.</param>
    /// <param name="maximumY">Highest section Y.</param>
    public static int Volume(int radius, int minimumY, int maximumY)
    {
        var span = (2 * radius) + 1;
        return span * span * (maximumY - minimumY + 1);
    }

    private static int Index(SectionCoord coord, SectionCoord camera, int minimumY, int radius)
    {
        var span = (2 * radius) + 1;
        var x = coord.X - camera.X + radius;
        var z = coord.Z - camera.Z + radius;
        var y = coord.Y - minimumY;
        return ((y * span) + z) * span + x;
    }

    /// <summary>One section waiting to be expanded, with the direction it was entered from.</summary>
    /// <param name="Coord">Section.</param>
    /// <param name="EntryFace">Face it was entered through, or <see cref="NoEntryFace"/>.</param>
    /// <param name="DirectionsTaken">Directions used along this path.</param>
    [ReferenceClass("net.minecraft.client.renderer.SectionOcclusionGraph", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/SectionOcclusionGraph.java", Kind = ReferenceKind.Mirror, Status = ReferenceStatus.Partial, Note = "the reference node also carries a step count; behaviour here needs only the direction state")]
    public readonly record struct TraversalNode(SectionCoord Coord, byte EntryFace, byte DirectionsTaken);
}
