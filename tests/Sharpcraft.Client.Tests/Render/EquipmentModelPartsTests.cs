// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Common.Registries;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// R-047: the legacy <c>EquipmentModelParts</c> flat box set and the two zero-caller
/// <c>EntityModelLayerHelpers.WithEquipment</c> legacy overloads were removed. The same extra equipment boxes now
/// live in the single compiled-tree source of truth <see cref="EquipmentModelTrees"/>. These structural invariants
/// assert each equipment tree carries its expected extra boxes; the strict jar source equivalence for these boxes is
/// covered by <see cref="MountEquipmentRawReferenceSampleTests"/> and <see cref="MountArmorRawReferenceSampleTests"/>
/// (keyed, 1e-5 + UV half through the compiled mesher).
/// </summary>
public sealed class EquipmentModelPartsTests
{
    [Fact]
    public void HorseSaddleTreeHasTheSaddleAndBridleBoxes()
    {
        var tree = EquipmentModelTrees.For(EquipmentAssetKind.HorseSaddle);
        Assert.NotNull(tree);
        AssertAllCubes(tree!, "saddle", "left_saddle_mouth", "right_saddle_mouth",
            "left_saddle_line", "right_saddle_line", "head_saddle", "mouth_saddle_wrap");
    }

    [Fact]
    public void CamelSaddleTreeHasTheSaddleAndReinBoxes()
    {
        var tree = EquipmentModelTrees.For(EquipmentAssetKind.CamelSaddle);
        Assert.NotNull(tree);
        AssertAllCubes(tree!, "saddle", "saddle_upper", "saddle_base", "reins", "reins_front", "reins_left",
            "bridle", "bridle_upper", "bridle_snout", "bridle_right", "bridle_left");
    }

    [Fact]
    public void NautilusArmorAndSaddleTreesHaveTheShellBoxes()
    {
        var armor = EquipmentModelTrees.For(EquipmentAssetKind.NautilusBody);
        Assert.NotNull(armor);
        AssertAllCubes(armor!, "shell", "shell_mid", "shell_back");

        var saddle = EquipmentModelTrees.For(EquipmentAssetKind.NautilusSaddle);
        Assert.NotNull(saddle);
        AssertCube(saddle!, "shell");
    }

    [Fact]
    public void HappyHarnessTreeHasTheHarnessAndGogglesBoxes()
    {
        var tree = EquipmentModelTrees.For(EquipmentAssetKind.HappyGhastBody);
        Assert.NotNull(tree);
        AssertAllCubes(tree!, "harness", "goggles");
    }

    private static void AssertAllCubes(CompiledEntityModel tree, params string[] names)
    {
        foreach (var name in names)
        {
            AssertCube(tree, name);
        }
    }

    private static void AssertCube(CompiledEntityModel tree, string name) =>
        Assert.Contains(tree.Cubes, cube => cube.Name == name);
}
