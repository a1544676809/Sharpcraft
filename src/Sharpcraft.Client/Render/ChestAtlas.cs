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

    /// <summary>Material of a chest, as the reference's chest material enum orders them.</summary>
    public const byte Normal = 0;

    /// <summary>Trapped chest material.</summary>
    public const byte Trapped = 1;

    /// <summary>Ender chest material; it has no double halves.</summary>
    public const byte Ender = 2;

    /// <summary>Copper chest material, unaffected by oxidation.</summary>
    public const byte Copper = 3;

    /// <summary>Exposed copper chest material.</summary>
    public const byte CopperExposed = 4;

    /// <summary>Weathered copper chest material.</summary>
    public const byte CopperWeathered = 5;

    /// <summary>Oxidized copper chest material.</summary>
    public const byte CopperOxidized = 6;

    /// <summary>
    /// Texture path of one chest, given its material and which half it is. The ender chest has no
    /// halves because it cannot be doubled, so the type is ignored for it.
    /// </summary>
    /// <param name="material">Chest material constant.</param>
    /// <param name="type">Zero single, one left half, two right half.</param>
    public static string TexturePathFor(byte material, byte type)
    {
        var stem = material switch
        {
            Trapped => "trapped",
            Ender => "ender",
            Copper => "copper",
            CopperExposed => "copper_exposed",
            CopperWeathered => "copper_weathered",
            CopperOxidized => "copper_oxidized",
            _ => "normal"
        };

        if (material == Ender)
        {
            return $"entity/chest/{stem}";
        }

        return type switch
        {
            1 => $"entity/chest/{stem}_left",
            2 => $"entity/chest/{stem}_right",
            _ => $"entity/chest/{stem}"
        };
    }

    /// <summary>Resolves one chest's sprite, falling back to the plain chest when it is absent.</summary>
    /// <param name="atlas">Atlas returned by <see cref="Load"/>.</param>
    /// <param name="material">Chest material constant.</param>
    /// <param name="type">Zero single, one left half, two right half.</param>
    public static SpriteRect SpriteFor(ItemAtlas atlas, byte material, byte type)
    {
        ArgumentNullException.ThrowIfNull(atlas);

        return atlas.Get(TexturePathFor(material, type))
            ?? atlas.Get(NormalTexture)
            ?? throw new InvalidOperationException("the chest atlas has no plain chest sprite");
    }
}
