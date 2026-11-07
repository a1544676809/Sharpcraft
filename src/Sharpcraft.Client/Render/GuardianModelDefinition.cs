// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// GuardianModel: a big head block with side/top/bottom plates, twelve spikes, one eye and a three-segment tail. Parts are
/// declared with the <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake</c> (T-689); the spike layout
/// follows the reference's SPIKE_X/Y/Z and rotation tables.
/// </summary>
/// <remarks>
/// Bind pose only. The elder guardian reuses the same geometry at scale 2.35 with a different texture; the target beam
/// layer is not a model part and is not ported.
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

    private static readonly VanillaMeshDefinition GuardianMesh = BuildGuardianMesh();

    public static readonly EntityModelDefinition Definition = new(
        "guardian",
        64,
        64,
        [.. VanillaMeshBake.Bake(GuardianMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/guardian/guardian");

    public static readonly EntityModelDefinition Elder = new(
        "elder_guardian",
        64,
        64,
        [.. VanillaMeshBake.Bake(GuardianMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/guardian/guardian_elder",
        2.35f);

    private static VanillaMeshDefinition BuildGuardianMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var head = root.AddOrReplaceChild("head", VanillaPartPose.Zero,
            new VanillaCubeDefinition("head", 0, 0, -6f, 10f, -8f, 12f, 12f, 16f));
        head.AddOrReplaceChild("head_left", VanillaPartPose.Zero,
            new VanillaCubeDefinition("head_left", 0, 28, -8f, 10f, -6f, 2f, 12f, 12f));
        head.AddOrReplaceChild("head_right", VanillaPartPose.Zero,
            new VanillaCubeDefinition("head_right", 0, 28, 6f, 10f, -6f, 2f, 12f, 12f, Mirror: true));
        head.AddOrReplaceChild("head_top", VanillaPartPose.Zero,
            new VanillaCubeDefinition("head_top", 16, 40, -6f, 8f, -6f, 12f, 2f, 12f));
        head.AddOrReplaceChild("head_bottom", VanillaPartPose.Zero,
            new VanillaCubeDefinition("head_bottom", 16, 40, -6f, 22f, -6f, 12f, 2f, 12f));
        for (var i = 0; i < 12; i++)
        {
            head.AddOrReplaceChild("spike" + i,
                VanillaPartPose.OffsetAndRotation(SpikeX[i], 16f + SpikeY[i], SpikeZ[i],
                    MathF.PI * SpikeXRot[i], MathF.PI * SpikeYRot[i], MathF.PI * SpikeZRot[i]),
                new VanillaCubeDefinition("spike" + i, 0, 0, -1f, -4.5f, -1f, 2f, 9f, 2f));
        }

        head.AddOrReplaceChild("eye", VanillaPartPose.Offset(0f, 0f, -8.25f),
            new VanillaCubeDefinition("eye", 8, 0, -1f, 15f, 0f, 2f, 2f, 1f));
        var tail0 = head.AddOrReplaceChild("tail0", VanillaPartPose.Zero,
            new VanillaCubeDefinition("tail0", 40, 0, -2f, 14f, 7f, 4f, 4f, 8f));
        var tail1 = tail0.AddOrReplaceChild("tail1", VanillaPartPose.Offset(-1.5f, 0.5f, 14f),
            new VanillaCubeDefinition("tail1", 0, 54, 0f, 14f, 0f, 3f, 3f, 7f));
        var tail2 = tail1.AddOrReplaceChild("tail2", VanillaPartPose.Offset(0.5f, 0.5f, 6f),
            new VanillaCubeDefinition("tail2", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        tail2.AddOrReplaceChild("tail2_body", VanillaPartPose.Zero,
            new VanillaCubeDefinition("tail2_body", 41, 32, 0f, 14f, 0f, 2f, 2f, 6f));
        tail2.AddOrReplaceChild("tail2_fin", VanillaPartPose.Zero,
            new VanillaCubeDefinition("tail2_fin", 25, 19, 1f, 10.5f, 3f, 1f, 9f, 9f));
        return new VanillaMeshDefinition(root);
    }
}
