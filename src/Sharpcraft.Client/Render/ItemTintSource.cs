// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Client.Render;

/// <summary>
/// One entry of an item model's <c>tints</c> array. A face whose <c>tintindex</c> points at this entry is multiplied by
/// the evaluated colour; faces with <c>tintindex</c> −1 stay white.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.color.item.ItemTintSource", "ref://mc-26.3-analysis/src/net/minecraft/client/color/item/ItemTintSource.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "constant and grass sources; dye/potion/leaves sources are not modelled yet")]
public readonly record struct ItemTintSource(ItemTintKind Kind, int Argb, double Temperature, double Downfall)
{
    /// <summary>Parses one tint entry; unknown types yield null so the caller can skip the face tint.</summary>
    public static ItemTintSource? Parse(System.Text.Json.JsonElement element)
    {
        if (element.ValueKind != System.Text.Json.JsonValueKind.Object ||
            !element.TryGetProperty("type", out var type) ||
            type.ValueKind != System.Text.Json.JsonValueKind.String)
        {
            return null;
        }

        switch (type.GetString())
        {
            case "minecraft:constant":
                return element.TryGetProperty("value", out var value) &&
                    value.ValueKind == System.Text.Json.JsonValueKind.Number
                    ? new ItemTintSource(ItemTintKind.Constant, value.GetInt32(), 0d, 0d)
                    : null;

            case "minecraft:grass":
                var temperature = element.TryGetProperty("temperature", out var t) ? t.GetDouble() : 0.5d;
                var downfall = element.TryGetProperty("downfall", out var d) ? d.GetDouble() : 1d;
                return new ItemTintSource(ItemTintKind.Grass, 0, temperature, downfall);

            default:
                return null;
        }
    }

    /// <summary>Colour this source contributes, in 0..1 per channel.</summary>
    public Vector3 Evaluate(GrassColorMap? grass)
    {
        var argb = Kind switch
        {
            ItemTintKind.Constant => Argb,
            ItemTintKind.Grass => grass?.Get(Temperature, Downfall) ?? White,
            _ => White
        };

        return new Vector3(
            ((argb >> 16) & 0xFF) / 255f,
            ((argb >> 8) & 0xFF) / 255f,
            (argb & 0xFF) / 255f);
    }

    /// <summary>Opaque white, the neutral tint multiplier.</summary>
    public const int White = unchecked((int)0xFFFFFFFF);
}

/// <summary>Which reference tint source an entry is.</summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.color.item.ItemTintSource", "ref://mc-26.3-analysis/src/net/minecraft/client/color/item/ItemTintSource.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.New, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Discriminates the tint source kinds this repo models; vanilla uses one type per source")]
public enum ItemTintKind : byte
{
    /// <summary><c>minecraft:constant</c>.</summary>
    Constant = 0,

    /// <summary><c>minecraft:grass</c>: sampled from the grass colormap.</summary>
    Grass = 1
}

/// <summary>
/// The 256×256 grass colormap, sampled exactly like the reference
/// <c>ColorMapColorUtil.get(temp, rain, pixels, defaultMapColor)</c>.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.level.GrassColor", "ref://mc-26.3-analysis/src/net/minecraft/world/level/GrassColor.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Full)]
public sealed class GrassColorMap
{
    /// <summary>Reference fallback when the sampled index is outside the map.</summary>
    public const int DefaultColor = unchecked((int)0xFFFF00FF);

    private readonly int[] _pixels;

    /// <summary>Creates the map from 256×256 RGBA pixels.</summary>
    public GrassColorMap(ReadOnlySpan<byte> rgba)
    {
        _pixels = new int[65536];
        var count = Math.Min(_pixels.Length, rgba.Length / 4);
        for (var i = 0; i < count; i++)
        {
            var offset = i * 4;
            _pixels[i] = unchecked((int)0xFF000000) | (rgba[offset] << 16) | (rgba[offset + 1] << 8) | rgba[offset + 2];
        }
    }

    /// <summary>Reference sampling: rain is scaled by temperature, then both index the 256×256 table.</summary>
    public int Get(double temperature, double downfall)
    {
        var rain = downfall * temperature;
        var x = (int)((1d - temperature) * 255d);
        var y = (int)((1d - rain) * 255d);
        var index = (y << 8) | x;
        return index >= 0 && index < _pixels.Length ? _pixels[index] : DefaultColor;
    }

    /// <summary>The reference default grass colour, <c>get(0.5, 1.0)</c>.</summary>
    public int DefaultGrassColor => Get(0.5d, 1d);
}
