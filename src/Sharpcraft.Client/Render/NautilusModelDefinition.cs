// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// NautilusModel: a three-box shell, a body with a back layer and three mouth parts, all under an empty root pivot.
/// </summary>
/// <remarks>Bind pose only; swimming animation and the armor/saddle layers are not ported. Zombie Nautilus has its own model.</remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.nautilus.NautilusModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/nautilus/NautilusModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class NautilusModelDefinition
{
    public const int NautilusEntityTypeId = 89;

    public static readonly EntityModelDefinition Definition = new(
        "nautilus",
        128,
        128,
        [
            PlayerModelDefinition.FromVanilla("root", 0f, 29f, -6f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("shell", "root", 0f, -13f, 5f, -7f, -10f, -7f, 14f, 10f, 16f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("shell_mid", "shell", 0f, 0f, 0f, -7f, 0f, -7f, 14f, 8f, 20f, 0, 26),
            PlayerModelDefinition.FromVanillaChild("shell_back", "shell", 0f, 0f, 0f, -7f, 0f, 6f, 14f, 8f, 0f, 48, 26),
            PlayerModelDefinition.FromVanillaChild("body", "root", 0f, -8.5f, 12.3f, -5f, -4.51f, -3f, 10f, 8f, 14f, 0, 54),
            PlayerModelDefinition.FromVanillaChild("body_back", "body", 0f, 0f, 0f, -5f, -4.51f, 7f, 10f, 8f, 0f, 0, 76),
            PlayerModelDefinition.FromVanillaChild("upper_mouth", "body", 0f, -2.51f, 7f, -5f, -2f, 0f, 10f, 4f, 4f, 54, 54, grow: -0.001f),
            PlayerModelDefinition.FromVanillaChild("inner_mouth", "body", 0f, -0.51f, 7.5f, -3f, -2f, -0.5f, 6f, 4f, 4f, 54, 70),
            PlayerModelDefinition.FromVanillaChild("lower_mouth", "body", 0f, 1.49f, 7f, -5f, -1.98f, 0f, 10f, 4f, 4f, 54, 62, grow: -0.001f)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/nautilus/nautilus");
}
