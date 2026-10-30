// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Platform;
using Xunit;

namespace Sharpcraft.Framework.Tests;

/// <summary>
/// Equivalence check for the interface scale (docs/modules/ui.md 9.1).
/// </summary>
/// <remarks>
/// Rather than spot checking a few window sizes, the reference's own loop is transcribed here verbatim and both
/// implementations are compared across a sweep of sizes, scale caps and the unicode flag. A transcription that
/// drifts from the original, or an implementation that only agrees on round numbers, fails here.
/// </remarks>
public sealed class GuiScaleEquivalenceTests
{
    /// <summary>The reference's calculation, transcribed from its window code.</summary>
    private static int Reference(int framebufferWidth, int framebufferHeight, int maxScale, bool enforceUnicode)
    {
        var limit = maxScale == 0 ? int.MaxValue : maxScale;
        var scale = 1;

        while (scale != limit
            && scale < framebufferWidth
            && scale < framebufferHeight
            && (framebufferWidth / (scale + 1)) >= 320
            && (framebufferHeight / (scale + 1)) >= 240)
        {
            scale++;
        }

        if (enforceUnicode && (scale % 2) != 0)
        {
            scale++;
        }

        return scale;
    }

    [Fact]
    public void TheScaleAgreesWithTheTranscribedReferenceAcrossManyWindows()
    {
        int[] caps = [0, 1, 2, 3, 4, 5];

        for (var width = 200; width <= 2400; width += 13)
        {
            for (var height = 150; height <= 1400; height += 17)
            {
                foreach (var cap in caps)
                {
                    foreach (var unicode in new[] { false, true })
                    {
                        var expected = Reference(width, height, cap, unicode);
                        var actual = GuiScale.Calculate(width, height, cap, unicode);

                        Assert.True(
                            expected == actual,
                            $"{width}x{height} cap {cap} unicode {unicode}: expected {expected}, got {actual}");
                    }
                }
            }
        }
    }

    [Fact]
    public void TheCanvasAgreesWithTheReferenceRoundingAcrossManyWindows()
    {
        for (var width = 200; width <= 2400; width += 29)
        {
            for (var height = 150; height <= 1400; height += 31)
            {
                var scale = Reference(width, height, 0, false);
                var (expectedWidth, expectedHeight) = (
                    (int)Math.Ceiling(width / (double)scale),
                    (int)Math.Ceiling(height / (double)scale));

                var (width2, height2) = GuiScale.ScaledSize(width, height, scale);

                Assert.Equal((expectedWidth, expectedHeight), (width2, height2));
            }
        }
    }

    [Fact]
    public void TheCanvasIsNeverSmallerThanTheReferenceMinimum()
    {
        for (var width = 200; width <= 2400; width += 41)
        {
            for (var height = 150; height <= 1400; height += 43)
            {
                // The minimum only applies to windows large enough to satisfy it; a window below the minimum gets
                // scale one and a correspondingly small canvas, exactly as the reference does.
                if (width < 320 || height < 240)
                {
                    continue;
                }

                var scale = GuiScale.Calculate(width, height);
                var (canvasWidth, canvasHeight) = GuiScale.ScaledSize(width, height, scale);

                // Within one pixel of rounding, the canvas must still cover the minimum the scale rule protects.
                Assert.True(canvasWidth >= 320 - 1, $"{width}x{height}: canvas width {canvasWidth}");
                Assert.True(canvasHeight >= 240 - 1, $"{width}x{height}: canvas height {canvasHeight}");
            }
        }
    }
}
