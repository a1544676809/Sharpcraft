// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// CodModel: body, head, nose, two side fins, tail fin and a flat top fin; static bind pose. Parts are declared with the
/// <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake.Bake</c> (T-680).
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.fish.CodModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/fish/CodModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class CodModelDefinition
{
    public const int CodEntityTypeId = 27;

    /// <summary>Declarative DSL tree for the seven cod parts (T-680).</summary>
    private static readonly VanillaMeshDefinition CodMesh = BuildCodMesh();

    private static readonly CompiledEntityModel CodCompiled =
        VanillaMeshBakery.Build(CodMesh, 32, 32);

    public static readonly EntityModelDefinition Definition = new(
        "cod",
        32,
        32,
        [.. VanillaMeshBake.Bake(CodMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/fish/cod",
        compiled: CodCompiled);

    private static VanillaMeshDefinition BuildCodMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 22f, 0f),
            new VanillaCubeDefinition("body", 0, 0, -1f, -2f, 0f, 2f, 4f, 7f));
        root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 22f, 0f),
            new VanillaCubeDefinition("head", 11, 0, -1f, -2f, -3f, 2f, 4f, 3f));
        root.AddOrReplaceChild("nose", VanillaPartPose.Offset(0f, 22f, -3f),
            new VanillaCubeDefinition("nose", 0, 0, -1f, -2f, -1f, 2f, 3f, 1f));
        root.AddOrReplaceChild("right_fin", VanillaPartPose.OffsetAndRotation(-1f, 23f, 0f, 0f, 0f, MathF.PI / 4f),
            new VanillaCubeDefinition("right_fin", 22, 1, -2f, 0f, -1f, 2f, 0f, 2f));
        root.AddOrReplaceChild("left_fin", VanillaPartPose.OffsetAndRotation(1f, 23f, 0f, 0f, 0f, -MathF.PI / 4f),
            new VanillaCubeDefinition("left_fin", 22, 4, 0f, 0f, -1f, 2f, 0f, 2f));
        root.AddOrReplaceChild("tail_fin", VanillaPartPose.Offset(0f, 22f, 7f),
            new VanillaCubeDefinition("tail_fin", 22, 3, 0f, -2f, 0f, 0f, 4f, 4f));
        root.AddOrReplaceChild("top_fin", VanillaPartPose.Offset(0f, 20f, 0f),
            new VanillaCubeDefinition("top_fin", 20, -6, 0f, -1f, -1f, 0f, 1f, 6f));
        return new VanillaMeshDefinition(root);
    }
}
