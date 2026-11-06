// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// BlazeModel: one head cube and twelve root rods arranged in three rings. Parts are declared with the
/// <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake.Bake</c> (T-681); the ring formulas and
/// <c>part0</c>..<c>part11</c> names are preserved.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.blaze.BlazeModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/blaze/BlazeModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class BlazeModelDefinition
{
    public const int BlazeEntityTypeId = 14;

    /// <summary>Declarative DSL tree for the head and twelve computed rods (T-681).</summary>
    private static readonly VanillaMeshDefinition BlazeMesh = BuildBlazeMesh();

    public static readonly EntityModelDefinition Definition = new(
        "blaze",
        64,
        32,
        [.. VanillaMeshBake.Bake(BlazeMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/blaze/blaze");

    private static VanillaMeshDefinition BuildBlazeMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("head", VanillaPartPose.Zero,
            new VanillaCubeDefinition("head", 0, 0, -4f, -4f, -4f, 8f, 8f, 8f));

        var angle = 0f;
        for (var i = 0; i < 4; i++)
        {
            AddRod(root, i, MathF.Cos(angle) * 9f, -2f + MathF.Cos(i * 2 * 0.25f), MathF.Sin(angle) * 9f);
            angle += MathF.PI / 2f;
        }

        angle = MathF.PI / 4f;
        for (var i = 4; i < 8; i++)
        {
            AddRod(root, i, MathF.Cos(angle) * 7f, 2f + MathF.Cos(i * 2 * 0.25f), MathF.Sin(angle) * 7f);
            angle += MathF.PI / 2f;
        }

        angle = 0.47123894f;
        for (var i = 8; i < 12; i++)
        {
            AddRod(root, i, MathF.Cos(angle) * 5f, 11f + MathF.Cos(i * 1.5f * 0.5f), MathF.Sin(angle) * 5f);
            angle += MathF.PI / 2f;
        }

        return new VanillaMeshDefinition(root);
    }

    private static void AddRod(VanillaPartDefinition root, int index, float x, float y, float z) =>
        root.AddOrReplaceChild($"part{index}", VanillaPartPose.Offset(x, y, z),
            new VanillaCubeDefinition($"part{index}", 0, 16, 0f, 0f, 0f, 2f, 8f, 2f));
}
