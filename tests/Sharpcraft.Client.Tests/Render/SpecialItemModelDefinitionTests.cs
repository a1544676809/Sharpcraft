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
