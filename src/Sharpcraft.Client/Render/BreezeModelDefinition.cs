// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// BreezeModel: the body with head/eyes/rods and a separate translucent wind body made of nested shells.
/// Base body texture is 32x32; wind uses a 128x128 texture and eyes use an emissive 32x32 texture.
/// </summary>
/// <remarks>
/// Bind pose only; idle/shoot/slide animations and the wind UV scroll are not ported. Parts are declared with the
/// <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake.Bake</c> (T-679), using the per-cube emissive flags
/// and texture-dimension overrides for the eyes and wind shells.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.breeze.BreezeModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/breeze/BreezeModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class BreezeModelDefinition
{
    public const int BreezeEntityTypeId = 17;

    /// <summary>Declarative DSL tree for the body and wind-shell parts (T-679).</summary>
    private static readonly VanillaMeshDefinition BreezeMesh = BuildBreezeMesh();

    internal static VanillaMeshDefinition Mesh => BreezeMesh;

    private static readonly CompiledEntityModel CompiledModel =
        VanillaMeshBakery.Build(BreezeMesh, 32, 32, texturePath: "entity/breeze/breeze");

    public static readonly EntityModelDefinition Definition = new(
        "breeze",
        32,
        32,
        [.. VanillaMeshBake.Bake(BreezeMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/breeze/breeze",
        1f,
        "entity/breeze/breeze_eyes",
        additionalTexturePaths: ["entity/breeze/breeze_wind", "entity/breeze/breeze_eyes"], compiled: CompiledModel);

    private static VanillaMeshDefinition BuildBreezeMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var body = root.AddOrReplaceChild("body", VanillaPartPose.Zero,
            new VanillaCubeDefinition("body", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        var rods = body.AddOrReplaceChild("rods", VanillaPartPose.Offset(0f, 8f, 0f),
            new VanillaCubeDefinition("rods", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        rods.AddOrReplaceChild("rod_1", VanillaPartPose.OffsetAndRotation(2.5981f, -3f, 1.5f, -2.7489f, -1.0472f, 3.1416f),
            new VanillaCubeDefinition("rod_1", 0, 17, -1f, 0f, -3f, 2f, 8f, 2f));
        rods.AddOrReplaceChild("rod_2", VanillaPartPose.OffsetAndRotation(-2.5981f, -3f, 1.5f, -2.7489f, 1.0472f, 3.1416f),
            new VanillaCubeDefinition("rod_2", 0, 17, -1f, 0f, -3f, 2f, 8f, 2f));
        rods.AddOrReplaceChild("rod_3", VanillaPartPose.OffsetAndRotation(0f, -3f, -3f, 0.3927f, 0f, 0f),
            new VanillaCubeDefinition("rod_3", 0, 17, -1f, 0f, -3f, 2f, 8f, 2f));
        var head = body.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 4f, 0f),
            new VanillaCubeDefinition("head", 4, 24, -5f, -5f, -4.2f, 10f, 3f, 4f),
            new VanillaCubeDefinition("head_cube", 0, 0, -4f, -8f, -4f, 8f, 8f, 8f));
        var eyes = head.AddOrReplaceChild("eyes", VanillaPartPose.Zero,
            new VanillaCubeDefinition("eyes", 4, 24, -5f, -5f, -4.2f, 10f, 3f, 4f, EmissiveOnly: true),
            new VanillaCubeDefinition("eyes_cube", 0, 0, -4f, -8f, -4f, 8f, 8f, 8f, EmissiveOnly: true));
        var windBody = root.AddOrReplaceChild("wind_body", VanillaPartPose.Zero,
            new VanillaCubeDefinition("wind_body", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        var windBottom = windBody.AddOrReplaceChild("wind_bottom", VanillaPartPose.Offset(0f, 24f, 0f),
            WindCube("wind_bottom", 1, 83, -2.5f, -7f, -2.5f, 5f, 7f, 5f));
        var windMid = windBottom.AddOrReplaceChild("wind_mid", VanillaPartPose.Offset(0f, -7f, 0f),
            WindCube("wind_mid", 74, 28, -6f, -6f, -6f, 12f, 6f, 12f),
            WindCube("wind_mid_mid", 78, 32, -4f, -6f, -4f, 8f, 6f, 8f),
            WindCube("wind_mid_inner", 49, 71, -2.5f, -6f, -2.5f, 5f, 6f, 5f));
        var windTop = windMid.AddOrReplaceChild("wind_top", VanillaPartPose.Offset(0f, -6f, 0f),
            WindCube("wind_top", 0, 0, -9f, -8f, -9f, 18f, 8f, 18f),
            WindCube("wind_top_mid", 6, 6, -6f, -8f, -6f, 12f, 8f, 12f),
            WindCube("wind_top_inner", 105, 57, -2.5f, -8f, -2.5f, 5f, 8f, 5f));
        return new VanillaMeshDefinition(root);
    }

    /// <summary>The translucent wind shell cuboids: wind texture, 128x128, excluded from the emissive pass.</summary>
    private static VanillaCubeDefinition WindCube(
        string name,
        int textureX,
        int textureY,
        float minX,
        float minY,
        float minZ,
        float width,
        float height,
        float depth) =>
        new(name, textureX, textureY, minX, minY, minZ, width, height, depth,
            Kind: EntityGeometryLayerKind.Translucent,
            TexturePath: "entity/breeze/breeze_wind",
            TextureWidth: 128,
            TextureHeight: 128,
            IncludeInEmissive: false);
}
