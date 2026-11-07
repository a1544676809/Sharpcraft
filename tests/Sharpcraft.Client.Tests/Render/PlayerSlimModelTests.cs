// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>SLIM player model golden vectors (docs/modules/entity-equipment.md §12).</summary>
public sealed class PlayerSlimModelTests
{
    private static EntityModelPart Arm(IReadOnlyList<EntityModelPart> parts, string name) =>
        parts.Single(part => part.Name == name);

    private static float MaxX(DynamicGeometry geometry)
    {
        var max = float.MinValue;
        for (var i = 0; i < geometry.VertexFloats; i += 14)
        {
            max = MathF.Max(max, geometry.Vertices[i]);
        }

        return max;
    }

    [Fact]
    public void SlimArmsAreThreePixelsWideAndKeepTheWideTextureOffsets()
    {
        var wide = PlayerModelDefinition.Definition.Parts;
        var slim = PlayerModelDefinition.SlimDefinition.Parts;

        var wideLeft = Arm(wide, "left_arm");
        var slimLeft = Arm(slim, "left_arm");
        Assert.Equal(4f, wideLeft.Width);
        Assert.Equal(3f, slimLeft.Width);
        Assert.Equal(12f, slimLeft.Height);
        Assert.Equal(4f, slimLeft.Depth);
        Assert.Equal(wideLeft.TextureX, slimLeft.TextureX);
        Assert.Equal(wideLeft.TextureY, slimLeft.TextureY);

        var wideRight = Arm(wide, "right_arm");
        var slimRight = Arm(slim, "right_arm");
        Assert.Equal(4f, wideRight.Width);
        Assert.Equal(3f, slimRight.Width);
        Assert.Equal(wideRight.TextureX, slimRight.TextureX);
        Assert.Equal(wideRight.TextureY, slimRight.TextureY);
        Assert.NotEqual(wideRight.CentreX, slimRight.CentreX);

        // Reference unwrap: u4 - texX = 2 * (depth + width); the arm width is the only UV-span difference.
        Assert.Equal(16, 2 * ((int)wideLeft.Depth + (int)wideLeft.Width));
        Assert.Equal(14, 2 * ((int)slimLeft.Depth + (int)slimLeft.Width));

        foreach (var name in new[] { "head", "body", "left_leg", "right_leg" })
        {
            var w = Arm(wide, name);
            var s = Arm(slim, name);
            Assert.Equal(w.Width, s.Width);
            Assert.Equal(w.Height, s.Height);
            Assert.Equal(w.Depth, s.Depth);
            Assert.Equal(w.TextureX, s.TextureX);
            Assert.Equal(w.TextureY, s.TextureY);
        }

        Assert.NotNull(PlayerModelDefinition.SlimDefinition.Compiled);
        Assert.True(PlayerModelDefinition.SlimDefinition.Compiled!.Cubes.Count > 0);
    }

    [Fact]
    public void DefaultSkinFallsBackToTheMatchingPlayerModel()
    {
        var wide = EntityModelResolver.ResolvePlayerModel(slim: false);
        var slim = EntityModelResolver.ResolvePlayerModel(slim: true);
        var state = new EntityRenderState(1, EntityPlaceholderMesher.PlayerEntityTypeId, 0f, 0f, 0f, 0f, 0f);

        Assert.Equal("entity/player/wide/steve", wide.TextureSelector!(state));
        Assert.Equal("entity/player/slim/steve", slim.TextureSelector!(state));
        Assert.Equal(4f, Arm(wide.Parts, "left_arm").Width);
        Assert.Equal(3f, Arm(slim.Parts, "left_arm").Width);

        var custom = state with { PlayerBodyTexture = "skins/custom" };
        Assert.Equal("skins/custom", slim.TextureSelector!(custom));

        Assert.Same(PlayerModelDefinition.SlimDefinition, PlayerModelDefinition.For(true));
        Assert.Same(PlayerModelDefinition.Definition, PlayerModelDefinition.For(false));
    }

    [Fact]
    public void SlimRenderStateSelectsTheSlimGeometry()
    {
        var sprite = new SpriteRect(0, 0, 64, 64, 1);
        var wideState = new EntityRenderState(1, EntityPlaceholderMesher.PlayerEntityTypeId, 0f, 0f, 0f, 0f, 0f);
        var slimState = wideState with { Slim = true };

        var wideGeometry = new EntityModelMesher().Build([wideState], sprite, 64, 64, variants: EntityVariantTable.Builtin);
        var slimGeometry = new EntityModelMesher().Build([slimState], sprite, 64, 64, variants: EntityVariantTable.Builtin);

        Assert.True(wideGeometry.VertexFloats > 0);
        Assert.True(slimGeometry.VertexFloats > 0);
        Assert.NotEqual(MaxX(wideGeometry), MaxX(slimGeometry));
    }
}
