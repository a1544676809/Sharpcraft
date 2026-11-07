// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>Baby nautilus/happy ghast/glow squid models. Parts are declared with the <c>VanillaMeshDefinition</c> DSL
/// and baked via <c>VanillaMeshBake</c> (T-698).</summary>
public static class BabyModelDefinitionsFinal
{
    private static readonly VanillaMeshDefinition NautilusMesh = BuildNautilusMesh();
    private static readonly VanillaMeshDefinition HappyGhastMesh = BuildHappyGhastMesh();

    public static readonly EntityModelDefinition Nautilus = new(
        "baby_nautilus", 64, 64, [.. VanillaMeshBake.Bake(NautilusMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms, "entity/nautilus/nautilus_baby");

    public static readonly EntityModelDefinition HappyGhast = new(
        "baby_happy_ghast", 64, 64, [.. VanillaMeshBake.Bake(HappyGhastMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms, "entity/ghast/happy_ghast_baby", 0.95f);

    public static readonly EntityModelDefinition GlowSquid = new(
        "baby_glow_squid", 32, 32, BabyModelDefinitionsAquatic.Squid.Parts,
        PlayerModelDefinition.DefaultFaceTransforms, "entity/squid/glow_squid_baby");

    public static readonly IReadOnlyDictionary<int, EntityModelDefinition> Models =
        new Dictionary<int, EntityModelDefinition>
        {
            [89] = Nautilus,
            [59] = HappyGhast,
            [62] = GlowSquid
        };

    private static VanillaMeshDefinition BuildNautilusMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var rootPart = root.AddOrReplaceChild("root", VanillaPartPose.Offset(-0.5f, 28f, -0.5f),
            new VanillaCubeDefinition("root", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        var shell = rootPart.AddOrReplaceChild("shell", VanillaPartPose.Offset(3f, -8f, -2f),
            new VanillaCubeDefinition("shell", 0, 0, -6f, -4f, -1f, 7f, 4f, 7f));
        shell.AddOrReplaceChild("shell_mid", VanillaPartPose.Zero,
            new VanillaCubeDefinition("shell_mid", 0, 11, -6f, 0f, -1f, 7f, 4f, 9f));
        shell.AddOrReplaceChild("shell_back", VanillaPartPose.Zero,
            new VanillaCubeDefinition("shell_back", 23, 11, -6f, 0f, 5f, 7f, 4f, 0f));
        var body = rootPart.AddOrReplaceChild("body", VanillaPartPose.Offset(0.5f, -5f, 3f),
            new VanillaCubeDefinition("body", 0, 24, -2.5f, -3.01f, -1f, 5f, 4f, 7f));
        body.AddOrReplaceChild("body_back", VanillaPartPose.Zero,
            new VanillaCubeDefinition("body_back", 0, 35, -2.5f, -3.01f, 4.1f, 5f, 4f, 0f));
        body.AddOrReplaceChild("upper_mouth", VanillaPartPose.Offset(0f, -2.01f, 3.9f),
            new VanillaCubeDefinition("upper_mouth", 24, 24, -2.5f, -1f, 0f, 5f, 2f, 2f, Grow: -0.001f));
        body.AddOrReplaceChild("inner_mouth", VanillaPartPose.Offset(0f, -1.01f, 4.9f),
            new VanillaCubeDefinition("inner_mouth", 24, 32, -1.5f, -1f, -1f, 3f, 2f, 2f));
        body.AddOrReplaceChild("lower_mouth", VanillaPartPose.Offset(0f, -0.01f, 3.9f),
            new VanillaCubeDefinition("lower_mouth", 24, 28, -2.5f, -1f, 0f, 5f, 2f, 2f, Grow: -0.001f));
        return new VanillaMeshDefinition(root);
    }

    private static VanillaMeshDefinition BuildHappyGhastMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var body = root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 16f, 0f),
            new VanillaCubeDefinition("body", 0, 0, -8f, -8f, -8f, 16f, 16f, 16f));
        body.AddOrReplaceChild("inner_body", VanillaPartPose.Offset(0f, 8f, 0f),
            new VanillaCubeDefinition("inner_body", 0, 32, -8f, -16f, -8f, 16f, 16f, 16f, Grow: -0.5f));
        (float X, float Y, float Z, float Length)[] tentacles =
        [
            (-3.75f, 7f, -5f, 5f), (1.25f, 7f, -5f, 7f), (6.25f, 7f, -5f, 4f),
            (-6.25f, 7f, 0f, 5f), (-1.25f, 7f, 0f, 5f), (3.75f, 7f, 0f, 7f),
            (-3.75f, 7f, 5f, 8f), (1.25f, 7f, 5f, 8f), (6.25f, 7f, 5f, 5f)
        ];
        for (var i = 0; i < tentacles.Length; i++)
        {
            var (x, y, z, length) = tentacles[i];
            body.AddOrReplaceChild("tentacle" + i, VanillaPartPose.Offset(x, y, z),
                new VanillaCubeDefinition("tentacle" + i, 0, 0, -1f, 0f, -1f, 2f, length, 2f));
        }

        return new VanillaMeshDefinition(root);
    }
}
