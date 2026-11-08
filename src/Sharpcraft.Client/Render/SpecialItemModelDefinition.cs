// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Minimal special item models for trident and shield. These are the first non-flat SpecialModelRenderer shapes; banner,
/// shulker, chest and component-driven special renderers remain separate work.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.special.SpecialModelRenderer", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/special/SpecialModelRenderer.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
[Sharpcraft.Common.Diagnostics.Simplification("SIM-ITEM-SPECIAL-MODELS", "T-631e", Note = "10 of 13 reference special renderers ported; bell, book and copper golem statue are absent")]
[Sharpcraft.Common.Diagnostics.Simplification("SIM-ITEM-SPECIAL-STATE", "T-631e", Note = "ChestSpecialRenderer openness/chestType and ShulkerBoxSpecialRenderer openness are ignored; shield patterns and dye are not composed")]
public static class SpecialItemModelDefinition
{
    public const string TridentTexture = "entity/trident/trident";
    public const string ShieldTexture = "entity/shield/shield_base_nopattern";
    public const string ShieldPatternedTexture = "entity/shield/shield_base";

    /// <summary>
    /// Reference <c>ShieldSpecialRenderer</c> sprite choice: the patterned base is used once the stack carries banner
    /// patterns or a base colour.
    /// </summary>
    public static string ShieldSprite(bool hasPatterns) => hasPatterns ? ShieldPatternedTexture : ShieldTexture;
    public const string BannerTexture = "entity/banner_base";
    public const string DecoratedPotTexture = "entity/decorated_pot/decorated_pot_base";
    public const string DecoratedPotSideTexture = "entity/decorated_pot/decorated_pot_side";
    public const string ConduitTexture = "entity/conduit/base";
    public const string EndPortalTexture = "entity/end_portal/end_portal";

    /// <summary>Standing banner: pole and bar, 64×64 texture (reference <c>BannerModel.createBodyLayer(true)</c>).</summary>
    public static readonly EntityModelDefinition BannerStanding = EntityModelDefinition.FromTree(
        "special_banner_standing", 64, 64, BuildBannerMesh(standing: true),
        PlayerModelDefinition.DefaultFaceTransforms, BannerTexture, 1f,
        additionalTexturePaths: [BannerTexture]);

    /// <summary>Wall banner: bar only, positioned for the wall attachment.</summary>
    public static readonly EntityModelDefinition BannerWall = EntityModelDefinition.FromTree(
        "special_banner_wall", 64, 64, BuildBannerMesh(standing: false),
        PlayerModelDefinition.DefaultFaceTransforms, BannerTexture, 1f,
        additionalTexturePaths: [BannerTexture]);

    /// <summary>Standing banner flag plane (reference <c>BannerFlagModel.createFlagLayer(true)</c>).</summary>
    public static readonly EntityModelDefinition BannerFlagStanding = EntityModelDefinition.FromTree(
        "special_banner_flag_standing", 64, 64, BuildBannerFlagMesh(standing: true),
        PlayerModelDefinition.DefaultFaceTransforms, BannerTexture, 1f,
        additionalTexturePaths: [BannerTexture]);

    /// <summary>Wall banner flag plane.</summary>
    public static readonly EntityModelDefinition BannerFlagWall = EntityModelDefinition.FromTree(
        "special_banner_flag_wall", 64, 64, BuildBannerFlagMesh(standing: false),
        PlayerModelDefinition.DefaultFaceTransforms, BannerTexture, 1f,
        additionalTexturePaths: [BannerTexture]);

    /// <summary>Decorated pot base: neck, top and bottom planes (reference <c>DecoratedPotRenderer.createBaseLayer</c>).</summary>
    public static readonly EntityModelDefinition DecoratedPot = EntityModelDefinition.FromTree(
        "special_decorated_pot", 32, 32, BuildDecoratedPotBaseMesh(),
        PlayerModelDefinition.DefaultFaceTransforms, DecoratedPotTexture, 1f,
        additionalTexturePaths: [DecoratedPotTexture]);

