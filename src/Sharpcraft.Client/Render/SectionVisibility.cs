// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Common.Reference;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Which faces of a 16³ section can see each other through its open space, as a 36 bit symmetric matrix.
/// </summary>
/// <remarks>
/// Bit <c>from * 6 + to</c> is set when an open path connects those two faces, so a section whose top and bottom are
/// separated by solid rock reports that they cannot see each other and the traversal stops there instead of walking into
/// everything buried underneath. Face order is west, east, down, up, north, south
/// (docs/specs/render/section-occlusion.md).
/// </remarks>
[ReferenceClass("net.minecraft.client.renderer.chunk.VisibilitySet", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/chunk/VisibilitySet.java", Kind = ReferenceKind.Mirror, Status = ReferenceStatus.Full)]
public readonly record struct SectionVisibility(ulong Bits)
{
    /// <summary>Every face sees every other face; what an almost empty section resolves to.</summary>
    public static readonly SectionVisibility AllVisible = new((1UL << 36) - 1UL);

    /// <summary>No face sees any other face; what a completely solid section resolves to.</summary>
    public static readonly SectionVisibility NoneVisible = new(0UL);

    /// <summary>Tests one direction pair.</summary>
    /// <param name="from">Face the path enters through, 0..5.</param>
    /// <param name="to">Face it leaves through, 0..5.</param>
    public bool CanSee(int from, int to) => (Bits & (1UL << ((from * 6) + to))) != 0;
}

/// <summary>
/// Computes <see cref="SectionVisibility"/> from which of a section's 4096 cells block sight entirely.
/// </summary>
/// <remarks>
/// The reference builds the same table with a flood fill over the open cells, seeded from the section boundary: a
/// cavity that touches several faces proves those faces can see each other. Two shortcuts make it cheap in practice —
/// fewer than 256 blocking cells cannot fill a whole face, so everything is visible, and a fully blocking section
/// resolves immediately. Nothing is allocated here; the scratch buffers come from the caller because this runs on the
/// meshing path.
/// </remarks>
[ReferenceClass("net.minecraft.client.renderer.chunk.VisGraph", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/chunk/VisGraph.java", Kind = ReferenceKind.Mirror, Status = ReferenceStatus.Full)]
public static class SectionVisibilityCompute
{
    /// <summary>Cells that must block before any face pair can be separated.</summary>
    public const int MinimumBlockingCells = 256;

    /// <summary>Cells in a section.</summary>
    public const int CellCount = 4096;

    /// <summary>Words needed to hold one bit per cell.</summary>
    public const int CellWordCount = CellCount / 64;

    /// <summary>Resolves the face matrix for one section.</summary>
    /// <param name="blocking">One bit per cell, set where the block blocks sight entirely. Consumed, not preserved.</param>
    /// <param name="visited">Scratch of at least <see cref="CellWordCount"/> words, overwritten.</param>
    /// <param name="queue">Scratch of at least <see cref="CellCount"/> entries, overwritten.</param>
    public static SectionVisibility Compute(Span<ulong> blocking, Span<ulong> visited, Span<ushort> queue)
    {
        if (blocking.Length < CellWordCount)
        {
            throw new ArgumentException("blocking bitmap is too small", nameof(blocking));
        }

        var blockingCount = 0;
        for (var index = 0; index < CellWordCount; index++)
        {
            blockingCount += BitOperations.PopCount(blocking[index]);
        }

        if (blockingCount < MinimumBlockingCells)
        {
            return SectionVisibility.AllVisible;
        }

        if (blockingCount == CellCount)
        {
            return SectionVisibility.NoneVisible;
        }

        blocking[..CellWordCount].CopyTo(visited);
        var bits = 0UL;

        for (var cell = 0; cell < CellCount; cell++)
        {
            // Only boundary cells can touch a face, so only they can seed a cavity that matters.
            if (!IsBoundary(cell) || IsSet(visited, cell))
            {
                continue;
            }

            var touchedFaces = FloodFill(visited, queue, cell);
            bits |= ConnectPairs(touchedFaces);
        }

        return new SectionVisibility(bits);
    }

    private static bool IsBoundary(int cell)
    {
        var x = cell & 15;
        var z = (cell >> 4) & 15;
        var y = (cell >> 8) & 15;
        return x is 0 or 15 || y is 0 or 15 || z is 0 or 15;
    }

    private static bool IsSet(Span<ulong> bits, int cell) => (bits[cell >> 6] & (1UL << (cell & 63))) != 0;

    private static void Set(Span<ulong> bits, int cell) => bits[cell >> 6] |= 1UL << (cell & 63);

    /// <summary>Walks one open cavity and returns the six bit set of faces it touches.</summary>
    private static int FloodFill(Span<ulong> visited, Span<ushort> queue, int seed)
    {
        var head = 0;
        var tail = 0;
        queue[tail++] = (ushort)seed;
        Set(visited, seed);
        var faces = 0;

        while (head < tail)
        {
            var cell = queue[head++];
            faces |= FacesTouched(cell);

            var x = cell & 15;
            var z = (cell >> 4) & 15;
            var y = (cell >> 8) & 15;

            if (x > 0)
            {
                Visit(visited, queue, ref tail, cell - 1);
            }

            if (x < 15)
            {
                Visit(visited, queue, ref tail, cell + 1);
            }

            if (z > 0)
            {
                Visit(visited, queue, ref tail, cell - 16);
            }

            if (z < 15)
            {
                Visit(visited, queue, ref tail, cell + 16);
            }

            if (y > 0)
            {
                Visit(visited, queue, ref tail, cell - 256);
            }

            if (y < 15)
            {
                Visit(visited, queue, ref tail, cell + 256);
            }
        }

        return faces;
    }

    private static void Visit(Span<ulong> visited, Span<ushort> queue, ref int tail, int cell)
    {
        if (IsSet(visited, cell))
        {
            return;
        }

        Set(visited, cell);
        queue[tail++] = (ushort)cell;
    }

    private static int FacesTouched(int cell)
    {
        var x = cell & 15;
        var z = (cell >> 4) & 15;
        var y = (cell >> 8) & 15;
        var faces = 0;

        if (x == 0)
        {
            faces |= 1 << 0;
        }
        else if (x == 15)
        {
            faces |= 1 << 1;
        }

        if (y == 0)
        {
            faces |= 1 << 2;
        }
        else if (y == 15)
        {
            faces |= 1 << 3;
        }

        if (z == 0)
        {
            faces |= 1 << 4;
        }
        else if (z == 15)
        {
            faces |= 1 << 5;
        }

        return faces;
    }

    /// <summary>Turns a set of faces into every ordered pair within it, both directions.</summary>
    private static ulong ConnectPairs(int faceMask)
    {
        var bits = 0UL;

        for (var from = 0; from < 6; from++)
        {
            if ((faceMask & (1 << from)) == 0)
            {
                continue;
            }

            for (var to = from; to < 6; to++)
            {
                if ((faceMask & (1 << to)) == 0)
                {
                    continue;
                }

                bits |= 1UL << ((from * 6) + to);
                bits |= 1UL << ((to * 6) + from);
            }
        }

        return bits;
    }
}
