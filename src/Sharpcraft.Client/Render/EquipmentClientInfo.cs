// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Text.Json;
using Sharpcraft.Assets;
using Sharpcraft.Common.Identifiers;

namespace Sharpcraft.Client.Render;

/// <summary>The 19 equipment layer families defined by the 26.3 equipment client asset format.</summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.resources.model.EquipmentClientInfo.LayerType", "ref://mc-26.3-analysis/src/net/minecraft/client/resources/model/EquipmentClientInfo.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Full)]
public enum EquipmentLayerType
{
    Humanoid,
    HumanoidLeggings,
    HumanoidBaby,
    Wings,
    WolfBody,
    HorseBody,
    LlamaBody,
    PigSaddle,
    StriderSaddle,
    CamelSaddle,
    CamelHuskSaddle,
    HorseSaddle,
    DonkeySaddle,
    MuleSaddle,
    ZombieHorseSaddle,
    SkeletonHorseSaddle,
    HappyGhastBody,
    NautilusSaddle,
    NautilusBody
}

/// <summary>One equipment layer: texture, optional dyeability and optional player-texture override.</summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.resources.model.EquipmentClientInfo.Layer", "ref://mc-26.3-analysis/src/net/minecraft/client/resources/model/EquipmentClientInfo.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Full)]
public sealed record EquipmentClientLayer(Identifier Texture, EquipmentDyeable? Dyeable = null, bool UsePlayerTexture = false);

/// <summary>Dye metadata for an equipment layer; the nullable colour means "only when dyed".</summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.resources.model.EquipmentClientInfo.Dyeable", "ref://mc-26.3-analysis/src/net/minecraft/client/resources/model/EquipmentClientInfo.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Full)]
public sealed record EquipmentDyeable(int? ColourWhenUndyed = null);

/// <summary>Predicate that selects when a trim override applies.</summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.resources.model.EquipmentClientInfo.TrimPredicate", "ref://mc-26.3-analysis/src/net/minecraft/client/resources/model/EquipmentClientInfo.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Full)]
public sealed record EquipmentTrimPredicate(Identifier? Material = null, Identifier? Pattern = null);

/// <summary>Optional trim texture/palette override.</summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.resources.model.EquipmentClientInfo.TrimOverride", "ref://mc-26.3-analysis/src/net/minecraft/client/resources/model/EquipmentClientInfo.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Full)]
public sealed record EquipmentTrimOverride(
    EquipmentTrimPredicate When,
    Identifier? Texture = null,
    Identifier? Palette = null);

