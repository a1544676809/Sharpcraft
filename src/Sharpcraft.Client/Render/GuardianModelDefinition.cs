// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// GuardianModel: a big head block with side/top/bottom plates, twelve spikes, one eye and a three-segment tail.
/// </summary>
/// <remarks>
/// Bind pose only. The elder guardian reuses the same geometry at scale 2.35 with a different texture. The spike layout
/// follows the reference's SPIKE_X/Y/Z and rotation tables; the target beam layer is not a model part and is not ported.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.guardian.GuardianModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/guardian/GuardianModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class GuardianModelDefinition
{
    public const int GuardianEntityTypeId = 64;
    public const int ElderGuardianEntityTypeId = 41;

    private static readonly float[] SpikeXRot = [1.75f, 0.25f, 0f, 0f, 0.5f, 0.5f, 0.5f, 0.5f, 1.25f, 0.75f, 0f, 0f];
    private static readonly float[] SpikeYRot = [0f, 0f, 0f, 0f, 0.25f, 1.75f, 1.25f, 0.75f, 0f, 0f, 0f, 0f];
    private static readonly float[] SpikeZRot = [0f, 0f, 0.25f, 1.75f, 0f, 0f, 0f, 0f, 0f, 0f, 0.75f, 1.25f];
    private static readonly float[] SpikeX = [0f, 0f, 8f, -8f, -8f, 8f, 8f, -8f, 0f, 0f, 8f, -8f];
    private static readonly float[] SpikeY = [-8f, -8f, -8f, -8f, 0f, 0f, 0f, 0f, 8f, 8f, 8f, 8f];
    private static readonly float[] SpikeZ = [8f, -8f, 0f, 0f, -8f, -8f, 8f, 8f, 8f, -8f, 0f, 0f];

    public static readonly EntityModelDefinition Definition = new(
        "guardian",
        64,
        64,
        BuildParts(),
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/guardian/guardian");

    public static readonly EntityModelDefinition Elder = new(
        "elder_guardian",
        64,
        64,
        BuildParts(),
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/guardian/guardian_elder",
        2.35f);

    private static EntityModelPart[] BuildParts()
    {
        var parts = new List<EntityModelPart>(24)
        {
            PlayerModelDefinition.FromVanilla("head", 0f, 0f, 0f, -6f, 10f, -8f, 12f, 12f, 16f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("head_left", "head", 0f, 0f, 0f, -8f, 10f, -6f, 2f, 12f, 12f, 0, 28),
            PlayerModelDefinition.FromVanillaChild("head_right", "head", 0f, 0f, 0f, 6f, 10f, -6f, 2f, 12f, 12f, 0, 28, mirror: true),
            PlayerModelDefinition.FromVanillaChild("head_top", "head", 0f, 0f, 0f, -6f, 8f, -6f, 12f, 2f, 12f, 16, 40),
            PlayerModelDefinition.FromVanillaChild("head_bottom", "head", 0f, 0f, 0f, -6f, 22f, -6f, 12f, 2f, 12f, 16, 40)
        };

        for (var i = 0; i < 12; i++)
        {
            parts.Add(PlayerModelDefinition.FromVanillaChild(
                "spike" + i,
                "head",
                SpikeX[i],
                16f + SpikeY[i],
                SpikeZ[i],
                -1f,
                -4.5f,
                -1f,
                2f,
                9f,
                2f,
                0,
                0,
                rotationX: MathF.PI * SpikeXRot[i],
                rotationY: MathF.PI * SpikeYRot[i],
                rotationZ: MathF.PI * SpikeZRot[i]));
        }

        parts.Add(PlayerModelDefinition.FromVanillaChild("eye", "head", 0f, 0f, -8.25f, -1f, 15f, 0f, 2f, 2f, 1f, 8, 0));
        parts.Add(PlayerModelDefinition.FromVanillaChild("tail0", "head", 0f, 0f, 0f, -2f, 14f, 7f, 4f, 4f, 8f, 40, 0));
        parts.Add(PlayerModelDefinition.FromVanillaChild("tail1", "tail0", -1.5f, 0.5f, 14f, 0f, 14f, 0f, 3f, 3f, 7f, 0, 54));
        parts.Add(PlayerModelDefinition.FromVanillaChild("tail2", "tail1", 0.5f, 0.5f, 6f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0));
        parts.Add(PlayerModelDefinition.FromVanillaChild("tail2_body", "tail2", 0f, 0f, 0f, 0f, 14f, 0f, 2f, 2f, 6f, 41, 32));
        parts.Add(PlayerModelDefinition.FromVanillaChild("tail2_fin", "tail2", 0f, 0f, 0f, 1f, 10.5f, 3f, 1f, 9f, 9f, 25, 19));
        return [.. parts];
    }
}
