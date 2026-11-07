// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// SquidModel: one 12x16x12 body and eight 2x18x2 tentacles rotated around Y. Parts are declared with the
/// <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake</c> (T-689).
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.squid.SquidModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/squid/SquidModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class SquidModelDefinition
{
    public const int SquidEntityTypeId = 130;
    public const int GlowSquidEntityTypeId = 62;

    private static readonly VanillaMeshDefinition SquidMesh = BuildSquidMesh();

    private static readonly EntityModelPart[] Parts = [.. VanillaMeshBake.Bake(SquidMesh, 0, 0)];

    public static readonly EntityModelDefinition Definition = EntityModelDefinition.FromTree(
        "squid",
        64,
        32,
        SquidMesh,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/squid/squid");

    public static readonly EntityModelDefinition GlowSquid = new(
        "glow_squid",
        64,
        32,
        Parts,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/squid/glow_squid",
        compiled: Definition.Compiled);

    private static VanillaMeshDefinition BuildSquidMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 8f, 0f),
            new VanillaCubeDefinition("body", 0, 0, -6f, -8f, -6f, 12f, 16f, 12f, Grow: 0.02f));
        AddTentacle(root, 0, 5f, 0f, MathF.PI / 2f);
        AddTentacle(root, 1, 3.535f, 3.535f, MathF.PI / 4f);
        AddTentacle(root, 2, 0f, 5f, 0f);
        AddTentacle(root, 3, -3.535f, 3.535f, -MathF.PI / 4f);
        AddTentacle(root, 4, -5f, 0f, -MathF.PI / 2f);
        AddTentacle(root, 5, -3.535f, -3.535f, -3f * MathF.PI / 4f);
        AddTentacle(root, 6, 0f, -5f, MathF.PI);
        AddTentacle(root, 7, 3.535f, -3.535f, 3f * MathF.PI / 4f);
        return new VanillaMeshDefinition(root);
    }

    private static void AddTentacle(VanillaPartDefinition root, int index, float x, float z, float yRot) =>
        root.AddOrReplaceChild("tentacle" + index,
            VanillaPartPose.OffsetAndRotation(x, 15f, z, 0f, yRot, 0f),
            new VanillaCubeDefinition("tentacle" + index, 48, 0, -1f, 0f, -1f, 2f, 18f, 2f));
}
