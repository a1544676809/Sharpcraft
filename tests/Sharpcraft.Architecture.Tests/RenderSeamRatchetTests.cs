// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Architecture.Tests;

/// <summary>
/// Keeps the render backend seam from regressing while it is still being finished.
/// </summary>
/// <remarks>
/// The goal is for renderers to speak only the seam's language. They do not yet: a handful of paths still hand the
/// backend native objects, and the escape hatches that allow it are what will be deleted last. A plain "must be zero"
/// assertion would fail today and teach everyone to ignore it, so these are ceilings instead: they may only fall.
/// Removing a hatch call means lowering the number in the same commit; adding one means the test fails, which is the
/// point.
/// </remarks>
public sealed class RenderSeamRatchetTests
{
    private const int BackendCreations = 2;

    private const int EscapeHatchCalls = 70;

    [Fact]
    public void BackendIsNotInstantiatedMoreOftenThanRecorded()
    {
        var created = CountAcross("new WgpuRenderBackend");

        Assert.True(
            created <= BackendCreations,
            $"renderers now create {created} backends, up from the recorded {BackendCreations}; pass the backend the frame already has instead of making another");
    }

    [Fact]
    public void EscapeHatchCallsDoNotGrow()
    {
        var calls = CountAcross("AsTexture", "AsTextureView", "AsBuffer", "AsSampler", "AsBindGroup", "AsBindGroupLayout", "AsPipeline", "AsPipelineLayout", "AsCommandEncoder", "AsRenderPass", "AdoptTexture", "AdoptTextureView", "AdoptBuffer", "AdoptSampler", "AdoptBindGroup", "AdoptBindGroupLayout", "AdoptPipeline", "AdoptPipelineLayout", "AdoptCommandEncoder");

        Assert.True(
            calls <= EscapeHatchCalls,
            $"the renderers call the escape hatches {calls} times, up from the recorded {EscapeHatchCalls}; every new call is a place the seam will have to be revisited, so say why in the contract and raise the ceiling deliberately");
    }

    private static int CountAcross(params string[] patterns)
    {
        var rendering = Path.Combine(RepositoryRoot(), "src", "Sharpcraft.Framework", "Rendering");

        var total = 0;
        foreach (var file in Directory.EnumerateFiles(rendering, "*.cs", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(file);
            if (name == "RenderBackend.cs" || name == "BackendTypes.cs")
            {
                continue;
            }

            var text = File.ReadAllText(file);
            foreach (var pattern in patterns)
            {
                var index = 0;
                while ((index = text.IndexOf(pattern + "(", index, StringComparison.Ordinal)) >= 0)
                {
                    total++;
                    index += pattern.Length;
                }
            }
        }

        return total;
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Sharpcraft.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("the repository root was not found");
    }
}
