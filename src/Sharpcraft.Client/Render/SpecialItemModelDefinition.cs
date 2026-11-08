// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Minimal special item models for trident and shield. These are the first non-flat SpecialModelRenderer shapes; banner,
/// shulker, chest and component-driven special renderers remain separate work.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.special.SpecialModelRenderer", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/special/SpecialModelRenderer.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class SpecialItemModelDefinition
{
    public const string TridentTexture = "entity/trident/trident";
    public const string ShieldTexture = "entity/shield/shield_base_nopattern";

    /// <summary>Trident model: pole plus base and three spikes (32×32 texture).</summary>
    public static readonly EntityModelDefinition Trident = EntityModelDefinition.FromTree(
        "special_trident",
        32,
        32,
        BuildTridentMesh(),
        PlayerModelDefinition.DefaultFaceTransforms,
        TridentTexture,
        1f,
        additionalTexturePaths: [TridentTexture]);

    /// <summary>Held trident models; the special geometry is wrapped in the humanoid arm pivot.</summary>
    public static readonly EntityModelDefinition HeldTridentRight = EntityModelDefinition.FromTree(
        "held_special_trident_right", 32, 32, BuildTridentMesh(held: true),
        PlayerModelDefinition.DefaultFaceTransforms, TridentTexture, 1f,
        additionalTexturePaths: [TridentTexture]);

    public static readonly EntityModelDefinition HeldTridentLeft = EntityModelDefinition.FromTree(
        "held_special_trident_left", 32, 32, BuildTridentMesh(held: true, left: true),
        PlayerModelDefinition.DefaultFaceTransforms, TridentTexture, 1f,
        additionalTexturePaths: [TridentTexture]);

    /// <summary>Shield model: plate plus handle (64×64 texture). Banner patterns are not yet composed.</summary>
    public static readonly EntityModelDefinition Shield = EntityModelDefinition.FromTree(
        "special_shield",
        64,
        64,
        BuildShieldMesh(),
        PlayerModelDefinition.DefaultFaceTransforms,
        ShieldTexture,
        1f,
        additionalTexturePaths: [ShieldTexture]);

    public static readonly EntityModelDefinition HeldShieldRight = EntityModelDefinition.FromTree(
        "held_special_shield_right", 64, 64, BuildShieldMesh(held: true),
        PlayerModelDefinition.DefaultFaceTransforms, ShieldTexture, 1f,
        additionalTexturePaths: [ShieldTexture]);

    public static readonly EntityModelDefinition HeldShieldLeft = EntityModelDefinition.FromTree(
        "held_special_shield_left", 64, 64, BuildShieldMesh(held: true, left: true),
        PlayerModelDefinition.DefaultFaceTransforms, ShieldTexture, 1f,
        additionalTexturePaths: [ShieldTexture]);

    private static VanillaMeshDefinition BuildTridentMesh(bool held = false, bool left = false)
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var parent = held
            ? root.AddOrReplaceChild(left ? "left_arm" : "right_arm",
                VanillaPartPose.Offset(left ? 5f : -5f, 2f, 0f),
                new VanillaCubeDefinition(left ? "left_arm" : "right_arm", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f))
            : root;
        var pole = parent.AddOrReplaceChild("pole", VanillaPartPose.Zero,
            new VanillaCubeDefinition("pole", 0, 6, -0.5f, 2f, -0.5f, 1f, 25f, 1f));
        pole.AddOrReplaceChild("base", VanillaPartPose.Zero,
            new VanillaCubeDefinition("base", 4, 0, -1.5f, 0f, -0.5f, 3f, 2f, 1f));
        pole.AddOrReplaceChild("left_spike", VanillaPartPose.Zero,
            new VanillaCubeDefinition("left_spike", 4, 3, -2.5f, -3f, -0.5f, 1f, 4f, 1f));
        pole.AddOrReplaceChild("middle_spike", VanillaPartPose.Zero,
            new VanillaCubeDefinition("middle_spike", 0, 0, -0.5f, -4f, -0.5f, 1f, 4f, 1f));
        pole.AddOrReplaceChild("right_spike", VanillaPartPose.Zero,
            new VanillaCubeDefinition("right_spike", 4, 3, 1.5f, -3f, -0.5f, 1f, 4f, 1f, Mirror: true));
        return new VanillaMeshDefinition(root);
    }

    private static VanillaMeshDefinition BuildShieldMesh(bool held = false, bool left = false)
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var parent = held
            ? root.AddOrReplaceChild(left ? "left_arm" : "right_arm",
                VanillaPartPose.Offset(left ? 5f : -5f, 2f, 0f),
                new VanillaCubeDefinition(left ? "left_arm" : "right_arm", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f))
            : root;
        parent.AddOrReplaceChild("plate", VanillaPartPose.Zero,
            new VanillaCubeDefinition("plate", 0, 0, -6f, -11f, -2f, 12f, 22f, 1f));
        parent.AddOrReplaceChild("handle", VanillaPartPose.Zero,
            new VanillaCubeDefinition("handle", 26, 0, -1f, -3f, -1f, 2f, 6f, 6f));
        return new VanillaMeshDefinition(root);
    }
}
