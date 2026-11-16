// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Rendering.Shaders;

namespace Sharpcraft.Framework.Tests.Rendering;

/// <summary>
/// Pins the solid block entity fragment entry point to entitySolid semantics: no alpha test, opaque output.
/// </summary>
public sealed class BlockEntitySolidShaderTests
{
    [Fact]
    public void SolidEntryPointDoesNotDiscardAlphaOrPreserveTextureAlpha()
    {
        var source = ShaderLibrary.Load("terrain");
        var start = source.IndexOf("fn fs_solid", StringComparison.Ordinal);
        Assert.True(start >= 0, "terrain.wgsl has no fs_solid entry point");

        var end = source.IndexOf("@fragment", start + 1, StringComparison.Ordinal);
        Assert.True(end > start, "fs_solid is not followed by another entry point");
        var body = source[start..end];

        Assert.DoesNotContain("discard;", body, StringComparison.Ordinal);
        Assert.Contains("vec4<f32>(litColour(sampled.rgb, light), 1.0)", body, StringComparison.Ordinal);
        Assert.Contains("fogged", body, StringComparison.Ordinal);
    }
}
