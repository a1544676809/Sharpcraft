// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Xunit;

namespace Sharpcraft.Architecture.Tests;

/// <summary>
/// Shader organisation gate (docs/specs/render/shader-organisation.md 7): WGSL belongs in
/// <c>Shaders/pipelines/*.wgsl</c>, never inline in a C# file, because an inline copy is how a binding declaration silently
/// diverges from its layout.
/// </summary>
public sealed class ShaderSourceTests
{
    [Fact]
    public void NoRenderingSourceFileContainsInlineWgsl()
    {
        var rendering = Path.Combine(RepositoryRoot(), "src", "Sharpcraft.Framework", "Rendering");
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(rendering, "*.cs", SearchOption.AllDirectories))
        {
            // The Shaders folder holds the shader tooling itself (parsers mention the attribute names as tokens) and the
            // .wgsl files; only the renderer sources are inspected here.
            if (file.Contains(Path.Combine("Rendering", "Shaders"), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var text = File.ReadAllText(file);

            if (text.Contains("@vertex", StringComparison.Ordinal) ||
                text.Contains("@fragment", StringComparison.Ordinal) ||
                text.Contains("@compute", StringComparison.Ordinal))
            {
                offenders.Add(Path.GetRelativePath(RepositoryRoot(), file));
            }
        }

        Assert.True(offenders.Count == 0, "WGSL must live in Shaders/pipelines, found inline in:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void EveryPipelineShaderIsEmbeddedAndLoadable()
    {
        var pipelines = Path.Combine(RepositoryRoot(), "src", "Sharpcraft.Framework", "Rendering", "Shaders", "pipelines");
        var files = Directory.GetFiles(pipelines, "*.wgsl");

        Assert.NotEmpty(files);

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            Assert.True(
                text.Contains("@vertex", StringComparison.Ordinal) || text.Contains("@compute", StringComparison.Ordinal),
                $"{Path.GetFileName(file)} declares no entry point");
        }
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Sharpcraft.slnx")) &&
                File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("repository root not found");
    }
}
