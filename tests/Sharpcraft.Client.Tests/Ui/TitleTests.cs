// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Ui;
using Xunit;

namespace Sharpcraft.Client.Tests.Ui;

/// <summary>The title command's large centred text (docs/modules/ui.md 8.7).</summary>
public sealed class TitleTests
{
    [Theory]
    [InlineData(100, 0f)]
    [InlineData(95, 0.5f)]
    [InlineData(80, 1f)]
    [InlineData(20, 1f)]
    [InlineData(10, 0.5f)]
    [InlineData(0, 0f)]
    public void TheTitleFadesInHoldsAndFadesOut(int remaining, float expected) =>
        Assert.Equal(expected, Title.AlphaFor(remaining), 3);

    [Fact]
    public void ASubtitleDoesNotClearTheTitle()
    {
        var title = new Title();
        title.Show([new Sharpcraft.Common.Network.TextRun("main", 0xFFFFFFFFu, false)]);
        title.ShowSubtitle([new Sharpcraft.Common.Network.TextRun("sub", 0xFFFFFFFFu, false)]);

        Assert.True(title.IsVisible);
        Assert.Equal(Title.LifetimeTicks, title.RemainingTicks);
    }

    [Fact]
    public void ClearingRemovesBothParts()
    {
        var title = new Title();
        title.Show([new Sharpcraft.Common.Network.TextRun("main", 0xFFFFFFFFu, false)]);
        title.Clear();

        Assert.False(title.IsVisible);
    }
}
