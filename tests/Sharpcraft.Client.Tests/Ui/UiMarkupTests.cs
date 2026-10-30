// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Ui;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Ui;

/// <summary>
/// The declarative kernel (docs/modules/ui.md 6 and 7). The parser refuses what it does not understand rather
/// than ignoring it, and the layout runs one pass with no per frame allocation.
/// </summary>
public sealed class UiMarkupTests
{
    [Fact]
    public void TheDefaultDocumentParsesIntoTheExpectedTree()
    {
        var root = UiMarkup.Parse(UiScreenLayout.DefaultDocument);

        Assert.Equal("Screen", root.Name);
        Assert.Equal("8", root.Get("Padding"));
        Assert.Equal("", root.Get("Background"));

        var panel = Assert.Single(root.Children);
        Assert.Equal("StackPanel", panel.Name);
        Assert.Equal(3, panel.Children.Count);
        Assert.All(panel.Children, child => Assert.Equal("TextBlock", child.Name));
        Assert.Equal("SHARPCRAFT", panel.Children[0].Get("Text"));
    }

    [Fact]
    public void CommentsAndSelfClosingElementsAreHandled()
    {
        const string document = """
<Screen>
  <!-- a comment -->
  <TextBlock Text="a" />
  <TextBlock Text="b"></TextBlock>
</Screen>
""";

        var root = UiMarkup.Parse(document);

        Assert.Equal(2, root.Children.Count);
        Assert.Equal("b", root.Children[1].Get("Text"));
    }

    [Fact]
    public void AnUnknownElementIsRefused()
    {
        var error = Assert.Throws<FormatException>(() => UiMarkup.Parse("<Screen><Slider/></Screen>"));

        Assert.Contains("unknown element Slider", error.Message);
    }

    [Fact]
    public void AnUnknownAttributeIsRefused()
    {
        var error = Assert.Throws<FormatException>(() => UiMarkup.Parse("<Screen Opacity=\"0.5\"/>"));

        Assert.Contains("unknown attribute Opacity", error.Message);
    }

    [Fact]
    public void ATextBlockIsNotAcceptedAsTheRoot()
    {
        var error = Assert.Throws<FormatException>(() => UiMarkup.Parse("<TextBlock Text=\"x\"/>"));

        Assert.Contains("root element must be Screen", error.Message);
    }

    [Fact]
    public void ColoursParseInBothAcceptedForms()
    {
        Assert.Equal(0xFFFFFFFFu, UiColours.Parse("#FFFFFF"));
        Assert.Equal(0xFF0000FFu, UiColours.Parse("#FF0000"));
        Assert.Equal(0x80332211u, UiColours.Parse("#80112233"));
    }

    [Fact]
    public void LayoutPlacesTheBackgroundThenOneCellPerVisibleCharacter()
    {
        const string document = """
<Screen Background="#FF000000" Padding="4">
  <TextBlock Text="ab c" Colour="#FFFFFFFF" />
</Screen>
""";

        var root = UiMarkup.Parse(document);
        var styles = UiStyles.Compile(root);
        var geometry = new UiGeometry();

        UiScreenLayout.Build(root, styles, 100f, 50f, geometry);

        // Three visible characters plus one background fill; the space advances without drawing.
        Assert.Equal(4, geometry.QuadCount);
        Assert.Equal(1, geometry.Ranges.Length);
        Assert.Equal(0f, geometry.Ranges[0].MinX);
        Assert.Equal(100f, geometry.Ranges[0].MaxX);
    }

    [Fact]
    public void AStackPanelCarriesItsSpacingAndPaddingIntoThePlacement()
    {
        const string document = """
<Screen Padding="4">
  <StackPanel Spacing="3">
    <TextBlock Text="x" Height="10" />
    <TextBlock Text="y" Height="10" />
  </StackPanel>
</Screen>
""";

        var root = UiMarkup.Parse(document);
        var styles = UiStyles.Compile(root);
        var geometry = new UiGeometry();

        UiScreenLayout.Build(root, styles, 100f, 50f, geometry);

        var vertices = geometry.Vertices;

        // The first cell starts at x = padding, and is centred inside its ten pixel line: 4 + (10 - 7) / 2.
        Assert.Equal(4f, vertices[0], 3);
        Assert.Equal(5.5f, vertices[1], 3);

        // The second row starts after the first line plus the spacing, then centres again: 4 + 10 + 3 + 1.5.
        Assert.Equal(18.5f, vertices[21], 3);
    }

    [Fact]
    public void BuildingAFrameDoesNotAllocateOnceWarm()
    {
        var root = UiMarkup.Parse(UiScreenLayout.DefaultDocument);
        var styles = UiStyles.Compile(root);
        var geometry = new UiGeometry();

        // Warm enough that one time costs (buffer growth, type initialisers) are behind us; four passes is more
        // than any of them need and costs microseconds.
        // Warm past the point where tiered compilation replaces the method: a recompile installed in the middle of
        // the measured loop allocates once and looks like a regression that is not there.
        for (var warm = 0; warm < 32; warm++)
        {
            UiScreenLayout.Build(root, styles, 320f, 240f, geometry);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var frame = 0; frame < 64; frame++)
        {
            UiScreenLayout.Build(root, styles, 320f, 240f, geometry);
        }

        var after = GC.GetAllocatedBytesForCurrentThread();
        Assert.True(after == before, $"building a frame allocated {after - before} bytes");
    }
}
