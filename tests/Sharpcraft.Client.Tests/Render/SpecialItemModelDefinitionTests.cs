// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

public sealed class SpecialItemModelDefinitionTests
{
    [Fact]
    public void TridentHasPoleBaseAndThreeSpikes()
    {
        var names = SpecialItemModelDefinition.Trident.Compiled!.Parts.Select(part => part.Name).ToArray();
        Assert.Contains("pole", names);
        Assert.Contains("base", names);
        Assert.Contains("left_spike", names);
        Assert.Contains("middle_spike", names);
        Assert.Contains("right_spike", names);
        Assert.Equal("entity/trident/trident", SpecialItemModelDefinition.Trident.TexturePath);
    }

    [Fact]
    public void SkullModelHasAnEightPixelHeadCube()
    {
        var part = SpecialItemModelDefinition.Skull.Compiled!.Find("head");
        Assert.NotNull(part);
        var cube = part!.Cubes.Single();
        Assert.Equal(8f, cube.Width);
        Assert.Equal(8f, cube.Height);
        Assert.Equal(8f, cube.Depth);
    }

    [Fact]
    public void ChestModelHasBottomLidAndLock()
    {
        var names = SpecialItemModelDefinition.Chest.Compiled!.Parts.Select(part => part.Name).ToArray();
        Assert.Contains("bottom", names);
        Assert.Contains("lid", names);
        Assert.Contains("lock", names);
    }

    [Fact]
    public void ShulkerBoxModelHasLidAndBase()
    {
        var names = SpecialItemModelDefinition.ShulkerBox.Compiled!.Parts.Select(part => part.Name).ToArray();
        Assert.Contains("lid", names);
        Assert.Contains("base", names);
    }

    [Fact]
    public void BannerHasPoleBarAndFlag()
    {
        var standing = SpecialItemModelDefinition.BannerStanding.Compiled!.Parts.Select(part => part.Name).ToArray();
        Assert.Contains("pole", standing);
        Assert.Contains("bar", standing);
        var wall = SpecialItemModelDefinition.BannerWall.Compiled!.Parts.Select(part => part.Name).ToArray();
        Assert.DoesNotContain("pole", wall);
        Assert.Contains("bar", wall);
        Assert.Contains("flag",
            SpecialItemModelDefinition.BannerFlagStanding.Compiled!.Parts.Select(part => part.Name));
    }

    [Fact]
    public void DecoratedPotMatchesTheReferenceCuboids()
    {
        var baseParts = SpecialItemModelDefinition.DecoratedPot.Compiled!.Parts.Select(part => part.Name).ToArray();
        Assert.Contains("neck", baseParts);
        Assert.Contains("top", baseParts);
        Assert.Contains("bottom", baseParts);
        var sides = SpecialItemModelDefinition.DecoratedPotSides.Compiled!.Parts.Select(part => part.Name).ToArray();
        Assert.Equal(4, sides.Length);
        Assert.Contains("front", sides);
        Assert.Contains("back", sides);
        Assert.Contains("left", sides);
        Assert.Contains("right", sides);
    }

    [Fact]
    public void ConduitAndEndCubeUseReferenceCubeSizes()
    {
        var shell = SpecialItemModelDefinition.Conduit.Compiled!.Find("shell")!.Cubes.Single();
        Assert.Equal(6f, shell.Width);
        Assert.Equal(6f, shell.Height);
        Assert.Equal(6f, shell.Depth);

        var cube = SpecialItemModelDefinition.EndCube.Compiled!.Find("cube")!.Cubes.Single();
        Assert.Equal(16f, cube.Width);
        Assert.Equal(16f, cube.Height);
        Assert.Equal(16f, cube.Depth);
    }

    [Fact]
    public void CubeLevelTextureAndTintSelectorsSurviveBaking()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("plane", VanillaPartPose.Zero,
            new VanillaCubeDefinition("plane", 0, 0, 0f, 0f, 0f, 16f, 16f, 1f,
                TextureSelector: static _ => "entity/shield/shield_base",
                TintSelector: static _ => new System.Numerics.Vector3(0.5f, 0.25f, 0.75f)));
        var definition = EntityModelDefinition.FromTree(
            "selector_probe", 16, 16, new VanillaMeshDefinition(root),
            PlayerModelDefinition.DefaultFaceTransforms, "entity/shield/base", 1f);

        var part = definition.Compiled!.Find("plane")!;
        Assert.Equal("entity/shield/shield_base", part.Cubes.Single().TextureSelector!(default!));
        Assert.Equal(new System.Numerics.Vector3(0.5f, 0.25f, 0.75f), part.TintSelector!(default!));
    }

    [Fact]
    public void HeldSpecialModelsCarryTheArmPivot()
    {
        Assert.NotNull(SpecialItemModelDefinition.HeldTridentRight.Compiled!.Find("right_arm"));
        Assert.NotNull(SpecialItemModelDefinition.HeldTridentLeft.Compiled!.Find("left_arm"));
        Assert.NotNull(SpecialItemModelDefinition.HeldShieldRight.Compiled!.Find("right_arm"));
        Assert.NotNull(SpecialItemModelDefinition.HeldShieldLeft.Compiled!.Find("left_arm"));
    }

    [Fact]
    public void ShieldHasPlateAndHandle()
    {
        var names = SpecialItemModelDefinition.Shield.Compiled!.Parts.Select(part => part.Name).ToArray();
        Assert.Contains("plate", names);
        Assert.Contains("handle", names);
        Assert.Equal("entity/shield/shield_base_nopattern", SpecialItemModelDefinition.Shield.TexturePath);
    }
}