    /// <summary>Decorated pot side planes; one plane per cardinal face.</summary>
    public static readonly EntityModelDefinition DecoratedPotSides = EntityModelDefinition.FromTree(
        "special_decorated_pot_sides", 16, 16, BuildDecoratedPotSidesMesh(),
        PlayerModelDefinition.DefaultFaceTransforms, DecoratedPotSideTexture, 1f,
        additionalTexturePaths: [DecoratedPotSideTexture]);

    /// <summary>Conduit shell cube (reference <c>ConduitRenderer.createShellLayer</c>), 32×16 texture.</summary>
    public static readonly EntityModelDefinition Conduit = EntityModelDefinition.FromTree(
        "special_conduit", 32, 16, BuildConduitMesh(),
        PlayerModelDefinition.DefaultFaceTransforms, ConduitTexture, 1f,
        additionalTexturePaths: [ConduitTexture]);

    /// <summary>End portal/gateway cube: the reference submits all six faces of a unit block.</summary>
    public static readonly EntityModelDefinition EndCube = EntityModelDefinition.FromTree(
        "special_end_cube", 16, 16, BuildEndCubeMesh(),
        PlayerModelDefinition.DefaultFaceTransforms, EndPortalTexture, 1f,
        additionalTexturePaths: [EndPortalTexture]);

    /// <summary>Chest item model: bottom, lid and lock (64×64 texture). The texture is selected from the special property.</summary>
    public static readonly EntityModelDefinition Chest = EntityModelDefinition.FromTree(
        "special_chest",
        64,
        64,
        BuildChestMesh(),
        PlayerModelDefinition.DefaultFaceTransforms,
        texturePath: null,
        1f);

    public static readonly EntityModelDefinition HeldChestRight = EntityModelDefinition.FromTree(
        "held_special_chest_right", 64, 64, BuildChestMesh(held: true),
        PlayerModelDefinition.DefaultFaceTransforms, texturePath: null, 1f);

    public static readonly EntityModelDefinition HeldChestLeft = EntityModelDefinition.FromTree(
        "held_special_chest_left", 64, 64, BuildChestMesh(held: true, left: true),
        PlayerModelDefinition.DefaultFaceTransforms, texturePath: null, 1f);

    /// <summary>Shulker box item model: lid plus base; texture comes from the special property.</summary>
    public static readonly EntityModelDefinition ShulkerBox = EntityModelDefinition.FromTree(
        "special_shulker_box",
        64,
        64,
        BuildShulkerMesh(),
        PlayerModelDefinition.DefaultFaceTransforms,
        texturePath: null,
        1f);

    public static readonly EntityModelDefinition HeldShulkerRight = EntityModelDefinition.FromTree(
        "held_special_shulker_right", 64, 64, BuildShulkerMesh(held: true),
        PlayerModelDefinition.DefaultFaceTransforms, texturePath: null, 1f);

    public static readonly EntityModelDefinition HeldShulkerLeft = EntityModelDefinition.FromTree(
        "held_special_shulker_left", 64, 64, BuildShulkerMesh(held: true, left: true),
        PlayerModelDefinition.DefaultFaceTransforms, texturePath: null, 1f);

    /// <summary>Mob/player skull cube (8×8×8); the texture is selected from the special `kind` property.</summary>
    public static readonly EntityModelDefinition Skull = EntityModelDefinition.FromTree(
        "special_skull",
        64,
        32,
        BuildSkullMesh(),
        PlayerModelDefinition.DefaultFaceTransforms,
        texturePath: null,
        1f);

    public static readonly EntityModelDefinition HeldSkullRight = EntityModelDefinition.FromTree(
        "held_special_skull_right", 64, 32, BuildSkullMesh(held: true),
        PlayerModelDefinition.DefaultFaceTransforms, texturePath: null, 1f);

