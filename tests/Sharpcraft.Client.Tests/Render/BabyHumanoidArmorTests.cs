// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Linq;
using Sharpcraft.Client.Render;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Registries;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>Baby humanoid armor: dedicated baby mesh, non-uniform deformation, humanoid_baby texture layer.</summary>
public sealed class BabyHumanoidArmorTests
{
    [Fact]
    public void BabyHumanoidUsesTheDedicatedArmorMeshAndBabyTexture()
    {
        var definition = EntityModelResolver.Resolve(154, true);
        var names = definition.Parts.Select(part => part.Name).ToHashSet();

        Assert.Contains("equipment_HumanoidArmorBaby_5_head", names);
        Assert.Contains("equipment_HumanoidArmorBaby_4_body", names);
        Assert.Contains("equipment_HumanoidArmorBaby_3_left_leg", names);
        Assert.Contains("equipment_HumanoidArmorBaby_2_left_leg", names);
        // The head slot must not carry body/legs.
        Assert.DoesNotContain("equipment_HumanoidArmorBaby_5_body", names);

        var leg = definition.Parts.First(part => part.Name == "equipment_HumanoidArmorBaby_3_left_leg");
        Assert.NotNull(leg.GrowXyz);
    }

    [Fact]
    public void BabyArmorResolvesToTheHumanoidBabyLayer()
    {
        var texture = EquipmentTextureCatalog.Resolve(EquipmentAssetKind.HumanoidArmorBaby, new ItemInfo(Identifier.Parse("minecraft:iron_helmet")));
        Assert.NotNull(texture);
        Assert.Equal("entity/equipment/humanoid_baby/iron", texture!.PrimaryPath);
    }
}
