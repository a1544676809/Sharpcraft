// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Framework.Tests;

/// <summary>
/// Equivalence check for the interface projection (docs/modules/ui.md 9).
/// </summary>
/// <remarks>
/// The reference draws its interface with an orthographic projection from (0, width, height, 0), which maps the top
/// left corner to the top left of the clip volume and the bottom right to the bottom right. These cases pin that
/// exact mapping, and also check that the matrix and the point helper agree, since hit testing uses the helper while
/// drawing uses the matrix and the two drifting apart would put clicks a few pixels away from the widget.
/// </remarks>
public sealed class UiProjectionTests
{
    [Theory]
    [InlineData(0f, 0f, -1f, 1f)]
    [InlineData(480f, 0f, 1f, 1f)]
    [InlineData(0f, 270f, -1f, -1f)]
    [InlineData(480f, 270f, 1f, -1f)]
    [InlineData(240f, 135f, 0f, 0f)]
    public void CornersMapToTheClipVolumeEdges(float x, float y, float clipX, float clipY)
    {
        const float width = 480f;
        const float height = 270f;

        var (actualX, actualY) = UiProjection.ToClip(x, y, width, height);

        Assert.Equal(clipX, actualX, 4);
        Assert.Equal(clipY, actualY, 4);
    }

    [Theory]
    [InlineData(480f, 270f)]
    [InlineData(320f, 180f)]
    [InlineData(640f, 360f)]
    public void TheMatrixAndThePointHelperAgree(float width, float height)
    {
        var matrix = new float[UiProjection.FloatsPerMatrix];
        UiProjection.WriteOrthographic(width, height, matrix);

        foreach (var (x, y) in new[] { (0f, 0f), (width, height), (width / 3f, height / 7f), (17f, 231f) })
        {
            // Column major: clip x = m0 * x + m12, clip y = m5 * y + m13, with w = 1.
            var fromMatrix = ((matrix[0] * x) + matrix[12], (matrix[5] * y) + matrix[13]);
            var fromHelper = UiProjection.ToClip(x, y, width, height);

            Assert.Equal(fromHelper.X, fromMatrix.Item1, 4);
            Assert.Equal(fromHelper.Y, fromMatrix.Item2, 4);
        }
    }

    [Fact]
    public void NonPositiveSizesAreRejectedRatherThanProducingAnInvertedViewport()
    {
        var matrix = new float[UiProjection.FloatsPerMatrix];

        Assert.Throws<ArgumentOutOfRangeException>(() => UiProjection.WriteOrthographic(0f, 270f, matrix));
        Assert.Throws<ArgumentOutOfRangeException>(() => UiProjection.WriteOrthographic(480f, -1f, matrix));
    }
}
