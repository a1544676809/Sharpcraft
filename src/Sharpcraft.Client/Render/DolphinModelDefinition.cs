// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// DolphinModel: body with back fin and both side fins, a two-segment tail, and a head with nose. All pivots and the
/// bind rotations follow the reference; animations are not ported. Parts are declared with the
/// <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake.Bake</c> (T-680).
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.dolphin.DolphinModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/dolphin/DolphinModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class DolphinModelDefinition
{
    public const int DolphinEntityTypeId = 36;

    /// <summary>Declarative DSL tree for the eight nested dolphin parts (T-680).</summary>
    private static readonly VanillaMeshDefinition DolphinMesh = BuildDolphinMesh();

    public static readonly EntityModelDefinition Definition = new(
        "dolphin",
        64,
        64,
        [.. VanillaMeshBake.Bake(DolphinMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/dolphin/dolphin");

    private static VanillaMeshDefinition BuildDolphinMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var body = root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 22f, -5f),
            new VanillaCubeDefinition("body", 22, 0, -4f, -7f, 0f, 8f, 7f, 13f));
        body.AddOrReplaceChild("back_fin", VanillaPartPose.OffsetAndRotation(0f, 0f, 0f, MathF.PI / 3f, 0f, 0f),
            new VanillaCubeDefinition("back_fin", 51, 0, -0.5f, 0f, 8f, 1f, 4f, 5f));
        body.AddOrReplaceChild("left_fin", VanillaPartPose.OffsetAndRotation(2f, -2f, 4f, MathF.PI / 3f, 0f, MathF.PI * 2f / 3f),
            new VanillaCubeDefinition("left_fin", 48, 20, -0.5f, -4f, 0f, 1f, 4f, 7f, Mirror: true));
        body.AddOrReplaceChild("right_fin", VanillaPartPose.OffsetAndRotation(-2f, -2f, 4f, MathF.PI / 3f, 0f, -MathF.PI * 2f / 3f),
            new VanillaCubeDefinition("right_fin", 48, 20, -0.5f, -4f, 0f, 1f, 4f, 7f));
        var tail = body.AddOrReplaceChild("tail", VanillaPartPose.OffsetAndRotation(0f, -2.5f, 11f, -0.10471976f, 0f, 0f),
            new VanillaCubeDefinition("tail", 0, 19, -2f, -2.5f, 0f, 4f, 5f, 11f));
        tail.AddOrReplaceChild("tail_fin", VanillaPartPose.Offset(0f, 0f, 9f),
            new VanillaCubeDefinition("tail_fin", 19, 20, -5f, -0.5f, 0f, 10f, 1f, 6f));
        var head = body.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, -4f, -3f),
            new VanillaCubeDefinition("head", 0, 0, -4f, -3f, -3f, 8f, 7f, 6f));
        head.AddOrReplaceChild("nose", VanillaPartPose.Zero,
            new VanillaCubeDefinition("nose", 0, 13, -1f, 2f, -7f, 2f, 2f, 4f));
        return new VanillaMeshDefinition(root);
    }
}
