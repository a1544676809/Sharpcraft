// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// AdultTurtleModel: root head/body shell/belly, four flat legs and the optional egg belly. Parts are declared with the
/// <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake</c> (T-693).
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.turtle.TurtleModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/turtle/TurtleModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class TurtleModelDefinition
{
    public const int TurtleEntityTypeId = 141;

    private static readonly VanillaMeshDefinition TurtleMesh = BuildTurtleMesh();

    public static readonly EntityModelDefinition Definition = new(
        "turtle",
        128,
        64,
        [.. VanillaMeshBake.Bake(TurtleMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/turtle/turtle");

    private static VanillaMeshDefinition BuildTurtleMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 19f, -10f),
            new VanillaCubeDefinition("head", 3, 0, -3f, -1f, -3f, 6f, 5f, 6f));
        root.AddOrReplaceChild("body", VanillaPartPose.OffsetAndRotation(0f, 11f, -10f, -MathF.PI / 2f, 0f, 0f),
            new VanillaCubeDefinition("body", 7, 37, -9.5f, 3f, -10f, 19f, 20f, 6f));
        root.AddOrReplaceChild("belly", VanillaPartPose.OffsetAndRotation(0f, 11f, -10f, -MathF.PI / 2f, 0f, 0f),
            new VanillaCubeDefinition("belly", 31, 1, -5.5f, 3f, -13f, 11f, 18f, 3f));
        root.AddOrReplaceChild("egg_belly", VanillaPartPose.OffsetAndRotation(0f, 11f, -10f, -MathF.PI / 2f, 0f, 0f),
            new VanillaCubeDefinition("egg_belly", 70, 33, -4.5f, 3f, -14f, 9f, 18f, 1f));
        root.AddOrReplaceChild("right_hind_leg", VanillaPartPose.Offset(-3.5f, 22f, 11f),
            new VanillaCubeDefinition("right_hind_leg", 1, 23, -2f, 0f, 0f, 4f, 1f, 10f));
        root.AddOrReplaceChild("left_hind_leg", VanillaPartPose.Offset(3.5f, 22f, 11f),
            new VanillaCubeDefinition("left_hind_leg", 1, 12, -2f, 0f, 0f, 4f, 1f, 10f));
        root.AddOrReplaceChild("right_front_leg", VanillaPartPose.Offset(-5f, 21f, -4f),
            new VanillaCubeDefinition("right_front_leg", 27, 30, -13f, 0f, -2f, 13f, 1f, 5f));
        root.AddOrReplaceChild("left_front_leg", VanillaPartPose.Offset(5f, 21f, -4f),
            new VanillaCubeDefinition("left_front_leg", 27, 24, 0f, 0f, -2f, 13f, 1f, 5f));
        return new VanillaMeshDefinition(root);
    }
}
