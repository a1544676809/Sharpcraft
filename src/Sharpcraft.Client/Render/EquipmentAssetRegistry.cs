// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Assets;
using Sharpcraft.Common.Identifiers;

namespace Sharpcraft.Client.Render;

/// <summary>All equipment assets loaded from a resource stack, keyed by their asset identifier.</summary>
[Sharpcraft.Common.Reference.ReferenceClass("sharpcraft.EquipmentAssetRegistry", "ref://mc-26.3-analysis/src", Kind = Sharpcraft.Common.Reference.ReferenceKind.New, Status = Sharpcraft.Common.Reference.ReferenceStatus.Full, Note = "Sharpcraft host/infra; no reference counterpart")]
public sealed class EquipmentAssetRegistry
{
    private readonly IReadOnlyDictionary<Identifier, EquipmentClientInfo> _assets;

    private EquipmentAssetRegistry(IReadOnlyDictionary<Identifier, EquipmentClientInfo> assets)
    {
        _assets = assets;
    }

    public static EquipmentAssetRegistry Empty { get; } = new(new Dictionary<Identifier, EquipmentClientInfo>());

    /// <summary>Builds a registry from already parsed definitions; used by tests and resource reload code.</summary>
    public static EquipmentAssetRegistry From(IReadOnlyDictionary<Identifier, EquipmentClientInfo> assets)
    {
        ArgumentNullException.ThrowIfNull(assets);
        return new EquipmentAssetRegistry(assets);
    }

    public int Count => _assets.Count;

    /// <summary>Loads known equipment assets; missing or malformed files are skipped so the static fallback stays usable.</summary>
    public static EquipmentAssetRegistry Load(ResourceManager resources, IEnumerable<Identifier> ids)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(ids);
        var loaded = new Dictionary<Identifier, EquipmentClientInfo>();
        foreach (var id in ids.Distinct())
        {
            try
            {
                loaded[id] = EquipmentClientInfo.Load(resources, id);
            }
            catch (InvalidDataException)
            {
                // A pack may omit an equipment family; the catalog's static mapping remains the fallback.
            }
        }

        return new EquipmentAssetRegistry(loaded);
    }

    public EquipmentClientInfo? Get(Identifier id) => _assets.TryGetValue(id, out var info) ? info : null;

    /// <summary>Every loaded asset, used to pre-generate trim palette variants.</summary>
    public IEnumerable<EquipmentClientInfo> All => _assets.Values;

    /// <summary>All texture paths referenced by loaded assets, for the skin atlas.</summary>
    public IEnumerable<string> TexturePaths
    {
        get
        {
            foreach (var (_, info) in _assets)
            {
                foreach (var (type, layers) in info.Layers)
                {
                    foreach (var layer in layers)
                    {
                        yield return EquipmentLayerPath(type, layer.Texture.Path);
                    }
                }
            }
        }
    }

    public static string EquipmentLayerPath(EquipmentLayerType type, string texturePath) =>
        "entity/equipment/" + SerializedName(type) + "/" + texturePath;

    public static string SerializedName(EquipmentLayerType type) => type switch
    {
        EquipmentLayerType.Humanoid => "humanoid",
        EquipmentLayerType.HumanoidLeggings => "humanoid_leggings",
        EquipmentLayerType.HumanoidBaby => "humanoid_baby",
        EquipmentLayerType.Wings => "wings",
        EquipmentLayerType.WolfBody => "wolf_body",
        EquipmentLayerType.HorseBody => "horse_body",
        EquipmentLayerType.LlamaBody => "llama_body",
        EquipmentLayerType.PigSaddle => "pig_saddle",
        EquipmentLayerType.StriderSaddle => "strider_saddle",
        EquipmentLayerType.CamelSaddle => "camel_saddle",
        EquipmentLayerType.CamelHuskSaddle => "camel_husk_saddle",
        EquipmentLayerType.HorseSaddle => "horse_saddle",
        EquipmentLayerType.DonkeySaddle => "donkey_saddle",
        EquipmentLayerType.MuleSaddle => "mule_saddle",
        EquipmentLayerType.ZombieHorseSaddle => "zombie_horse_saddle",
        EquipmentLayerType.SkeletonHorseSaddle => "skeleton_horse_saddle",
        EquipmentLayerType.HappyGhastBody => "happy_ghast_body",
        EquipmentLayerType.NautilusSaddle => "nautilus_saddle",
        EquipmentLayerType.NautilusBody => "nautilus_body",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "unknown equipment layer type")
    };
}
