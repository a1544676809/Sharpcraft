// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Common.Identifiers;
using Xunit;

namespace Sharpcraft.Client.Tests.Assets;

/// <summary>
/// Where each kind of resource lives inside the pack (docs/modules/asset-pipeline.md). A wrong prefix is invisible
/// until something quietly falls back to a default, which is why the paths are asserted rather than assumed.
/// </summary>
public sealed class ResourcePathTests
{
    [Theory]
    [InlineData(ResourceKind.Language, "assets/minecraft/lang/en_us.json")]
    [InlineData(ResourceKind.Texture, "assets/minecraft/textures/font/ascii.png")]
    [InlineData(ResourceKind.BlockState, "assets/minecraft/blockstates/stone.json")]
    [InlineData(ResourceKind.Model, "assets/minecraft/models/block/stone.json")]
    public void TheFilePathFollowsThePackLayout(ResourceKind kind, string expected)
    {
        var path = kind switch
        {
            ResourceKind.Language => ResourcePath.FilePath(kind, new Identifier("minecraft", "en_us")),
            ResourceKind.Texture => ResourcePath.FilePath(kind, new Identifier("minecraft", "font/ascii")),
            ResourceKind.BlockState => ResourcePath.FilePath(kind, new Identifier("minecraft", "stone")),
            _ => ResourcePath.FilePath(kind, new Identifier("minecraft", "block/stone"))
        };

        Assert.Equal(expected, path);
    }
}
