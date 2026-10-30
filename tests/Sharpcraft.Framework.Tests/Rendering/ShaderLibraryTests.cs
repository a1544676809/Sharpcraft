// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Rendering.Shaders;
using Xunit;

namespace Sharpcraft.Framework.Tests.Rendering;

/// <summary>
/// Embedded shaders (docs/specs/render/shader-organisation.md 1-3): a pipeline is a file, it is embedded so the renderer has
/// no runtime file dependency, and a missing pipeline must fail loudly rather than compile an empty shader.
/// </summary>
public sealed class ShaderLibraryTests
{
    [Theory]
    [InlineData("terrain")]
    [InlineData("sky")]
    public void APipelineLoadsFromItsEmbeddedFile(string pipeline)
    {
        var source = ShaderLibrary.Load(pipeline);

        Assert.Contains("@vertex", source);
        Assert.True(source.Length > 200, "a composed shader should be more than a stub");
    }

    [Fact]
    public void AnUnknownPipelineFailsByName()
    {
        var error = Assert.Throws<InvalidOperationException>(() => ShaderLibrary.Load("does_not_exist"));

        Assert.Contains("does_not_exist", error.Message);
    }
}
