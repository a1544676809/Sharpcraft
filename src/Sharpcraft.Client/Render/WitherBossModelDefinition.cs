// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// WitherBossModel: shoulders, a ribcage with three rib slabs, a tail, a centre head and two side heads. Parts are
/// declared with the <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake</c> (T-690).
/// </summary>
/// <remarks>Bind pose only; head/ribcage animation and the invulnerable armour layer are not ported.</remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.wither.WitherBossModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/wither/WitherBossModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class WitherBossModelDefinition
{
    public const int WitherEntityTypeId = 149;

    private static readonly VanillaMeshDefinition WitherMesh = BuildWitherMesh();

    private static readonly CompiledEntityModel WitherCompiled =
        VanillaMeshBakery.Build(WitherMesh, 64, 64);

    public static readonly EntityModelDefinition Definition = new(
        "wither",
        64,
        64,
        [.. VanillaMeshBake.Bake(WitherMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/wither/wither",
        compiled: WitherCompiled);

    private static VanillaMeshDefinition BuildWitherMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("shoulders", VanillaPartPose.Zero,
            new VanillaCubeDefinition("shoulders", 0, 16, -10f, 3.9f, -0.5f, 20f, 3f, 3f, MirrorX: false));
        var ribcage = root.AddOrReplaceChild("ribcage", VanillaPartPose.OffsetAndRotation(-2f, 6.9f, -0.5f, -0.20420352f, 0f, 0f),
            new VanillaCubeDefinition("ribcage", 0, 22, 0f, 0f, 0f, 3f, 10f, 3f));
        ribcage.AddOrReplaceChild("rib_1", VanillaPartPose.Zero,
            new VanillaCubeDefinition("rib_1", 24, 22, -4f, 1.5f, 0.5f, 11f, 2f, 2f));
        ribcage.AddOrReplaceChild("rib_2", VanillaPartPose.Zero,
            new VanillaCubeDefinition("rib_2", 24, 22, -4f, 4f, 0.5f, 11f, 2f, 2f));
        ribcage.AddOrReplaceChild("rib_3", VanillaPartPose.Zero,
            new VanillaCubeDefinition("rib_3", 24, 22, -4f, 6.5f, 0.5f, 11f, 2f, 2f));
        root.AddOrReplaceChild("tail", VanillaPartPose.OffsetAndRotation(-2f, 16.692228f, 1.527873f, -0.83252203f, 0f, 0f),
            new VanillaCubeDefinition("tail", 12, 22, 0f, 0f, 0f, 3f, 6f, 3f));
        root.AddOrReplaceChild("center_head", VanillaPartPose.Zero,
            new VanillaCubeDefinition("center_head", 0, 0, -4f, -4f, -4f, 8f, 8f, 8f));
        root.AddOrReplaceChild("right_head", VanillaPartPose.Offset(-8f, 4f, 0f),
            new VanillaCubeDefinition("right_head", 32, 0, -4f, -4f, -4f, 6f, 6f, 6f));
        root.AddOrReplaceChild("left_head", VanillaPartPose.Offset(10f, 4f, 0f),
            new VanillaCubeDefinition("left_head", 32, 0, -4f, -4f, -4f, 6f, 6f, 6f));
        return new VanillaMeshDefinition(root);
    }
}
