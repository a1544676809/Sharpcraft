// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// T-679: proves the DSL-baked breeze parts are value-identical to the legacy FromVanilla/FromVanillaChild records
/// (rod rotations, emissive-only eyes, translucent 128x128 wind shells), that the nested order and parent links are
/// preserved, that the definition's emissive/additional texture paths are unchanged, and that the new per-cube
/// overrides take precedence over mesh-level Bake dimensions.
/// </summary>
public sealed class BreezeModelDefinitionTests
{
    [Fact]
    public void BreezePartsMatchLegacyFromVanillaRecords()
    {
        AssertPartsMatch(ExpectedBreezeParts(), BreezeModelDefinition.Definition.Parts);
    }

    [Fact]
    public void BreezeKeepsChildOrderAndParentLinks()
    {
        AssertNamesAndParents(BreezeModelDefinition.Definition.Parts,
            ["body", "rods", "rod_1", "rod_2", "rod_3", "head", "head_cube", "eyes", "eyes_cube", "wind_body", "wind_bottom", "wind_mid", "wind_mid_mid", "wind_mid_inner", "wind_top", "wind_top_mid", "wind_top_inner"],
            [null, "body", "rods", "rods", "rods", "body", "head", "head", "eyes", null, "wind_body", "wind_bottom", "wind_mid", "wind_mid", "wind_mid", "wind_top", "wind_top"]);
    }

    [Fact]
    public void BreezeKeepsEmissiveAndAdditionalTexturePaths()
    {
        var definition = BreezeModelDefinition.Definition;
        Assert.Equal("breeze", definition.Name);
        Assert.Equal(32, definition.TextureWidth);
        Assert.Equal(32, definition.TextureHeight);
        Assert.Equal("entity/breeze/breeze", definition.TexturePath);
        Assert.Equal(1f, definition.Scale);
        Assert.Equal("entity/breeze/breeze_eyes", definition.EmissiveTexturePath);

        var paths = definition.AdditionalTexturePaths;
        Assert.Equal(2, paths.Count);
        Assert.Equal("entity/breeze/breeze_wind", paths[0]);
        Assert.Equal("entity/breeze/breeze_eyes", paths[1]);

        var parts = definition.Parts;
        foreach (var part in parts)
        {
            if (part.Name.StartsWith("wind_", StringComparison.Ordinal) && part.Name != "wind_body")
            {
                Assert.Equal(EntityGeometryLayerKind.Translucent, part.Kind);
                Assert.Equal("entity/breeze/breeze_wind", part.TexturePath);
                Assert.Equal(128, part.TextureWidth);
                Assert.Equal(128, part.TextureHeight);
                Assert.False(part.IncludeInEmissive);
                Assert.False(part.EmissiveOnly);
            }
        }

        var windBody = Assert.Single(parts, part => part.Name == "wind_body");
        Assert.Equal(EntityGeometryLayerKind.Cutout, windBody.Kind);
        Assert.Null(windBody.TexturePath);
        Assert.Equal(0, windBody.TextureWidth);
        Assert.Equal(0, windBody.TextureHeight);
        Assert.True(windBody.IncludeInEmissive);

        Assert.True(Assert.Single(parts, part => part.Name == "eyes").EmissiveOnly);
        Assert.True(Assert.Single(parts, part => part.Name == "eyes_cube").EmissiveOnly);
    }

    [Fact]
    public void PerCubeFieldsOverrideMeshLevelDefaults()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("plain", VanillaPartPose.Zero,
            new VanillaCubeDefinition("plain", 0, 0, 0f, 0f, 0f, 1f, 1f, 1f));
        root.AddOrReplaceChild("custom", VanillaPartPose.Zero,
            new VanillaCubeDefinition("custom", 0, 0, 0f, 0f, 0f, 1f, 1f, 1f,
                IncludeInEmissive: false, EmissiveOnly: true, TextureWidth: 16, TextureHeight: 8));

        var parts = VanillaMeshBake.Bake(new VanillaMeshDefinition(root), 64, 32);

        // Mesh-level dimensions still apply to a cube that does not override them.
        Assert.Equal(64, parts[0].TextureWidth);
        Assert.Equal(32, parts[0].TextureHeight);
        Assert.True(parts[0].IncludeInEmissive);
        Assert.False(parts[0].EmissiveOnly);

