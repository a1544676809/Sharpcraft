// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// NautilusModel: a three-box shell, a body with a back layer and three mouth parts, all under an empty root pivot.
/// Parts are declared with the <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake</c> (T-691).
/// </summary>
/// <remarks>Bind pose only; swimming animation and the armor/saddle layers are not ported. Zombie Nautilus has its own model.</remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.nautilus.NautilusModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/nautilus/NautilusModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class NautilusModelDefinition
{
    public const int NautilusEntityTypeId = 89;

    private static readonly VanillaMeshDefinition NautilusMesh = BuildNautilusMesh();

    public static readonly EntityModelDefinition Definition = new(
        "nautilus",
        128,
        128,
        [.. VanillaMeshBake.Bake(NautilusMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/nautilus/nautilus");

    private static VanillaMeshDefinition BuildNautilusMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var rootPart = root.AddOrReplaceChild("root", VanillaPartPose.Offset(0f, 29f, -6f),
            new VanillaCubeDefinition("root", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        var shell = rootPart.AddOrReplaceChild("shell", VanillaPartPose.Offset(0f, -13f, 5f),
            new VanillaCubeDefinition("shell", 0, 0, -7f, -10f, -7f, 14f, 10f, 16f));
        shell.AddOrReplaceChild("shell_mid", VanillaPartPose.Zero,
            new VanillaCubeDefinition("shell_mid", 0, 26, -7f, 0f, -7f, 14f, 8f, 20f));
        shell.AddOrReplaceChild("shell_back", VanillaPartPose.Zero,
            new VanillaCubeDefinition("shell_back", 48, 26, -7f, 0f, 6f, 14f, 8f, 0f));
        var body = rootPart.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, -8.5f, 12.3f),
            new VanillaCubeDefinition("body", 0, 54, -5f, -4.51f, -3f, 10f, 8f, 14f));
        body.AddOrReplaceChild("body_back", VanillaPartPose.Zero,
            new VanillaCubeDefinition("body_back", 0, 76, -5f, -4.51f, 7f, 10f, 8f, 0f));
        body.AddOrReplaceChild("upper_mouth", VanillaPartPose.Offset(0f, -2.51f, 7f),
            new VanillaCubeDefinition("upper_mouth", 54, 54, -5f, -2f, 0f, 10f, 4f, 4f, Grow: -0.001f));
        body.AddOrReplaceChild("inner_mouth", VanillaPartPose.Offset(0f, -0.51f, 7.5f),
            new VanillaCubeDefinition("inner_mouth", 54, 70, -3f, -2f, -0.5f, 6f, 4f, 4f));
        body.AddOrReplaceChild("lower_mouth", VanillaPartPose.Offset(0f, 1.49f, 7f),
            new VanillaCubeDefinition("lower_mouth", 54, 62, -5f, -1.98f, 0f, 10f, 4f, 4f, Grow: -0.001f));
        return new VanillaMeshDefinition(root);
    }
}
