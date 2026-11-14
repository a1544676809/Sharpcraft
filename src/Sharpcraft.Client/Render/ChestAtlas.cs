// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Assets;
using Sharpcraft.Client.Assets;

namespace Sharpcraft.Client.Render;

/// <summary>
/// The chest atlas: the reference stitches every chest texture into its own sheet
/// (<c>textures/atlas/chest.png</c>) rather than sharing the block or item atlas, and block entity
/// chests bind that sheet. The same stitching the item atlas uses produces it here.
/// </summary>
/// <remarks>
/// Paths are the reference's <c>textures/entity/chest</c> directory: one standing variant per
/// material, plus left and right halves for double chests. Ender chests have no halves because they
/// cannot be doubled.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.Sheets", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/Sheets.java")]
public static class ChestAtlas
{
    /// <summary>Symbolic name of the sheet in the reference.</summary>
    public const string SheetPath = "textures/atlas/chest.png";

    /// <summary>Every texture the reference stitches into the chest sheet.</summary>
    public static readonly string[] Textures =
    [
        "entity/chest/normal",
        "entity/chest/normal_left",
        "entity/chest/normal_right",
        "entity/chest/trapped",
        "entity/chest/trapped_left",
        "entity/chest/trapped_right",
        "entity/chest/ender",
        "entity/chest/copper",
        "entity/chest/copper_left",
        "entity/chest/copper_right",
        "entity/chest/copper_exposed",
        "entity/chest/copper_exposed_left",
        "entity/chest/copper_exposed_right",
        "entity/chest/copper_weathered",
        "entity/chest/copper_weathered_left",
        "entity/chest/copper_weathered_right",
        "entity/chest/copper_oxidized",
        "entity/chest/copper_oxidized_left",
        "entity/chest/copper_oxidized_right",
        "entity/chest/christmas",
        "entity/chest/christmas_left",
        "entity/chest/christmas_right",
    ];

    /// <summary>Texture the model uses when the material is plain wood.</summary>
    public const string NormalTexture = "entity/chest/normal";

    /// <summary>Texture of the single chest model, as opposed to its left and right halves.</summary>
    public const string SingleTexture = NormalTexture;

    /// <summary>Stitches the chest sheet through the shared sprite builder.</summary>
    /// <param name="resources">Resource manager holding the extracted assets.</param>
    public static ItemAtlas Load(ResourceManager resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        return ItemAtlas.Load(resources, Textures);
    }

    /// <summary>
    /// Atlas bounds as four floats per sprite, indexed by the vertex sprite id. Entry zero covers the
    /// whole texture, matching how the block atlas builds its own table.
    /// </summary>
    /// <param name="atlas">Atlas returned by <see cref="Load"/>.</param>
    public static float[] BuildRectTable(ItemAtlas atlas)
    {
        ArgumentNullException.ThrowIfNull(atlas);

        var highest = 0;
        foreach (var path in Textures)
        {
            if (atlas.Get(path) is { } rect && rect.RectId >= highest)
            {
                highest = rect.RectId + 1;
            }
        }

        var table = new float[(highest + 1) * 4];
        table[2] = 1f;
        table[3] = 1f;

        foreach (var path in Textures)
        {
            if (atlas.Get(path) is not { } rect || rect.RectId < 0 || (rect.RectId * 4) + 3 >= table.Length)
            {
                continue;
            }

            var at = rect.RectId * 4;
            table[at] = rect.U0;
            table[at + 1] = rect.V0;
            table[at + 2] = rect.U1;
            table[at + 3] = rect.V1;
        }

        return table;
    }
}
