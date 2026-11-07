// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Baked equipment layer models. Each entry is a complete model tree of the reference equipment layer class, so the
/// layer is merged into the entity definition as an independent tree instead of cloning flat parts or synthesising fake
/// parent nodes.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.equine.EquineSaddleModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/equine/EquineSaddleModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Equine saddle/armor, camel saddle, llama decor, nautilus shell and happy ghast harness layer meshes baked as independent trees")]
internal static class EquipmentModelTrees
{
    private static readonly CompiledEntityModel HorseArmor =
        VanillaMeshBakery.Build(HorseModelDefinition.BuildBodyMesh(0.1f), 64, 64);

    private static readonly CompiledEntityModel EquineSaddle =
        VanillaMeshBakery.Build(HorseModelDefinition.BuildSaddleMesh(), 64, 64);

    private static readonly CompiledEntityModel DonkeySaddle =
        VanillaMeshBakery.Build(HorseModelDefinition.BuildSaddleMesh(donkey: true), 64, 64);

    private static readonly CompiledEntityModel CamelSaddle =
        VanillaMeshBakery.Build(CamelModelDefinition.BuildSaddleMesh(), 128, 128);

    private static readonly CompiledEntityModel LlamaDecor =
        VanillaMeshBakery.Build(LlamaModelDefinition.BuildBodyMesh(0.5f), 128, 64);

    private static readonly CompiledEntityModel NautilusArmor =
        VanillaMeshBakery.Build(NautilusModelDefinition.BuildBodyMesh(0.01f), 128, 128);

    private static readonly CompiledEntityModel NautilusSaddle =
        VanillaMeshBakery.Build(NautilusModelDefinition.BuildBodyMesh(0.2f, saddleShell: true), 128, 128);

    private static readonly CompiledEntityModel HappyHarness =
        VanillaMeshBakery.Build(BuildHappyHarnessMesh(), 64, 64);

    private static readonly CompiledEntityModel WolfArmor =
        VanillaMeshBakery.Build(WolfModelDefinition.BuildBodyMesh(0.2f), 64, 32);

    /// <summary>Returns the armor model for a humanoid slot or the baked layer model for another equipment kind.</summary>
    public static CompiledEntityModel? For(EquipmentAssetKind kind, int slot, bool player = false, bool baby = false)
    {
        if (kind is EquipmentAssetKind.HumanoidArmor or EquipmentAssetKind.HumanoidLeggings or EquipmentAssetKind.HumanoidArmorBaby)
        {
            return baby ? HumanoidArmorTrees.ForBaby(slot) : HumanoidArmorTrees.For(slot, player);
        }

        return For(kind);
    }

    /// <summary>Returns the baked layer model for a supported equipment kind, or null when the kind has no tree yet.</summary>
    public static CompiledEntityModel? For(EquipmentAssetKind kind) => kind switch
    {
        EquipmentAssetKind.HorseBody => HorseArmor,
        EquipmentAssetKind.HorseSaddle => EquineSaddle,
        EquipmentAssetKind.DonkeySaddle => DonkeySaddle,
        EquipmentAssetKind.MuleSaddle => DonkeySaddle,
        EquipmentAssetKind.SkeletonHorseSaddle => EquineSaddle,
        EquipmentAssetKind.ZombieHorseSaddle => EquineSaddle,
        EquipmentAssetKind.CamelSaddle => CamelSaddle,
        EquipmentAssetKind.LlamaBody => LlamaDecor,
        EquipmentAssetKind.NautilusBody => NautilusArmor,
        EquipmentAssetKind.NautilusSaddle => NautilusSaddle,
        EquipmentAssetKind.HappyGhastBody => HappyHarness,
        EquipmentAssetKind.WolfBody => WolfArmor,
        _ => null
    };

    private static VanillaMeshDefinition BuildHappyHarnessMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("harness", VanillaPartPose.Offset(0f, 24f, 0f),
            new VanillaCubeDefinition("harness", 0, 0, -8f, -16f, -8f, 16f, 16f, 16f));
        root.AddOrReplaceChild("goggles", VanillaPartPose.Offset(0f, 14f, -5.5f),
            new VanillaCubeDefinition("goggles", 0, 32, -8f, -2.5f, -2.5f, 16f, 5f, 5f, Grow: 0.15f));
        return new VanillaMeshDefinition(root);
    }
}
