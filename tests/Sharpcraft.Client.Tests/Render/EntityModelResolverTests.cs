// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

public sealed class EntityModelResolverTests
{
    [Fact]
    public void ThePlayerTypeResolvesToThePlayerDefinition()
    {
        var definition = EntityModelResolver.Resolve(Sharpcraft.Client.Render.EntityPlaceholderMesher.PlayerEntityTypeId);

        Assert.NotNull(definition);
        Assert.Equal("player", definition.Name);
        Assert.Equal(6, definition.Parts.Count);
    }

    [Fact]
    public void TheZombieTypeResolvesToTheHumanoidZombieDefinition()
    {
        var definition = EntityModelResolver.Resolve(HumanoidMobModelDefinition.ZombieEntityTypeId);

        Assert.NotNull(definition);
        Assert.Equal("zombie", definition.Name);
        Assert.Equal(6, definition.Parts.Count);
    }

    [Fact]
    public void AnUnregisteredTypeHasNoDefinition()
    {
        Assert.Null(EntityModelResolver.Resolve(123456));
    }
}
