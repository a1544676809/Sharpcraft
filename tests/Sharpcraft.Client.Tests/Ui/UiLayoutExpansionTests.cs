// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Ui;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Ui;

/// <summary>
/// Pins the expanded XAML subset: XML entities, xmlns/x:Class, 9-point anchors, horizontal panels,
/// dynamic bindings and zero-GC steady-state layout.
/// </summary>
public sealed class UiLayoutExpansionTests
{
    [Fact]
    public void XmlEntitiesAndNamespaceAttributesAreAccepted()
    {
        const string document = """
<Screen xmlns="https://sharpcraft.dev/ui" xmlns:x="https://sharpcraft.dev/ui/xaml" x:Class="Sharpcraft.Client.Ui.Hud">
  <TextBlock Text="HP &lt; 5 &amp; &quot;OK&quot;" />
</Screen>
""";

        var root = UiMarkup.Parse(document);
        Assert.Equal("Sharpcraft.Client.Ui.Hud", root.Get("x:Class"));
        Assert.Equal("HP < 5 & \"OK\"", root.Children[0].Get("Text"));
    }

    [Fact]
    public void BottomCenterAnchorPlacesPanelAtBottomMiddle()
    {
        const string document = """
<Screen Padding="10">
  <StackPanel Anchor="BottomCenter" Margin="4" Orientation="Horizontal" Spacing="2">
    <Spacer Width="20" Height="10" Background="#FFFFFFFF" />
    <Spacer Width="10" Height="10" Background="#FFFFFFFF" />
  </StackPanel>
</Screen>
""";

        var root = UiMarkup.Parse(document);
        var styles = UiStyles.Compile(root);
        var geometry = new UiGeometry();

        UiScreenLayout.Build(root, styles, 200f, 100f, geometry);

        // Total panel width = 20 + 2 + 10 = 32. Centered inside [10..190] (width 180): x = 10 + (180 - 32) / 2 = 84.
        // Bottom inside [10..90] with margin 4 and height 10: y = 10 + 80 - 10 - 4 = 76.
        Assert.Equal(2, geometry.QuadCount);
        Assert.Equal(84f, geometry.Vertices[0], 3);
        Assert.Equal(76f, geometry.Vertices[1], 3);
    }

    [Fact]
    public void BindingResolvesWithoutAllocatingOnceWarm()
    {
        const string document = """
<Screen Padding="4">
  <StackPanel Anchor="TopRight" Margin="2">
    <TextBlock Text="{Binding Health}" Colour="#FFFF5555" Shadow="false" />
  </StackPanel>
</Screen>
""";

        var root = UiMarkup.Parse(document);
        var styles = UiStyles.Compile(root);
        var geometry = new UiGeometry();
        const string cachedValue = "20";
        Func<string, string?> resolver = static key => key == "Health" ? cachedValue : null;

        for (var warm = 0; warm < 32; warm++)
        {
            UiScreenLayout.Build(root, styles, 320f, 240f, geometry, resolveBinding: resolver);
        }

        var smallest = long.MaxValue;
        for (var run = 0; run < 5; run++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var frame = 0; frame < 64; frame++)
            {
                UiScreenLayout.Build(root, styles, 320f, 240f, geometry, resolveBinding: resolver);
            }

            var after = GC.GetAllocatedBytesForCurrentThread();
            smallest = Math.Min(smallest, after - before);
        }

        Assert.Equal(0, smallest);
        Assert.Equal(2, geometry.QuadCount);
    }
}
