// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Linq;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>Reference ZombieNautilusCoralModel: coral growths hide while body armour is worn.</summary>
public sealed class ZombieNautilusCoralTests
{
    [Fact]
    public void CoralHidesWhenBodyArmorIsEquipped()
    {
        var definition = EntityModelResolver.ResolveVariantStrict(ZombieNautilusModelDefinition.ZombieNautilusEntityTypeId, "warm");
        // #231: the coral visibility predicate lives on the compiled "corals" group node, whose children
        // (yellow_coral_second, etc.) inherit it — previously the flat bake copied it onto each part.
        var coralGroup = definition.Compiled!.Parts.First(node => node.Name == "corals");
        Assert.NotNull(coralGroup.VisibleWhen);

        var unarmored = new EntityRenderState(1, ZombieNautilusModelDefinition.ZombieNautilusEntityTypeId, 0f, 0f, 0f, 0f, 0f);
        Assert.True(coralGroup.VisibleWhen!(unarmored));

        var armored = unarmored with { Equipment = new EntityEquipment(-1, -1, -1, -1, -1, -1, 5, -1) };
        Assert.False(coralGroup.VisibleWhen!(armored));
    }
}