/// <summary>
/// Parsed <c>assets/&lt;namespace&gt;/equipment/&lt;path&gt;.json</c>. The client keeps this as data; the renderer maps an
/// item's equipment component to one of these asset ids before choosing layers and textures.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.resources.model.EquipmentClientInfo", "ref://mc-26.3-analysis/src/net/minecraft/client/resources/model/EquipmentClientInfo.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Full)]
public sealed record EquipmentClientInfo(
    IReadOnlyDictionary<EquipmentLayerType, IReadOnlyList<EquipmentClientLayer>> Layers,
    IReadOnlyList<EquipmentTrimOverride> TrimOverrides)
{
    /// <summary>Returns layers of one family, or an empty list when the asset omits that family.</summary>
    public IReadOnlyList<EquipmentClientLayer> GetLayers(EquipmentLayerType type) =>
        Layers.TryGetValue(type, out var layers) ? layers : [];

    /// <summary>Reads one equipment asset through the resource manager.</summary>
    public static EquipmentClientInfo Load(ResourceManager resources, Identifier id)
    {
        ArgumentNullException.ThrowIfNull(resources);
        var bytes = resources.Read(ResourceKind.Equipment, id)
            ?? throw new InvalidDataException($"equipment asset not found: {id}");
        return Parse(bytes);
    }

    /// <summary>Parses the JSON shape used by 26.3 equipment assets.</summary>
    public static EquipmentClientInfo Parse(ReadOnlyMemory<byte> utf8Json)
    {
        using var document = JsonDocument.Parse(utf8Json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("layers", out var layersElement)
            || layersElement.ValueKind != JsonValueKind.Object
            || !layersElement.EnumerateObject().Any())
        {
            throw new InvalidDataException("equipment asset must contain a non-empty layers object");
        }

        var layers = new Dictionary<EquipmentLayerType, IReadOnlyList<EquipmentClientLayer>>();
        foreach (var property in layersElement.EnumerateObject())
        {
            var type = ParseLayerType(property.Name);
            if (property.Value.ValueKind != JsonValueKind.Array || property.Value.GetArrayLength() == 0)
            {
                throw new InvalidDataException($"equipment layer '{property.Name}' must be a non-empty array");
            }

            var parsed = new List<EquipmentClientLayer>(property.Value.GetArrayLength());
            foreach (var item in property.Value.EnumerateArray())
            {
                parsed.Add(ParseLayer(item, property.Name));
            }

            layers[type] = parsed;
        }

        var trimOverrides = new List<EquipmentTrimOverride>();
        if (root.TryGetProperty("trim_overrides", out var trims))
        {
            if (trims.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("trim_overrides must be an array");
            }

            foreach (var item in trims.EnumerateArray())
            {
                trimOverrides.Add(ParseTrimOverride(item));
            }
        }

        return new EquipmentClientInfo(layers, trimOverrides);
    }

    private static EquipmentClientLayer ParseLayer(JsonElement element, string layerName)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty("texture", out var textureElement)
            || textureElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"equipment layer '{layerName}' must contain a texture string");
        }

        var texture = ParseIdentifier(textureElement.GetString(), "texture");
        EquipmentDyeable? dyeable = null;
        if (element.TryGetProperty("dyeable", out var dyeableElement))
        {
            if (dyeableElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException($"equipment layer '{layerName}' dyeable must be an object");
            }

            int? colour = null;
            if (dyeableElement.TryGetProperty("color_when_undyed", out var colourElement))
            {
                if (!colourElement.TryGetInt32(out var value))
                {
                    throw new InvalidDataException($"equipment layer '{layerName}' color_when_undyed must be an int");
                }

                colour = value;
            }

            dyeable = new EquipmentDyeable(colour);
        }

        var usePlayerTexture = false;
        if (element.TryGetProperty("use_player_texture", out var playerTextureElement))
        {
            if (playerTextureElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                throw new InvalidDataException($"equipment layer '{layerName}' use_player_texture must be a bool");
            }

            usePlayerTexture = playerTextureElement.GetBoolean();
        }

        return new EquipmentClientLayer(texture, dyeable, usePlayerTexture);
    }

    private static EquipmentTrimOverride ParseTrimOverride(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty("when", out var whenElement)
            || whenElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("trim override must contain a when object");
        }

        var material = ParseOptionalIdentifier(whenElement, "material");
        var pattern = ParseOptionalIdentifier(whenElement, "pattern");
        if (material is null && pattern is null)
        {
            throw new InvalidDataException("trim override when must specify material or pattern");
        }

        var texture = ParseOptionalIdentifier(element, "texture");
        var palette = ParseOptionalIdentifier(element, "palette");
        if (texture is null && palette is null)
        {
            throw new InvalidDataException("trim override must specify texture or palette");
        }

        return new EquipmentTrimOverride(new EquipmentTrimPredicate(material, pattern), texture, palette);
    }

    private static EquipmentLayerType ParseLayerType(string name) => name switch
    {
        "humanoid" => EquipmentLayerType.Humanoid,
        "humanoid_leggings" => EquipmentLayerType.HumanoidLeggings,
        "humanoid_baby" => EquipmentLayerType.HumanoidBaby,
        "wings" => EquipmentLayerType.Wings,
        "wolf_body" => EquipmentLayerType.WolfBody,
        "horse_body" => EquipmentLayerType.HorseBody,
        "llama_body" => EquipmentLayerType.LlamaBody,
        "pig_saddle" => EquipmentLayerType.PigSaddle,
        "strider_saddle" => EquipmentLayerType.StriderSaddle,
        "camel_saddle" => EquipmentLayerType.CamelSaddle,
        "camel_husk_saddle" => EquipmentLayerType.CamelHuskSaddle,
        "horse_saddle" => EquipmentLayerType.HorseSaddle,
        "donkey_saddle" => EquipmentLayerType.DonkeySaddle,
        "mule_saddle" => EquipmentLayerType.MuleSaddle,
        "zombie_horse_saddle" => EquipmentLayerType.ZombieHorseSaddle,
        "skeleton_horse_saddle" => EquipmentLayerType.SkeletonHorseSaddle,
        "happy_ghast_body" => EquipmentLayerType.HappyGhastBody,
        "nautilus_saddle" => EquipmentLayerType.NautilusSaddle,
        "nautilus_body" => EquipmentLayerType.NautilusBody,
        _ => throw new InvalidDataException($"unknown equipment layer type: {name}")
    };

    private static Identifier ParseIdentifier(string? text, string field)
    {
        if (!Identifier.TryParse(text, out var id))
        {
            throw new InvalidDataException($"equipment {field} is not a valid identifier: {text}");
        }

        return id;
    }

    private static Identifier? ParseOptionalIdentifier(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"equipment {name} must be a string");
        }

        return ParseIdentifier(value.GetString(), name);
    }
}
