// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Xunit;

namespace Sharpcraft.Framework.Tests.Repro;

/// <summary>
/// Repro-001：顶点布局曾被离屏与窗口两条管线各写一份，加纹理时只改了其中一份，窗口渲染随即在
/// `wgpuDeviceCreateRenderPipeline` 报 "Location[2] Float32x2 … is not provided by the previous stage outputs"。
/// 修复方式是把布局收敛到 <c>VertexLayout</c>；本用例守住"不得再出现第二份声明"。
/// 关联：docs/modules/render.md §6.1；ADR 无（缺陷修复）。
/// </summary>
public sealed class Repro_001_VertexLayoutDeclaredOnceTests
{
    [Fact]
    public void OnlyOneFileMentionsVertexAttributes()
    {
        var root = RepositoryRoot();
        var declarations = Directory
            .EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => File.ReadAllText(file).Contains("VertexAttribute", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/'))
            .ToArray();

        Assert.Equal(["src/Sharpcraft.Framework/Rendering/VertexLayout.cs"], declarations);
    }

    [Fact]
    public void BothRenderersGoThroughTheSharedLayout()
    {
        var rendering = Path.Combine(RepositoryRoot(), "src", "Sharpcraft.Framework", "Rendering");

        foreach (var renderer in new[] { "OffscreenRenderer.cs", "WindowedRenderer.cs" })
        {
            var text = File.ReadAllText(Path.Combine(rendering, renderer));
            Assert.Contains("VertexLayout.Attributes()", text, StringComparison.Ordinal);
            Assert.Contains("vertexStride: (ulong)VertexLayout.Stride", text, StringComparison.Ordinal);
        }
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

        throw new InvalidOperationException("repository root not found");
    }
}
