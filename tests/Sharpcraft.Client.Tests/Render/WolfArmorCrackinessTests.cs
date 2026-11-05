// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Linq;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>Wolf armor crackiness overlay (reference Crackiness.WOLF_ARMOR + WolfArmorLayer).</summary>
public sealed class WolfArmorCrackinessTests
{
    [Fact]
    public void CrackinessOverlayTracksTheDurabilityLevel()
    {
        var definition = EntityModelResolver.Resolve(WolfModelDefinition.WolfEntityTypeId);
        var part = definition.Parts.First(p => p.Name == "crackiness_body");
        Assert.NotNull(part.VisibleWhen);
        Assert.NotNull(part.TextureSelector);

        var state = new EntityRenderState(1, WolfModelDefinition.WolfEntityTypeId, 0f, 0f, 0f, 0f, 0f) { Crackiness = 1 };
        Assert.True(part.VisibleWhen!(state));
        Assert.Equal("entity/wolf/wolf_armor_crackiness_low", part.TextureSelector!(state));

        var medium = state with { Crackiness = 2 };
        Assert.Equal("entity/wolf/wolf_armor_crackiness_medium", part.TextureSelector!(medium));

        var none = state with { Crackiness = 0 };
        Assert.False(part.VisibleWhen!(none));
        Assert.Null(part.TextureSelector!(none));
    }
}
