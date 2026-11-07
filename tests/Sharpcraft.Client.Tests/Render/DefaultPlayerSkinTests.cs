// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Java-sampled golden vectors for DefaultPlayerSkin (docs/modules/entity-equipment.md §12.2). The hash, index,
/// path and slim values below were produced by running java.util.UUID.hashCode()/Math.floorMod under GraalVM JDK 25
/// jshell; they are not derived from the C# implementation.
/// </summary>
public sealed class DefaultPlayerSkinTests
{
    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000", 0, 0, "entity/player/slim/alex", true)]
    [InlineData("00000000-0000-0000-0000-000000000001", 1, 1, "entity/player/slim/ari", true)]
    [InlineData("069a79f4-44e9-4726-a5be-fca90e38aaf5", -369792882, 0, "entity/player/slim/alex", true)]
    [InlineData("853c80ef-3c37-49fd-aa49-938b674adae6", 1946714239, 1, "entity/player/slim/ari", true)]
    [InlineData("61699b2e-d327-4a01-9f1e-0ea8c3f06bc6", -291457983, 15, "entity/player/wide/steve", false)]
    [InlineData("ffffffff-ffff-ffff-ffff-ffffffffffff", 0, 0, "entity/player/slim/alex", true)]
    [InlineData("80000000-0000-0000-0000-000000000000", -2147483648, 16, "entity/player/wide/sunny", false)]
    [InlineData("a1b2c3d4-e5f6-4a5b-8c9d-0e1f2a3b4c5d", -488453171, 13, "entity/player/wide/makena", false)]
    [InlineData("deadbeef-0000-0000-0000-00000000cafe", -559057903, 11, "entity/player/wide/efe", false)]
    [InlineData("12345678-1234-5678-9abc-def012345678", -2004318072, 0, "entity/player/slim/alex", true)]
    public void JavaUuidHashAndDefaultSkinMatchSampledVanillaVectors(
        string uuidText, int hashCode, int index, string path, bool slim)
    {
        var uuid = Guid.Parse(uuidText);

        Assert.Equal(hashCode, DefaultPlayerSkin.JavaUuidHashCode(uuid));
        Assert.Equal(index, DefaultPlayerSkin.IndexOf(uuid));
        var skin = DefaultPlayerSkin.Get(uuid);
        Assert.Equal(path, skin.Path);
        Assert.Equal(slim, skin.Slim);
        Assert.Equal(slim, skin.Path.Contains("/slim/", StringComparison.Ordinal));
    }

    [Fact]
    public void GetDefaultSkinIsFixedIndexSix()
    {
        Assert.Equal("entity/player/slim/steve", DefaultPlayerSkin.Default.Path);
        Assert.True(DefaultPlayerSkin.Default.Slim);
    }

    [Fact]
    public void AllEighteenBuiltInSkinsAreRegisteredForTheAtlas()
    {
        Assert.Equal(18, DefaultPlayerSkin.AllPaths.Count);
        Assert.Equal(18, DefaultPlayerSkin.AllPaths.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(9, DefaultPlayerSkin.AllPaths.Count(path => path.Contains("/slim/", StringComparison.Ordinal)));
        Assert.Equal(9, DefaultPlayerSkin.AllPaths.Count(path => path.Contains("/wide/", StringComparison.Ordinal)));
        Assert.Contains("entity/player/slim/steve", DefaultPlayerSkin.AllPaths);
        Assert.Contains("entity/player/wide/steve", DefaultPlayerSkin.AllPaths);
        Assert.Contains(DefaultPlayerSkin.AllPaths, PlayerModelDefinition.Definition.TexturePaths.Contains);
    }

    [Fact]
    public void SlimFlagAndModelSelectionAgreeForTheDefaultSkin()
    {
        // Java-sampled wide default (index 15) and slim default (index 0). A state with no profile skin must fall
        // back to the UUID's built-in skin, and the model chosen by Slim must use that same skin family.
        var wideUuid = Guid.Parse("61699b2e-d327-4a01-9f1e-0ea8c3f06bc6");
        var slimUuid = Guid.Parse("069a79f4-44e9-4726-a5be-fca90e38aaf5");
        var wideState = new EntityRenderState(1, EntityPlaceholderMesher.PlayerEntityTypeId, 0f, 0f, 0f, 0f, 0f,
            Uuid: wideUuid);
        var slimState = wideState with { Uuid = slimUuid, Slim = true };

        Assert.Equal("entity/player/wide/steve", PlayerModelDefinition.Definition.TextureSelector!(wideState));
        Assert.Equal("entity/player/slim/alex", PlayerModelDefinition.SlimDefinition.TextureSelector!(slimState));
        Assert.False(DefaultPlayerSkin.Get(wideUuid).Slim);
        Assert.True(DefaultPlayerSkin.Get(slimUuid).Slim);
    }
}
