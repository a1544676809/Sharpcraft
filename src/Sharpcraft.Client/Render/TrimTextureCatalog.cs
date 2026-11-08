// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Imaging;
using Sharpcraft.Common.Registries;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Builds the palette-mapped trim textures used by the humanoid armor layers. The reference uses
/// <c>PalettedTextureManager</c> and paints each pattern with the material's eight-colour palette; this catalog
/// performs the same CPU mapping into the existing entity skin atlas.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.resources.palette.PalettedTextureManager", "ref://mc-26.3-analysis/src/net/minecraft/client/resources/palette/PalettedTextureManager.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Palette mapping into the shared entity atlas; no GPU palette texture")]
public static class TrimTextureCatalog
{
    private const string BasePalettePath = "palettes/trim_base";

    /// <summary>Atlas key for one pattern/material/layer combination, including an equipment-asset palette override.</summary>
    /// <param name="layerType">Humanoid or leggings armor layer.</param>
    /// <param name="patternId">Trim pattern registry id.</param>
    /// <param name="materialId">Trim material registry id.</param>
    /// <param name="equipmentInfo">Equipment asset supplying a matching trim palette override, or null for the default.</param>
    public static string Path(
        EquipmentLayerType layerType,
        string patternId,
        string materialId,
        EquipmentClientInfo? equipmentInfo = null)
    {
        var layer = layerType switch
        {
            EquipmentLayerType.Humanoid => "humanoid",
            EquipmentLayerType.HumanoidLeggings => "humanoid_leggings",
            _ => throw new ArgumentOutOfRangeException(nameof(layerType), layerType, "trim is only defined for humanoid armor layers")
        };
        var pattern = LocalPath(patternId);
        var material = LocalPath(materialId);
        var paletteSuffix = PaletteSuffix(equipmentInfo, patternId, materialId);
        return "trims/entity/" + layer + "/" + pattern + "/" + material +
               (paletteSuffix is null ? string.Empty : "/" + paletteSuffix);
    }

