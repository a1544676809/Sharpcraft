// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Zombie Nautilus: the nautilus body with the zombie texture; the warm variant adds the coral shell growths.
/// Bind pose only; the coral layer is hidden in the reference when body armour is worn (armour layers are not ported).
/// Parts are declared with the <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake</c> (T-693).
/// </summary>
public static class ZombieNautilusModelDefinition
{
    public const int ZombieNautilusEntityTypeId = 156;

    /// <summary>Variant ids: TEMPERATE (normal model), WARM (coral model).</summary>
    public static readonly IReadOnlyList<string> VariantKeys = ["temperate", "warm"];

    private static readonly VanillaMeshDefinition CoralMesh = BuildCoralMesh();

    private static readonly EntityModelPart[] CoralParts = BuildCoralParts();

    // Reference ZombieNautilusCoralModel: the coral growths are hidden while body armour is worn.
    private static readonly EntityModelPart[] CoralPartsWhileUnarmored = HideWhileArmored();

    private static EntityModelPart[] BuildCoralParts()
    {
        var baked = VanillaMeshBake.Bake(CoralMesh, 0, 0);
        var result = new List<EntityModelPart>(baked.Count - 1);
        foreach (var part in baked)
        {
            if (part.Name != "shell")
            {
                result.Add(part);
            }
        }

        return [.. result];
    }

    private static EntityModelPart[] HideWhileArmored()
    {
        var result = new EntityModelPart[CoralParts.Length];
        for (var i = 0; i < CoralParts.Length; i++)
        {
            result[i] = CoralParts[i] with { VisibleWhen = static state => state.Equipment[6] <= 0 };
        }

        return result;
    }

    public static readonly EntityModelDefinition Definition = new(
        "zombie_nautilus",
        128,
        128,
        NautilusModelDefinition.Definition.Parts,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/nautilus/zombie_nautilus");

    public static readonly IReadOnlyDictionary<string, EntityModelDefinition> Variants =
        new Dictionary<string, EntityModelDefinition>(StringComparer.Ordinal)
        {
            ["temperate"] = Definition,
            ["warm"] = new(
                "zombie_nautilus_coral",
                128,
                128,
                [.. NautilusModelDefinition.Definition.Parts, .. CoralPartsWhileUnarmored],
                PlayerModelDefinition.DefaultFaceTransforms,
                "entity/nautilus/zombie_nautilus_coral")
        };

    private static VanillaMeshDefinition BuildCoralMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var shell = root.AddOrReplaceChild("shell", VanillaPartPose.Zero,
            new VanillaCubeDefinition("shell", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        var corals = shell.AddOrReplaceChild("corals", VanillaPartPose.Offset(8f, 4.5f, -8f),
            new VanillaCubeDefinition("corals", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        var yellow = corals.AddOrReplaceChild("yellow_coral", VanillaPartPose.Offset(0f, -11f, 11f),
            new VanillaCubeDefinition("yellow_coral", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        yellow.AddOrReplaceChild("yellow_coral_second", VanillaPartPose.OffsetAndRotation(0f, 0f, 2f, 0f, -0.7854f, 0f),
            new VanillaCubeDefinition("yellow_coral_second", 0, 85, -4.5f, -3.5f, 0f, 6f, 8f, 0f));
        yellow.AddOrReplaceChild("yellow_coral_first", VanillaPartPose.OffsetAndRotation(0f, 0f, 0f, 0f, 0.7854f, 0f),
            new VanillaCubeDefinition("yellow_coral_first", 0, 85, -4.5f, -3.5f, 0f, 6f, 8f, 0f));
        var pink = corals.AddOrReplaceChild("pink_coral", VanillaPartPose.Offset(-12.5f, -18f, 11f),
            new VanillaCubeDefinition("pink_coral", -8, 94, -4.5f, 4.5f, 0f, 6f, 0f, 8f));
        pink.AddOrReplaceChild("pink_coral_second", VanillaPartPose.OffsetAndRotation(-1.5f, 4.5f, 4f, 0f, 0f, 1.5708f),
            new VanillaCubeDefinition("pink_coral_second", -8, 94, -3f, 0f, -4f, 6f, 0f, 8f));
        var blue = corals.AddOrReplaceChild("blue_coral", VanillaPartPose.Offset(-14f, 0f, 5.5f),
            new VanillaCubeDefinition("blue_coral", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        blue.AddOrReplaceChild("blue_second", VanillaPartPose.OffsetAndRotation(0f, 0f, -2f, 0f, 0.7854f, 0f),
            new VanillaCubeDefinition("blue_second", 0, 102, -3.5f, -5.5f, 0f, 5f, 10f, 0f));
        blue.AddOrReplaceChild("blue_first", VanillaPartPose.OffsetAndRotation(0f, 0f, 0f, 0f, -0.7854f, 0f),
            new VanillaCubeDefinition("blue_first", 0, 102, -3.5f, -5.5f, 0f, 5f, 10f, 0f));
        var red = corals.AddOrReplaceChild("red_coral", VanillaPartPose.Zero,
            new VanillaCubeDefinition("red_coral", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        red.AddOrReplaceChild("red_coral_second", VanillaPartPose.OffsetAndRotation(-0.5f, -1f, 1.5f, 0f, -0.829f, 0f),
            new VanillaCubeDefinition("red_coral_second", 0, 112, -2.5f, -5.5f, 0f, 4f, 10f, 0f));
        red.AddOrReplaceChild("red_coral_first", VanillaPartPose.OffsetAndRotation(0f, 0f, 0f, 0f, 0.7854f, 0f),
            new VanillaCubeDefinition("red_coral_first", 0, 112, -4.5f, -5.5f, 0f, 6f, 10f, 0f));
        return new VanillaMeshDefinition(root);
    }
}
