// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// Decides how large the geometry buffers should be when the arena outgrows them
/// (docs/specs/network/play-movement.md 7.27).
/// </summary>
/// <remarks>
/// Doubling alone means the arena rewrites every resident chunk about log2(n) times while the world streams
/// in, and each of those rewrites is a visible spike. The client knows how many chunks its view distance can
/// hold, so once enough chunks have arrived to estimate an average size, the arena can size itself for the
/// whole view in one deliberate step and then not grow again.
/// </remarks>
public static class ArenaSizing
{
    /// <summary>Chunks needed before the average size is trusted enough to presize.</summary>
    public const int SamplesForPresize = 16;

    /// <summary>Slack over the estimate, so small variation does not force another growth.</summary>
    public const double PresizeSlack = 1.3;

    /// <summary>
    /// Capacity to use next.
    /// </summary>
    /// <param name="currentCapacity">Capacity in use now.</param>
    /// <param name="needed">Bytes the new geometry requires.</param>
    /// <param name="residentBytes">Bytes of geometry currently resident.</param>
    /// <param name="residentChunks">Chunks currently resident.</param>
    /// <param name="expectedChunks">Chunks the view distance can hold, or 0 when unknown.</param>
    /// <param name="alreadyPresized">True once a presize has happened; further growth doubles.</param>
    public static ulong NextCapacity(
        ulong currentCapacity,
        ulong needed,
        ulong residentBytes,
        int residentChunks,
        int expectedChunks,
        bool alreadyPresized)
    {
        var doubled = Math.Max(currentCapacity * 2, needed);

        if (alreadyPresized || expectedChunks <= 0 || residentChunks < SamplesForPresize)
        {
            return doubled;
        }

        var averageBytes = residentChunks == 0 ? 0d : (double)residentBytes / residentChunks;
        var projected = (ulong)(averageBytes * expectedChunks * PresizeSlack);

        // Presizing is only worth it when it actually covers the whole view; otherwise keep doubling.
        return Math.Max(doubled, Math.Max(projected, needed));
    }
}