    /// <summary>Loads and palette-maps every bootstrap pattern/material pair for both humanoid armor layers.</summary>
    public static IReadOnlyDictionary<string, TextureData> Load(ResourceManager resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        var result = new Dictionary<string, TextureData>(StringComparer.Ordinal);
        if (!TryLoad(resources, BasePalettePath, out var basePalette))
        {
            return result;
        }

        var palettes = new Dictionary<string, TextureData>(StringComparer.Ordinal);
        foreach (var material in TrimMaterialRegistry.Entries)
        {
            if (TryLoad(resources, "palettes/" + LocalPath(material.PaletteId), out var palette))
            {
                palettes[material.Id] = palette;
            }
        }

        var patterns = new Dictionary<(EquipmentLayerType Layer, string Pattern), TextureData>();
        foreach (var pattern in TrimPatternRegistry.Entries)
        {
            var patternPath = LocalPath(pattern.AssetId);
            foreach (var layerType in new[] { EquipmentLayerType.Humanoid, EquipmentLayerType.HumanoidLeggings })
            {
                var layer = layerType == EquipmentLayerType.Humanoid ? "humanoid" : "humanoid_leggings";
                if (TryLoad(resources, "trims/entity/" + layer + "/" + patternPath, out var trimTexture))
                {
                    patterns[(layerType, pattern.Id)] = trimTexture;
                }
            }
        }

        foreach (var pattern in TrimPatternRegistry.Entries)
        {
            foreach (var material in TrimMaterialRegistry.Entries)
            {
                if (!palettes.TryGetValue(material.Id, out var targetPalette))
                {
                    continue;
                }

                foreach (var layerType in new[] { EquipmentLayerType.Humanoid, EquipmentLayerType.HumanoidLeggings })
                {
                    if (!patterns.TryGetValue((layerType, pattern.Id), out var trimTexture))
                    {
                        continue;
                    }

                    var mapped = MapPalette(basePalette, targetPalette, trimTexture);
                    if (mapped is not null)
                    {
                        result[Path(layerType, pattern.Id, material.Id)] = mapped;
                    }
                }
            }
        }

        // Equipment assets may replace the material palette (for example iron armor uses iron_darker trim).
        foreach (var equipmentInfo in EquipmentTextureCatalog.Assets.All)
        {
            foreach (var pattern in TrimPatternRegistry.Entries)
            {
                foreach (var material in TrimMaterialRegistry.Entries)
                {
                    var paletteSuffix = PaletteSuffix(equipmentInfo, pattern.Id, material.Id);
                    if (paletteSuffix is null || !TryLoad(resources, "palettes/" + paletteSuffix, out var targetPalette))
                    {
                        continue;
                    }

                    foreach (var layerType in new[] { EquipmentLayerType.Humanoid, EquipmentLayerType.HumanoidLeggings })
                    {
                        if (!patterns.TryGetValue((layerType, pattern.Id), out var trimTexture))
                        {
                            continue;
                        }

                        var mapped = MapPalette(basePalette, targetPalette, trimTexture);
                        if (mapped is not null)
                        {
                            result[Path(layerType, pattern.Id, material.Id, equipmentInfo)] = mapped;
                        }
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Applies the reference palette rule: base and target palettes have equal size; opaque base pixels map to the
    /// corresponding target colour, while alpha is multiplied by the target alpha. Transparent trim pixels stay clear.
    /// </summary>
    public static TextureData? MapPalette(TextureData basePalette, TextureData targetPalette, TextureData trimTexture)
    {
        ArgumentNullException.ThrowIfNull(basePalette);
        ArgumentNullException.ThrowIfNull(targetPalette);
        ArgumentNullException.ThrowIfNull(trimTexture);

        if (basePalette.Rgba.Length != targetPalette.Rgba.Length)
        {
            return null;
        }

        var map = new Dictionary<int, (byte R, byte G, byte B, byte A)>();
        for (var offset = 0; offset + 3 < basePalette.Rgba.Length; offset += 4)
        {
            if (basePalette.Rgba[offset + 3] == 0)
            {
                continue;
            }

            var key = (basePalette.Rgba[offset] << 16) | (basePalette.Rgba[offset + 1] << 8) | basePalette.Rgba[offset + 2];
            map[key] = (targetPalette.Rgba[offset], targetPalette.Rgba[offset + 1], targetPalette.Rgba[offset + 2], targetPalette.Rgba[offset + 3]);
        }

        var pixels = new byte[trimTexture.Rgba.Length];
        for (var offset = 0; offset + 3 < trimTexture.Rgba.Length; offset += 4)
        {
            var alpha = trimTexture.Rgba[offset + 3];
            if (alpha == 0)
            {
                continue;
            }

            var key = (trimTexture.Rgba[offset] << 16) | (trimTexture.Rgba[offset + 1] << 8) | trimTexture.Rgba[offset + 2];
            if (!map.TryGetValue(key, out var target))
            {
                pixels[offset] = trimTexture.Rgba[offset];
                pixels[offset + 1] = trimTexture.Rgba[offset + 1];
                pixels[offset + 2] = trimTexture.Rgba[offset + 2];
                pixels[offset + 3] = alpha;
                continue;
            }

            pixels[offset] = target.R;
            pixels[offset + 1] = target.G;
            pixels[offset + 2] = target.B;
            pixels[offset + 3] = (byte)(alpha * target.A / 255);
        }

        return new TextureData(trimTexture.Width, trimTexture.Height, pixels);
    }

    private static bool TryLoad(ResourceManager resources, string path, out TextureData texture)
    {
        var data = resources.Read(ResourceKind.Texture, new Identifier("minecraft", path));
        if (data is null)
        {
            texture = null!;
            return false;
        }

        var image = PngDecoder.Decode(data);
        texture = new TextureData(image.Width, image.Height, image.Pixels);
        return true;
    }

    private static string? PaletteSuffix(EquipmentClientInfo? equipmentInfo, string patternId, string materialId)
    {
        if (equipmentInfo is null ||
            !Identifier.TryParse(patternId, out var pattern) ||
            !Identifier.TryParse(materialId, out var material))
        {
            return null;
        }

        return equipmentInfo.ResolveTrim(material, pattern)?.Palette is { } palette
            ? LocalPath(palette.ToString())
            : null;
    }

    private static string LocalPath(string id)
    {
        if (!Identifier.TryParse(id, out var identifier))
        {
            return id;
        }

        return identifier.Path;
    }
}
