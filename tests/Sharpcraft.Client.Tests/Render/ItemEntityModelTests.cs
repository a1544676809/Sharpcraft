// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// T-763: item entities must not produce entity-model geometry.
/// </summary>
/// <remarks>
/// The reference routes item entities to <c>ItemEntityRenderer</c>, which submits the shared item model with the ground
/// display transform; it never bakes a <c>ModelPart</c> model, so there is no placeholder fallback for them. Sharpcraft
/// drew a placeholder box (textured from the entity skin at 64x64 UVs) around every dropped item, which is what the
/// floating texture mosaic came from.
/// </remarks>
public sealed class ItemEntityModelTests
{
    [Fact]
    public void AnItemEntityProducesNoEntityModelLayers()
    {
        var mesher = new EntityModelMesher();
        var state = new EntityRenderState(1, EntityTracker.ItemEntityTypeId, 0f, 64f, 0f, 0f, 0f);
        var skin = new Sharpcraft.Client.Assets.SpriteRect(0, 0, 64, 64, 1) { U0 = 0f, V0 = 0f, U1 = 1f, V1 = 1f };

        var layers = mesher.BuildLayers([state], skin, 64, 64);

        Assert.Empty(layers);
    }

    [Fact]
    public void AnOrdinaryMobStillProducesLayers()
    {
        // Guards the filter: skipping item entities must not skip everything.
        var mesher = new EntityModelMesher();
        var state = new EntityRenderState(2, HumanoidMobModelDefinition.ZombieEntityTypeId, 0f, 64f, 0f, 0f, 0f);
        var skin = new Sharpcraft.Client.Assets.SpriteRect(0, 0, 64, 64, 1) { U0 = 0f, V0 = 0f, U1 = 1f, V1 = 1f };

        var layers = mesher.BuildLayers([state], skin, 64, 64);

        Assert.NotEmpty(layers);
    }
}
