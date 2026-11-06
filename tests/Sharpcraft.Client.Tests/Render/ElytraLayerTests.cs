// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Linq;
using Sharpcraft.Client.Render;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Registries;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>Elytra wings: equipment texture resolution and the two-wing model layer.</summary>
public sealed class ElytraLayerTests
{
    [Fact]
    public void ElytraResolvesToTheWingsTexture()
    {
        var texture = EquipmentTextureCatalog.Resolve(EquipmentAssetKind.Elytra, new ItemInfo(Identifier.Parse("minecraft:elytra")));
        Assert.NotNull(texture);
        Assert.Equal("entity/equipment/wings/elytra", texture!.PrimaryPath);
    }

    [Fact]
    public void HumanoidModelCarriesTheTwoWings()
    {
        var names = EntityModelResolver.Resolve(154).Parts.Select(part => part.Name).ToHashSet();
        Assert.Contains("equipment_Elytra_4_left_wing", names);
        Assert.Contains("equipment_Elytra_4_right_wing", names);
    }

    [Fact]
    public void BabyHumanoidCarriesHalfSizedWings()
    {
        // Reference ELYTRA_BABY = ElytraModel.createLayer().apply(MeshTransformer.scaling(0.5F)). The transformer scales
        // every pose by 0.5 and then translates y by 24.016 * (1 - 0.5) = 12.008 in the y-down model frame: the wing
        // box becomes y 12.008..22.008 and the pivot y 12.008, i.e. bottom ≈ 2 and pivotY ≈ 12 in our y-up frame.
        var parts = EntityModelResolver.Resolve(154, baby: true).Parts;
        var body = parts.Single(part => part.Name == "body");
        var left = parts.Single(part => part.Name == "equipment_Elytra_4_left_wing");
        Assert.Equal(5f, left.Width);
        Assert.Equal(10f, left.Height);
        Assert.Equal(1f, left.Depth);
        Assert.Equal(2f, left.Bottom);
        Assert.Equal(12f, left.PivotY);
        Assert.Equal(0f, left.CentreX);
        Assert.Equal(body.CentreZ + 2.5f, left.CentreZ);
        Assert.Equal(-2.5f, left.PivotX);
        Assert.Equal(body.CentreZ + 2f, left.PivotZ);
        Assert.Equal(0.5f, left.Grow);
        // UV sampling uses the un-scaled CubeDefinition box: MeshTransformer.scaling only scales the root pose.
        Assert.Equal(10, left.UvWidth);
        Assert.Equal(20, left.UvHeight);
        Assert.Equal(2, left.UvDepth);
    }

    [Fact]
    public void WingsHangFromTheBodyInModelSpace()
    {
        var parts = EntityModelResolver.Resolve(154).Parts;
        var body = parts.Single(part => part.Name == "body");
        var left = parts.Single(part => part.Name == "equipment_Elytra_4_left_wing");
        Assert.Equal(body.Bottom + body.Height - 20f, left.Bottom);
        Assert.Equal(body.Bottom + body.Height, left.PivotY);
        Assert.Equal(body.CentreZ + 2f, left.PivotZ);
        Assert.Equal(body.CentreZ + 3f, left.CentreZ);
        Assert.Equal(64, left.TextureWidth);
        Assert.Equal(32, left.TextureHeight);
        Assert.Equal("entity/equipment/wings/elytra", left.TexturePath);
    }
}
