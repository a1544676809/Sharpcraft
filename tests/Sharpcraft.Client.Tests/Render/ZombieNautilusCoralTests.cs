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
        var coral = definition.Parts.First(part => part.Name == "yellow_coral_second");
        Assert.NotNull(coral.VisibleWhen);

        var unarmored = new EntityRenderState(1, ZombieNautilusModelDefinition.ZombieNautilusEntityTypeId, 0f, 0f, 0f, 0f, 0f);
        Assert.True(coral.VisibleWhen!(unarmored));

        var armored = unarmored with { Equipment = new EntityEquipment(-1, -1, -1, -1, -1, -1, 5, -1) };
        Assert.False(coral.VisibleWhen!(armored));
    }
}
