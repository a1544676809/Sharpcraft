// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Reflection;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Framework.Tests.Repro;

/// <summary>
/// Repro-287：物品半透明（OIT）目标曾用**一个**格式创建全部四张贴图，而 OIT 管线是按"深度边界一种、
/// 颜色另一种"声明的。地形目标一直分两种格式，物品目标没有，于是只要视野里出现一件半透明物品
/// （例如手持玻璃），wgpu 就在 <c>wgpuCommandEncoderFinish</c> 报
/// "Incompatible color attachments at indices [0, 1] … [Rgba32Float, Rgba32Float] vs [Rgba16Float, Rgba16Float]"
/// 并以 0xc0000409 终止进程。
/// 本用例守住"物品目标必须与地形目标用同一套双格式来源"。
/// 关联：docs/modules/render.md；缺陷修复，无 ADR。
/// </summary>
public sealed class Repro_287_ItemOitFormatTests
{
    [Fact]
    public void ItemOitTargetsDeclareTwoFormats()
    {
        var create = typeof(ItemOitSet).GetMethod("Create", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(create);

        var formats = create!.GetParameters()
            .Where(parameter => parameter.ParameterType == typeof(TextureFormat))
            .ToArray();

        // Bounds first, colour second: the same split FrameOitTargets uses for the terrain targets. A single
        // format argument is exactly the shape that produced the crash, so the arity is the invariant.
        Assert.Equal(2, formats.Length);
    }

    [Fact]
    public void FrameOitTargetsFeedsBothFormatsToTheItemSet()
    {
        var root = RepositoryRoot();
        var text = File.ReadAllText(Path.Combine(root, "src/Sharpcraft.Framework/Rendering/FrameOitTargets.cs"));

        var call = text.IndexOf("_itemOit.Create(", StringComparison.Ordinal);
        Assert.True(call >= 0, "FrameOitTargets must create the item OIT targets.");
        var end = text.IndexOf(';', call);
        Assert.True(end > call, "Unterminated _itemOit.Create call.");
        var arguments = text[call..end];

        Assert.Contains("_oitTargetFormat", arguments, StringComparison.Ordinal);
        Assert.Contains("_oitColorFormat", arguments, StringComparison.Ordinal);
    }

    [Fact]
    public void ColorTargetsAreNotCreatedWithTheBoundsFormat()
    {
        var root = RepositoryRoot();
        var text = File.ReadAllText(Path.Combine(root, "src/Sharpcraft.Framework/Rendering/ItemOitSet.cs"));

        // The three sampled-and-drawn targets must come from the colour description. Creating them from the
        // bounds description is the original defect in a different spelling.
        Assert.Contains("_coeff0 = backend.CreateTexture(colorDesc)", text, StringComparison.Ordinal);
        Assert.Contains("_coeff1 = backend.CreateTexture(colorDesc)", text, StringComparison.Ordinal);
        Assert.Contains("_accumulate = backend.CreateTexture(colorDesc)", text, StringComparison.Ordinal);
        Assert.Contains("_bounds = backend.CreateTexture(boundsDesc)", text, StringComparison.Ordinal);
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

        throw new InvalidOperationException("Sharpcraft.slnx not found above the test output directory.");
    }
}
