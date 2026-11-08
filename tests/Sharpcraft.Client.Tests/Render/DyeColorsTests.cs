// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// The dye tint table must match the reference <c>DyeColor.textureDiffuseColor</c> decimals exactly, because banner and
/// shield patterns are tinted with them.
/// </summary>
public sealed class DyeColorsTests
{
    // Reference DyeColor textureDiffuseColor values, in dye id order.
    private static readonly int[] Reference =
    [
        16383998, 16351261, 13061821, 3847130, 16701501, 8439583, 15961002, 4673362,
        10329495, 1481884, 8991416, 3949738, 8606770, 6192150, 11546150, 1908001
    ];

    [Fact]
    public void EveryDyeTintMatchesTheReferenceDecimal()
    {
        for (var id = 0; id < Reference.Length; id++)
        {
            var rgb = Reference[id];
            var expected = new System.Numerics.Vector3(
                ((rgb >> 16) & 0xFF) / 255f,
                ((rgb >> 8) & 0xFF) / 255f,
                (rgb & 0xFF) / 255f);
            Assert.Equal(expected, DyeColors.DiffuseAt(id));
        }
    }

    [Fact]
    public void OutOfRangeDyeIdFallsBackToWhite()
    {
        Assert.Equal(System.Numerics.Vector3.One, DyeColors.DiffuseAt(16));
        Assert.Equal(System.Numerics.Vector3.One, DyeColors.DiffuseAt(-1));
    }
}
