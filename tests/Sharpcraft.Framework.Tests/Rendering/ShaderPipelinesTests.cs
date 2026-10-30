// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Rendering.Shaders;
using Xunit;

namespace Sharpcraft.Framework.Tests.Rendering;

/// <summary>
/// Pipeline registry (docs/specs/render/shader-organisation.md 5): the list of pipelines lives in one place, every entry
/// loads, and a disagreement between a shader and its declared bindings is refused.
/// </summary>
public sealed class ShaderPipelinesTests
{
    [Fact]
    public void EveryPipelineInTheRegistryLoadsAndValidates()
    {
        ShaderPipelines.EnsureValid();

        Assert.True(ShaderPipelines.All.Count >= 5, "terrain, sky and the three interface shaders are expected");
    }

    [Fact]
    public void TheRegistryNamesMatchTheirFiles()
    {
        foreach (var descriptor in ShaderPipelines.All)
        {
            Assert.Equal(descriptor.Name + ".wgsl", descriptor.File);
            Assert.NotEmpty(descriptor.EntryPoints);
        }
    }

    [Fact]
    public void ADescriptorThatExpectsAMissingBindingIsRefused()
    {
        var descriptor = new ShaderDescriptor("terrain", "terrain.wgsl", ["vs_main", "fs_main"], [0, 1, 2, 3, 9]);

        Assert.Contains(descriptor.Validate(ShaderLibrary.Load("terrain")), text => text.Contains("binding 9"));
    }
}
