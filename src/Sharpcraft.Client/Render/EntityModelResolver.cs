// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Resolves a render-only entity model definition from the registered entity type id. This is the C# equivalent of
/// EntityModelSet.bakeLayer: no world or simulation dependency, and missing entries stay null for placeholder fallback.
/// </summary>
public static class EntityModelResolver
{
    public static readonly EntityModelDefinition Placeholder = new(
        "placeholder",
        64,
        64,
        [new EntityModelPart("body", 0f, 0f, 14f, 14f, 14f, 0, 0)]);

    private static readonly Dictionary<int, EntityModelDefinition> Models = new()
    {
        [EntityPlaceholderMesher.PlayerEntityTypeId] = PlayerModelDefinition.Definition,
        [HumanoidMobModelDefinition.SkeletonEntityTypeId] = HumanoidMobModelDefinition.Skeleton,
        [HumanoidMobModelDefinition.StrayEntityTypeId] = HumanoidMobModelDefinition.Stray,
        [HumanoidMobModelDefinition.BoggedEntityTypeId] = HumanoidMobModelDefinition.Bogged,
        [HumanoidMobModelDefinition.WitherSkeletonEntityTypeId] = HumanoidMobModelDefinition.WitherSkeleton,
        [HumanoidMobModelDefinition.DrownedEntityTypeId] = HumanoidMobModelDefinition.Drowned,
        [HumanoidMobModelDefinition.EndermanEntityTypeId] = HumanoidMobModelDefinition.Enderman,
        [CreeperModelDefinition.CreeperEntityTypeId] = CreeperModelDefinition.Definition,
        [SlimeModelDefinition.SlimeEntityTypeId] = SlimeModelDefinition.Definition,
        [MagmaCubeModelDefinition.MagmaCubeEntityTypeId] = MagmaCubeModelDefinition.Definition,
        [SpiderModelDefinition.SpiderEntityTypeId] = SpiderModelDefinition.Definition,
        [PigModelDefinition.PigEntityTypeId] = PigModelDefinition.Definition,
        [SheepModelDefinition.SheepEntityTypeId] = SheepModelDefinition.Definition,
        [CowModelDefinition.CowEntityTypeId] = CowModelDefinition.Definition,
        [CowModelDefinition.MooshroomEntityTypeId] = CowModelDefinition.Mooshroom,
        [ChickenModelDefinition.ChickenEntityTypeId] = ChickenModelDefinition.Definition,
        [WolfModelDefinition.WolfEntityTypeId] = WolfModelDefinition.Definition,
        [CatModelDefinition.CatEntityTypeId] = CatModelDefinition.Definition,
        [CatModelDefinition.OcelotEntityTypeId] = CatModelDefinition.Ocelot,
        [FoxModelDefinition.FoxEntityTypeId] = FoxModelDefinition.Definition,
        [PolarBearModelDefinition.PolarBearEntityTypeId] = PolarBearModelDefinition.Definition,
        [PandaModelDefinition.PandaEntityTypeId] = PandaModelDefinition.Definition,
        [GoatModelDefinition.GoatEntityTypeId] = GoatModelDefinition.Definition,
        [SnowGolemModelDefinition.SnowGolemEntityTypeId] = SnowGolemModelDefinition.Definition,
        [SquidModelDefinition.SquidEntityTypeId] = SquidModelDefinition.Definition,
        [SquidModelDefinition.GlowSquidEntityTypeId] = SquidModelDefinition.GlowSquid,
        [HorseModelDefinition.HorseEntityTypeId] = HorseModelDefinition.Definition,
        [HorseModelDefinition.DonkeyEntityTypeId] = HorseModelDefinition.Donkey,
        [HorseModelDefinition.MuleEntityTypeId] = HorseModelDefinition.Mule,
        [HorseModelDefinition.ZombieHorseEntityTypeId] = HorseModelDefinition.ZombieHorse,
        [HorseModelDefinition.SkeletonHorseEntityTypeId] = HorseModelDefinition.SkeletonHorse,
        [LlamaModelDefinition.LlamaEntityTypeId] = LlamaModelDefinition.Definition,
        [VillagerModelDefinition.VillagerEntityTypeId] = VillagerModelDefinition.Definition,
        [ZombieVillagerModelDefinition.ZombieVillagerEntityTypeId] = ZombieVillagerModelDefinition.Definition,
        [EndermiteModelDefinition.EndermiteEntityTypeId] = EndermiteModelDefinition.Definition,
        [SilverfishModelDefinition.SilverfishEntityTypeId] = SilverfishModelDefinition.Definition,
        [IronGolemModelDefinition.IronGolemEntityTypeId] = IronGolemModelDefinition.Definition,
        [HumanoidMobModelDefinition.HuskEntityTypeId] = HumanoidMobModelDefinition.Husk,
        [HumanoidMobModelDefinition.ZombieEntityTypeId] = HumanoidMobModelDefinition.Zombie
    };

    public static IReadOnlyCollection<EntityModelDefinition> Definitions => Models.Values.Distinct().ToArray();

    public static EntityModelDefinition? TryResolve(int typeId) =>
        Models.TryGetValue(typeId, out var definition) ? definition : null;

    public static EntityModelDefinition Resolve(int typeId) =>
        TryResolve(typeId) ?? Placeholder;

    public static int? FindTypeId(string name)
    {
        foreach (var pair in Models)
        {
            if (string.Equals(pair.Value.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Key;
            }
        }

        return null;
    }

    public static void Register(int typeId, EntityModelDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Models[typeId] = definition;
    }
}
