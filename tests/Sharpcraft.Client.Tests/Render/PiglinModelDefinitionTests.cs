// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// T-682 / task-185: proves the DSL-baked piglin tree is value-identical to independent raw source-frame records.
/// Source AdultPiglinModel.createBodyLayer = PlayerModel.createMesh(NONE, false) (body/arms/legs + jacket/sleeve/pants
/// overlays, hat cleared) + 4-cube head with 2 ears. Definition/Brute/Zombified share the raw geometry.
/// Source: ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/piglin/AdultPiglinModel.java:17-24,
/// AbstractPiglinModel.java:38-61, ref://.../player/PlayerModel.java:43-87.
/// </summary>
public sealed class PiglinModelDefinitionTests
{
    [Fact]
    public void PiglinTreeMatchesSourceFrameRecords()
    {
        foreach (var definition in Definitions())
        {
            var expected = ExpectedParts();
            Assert.Equal(expected.Length, definition.Parts.Count);
            for (var i = 0; i < expected.Length; i++)
            {
                ModelPartAssertions.AssertEqual(expected[i], definition.Parts[i]);
            }
        }
    }

    [Fact]
    public void DefinitionBruteZombifiedKeepNamesPathsAndCounts()
    {
        var definition = PiglinModelDefinition.Definition;
        Assert.Equal("piglin", definition.Name);
        Assert.Equal(64, definition.TextureWidth);
        Assert.Equal(64, definition.TextureHeight);
        Assert.Equal("entity/piglin/piglin", definition.TexturePath);
        Assert.Equal(16, definition.Parts.Count);
        Assert.True(definition.RawSourceFrame);

        var brute = PiglinModelDefinition.Brute;
        Assert.Equal("piglin_brute", brute.Name);
        Assert.Equal("entity/piglin/piglin_brute", brute.TexturePath);
        Assert.Equal(16, brute.Parts.Count);
        Assert.True(brute.RawSourceFrame);

        var zombified = PiglinModelDefinition.Zombified;
        Assert.Equal("zombified_piglin", zombified.Name);
        Assert.Equal("entity/piglin/zombified_piglin", zombified.TexturePath);
        Assert.Equal(16, zombified.Parts.Count);
        Assert.True(zombified.RawSourceFrame);
    }

    private static IReadOnlyList<EntityModelDefinition> Definitions() =>
        [PiglinModelDefinition.Definition, PiglinModelDefinition.Brute, PiglinModelDefinition.Zombified];

    private static EntityModelPart[] ExpectedParts() =>
    [
        // Body + jacket overlay (PlayerModel.createMesh, slim=false, CubeDeformation.NONE).
        RawModelPartFactory.FromVanilla("body", 0f, 0f, 0f, -4f, 0f, -2f, 8f, 12f, 4f, 16, 16),
        RawModelPartFactory.FromVanillaChild("jacket", "body", 0f, 0f, 0f, -4f, 0f, -2f, 8f, 12f, 4f, 16, 32, grow: 0.25f),
        RawModelPartFactory.FromVanilla("left_arm", 5f, 2f, 0f, -1f, -2f, -2f, 4f, 12f, 4f, 32, 48),
        RawModelPartFactory.FromVanillaChild("left_sleeve", "left_arm", 0f, 0f, 0f, -1f, -2f, -2f, 4f, 12f, 4f, 48, 48, grow: 0.25f),
        RawModelPartFactory.FromVanilla("right_arm", -5f, 2f, 0f, -3f, -2f, -2f, 4f, 12f, 4f, 40, 16),
        RawModelPartFactory.FromVanillaChild("right_sleeve", "right_arm", 0f, 0f, 0f, -3f, -2f, -2f, 4f, 12f, 4f, 40, 32, grow: 0.25f),
        RawModelPartFactory.FromVanilla("left_leg", 1.9f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 16, 48),
        RawModelPartFactory.FromVanillaChild("left_pants", "left_leg", 0f, 0f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 48, grow: 0.25f),
        RawModelPartFactory.FromVanilla("right_leg", -1.9f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 16),
        RawModelPartFactory.FromVanillaChild("right_pants", "right_leg", 0f, 0f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 32, grow: 0.25f),
        // Head: 4 cubes + 2 ears (source AbstractPiglinModel.addHead).
        RawModelPartFactory.FromVanilla("head", 0f, 0f, 0f, -5f, -8f, -4f, 10f, 8f, 8f, 0, 0),
        RawModelPartFactory.FromVanillaChild("head_nose", "head", 0f, 0f, 0f, -2f, -4f, -5f, 4f, 4f, 1f, 31, 1),
        RawModelPartFactory.FromVanillaChild("head_nostril_right", "head", 0f, 0f, 0f, 2f, -2f, -5f, 1f, 2f, 1f, 2, 4),
        RawModelPartFactory.FromVanillaChild("head_nostril_left", "head", 0f, 0f, 0f, -3f, -2f, -5f, 1f, 2f, 1f, 2, 0),
        RawModelPartFactory.FromVanillaChild("left_ear", "head", 4.5f, -6f, 0f, 0f, 0f, -2f, 1f, 5f, 4f, 51, 6,
            rotationZ: -MathF.PI / 6f),
        RawModelPartFactory.FromVanillaChild("right_ear", "head", -4.5f, -6f, 0f, -1f, 0f, -2f, 1f, 5f, 4f, 39, 6,
            rotationZ: MathF.PI / 6f)
    ];
}
