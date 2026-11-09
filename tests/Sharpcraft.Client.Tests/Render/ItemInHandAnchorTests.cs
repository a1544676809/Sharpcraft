// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Contract of the solved held-item anchor.
/// <para>
/// The anchor is independent of the item's display transform: the display is a separate factor of the reference composite
/// (<c>H · XP(-90°) · YP(180°) · T_off · D</c>) and is applied by the caller. It depends only on the hand's <c>T_off</c>
/// and on the holder's model scale. <see cref="ItemInHandPlacementComparisonTests"/> pins the resulting world points
/// against reference matrices for two poses and two display transforms.
/// </para>
/// </summary>
public sealed class ItemInHandAnchorTests
{
    [Fact]
    public void TheAdultAnchorMatchesTheSolvedMatrix()
    {
        var anchor = ReferenceFrame.HeldItemAnchor(ReferenceFrame.AdultOffset, modelScale: 0.9f);

        // Solved linear part: 0.9 · Rx(-90°).
        Assert.Equal(0.9f, anchor.M11, 5);
        Assert.Equal(-0.9f, anchor.M23, 5);
        Assert.Equal(0.9f, anchor.M32, 5);
        Assert.Equal(0f, anchor.M22, 5);
        Assert.Equal(0f, anchor.M33, 5);

        // Solved translation: 0.9 · (1, -10, -2)/16.
        Assert.Equal(0.05625f, anchor.Translation.X, 5);
        Assert.Equal(-0.5625f, anchor.Translation.Y, 5);
        Assert.Equal(-0.1125f, anchor.Translation.Z, 5);
    }

    [Fact]
    public void TheAnchorScalesWithTheModel()
    {
        var adult = ReferenceFrame.HeldItemAnchor(ReferenceFrame.AdultOffset, 0.9f);
        var baby = ReferenceFrame.HeldItemAnchor(ReferenceFrame.BabyOffset, 0.5f);

        Assert.NotEqual(adult.Translation, baby.Translation);
        Assert.True(UniformScale(baby) < UniformScale(adult));
    }

    [Fact]
    public void TheLeftHandMirrorsTheOffset()
    {
        var right = ReferenceFrame.HeldItemAnchor(ReferenceFrame.AdultOffset, 0.9f);
        var left = ReferenceFrame.HeldItemAnchor(
            new Vector3(-ReferenceFrame.AdultOffset.X, ReferenceFrame.AdultOffset.Y, ReferenceFrame.AdultOffset.Z),
            0.9f);

        Assert.Equal(-right.Translation.X, left.Translation.X, 5);
        Assert.Equal(right.Translation.Y, left.Translation.Y, 5);
        Assert.Equal(right.Translation.Z, left.Translation.Z, 5);
    }

    [Fact]
    public void TheAnchorDoesNotCarryTheDisplayTransform()
    {
        // The same anchor serves a generated item and a shield: the display transform is applied outside it.
        var anchor = ReferenceFrame.HeldItemAnchor(ReferenceFrame.AdultOffset, 0.9f);
        Assert.Equal(0.9f, UniformScale(anchor), 5);
    }

    private static float UniformScale(in Matrix4x4 matrix) =>
        new Vector3(matrix.M11, matrix.M12, matrix.M13).Length();
}
