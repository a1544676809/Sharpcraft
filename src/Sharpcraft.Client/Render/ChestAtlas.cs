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
    /// <summary>Shulker box textures in dye order, with the undyed default first.</summary>
    public static readonly string[] ShulkerTextures =
    [
        "entity/shulker/shulker",
        "entity/shulker/shulker_white",
        "entity/shulker/shulker_orange",
        "entity/shulker/shulker_magenta",
        "entity/shulker/shulker_light_blue",
        "entity/shulker/shulker_yellow",
        "entity/shulker/shulker_lime",
        "entity/shulker/shulker_pink",
        "entity/shulker/shulker_gray",
        "entity/shulker/shulker_light_gray",
        "entity/shulker/shulker_cyan",
        "entity/shulker/shulker_purple",
        "entity/shulker/shulker_blue",
        "entity/shulker/shulker_brown",
        "entity/shulker/shulker_green",
        "entity/shulker/shulker_red",
        "entity/shulker/shulker_black"
    ];

    /// <summary>Texture a shulker box uses for a dye index, or the undyed default.</summary>
    public static string ShulkerTexture(int color) =>
        color >= 0 && color < 16 ? ShulkerTextures[color + 1] : ShulkerTextures[0];

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

    /// <summary>Bell texture, stitched into the same block entity sheet as the chest and shulker textures.</summary>
    public const string BellTexture = "entity/bell/bell_body";

    /// <summary>Textures the bell renderer uses; kept apart from the chest directory list.</summary>
    private static readonly string[] BellTextures = [BellTexture];

    /// <summary>Decorated pot blank base texture, 32 by 32.</summary>
    public const string DecoratedPotBaseTexture = "entity/decorated_pot/decorated_pot_base";

    /// <summary>Decorated pot blank side texture, 16 by 16.</summary>
    public const string DecoratedPotSideTexture = "entity/decorated_pot/decorated_pot_side";

    /// <summary>Textures the decorated pot renderer uses; kept apart from the chest directory list.</summary>
    private static readonly string[] DecoratedPotTextures = [DecoratedPotBaseTexture, DecoratedPotSideTexture];

    /// <summary>Copper golem statue textures in WeatheringCopper stage order, matching the reference oxidation table.</summary>
    public static readonly string[] CopperGolemStatueTextures =
    [
        "entity/copper_golem/copper_golem",
        "entity/copper_golem/copper_golem_exposed",
        "entity/copper_golem/copper_golem_weathered",
        "entity/copper_golem/copper_golem_oxidized"
    ];

    /// <summary>Texture a copper golem statue uses for a weathering stage, or the unaffected texture.</summary>
    public static string CopperGolemStatueTexture(int oxidation) =>
        oxidation >= 0 && oxidation < CopperGolemStatueTextures.Length
            ? CopperGolemStatueTextures[oxidation]
            : CopperGolemStatueTextures[0];

    /// <summary>Enchanting table book texture, 64 by 32, reused by lecterns.</summary>
    public const string EnchantingTableBookTexture = "entity/enchantment/enchanting_table_book";

    /// <summary>Everything this block entity atlas stitches, in load order.</summary>
    private static readonly string[] AllTextures = [.. Textures, .. ShulkerTextures, .. BellTextures, .. DecoratedPotTextures, .. CopperGolemStatueTextures, EnchantingTableBookTexture];

    /// <summary>Texture the model uses when the material is plain wood.</summary>
    public const string NormalTexture = "entity/chest/normal";

    /// <summary>Texture of the single chest model, as opposed to its left and right halves.</summary>
    public const string SingleTexture = NormalTexture;

    /// <summary>Stitches the chest sheet through the shared sprite builder.</summary>
    /// <param name="resources">Resource manager holding the extracted assets.</param>
    public static ItemAtlas Load(ResourceManager resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        foreach (var path in ShulkerTextures)
        {
            if (resources.Read(Sharpcraft.Assets.ResourceKind.Texture, new Sharpcraft.Common.Identifiers.Identifier("minecraft", path)) is null)
            {
                Console.WriteLine($"shulker texture missing: {path}");
            }
        }

        return ItemAtlas.Load(resources, AllTextures);
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
        foreach (var path in AllTextures)
        {
            if (atlas.Get(path) is { } rect && rect.RectId >= highest)
            {
                highest = rect.RectId + 1;
            }
        }

        var table = new float[(highest + 1) * 4];
        table[2] = 1f;
        table[3] = 1f;

        foreach (var path in AllTextures)
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
        var index = (material * TypeCount) + type;
        return (uint)index < (uint)TexturePaths.Length ? TexturePaths[index] : TexturePaths[0];
    }

    private const int TypeCount = 3;

    private static readonly string[] MaterialStems =
    [
        "normal", "trapped", "ender", "copper", "copper_exposed", "copper_weathered", "copper_oxidized"
    ];

    /// <summary>
    /// Every material and half combination, built once. The per-frame path used to interpolate a new
    /// string for each chest on each frame, which allocated on a steady-state render path.
    /// </summary>
    private static readonly string[] TexturePaths = BuildTexturePaths();

    private static string[] BuildTexturePaths()
    {
        var paths = new string[MaterialStems.Length * TypeCount];
        for (var material = 0; material < MaterialStems.Length; material++)
        {
            var stem = MaterialStems[material];
            var plain = "entity/chest/" + stem;
            paths[(material * TypeCount) + 0] = plain;

            // The ender chest cannot be doubled, so both halves resolve to the plain texture.
            paths[(material * TypeCount) + 1] = material == Ender ? plain : plain + "_left";
            paths[(material * TypeCount) + 2] = material == Ender ? plain : plain + "_right";
        }

        return paths;
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

    /// <summary>Resolves the bell sprite, or null when this atlas does not hold the bell texture.</summary>
    /// <param name="atlas">Atlas returned by <see cref="Load"/>.</param>
    public static SpriteRect? SpriteForBell(ItemAtlas atlas)
    {
        ArgumentNullException.ThrowIfNull(atlas);
        return atlas.Get(BellTexture);
    }

    /// <summary>Resolves the decorated pot base sprite, or null when the atlas does not hold it.</summary>
    /// <param name="atlas">Atlas returned by <see cref="Load"/>.</param>
    public static SpriteRect? SpriteForDecoratedPotBase(ItemAtlas atlas)
    {
        ArgumentNullException.ThrowIfNull(atlas);
        return atlas.Get(DecoratedPotBaseTexture);
    }

    /// <summary>Resolves the decorated pot blank side sprite, or null when the atlas does not hold it.</summary>
    /// <param name="atlas">Atlas returned by <see cref="Load"/>.</param>
    public static SpriteRect? SpriteForDecoratedPotSide(ItemAtlas atlas)
    {
        ArgumentNullException.ThrowIfNull(atlas);
        return atlas.Get(DecoratedPotSideTexture);
    }

    /// <summary>Resolves the copper golem statue sprite for an oxidation stage, or null when absent.</summary>
    /// <param name="atlas">Atlas returned by <see cref="Load"/>.</param>
    /// <param name="oxidation">Weathering stage index 0..3.</param>
    public static SpriteRect? SpriteForCopperGolemStatue(ItemAtlas atlas, int oxidation)
    {
        ArgumentNullException.ThrowIfNull(atlas);
        return atlas.Get(CopperGolemStatueTexture(oxidation));
    }

    /// <summary>Resolves the enchanting table book sprite, or null when absent.</summary>
    /// <param name="atlas">Atlas returned by <see cref="Load"/>.</param>
    public static SpriteRect? SpriteForEnchantingTableBook(ItemAtlas atlas)
    {
        ArgumentNullException.ThrowIfNull(atlas);
        return atlas.Get(EnchantingTableBookTexture);
    }
}
