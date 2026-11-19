// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Common.Registries;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// R-047: the legacy <c>EquipmentModelParts</c> flat box set and the two zero-caller
/// <c>EntityModelLayerHelpers.WithEquipment</c> legacy overloads were removed. The same extra equipment boxes now
/// live in the single compiled-tree source of truth <see cref="EquipmentModelTrees"/>. These tests preserve the old
/// field-level coverage (Parent/hierarchy, pivot, rotation, cube origin/size, UV, Grow, mirror) by asserting each
/// compiled node/cube against values derived from the source DSL literals (the min/size/UV/grow the removed flat tests
/// used as input), with the DSL bake's node-pivot fold applied. Expected values are source-derived, never copied from
/// production output. Strict jar source equivalence stays in
/// <see cref="MountEquipmentRawReferenceSampleTests"/> and <see cref="MountArmorRawReferenceSampleTests"/>.
/// </summary>
public sealed class EquipmentModelPartsTests
{
    [Fact]
    public void HorseSaddleTreeBoxesMatchSourceDsl()
    {
        var tree = EquipmentModelTrees.For(EquipmentAssetKind.HorseSaddle)!;
        AssertBox(tree, "saddle", "body", pivot: (0, 0, 0), rot: (0, 0, 0), centre: (0, -8, -4.5f), size: (10, 9, 9), uv: (26, 0), grow: 0.5f);
        AssertBox(tree, "left_saddle_mouth", "head_parts", pivot: (0, 0, 0), rot: (0, 0, 0), centre: (2.5f, -9, -5), size: (1, 2, 2), uv: (29, 5), grow: 0f);
        AssertBox(tree, "right_saddle_mouth", "head_parts", pivot: (0, 0, 0), rot: (0, 0, 0), centre: (-2.5f, -9, -5), size: (1, 2, 2), uv: (29, 5), grow: 0f);
        AssertBox(tree, "left_saddle_line", "head_parts", pivot: (0, 0, 0), rot: (-MathF.PI / 6f, 0, 0), centre: (3.1f, -6, 0), size: (0, 3, 16), uv: (32, 2), grow: 0f);
        AssertBox(tree, "right_saddle_line", "head_parts", pivot: (0, 0, 0), rot: (-MathF.PI / 6f, 0, 0), centre: (-3.1f, -6, 0), size: (0, 3, 16), uv: (32, 2), grow: 0f);
        AssertBox(tree, "head_saddle", "head_parts", pivot: (0, 0, 0), rot: (0, 0, 0), centre: (0, -11, 1.1f), size: (6, 5, 6), uv: (1, 1), grow: 0.22f);
        AssertBox(tree, "mouth_saddle_wrap", "head_parts", pivot: (0, 0, 0), rot: (0, 0, 0), centre: (0, -11, -3), size: (4, 5, 2), uv: (19, 0), grow: 0.2f);
    }

    [Fact]
    public void CamelSaddleTreeBoxesMatchSourceDsl()
    {
        var tree = EquipmentModelTrees.For(EquipmentAssetKind.CamelSaddle)!;
        AssertBox(tree, "saddle", "body", pivot: (0, 0, 0), rot: (0, 0, 0), centre: (0, -17, -10), size: (9, 5, 11), uv: (74, 64), grow: 0.05f);
        AssertBox(tree, "saddle_upper", "body", pivot: (0, 0, 0), rot: (0, 0, 0), centre: (0, -20, -10), size: (7, 3, 11), uv: (92, 114), grow: 0.05f);
        AssertBox(tree, "saddle_base", "body", pivot: (0, 0, 0), rot: (0, 0, 0), centre: (0, -12, -10), size: (15, 12, 27), uv: (0, 89), grow: 0.05f);
        AssertBox(tree, "reins", "head", pivot: (0, 0, 0), rot: (0, 0, 0), centre: (3.51f, -18, -9.5f), size: (0, 7, 15), uv: (98, 42), grow: 0f);
        AssertBox(tree, "reins_front", "head", pivot: (0, 0, 0), rot: (0, 0, 0), centre: (0, -18, -2), size: (7, 7, 0), uv: (84, 57), grow: 0f);
        AssertBox(tree, "reins_left", "head", pivot: (0, 0, 0), rot: (0, 0, 0), centre: (-3.51f, -18, -9.5f), size: (0, 7, 15), uv: (98, 42), grow: 0f);
        AssertBox(tree, "bridle", "head", pivot: (0, 0, 0), rot: (0, 0, 0), centre: (0, -7, -5.5f), size: (7, 8, 19), uv: (60, 87), grow: 0.05f);
        AssertBox(tree, "bridle_upper", "head", pivot: (0, 0, 0), rot: (0, 0, 0), centre: (0, -21, -11.5f), size: (7, 14, 7), uv: (21, 64), grow: 0.05f);
        AssertBox(tree, "bridle_snout", "head", pivot: (0, 0, 0), rot: (0, 0, 0), centre: (0, -21, -18), size: (5, 5, 6), uv: (50, 64), grow: 0.05f);
        AssertBox(tree, "bridle_right", "head", pivot: (0, 0, 0), rot: (0, 0, 0), centre: (3, -19, -17), size: (1, 2, 2), uv: (74, 70), grow: 0f, mirror: false);
        AssertBox(tree, "bridle_left", "head", pivot: (0, 0, 0), rot: (0, 0, 0), centre: (-3, -19, -17), size: (1, 2, 2), uv: (74, 70), grow: 0f, mirror: true);
    }

