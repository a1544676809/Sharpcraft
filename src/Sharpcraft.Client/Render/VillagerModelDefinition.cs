// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// VillagerModel: head with grown hat and a rotated hat rim, nose, body with a grown jacket, a crossed-arm group and legs.
/// All values are raw PartPose/cube values; villagerLikeScale 0.9375 is applied at the definition level. Parts are
/// declared with the <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake.Bake</c> (T-683) with per-cube
/// <c>MirrorX: false</c> on the head/body/arms roots; variants and scale are unchanged.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.npc.VillagerModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/npc/VillagerModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class VillagerModelDefinition
{
    public const int VillagerEntityTypeId = 143;
    public const int WanderingTraderEntityTypeId = 145;

    /// <summary>Declarative DSL tree for the twelve villager parts (T-683).</summary>
    private static readonly VanillaMeshDefinition VillagerMesh = BuildVillagerMesh();

    private static readonly CompiledEntityModel VillagerCompiled =
        VanillaMeshBakery.Build(VillagerMesh, 64, 64, rawSourceFrame: true);

    public static readonly EntityModelDefinition Definition = new("villager", 64, 64, faceTransforms: null, texturePath: "entity/villager/villager", scale: 0.9375f, compiled: VillagerCompiled, baselinePx: 0f, rawSourceFrame: true);

    public static readonly EntityModelDefinition WanderingTrader = new("wandering_trader", 64, 64, faceTransforms: null, texturePath: "entity/wandering_trader/wandering_trader", scale: 0.9375f, compiled: VillagerCompiled, baselinePx: 0f, rawSourceFrame: true);

    public static readonly IReadOnlyList<string> VariantKeys = ["plains", "desert", "jungle", "savanna", "snow", "swamp", "taiga"];

    public static readonly IReadOnlyDictionary<string, EntityModelDefinition> Variants =
        VariantMap("entity/villager/type/", 0.9375f, baseCompiled: VillagerCompiled);

    internal static Dictionary<string, EntityModelDefinition> VariantMap(
        string texturePrefix,
        float scale,
        string namePrefix = "villager_",
        string baseTexture = "entity/villager/villager",
        CompiledEntityModel? baseCompiled = null)
    {
        var variants = new Dictionary<string, EntityModelDefinition>(StringComparer.Ordinal);
        foreach (var key in VariantKeys)
        {
            var texturePath = texturePrefix + key;
            var compiled = baseCompiled is null
                ? null
                : EntityModelLayerHelpers.WithModelLayer(
                    baseCompiled,
                    baseCompiled,
                    "copylayer_",
                    _ => texturePath,
                    fallbackTexture: texturePath,
                    inflate: 0.05f);
            variants[key] = new EntityModelDefinition(namePrefix + key, 64, 64, faceTransforms: null, texturePath: baseTexture, scale: scale, compiled: compiled, baselinePx: 0f, rawSourceFrame: true);
        }

        return variants;
    }

    private static VanillaMeshDefinition BuildVillagerMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var head = root.AddOrReplaceChild("head", VanillaPartPose.Zero,
            new VanillaCubeDefinition("head", 0, 0, -4f, -10f, -4f, 8f, 10f, 8f, MirrorX: false));
        var hat = head.AddOrReplaceChild("hat", VanillaPartPose.Zero,
            new VanillaCubeDefinition("hat", 32, 0, -4f, -10f, -4f, 8f, 10f, 8f, Grow: 0.51f));
        hat.AddOrReplaceChild("hat_rim", VanillaPartPose.OffsetAndRotation(0f, 0f, 0f, -MathF.PI / 2f, 0f, 0f),
            new VanillaCubeDefinition("hat_rim", 30, 47, -8f, -8f, -6f, 16f, 16f, 1f));
        head.AddOrReplaceChild("nose", VanillaPartPose.Offset(0f, -2f, 0f),
            new VanillaCubeDefinition("nose", 24, 0, -1f, -1f, -6f, 2f, 4f, 2f));
        var body = root.AddOrReplaceChild("body", VanillaPartPose.Zero,
            new VanillaCubeDefinition("body", 16, 20, -4f, 0f, -3f, 8f, 12f, 6f, MirrorX: false));
        body.AddOrReplaceChild("jacket", VanillaPartPose.Zero,
            new VanillaCubeDefinition("jacket", 0, 38, -4f, 0f, -3f, 8f, 20f, 6f, Grow: 0.5f));
        // Source VillagerModel: arms PartPose.offsetAndRotation(0,3,-1, -0.75,0,0) - raw literal sign.
        var arms = root.AddOrReplaceChild("arms", VanillaPartPose.OffsetAndRotation(0f, 3f, -1f, -0.75f, 0f, 0f),
            new VanillaCubeDefinition("arms", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f, MirrorX: false));
        arms.AddOrReplaceChild("arms_left", VanillaPartPose.Zero,
            new VanillaCubeDefinition("arms_left", 44, 22, -8f, -2f, -2f, 4f, 8f, 4f));
        arms.AddOrReplaceChild("arms_right", VanillaPartPose.Zero,
            new VanillaCubeDefinition("arms_right", 44, 22, 4f, -2f, -2f, 4f, 8f, 4f, Mirror: true));
        arms.AddOrReplaceChild("arms_middle", VanillaPartPose.Zero,
            new VanillaCubeDefinition("arms_middle", 40, 38, -4f, 2f, -2f, 8f, 4f, 4f));
        root.AddOrReplaceChild("right_leg", VanillaPartPose.Offset(-2f, 12f, 0f),
            new VanillaCubeDefinition("right_leg", 0, 22, -2f, 0f, -2f, 4f, 12f, 4f));
        root.AddOrReplaceChild("left_leg", VanillaPartPose.Offset(2f, 12f, 0f),
            new VanillaCubeDefinition("left_leg", 0, 22, -2f, 0f, -2f, 4f, 12f, 4f, Mirror: true));
        return new VanillaMeshDefinition(root);
    }
}