        // Per-cube values win over the mesh-level Bake dimensions and the bake defaults.
        Assert.Equal(16, parts[1].TextureWidth);
        Assert.Equal(8, parts[1].TextureHeight);
        Assert.False(parts[1].IncludeInEmissive);
        Assert.True(parts[1].EmissiveOnly);
    }

    private static EntityModelPart[] ExpectedBreezeParts() =>
    [
        LegacyModelPartFactory.FromVanilla("body", 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
        LegacyModelPartFactory.FromVanillaChild("rods", "body", 0f, 8f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
        LegacyModelPartFactory.FromVanillaChild("rod_1", "rods", 2.5981f, -3f, 1.5f, -1f, 0f, -3f, 2f, 8f, 2f, 0, 17,
            rotationX: -2.7489f, rotationY: -1.0472f, rotationZ: 3.1416f),
        LegacyModelPartFactory.FromVanillaChild("rod_2", "rods", -2.5981f, -3f, 1.5f, -1f, 0f, -3f, 2f, 8f, 2f, 0, 17,
            rotationX: -2.7489f, rotationY: 1.0472f, rotationZ: 3.1416f),
        LegacyModelPartFactory.FromVanillaChild("rod_3", "rods", 0f, -3f, -3f, -1f, 0f, -3f, 2f, 8f, 2f, 0, 17,
            rotationX: 0.3927f),
        LegacyModelPartFactory.FromVanillaChild("head", "body", 0f, 4f, 0f, -5f, -5f, -4.2f, 10f, 3f, 4f, 4, 24),
        LegacyModelPartFactory.FromVanillaChild("head_cube", "head", 0f, 0f, 0f, -4f, -8f, -4f, 8f, 8f, 8f, 0, 0),
        LegacyModelPartFactory.FromVanillaChild("eyes", "head", 0f, 0f, 0f, -5f, -5f, -4.2f, 10f, 3f, 4f, 4, 24) with { EmissiveOnly = true },
        LegacyModelPartFactory.FromVanillaChild("eyes_cube", "eyes", 0f, 0f, 0f, -4f, -8f, -4f, 8f, 8f, 8f, 0, 0) with { EmissiveOnly = true },
        LegacyModelPartFactory.FromVanilla("wind_body", 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
        WindPart("wind_bottom", "wind_body", 0f, 24f, 0f, -2.5f, -7f, -2.5f, 5f, 7f, 5f, 1, 83),
        WindPart("wind_mid", "wind_bottom", 0f, -7f, 0f, -6f, -6f, -6f, 12f, 6f, 12f, 74, 28),
        WindPart("wind_mid_mid", "wind_mid", 0f, 0f, 0f, -4f, -6f, -4f, 8f, 6f, 8f, 78, 32),
        WindPart("wind_mid_inner", "wind_mid", 0f, 0f, 0f, -2.5f, -6f, -2.5f, 5f, 6f, 5f, 49, 71),
        WindPart("wind_top", "wind_mid", 0f, -6f, 0f, -9f, -8f, -9f, 18f, 8f, 18f, 0, 0),
        WindPart("wind_top_mid", "wind_top", 0f, 0f, 0f, -6f, -8f, -6f, 12f, 8f, 12f, 6, 6),
        WindPart("wind_top_inner", "wind_top", 0f, 0f, 0f, -2.5f, -8f, -2.5f, 5f, 8f, 5f, 105, 57)
    ];

    /// <summary>Mirror of the legacy WindPart factory (same values) for the expected records.</summary>
    private static EntityModelPart WindPart(
        string name,
        string parent,
        float pivotX,
        float pivotY,
        float pivotZ,
        float minX,
        float minY,
        float minZ,
        float width,
        float height,
        float depth,
        int textureX,
        int textureY) =>
        LegacyModelPartFactory.FromVanillaChild(
            name, parent, pivotX, pivotY, pivotZ, minX, minY, minZ, width, height, depth, textureX, textureY) with
        {
            Kind = EntityGeometryLayerKind.Translucent,
            TexturePath = "entity/breeze/breeze_wind",
            TextureWidth = 128,
            TextureHeight = 128,
            IncludeInEmissive = false
        };

    private static void AssertNamesAndParents(IReadOnlyList<EntityModelPart> parts, string[] names, string?[] parents)
    {
        Assert.Equal(names.Length, parts.Count);
        Assert.Equal(parents.Length, parts.Count);
        for (var i = 0; i < names.Length; i++)
        {
            Assert.Equal(names[i], parts[i].Name);
            Assert.Equal(parents[i], parts[i].Parent);
        }
    }

    /// <summary>Value-wise array comparison via the shared oracle (record equality is not valid for overlay lists).</summary>
    private static void AssertPartsMatch(EntityModelPart[] expected, IReadOnlyList<EntityModelPart> actual)
    {
        Assert.Equal(expected.Length, actual.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            ModelPartAssertions.AssertEqual(expected[i], actual[i]);
        }
    }
}
