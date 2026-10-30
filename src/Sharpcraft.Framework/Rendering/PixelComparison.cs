// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// Result of comparing two RGBA frames.
/// </summary>
/// <param name="Mismatches">Pixels differing beyond the tolerance.</param>
/// <param name="MaxChannelDelta">Largest single channel difference seen.</param>
/// <param name="FirstX">X of the first mismatching pixel, or -1.</param>
/// <param name="FirstY">Y of the first mismatching pixel, or -1.</param>
public readonly record struct PixelDifference(int Mismatches, int MaxChannelDelta, int FirstX, int FirstY);

/// <summary>
/// Compares rendered frames, so a claim about pixels is a number rather than an impression.
/// </summary>
/// <remarks>
/// Used to prove that an optimisation changed nothing (culling against an unculled reference) and that a known colour
/// arrives where it should. Tolerance exists because a blend may round by one; an opaque fill must match exactly, which
/// the caller expresses by passing zero.
/// </remarks>
public static class PixelComparison
{
    /// <summary>Compares two RGBA buffers of the same size.</summary>
    /// <param name="expected">Reference pixels.</param>
    /// <param name="actual">Pixels under test.</param>
    /// <param name="width">Frame width.</param>
    /// <param name="height">Frame height.</param>
    /// <param name="tolerance">Largest per-channel difference still accepted.</param>
    public static PixelDifference Compare(
        ReadOnlySpan<byte> expected,
        ReadOnlySpan<byte> actual,
        int width,
        int height,
        int tolerance = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tolerance);

        var pixels = width * height * 4;

        if (expected.Length < pixels || actual.Length < pixels)
        {
            throw new ArgumentException("both buffers must hold width times height RGBA pixels", nameof(actual));
        }

        var mismatches = 0;
        var worst = 0;
        var firstX = -1;
        var firstY = -1;

        for (var index = 0; index < pixels; index += 4)
        {
            var overTolerance = false;

            for (var channel = 0; channel < 4; channel++)
            {
                var delta = Math.Abs(expected[index + channel] - actual[index + channel]);
                worst = Math.Max(worst, delta);

                if (delta > tolerance)
                {
                    overTolerance = true;
                }
            }

            if (overTolerance)
            {
                if (firstX < 0)
                {
                    var pixel = index / 4;
                    firstX = pixel % width;
                    firstY = pixel / width;
                }

                mismatches++;
            }
        }

        return new PixelDifference(mismatches, worst, firstX, firstY);
    }

    /// <summary>Reads one pixel as packed ARGB, which is the form colours are written in elsewhere.</summary>
    /// <param name="rgba">RGBA pixels.</param>
    /// <param name="width">Frame width.</param>
    /// <param name="x">Pixel x.</param>
    /// <param name="y">Pixel y.</param>
    public static uint ArgbAt(ReadOnlySpan<byte> rgba, int width, int x, int y)
    {
        var offset = ((y * width) + x) * 4;

        return ((uint)rgba[offset + 3] << 24) | ((uint)rgba[offset] << 16) | ((uint)rgba[offset + 1] << 8) | rgba[offset + 2];
    }
}