    public static readonly EntityModelDefinition HeldSkullLeft = EntityModelDefinition.FromTree(
        "held_special_skull_left", 64, 32, BuildSkullMesh(held: true, left: true),
        PlayerModelDefinition.DefaultFaceTransforms, texturePath: null, 1f);

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

    private static VanillaMeshDefinition BuildBannerMesh(bool standing)
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        if (standing)
        {
            // Reference BannerModel: pole texOffs(44,0) box(-1,-42,-1, 2,42,2).
            root.AddOrReplaceChild("pole", VanillaPartPose.Zero,
                new VanillaCubeDefinition("pole", 44, 0, -1f, -42f, -1f, 2f, 42f, 2f));
        }

        // Reference BannerModel: bar texOffs(0,42), y/z depend on the attachment.
        root.AddOrReplaceChild("bar", VanillaPartPose.Zero,
            new VanillaCubeDefinition("bar", 0, 42, -10f, standing ? -44f : -20.5f, standing ? -1f : 9.5f, 20f, 2f, 2f));
        return new VanillaMeshDefinition(root);
    }

    private static VanillaMeshDefinition BuildBannerFlagMesh(bool standing)
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        // Reference BannerFlagModel.createFlagLayer: texOffs(0,0) box(-10,0,-2, 20,40,1) under the attachment pose.
        root.AddOrReplaceChild("flag",
            VanillaPartPose.Offset(0f, standing ? -44f : -20.5f, standing ? 0f : 10.5f),
            new VanillaCubeDefinition("flag", 0, 0, -10f, 0f, -2f, 20f, 40f, 1f));
        return new VanillaMeshDefinition(root);
    }

    private static VanillaMeshDefinition BuildDecoratedPotBaseMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        // Reference DecoratedPotRenderer.createBaseLayer: neck rotated pi about X, deflated ring plus inflated rim.
        root.AddOrReplaceChild("neck", VanillaPartPose.OffsetAndRotation(0f, 37f, 16f, MathF.PI, 0f, 0f),
            new VanillaCubeDefinition("neck", 0, 0, 4f, 17f, 4f, 8f, 3f, 8f, Grow: -0.1f),
            new VanillaCubeDefinition("neck_rim", 0, 5, 5f, 20f, 5f, 6f, 1f, 6f, Grow: 0.2f));

        root.AddOrReplaceChild("top", VanillaPartPose.Offset(1f, 16f, 1f),
            new VanillaCubeDefinition("top", -14, 13, 0f, 0f, 0f, 14f, 0f, 14f));
        root.AddOrReplaceChild("bottom", VanillaPartPose.Offset(1f, 0f, 1f),
            new VanillaCubeDefinition("bottom", -14, 13, 0f, 0f, 0f, 14f, 0f, 14f));
        return new VanillaMeshDefinition(root);
    }

    private static VanillaMeshDefinition BuildDecoratedPotSidesMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        // Reference DecoratedPotRenderer.createSidesLayer: one 14x16 plane per cardinal face.
        root.AddOrReplaceChild("back", VanillaPartPose.OffsetAndRotation(15f, 16f, 1f, 0f, 0f, MathF.PI),
            new VanillaCubeDefinition("back", 1, 0, 0f, 0f, 0f, 14f, 16f, 0f));
        root.AddOrReplaceChild("left", VanillaPartPose.OffsetAndRotation(1f, 16f, 1f, 0f, -MathF.PI / 2f, MathF.PI),
            new VanillaCubeDefinition("left", 1, 0, 0f, 0f, 0f, 14f, 16f, 0f));
        root.AddOrReplaceChild("right", VanillaPartPose.OffsetAndRotation(15f, 16f, 15f, 0f, MathF.PI / 2f, MathF.PI),
            new VanillaCubeDefinition("right", 1, 0, 0f, 0f, 0f, 14f, 16f, 0f));
        root.AddOrReplaceChild("front", VanillaPartPose.OffsetAndRotation(1f, 16f, 15f, MathF.PI, 0f, 0f),
            new VanillaCubeDefinition("front", 1, 0, 0f, 0f, 0f, 14f, 16f, 0f));
        return new VanillaMeshDefinition(root);
    }

    private static VanillaMeshDefinition BuildConduitMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        // Reference ConduitRenderer.createShellLayer: texOffs(0,0) box(-3,-3,-3, 6,6,6) on a 32x16 sheet.
        root.AddOrReplaceChild("shell", VanillaPartPose.Zero,
            new VanillaCubeDefinition("shell", 0, 0, -3f, -3f, -3f, 6f, 6f, 6f));
        return new VanillaMeshDefinition(root);
    }

    private static VanillaMeshDefinition BuildEndCubeMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        // Reference AbstractEndPortalRenderer.submitSpecial submits every face of the unit cube (0,0,0)..(1,1,1).
        root.AddOrReplaceChild("cube", VanillaPartPose.Zero,
            new VanillaCubeDefinition("cube", 0, 0, 0f, 0f, 0f, 16f, 16f, 16f));
        return new VanillaMeshDefinition(root);
    }

    private static VanillaMeshDefinition BuildShulkerMesh(bool held = false, bool left = false)
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var parent = held
            ? root.AddOrReplaceChild(left ? "left_arm" : "right_arm",
                VanillaPartPose.Offset(left ? 5f : -5f, 2f, 0f),
                new VanillaCubeDefinition(left ? "left_arm" : "right_arm", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f))
            : root;
        parent.AddOrReplaceChild("lid", VanillaPartPose.Offset(0f, 24f, 0f),
            new VanillaCubeDefinition("lid", 0, 0, -8f, -16f, -8f, 16f, 12f, 16f));
        parent.AddOrReplaceChild("base", VanillaPartPose.Offset(0f, 24f, 0f),
            new VanillaCubeDefinition("base", 0, 28, -8f, -8f, -8f, 16f, 8f, 16f));
        return new VanillaMeshDefinition(root);
    }

    private static VanillaMeshDefinition BuildChestMesh(bool held = false, bool left = false)
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var parent = held
            ? root.AddOrReplaceChild(left ? "left_arm" : "right_arm",
                VanillaPartPose.Offset(left ? 5f : -5f, 2f, 0f),
                new VanillaCubeDefinition(left ? "left_arm" : "right_arm", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f))
            : root;
        parent.AddOrReplaceChild("bottom", VanillaPartPose.Zero,
            new VanillaCubeDefinition("bottom", 0, 19, 1f, 0f, 1f, 14f, 10f, 14f));
        parent.AddOrReplaceChild("lid", VanillaPartPose.Offset(0f, 9f, 1f),
            new VanillaCubeDefinition("lid", 0, 0, 1f, 0f, 0f, 14f, 5f, 14f));
        parent.AddOrReplaceChild("lock", VanillaPartPose.Offset(0f, 9f, 1f),
            new VanillaCubeDefinition("lock", 0, 0, 7f, -2f, 14f, 2f, 4f, 1f));
        return new VanillaMeshDefinition(root);
    }

    private static VanillaMeshDefinition BuildSkullMesh(bool held = false, bool left = false)
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var parent = held
            ? root.AddOrReplaceChild(left ? "left_arm" : "right_arm",
                VanillaPartPose.Offset(left ? 5f : -5f, 2f, 0f),
                new VanillaCubeDefinition(left ? "left_arm" : "right_arm", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f))
            : root;
        parent.AddOrReplaceChild("head", VanillaPartPose.Zero,
            new VanillaCubeDefinition("head", 0, 0, -4f, -8f, -4f, 8f, 8f, 8f));
        return new VanillaMeshDefinition(root);
    }

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