    [Fact]
    public void NautilusArmorAndSaddleTreeBoxesMatchSourceDsl()
    {
        var armor = EquipmentModelTrees.For(EquipmentAssetKind.NautilusBody)!;
        AssertBox(armor, "shell", "root", pivot: (0, 13, 5), rot: (0, 0, 0), centre: (0, 0, 1), size: (14, 10, 16), uv: (0, 0), grow: 0.01f);
        AssertBox(armor, "shell_mid", "root", pivot: (0, 13, 5), rot: (0, 0, 0), centre: (0, -8, 3), size: (14, 8, 20), uv: (0, 26), grow: 0.01f);
        AssertBox(armor, "shell_back", "root", pivot: (0, 13, 5), rot: (0, 0, 0), centre: (0, -8, 6), size: (14, 8, 0), uv: (48, 26), grow: 0.01f);

        var saddle = EquipmentModelTrees.For(EquipmentAssetKind.NautilusSaddle)!;
        AssertBox(saddle, "shell", "root", pivot: (0, -13, 5), rot: (0, 0, 0), centre: (0, -10, 1), size: (14, 10, 16), uv: (0, 0), grow: 0.2f);
    }

    [Fact]
    public void HappyHarnessTreeBoxesMatchSourceDsl()
    {
        var tree = EquipmentModelTrees.For(EquipmentAssetKind.HappyGhastBody)!;
        AssertBox(tree, "harness", parent: null, pivot: (0, 24, 0), rot: (0, 0, 0), centre: (0, 8, 0), size: (16, 16, 16), uv: (0, 0), grow: 0f);
        AssertBox(tree, "goggles", parent: null, pivot: (0, 14, -5.5f), rot: (0, 0, 0), centre: (0, 11.5f, -5.5f), size: (16, 5, 5), uv: (0, 32), grow: 0.15f);
    }

    private static void AssertBox(CompiledEntityModel tree, string name, string? parent,
        (float X, float Y, float Z) pivot, (float X, float Y, float Z) rot,
        (float X, float Y, float Z) centre, (float W, float H, float D) size, (int U, int V) uv,
        float grow, bool mirror = false)
    {
        var cube = tree.Cubes.Single(c => c.Name == name);
        // owning node: the node whose CubeArray contains this cube (record-struct equality, plus name to disambiguate duplicates)
        var node = tree.Parts.Single(n => n.CubeArray.Any(c => c.Name == name && c.Equals(cube)));
        Assert.Equal(parent, node.Parent);
        Assert.Equal(pivot.X, node.PivotX, 4);
        Assert.Equal(pivot.Y, node.PivotY, 4);
        Assert.Equal(pivot.Z, node.PivotZ, 4);
        Assert.Equal(rot.X, node.RotationX, 5);
        Assert.Equal(rot.Y, node.RotationY, 5);
        Assert.Equal(rot.Z, node.RotationZ, 5);

        Assert.Equal(name, cube.Name);
        Assert.Equal(centre.X, cube.CentreX, 4);
        Assert.Equal(centre.Y, cube.Bottom, 4);
        Assert.Equal(centre.Z, cube.CentreZ, 4);
        Assert.Equal(size.W, cube.Width);
        Assert.Equal(size.H, cube.Height);
        Assert.Equal(size.D, cube.Depth);
        Assert.Equal(uv.U, cube.TextureX);
        Assert.Equal(uv.V, cube.TextureY);
        Assert.Equal(grow, cube.Grow, 4);
        Assert.Equal(mirror, cube.Mirror);
    }
}
