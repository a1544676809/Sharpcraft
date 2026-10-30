// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Rendering.Shaders;
using Xunit;

namespace Sharpcraft.Framework.Tests.Rendering;

/// <summary>
/// Shader organisation rules (docs/specs/render/shader-organisation.md 3 and 6): includes expand once and fail loudly, and a
/// shader whose bindings disagree with its layout is refused before anything is created.
/// </summary>
public sealed class ShaderOrganisationTests
{
    [Fact]
    public void AnIncludeIsExpandedOnceEvenWhenRepeated()
    {
        var files = new Dictionary<string, string> { ["include/camera.wgsl"] = "struct ScCamera { m : mat4x4<f32> };" };

        var composed = ShaderIncludeComposer.Compose(
            "demo",
            "//#include \"include/camera.wgsl\"\n//#include \"include/camera.wgsl\"\n@vertex fn vs_demo() {}",
            path => files.GetValueOrDefault(path));

        Assert.Equal(1, CountOccurrences(composed, "struct ScCamera"));
    }

    [Fact]
    public void AMissingIncludeFailsWithThePipelineAndLine()
    {
        var error = Assert.Throws<InvalidOperationException>(() => ShaderIncludeComposer.Compose(
            "demo",
            "@vertex fn vs_demo() {}\n//#include \"include/nope.wgsl\"",
            _ => null));

        Assert.Contains("demo", error.Message);
        Assert.Contains("nope.wgsl", error.Message);
        Assert.Contains("line 2", error.Message);
    }

    [Fact]
    public void TooDeepAChainIsRefused()
    {
        var files = new Dictionary<string, string>
        {
            ["a"] = "//#include \"b\"",
            ["b"] = "//#include \"c\"",
            ["c"] = "//#include \"d\"",
            ["d"] = "//#include \"e\"",
            ["e"] = "//#include \"f\"",
            ["f"] = ""
        };

        Assert.Throws<InvalidOperationException>(() => ShaderIncludeComposer.Compose("demo", "//#include \"a\"", path => files.GetValueOrDefault(path)));
    }

    [Fact]
    public void AMatchingShaderHasNoViolations()
    {
        var descriptor = new ShaderDescriptor("terrain", "terrain.wgsl", ["vs_terrain", "fs_terrain"], [2, 3]);
        var source = "@group(0) @binding(2) var<uniform> camera : ScCamera;\n@group(0) @binding(3) var<uniform> fog : ScFog;\n@vertex fn vs_terrain() {}\n@fragment fn fs_terrain() {}";

        Assert.Empty(descriptor.Validate(source));
    }

    [Fact]
    public void ADisagreeingShaderReportsBothDirections()
    {
        var descriptor = new ShaderDescriptor("terrain", "terrain.wgsl", ["vs_terrain"], [2, 3]);
        var source = "@group(0) @binding(2) var<uniform> camera : ScCamera;\n@group(0) @binding(9) var<uniform> other : f32;\n@vertex fn vs_terrain() {}";

        var violations = descriptor.Validate(source);

        Assert.Contains(violations, text => text.Contains("binding 3"));
        Assert.Contains(violations, text => text.Contains("binding 9"));
    }

    [Fact]
    public void AMissingEntryPointIsReported()
    {
        var descriptor = new ShaderDescriptor("sky", "sky.wgsl", ["vs_sky", "fs_sky"], []);
        var source = "@vertex fn vs_sky() {}";

        Assert.Contains(descriptor.Validate(source), text => text.Contains("fs_sky"));
    }

    private static int CountOccurrences(string text, string needle)
    {
        var count = 0;
        var index = text.IndexOf(needle, StringComparison.Ordinal);

        while (index >= 0)
        {
            count++;
            index = text.IndexOf(needle, index + needle.Length, StringComparison.Ordinal);
        }

        return count;
    }
}
