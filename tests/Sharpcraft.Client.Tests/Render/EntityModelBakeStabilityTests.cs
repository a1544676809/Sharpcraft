// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Security.Cryptography;
using System.Text;
using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Byte-level stability gate for the default Scale=1 model bake over every registered model definition:
/// the flat <see cref="VanillaMeshBake.Bake"/> parts and the structural <see cref="VanillaMeshBakery.Build"/>
/// tree. This is a change-detector for bake output, NOT equivalence evidence. Raw source-frame models are
/// verified point-by-point against ModelProbe/vanilla-model-facts source samples in their per-family tests;
/// this gate only records the current baked values so an unintended bake change is caught. Hashes are
/// refreshed whenever a model family is deliberately migrated to a different frame.
/// </summary>
public sealed class EntityModelBakeStabilityTests
{
    [Fact]
    public void ScaleOneRegistryBakeAndTreeStayByteIdentical()
    {
        var definitions = EntityModelResolver.Definitions
            .OrderBy(definition => definition.Name, StringComparer.Ordinal)
            .ThenBy(definition => definition.TexturePath, StringComparer.Ordinal)
            .ThenBy(definition => definition.Parts.Count)
            .ToArray();

        // At least the 111 common definitions requested by the review; the registry currently has 294
        // distinct definitions (base, adult variant and baby variant entries).
        Assert.True(definitions.Length >= 111, $"expected at least 111 definitions, found {definitions.Length}");

        var flat = new StringBuilder();
        var tree = new StringBuilder();
        foreach (var definition in definitions)
        {
            flat.Append("D|").Append(definition.Name).Append('|').Append(definition.TexturePath).Append('|')
                .Append(definition.TextureWidth).Append('|').Append(definition.TextureHeight).Append('|')
                .Append(definition.Parts.Count).Append('\n');
            foreach (var part in definition.Parts)
            {
                AppendPart(flat, part);
            }

            if (definition.Compiled is { } compiled)
            {
                AppendNode(tree, compiled.Root, 0);
            }
        }

        // Change-detector only, not equivalence evidence; per-family source samples remain the
        // equivalence gate. Refreshed for the humanoid hat raw migration on the current baseline.
        // Refreshed for the humanoid hat raw migration on the b37cfdc0 baseline.
        // Refreshed for the 5-family special raw migration (task-109).
        // Refreshed for the baby quadruped raw migrations (task-158) on the feec5214 baseline.
        // Refreshed for the EnderDragon raw migration (task-160) on the ebc2646c baseline.
        // Refreshed for the SnowGolem/Strider/Sniffer (task-165) + Vex/Nautilus/ZombieNautilus (task-166) raw migrations.
        // Refreshed for the SnowGolem/Strider/Sniffer (task-165) + Vex/Nautilus/ZombieNautilus (task-166) raw migrations.
        // Refreshed for the ZombieVillager raw migration (task-173).
        // Refreshed for the Bogged raw mushroom geometry (task-178).
        Assert.Equal("6e196e78cf7c0b8a18e3a795b4383fbe67301583750bc606d72672e544ef3177", Hash(flat));
        Assert.Equal("5b5e8b1df3437f298c7e2ebfee9956a719e0be51e87dfe04bc1814292383c9b7", Hash(tree));
    }

    private static void AppendPart(StringBuilder builder, EntityModelPart part)
    {
        builder.Append("P|").Append(part.Name).Append('|').Append(part.Parent).Append('|')
            .Append(Bits(part.CentreX)).Append('|').Append(Bits(part.Bottom)).Append('|')
            .Append(Bits(part.Width)).Append('|').Append(Bits(part.Height)).Append('|')
            .Append(Bits(part.Depth)).Append('|').Append(Bits(part.CentreZ)).Append('|')
            .Append(Bits(part.PivotX)).Append('|').Append(Bits(part.PivotY)).Append('|')
            .Append(Bits(part.PivotZ)).Append('|').Append(Bits(part.RotationX)).Append('|')
            .Append(Bits(part.RotationY)).Append('|').Append(Bits(part.RotationZ)).Append('|')
            .Append(part.TextureX).Append('|').Append(part.TextureY).Append('|')
            .Append(part.Mirror).Append('|').Append(part.TexturePath).Append('|')
            .Append(Bits(part.Grow)).Append('|').Append(part.Kind).Append('|')
            .Append(part.TextureWidth).Append('|').Append(part.TextureHeight).Append('|')
            .Append(part.IncludeInEmissive).Append('|').Append(part.EmissiveOnly).Append('|')
            .Append(part.EmissiveTexturePath).Append('|').Append(part.PoseSource).Append('|')
            .Append(part.UvWidth).Append('|').Append(part.UvHeight).Append('|')
            .Append(part.UvDepth).Append('|').Append(part.UsesItemAtlas).Append('\n');

        if (part.OverlayLayers is null)
        {
            return;
        }

        foreach (var layer in part.OverlayLayers)
        {
            builder.Append("O|").Append(layer.Name).Append('|').Append(Bits(layer.Grow)).Append('|')
                .Append(layer.TextureX).Append('|').Append(layer.TextureY).Append('|')
                .Append(layer.Mirror).Append('|').Append(layer.TexturePath).Append('|')
                .Append(layer.Kind).Append('\n');
        }
    }

    private static void AppendNode(StringBuilder builder, EntityModelPartNode node, int depth)
    {
        builder.Append("N|").Append(depth).Append('|').Append(node.Name).Append('|').Append(node.Parent).Append('|')
            .Append(Bits(node.PivotX)).Append('|').Append(Bits(node.PivotY)).Append('|').Append(Bits(node.PivotZ)).Append('|')
            .Append(Bits(node.RotationX)).Append('|').Append(Bits(node.RotationY)).Append('|').Append(Bits(node.RotationZ)).Append('|')
            .Append(node.PoseSource).Append('\n');

        foreach (var cube in node.Cubes)
        {
            builder.Append("C|").Append(cube.Name).Append('|').Append(Bits(cube.CentreX)).Append('|')
                .Append(Bits(cube.Bottom)).Append('|').Append(Bits(cube.CentreZ)).Append('|')
                .Append(Bits(cube.Width)).Append('|').Append(Bits(cube.Height)).Append('|')
                .Append(Bits(cube.Depth)).Append('|').Append(cube.TextureX).Append('|')
                .Append(cube.TextureY).Append('|').Append(cube.Mirror).Append('|').Append(cube.TexturePath).Append('|')
                .Append(Bits(cube.Grow)).Append('|').Append(cube.Kind).Append('|')
                .Append(cube.UvWidth).Append('|').Append(cube.UvHeight).Append('|')
                .Append(cube.UvDepth).Append('|').Append(cube.UsesItemAtlas).Append('\n');
        }

        foreach (var child in node.Children)
        {
            AppendNode(builder, child, depth + 1);
        }
    }

    private static int Bits(float value) => BitConverter.SingleToInt32Bits(value);

    private static string Hash(StringBuilder builder) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
}
