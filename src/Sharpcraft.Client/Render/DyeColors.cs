// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Dye colours as used for texture tinting. The values are the reference <c>DyeColor.textureDiffuseColor</c> entries
/// (ARGB opaque) converted to the 0..1 channel triples the vertex tint carries.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.item.DyeColor", "ref://mc-26.3-analysis/src/net/minecraft/world/item/DyeColor.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "textureDiffuseColor table only; names and map colours live elsewhere")]
public static class DyeColors
{
    private static readonly Vector3[] Diffuse =
    [
        FromRgb(0xF9FFFE), // 0 white
        FromRgb(0xF9801D), // 1 orange
        FromRgb(0xC74EBD), // 2 magenta
        FromRgb(0x3AB3DA), // 3 light_blue
        FromRgb(0xFED83D), // 4 yellow
        FromRgb(0x80C71F), // 5 lime
        FromRgb(0xF38BAA), // 6 pink
        FromRgb(0x474F52), // 7 gray
        FromRgb(0x9D9D97), // 8 light_gray
        FromRgb(0x169C9C), // 9 cyan
        FromRgb(0x8932B8), // 10 purple
        FromRgb(0x3C44AA), // 11 blue
        FromRgb(0x835432), // 12 brown
        FromRgb(0x5E7C16), // 13 green
        FromRgb(0xB02E26), // 14 red
        FromRgb(0x1D1D21)  // 15 black
    ];

    /// <summary>Diffuse tint for a dye colour id; out-of-range ids fall back to white like the reference id mapper.</summary>
    public static Vector3 DiffuseAt(int colorId) =>
        (uint)colorId < (uint)Diffuse.Length ? Diffuse[colorId] : Vector3.One;

    private static Vector3 FromRgb(int rgb) => new(
        ((rgb >> 16) & 0xFF) / 255f,
        ((rgb >> 8) & 0xFF) / 255f,
        (rgb & 0xFF) / 255f);
}
