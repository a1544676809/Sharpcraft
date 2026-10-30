// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Ui;
using Sharpcraft.Common.Imaging;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Ui;

/// <summary>
/// Scrolling a list and the bar that shows it (docs/modules/ui.md 5.3). Both live outside the chat so a server list or
/// a book page can use them without copying the arithmetic.
/// </summary>
public sealed class ScrollTests
{
    [Fact]
    public void ThePositionStopsAtBothEnds()
    {
        var scroll = new ScrollState();

        scroll.ScrollBy(-5, itemCount: 30, pageSize: 11);
        Assert.Equal(0, scroll.Offset);

        scroll.ScrollBy(5, itemCount: 30, pageSize: 11);
        Assert.Equal(5, scroll.Offset);

        // Only items beyond a page can be scrolled away, so the limit is count minus the page.
        scroll.ScrollBy(100, itemCount: 30, pageSize: 11);
        Assert.Equal(19, scroll.Offset);
    }

    [Fact]
    public void AListThatFitsCannotScroll()
    {
        var scroll = new ScrollState();
        scroll.ScrollBy(3, itemCount: 4, pageSize: 11);

        Assert.Equal(0, scroll.Offset);
        Assert.False(scroll.IsScrolled);
    }

    [Fact]
    public void TheBarIsOnlyDrawnWhenTheListOverflows()
    {
        var geometry = new UiGeometry();
        ScrollBar.Draw(geometry, left: 10f, areaTop: 0f, areaBottom: 99f, itemCount: 5, pageSize: 11, offset: 0, pending: false);
        Assert.Equal(0, geometry.QuadCount);

        ScrollBar.Draw(geometry, left: 10f, areaTop: 0f, areaBottom: 99f, itemCount: 30, pageSize: 11, offset: 0, pending: false);
        Assert.True(geometry.QuadCount > 0);
    }

    [Fact]
    public void TheThumbMovesUpAsTheListIsScrolled()
    {
        var fresh = new UiGeometry();
        ScrollBar.Draw(fresh, 10f, 0f, 99f, 30, 11, offset: 0, pending: false);

        var scrolled = new UiGeometry();
        ScrollBar.Draw(scrolled, 10f, 0f, 99f, 30, 11, offset: 15, pending: false);

        // Row zero of each bar is its thumb top; scrolling moves it towards the top of the area.
        Assert.True(scrolled.Vertices[1] < fresh.Vertices[1]);
    }
}
