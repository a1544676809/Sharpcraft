// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Ui;
using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Client.Tests.Ui;

/// <summary>
/// A <c>change_page</c> click (docs/modules/ui.md 8.8): page one is the newest, and the offset is page minus one times the
/// page size, clamped to the available history.
/// </summary>
public sealed class ChatPageScrollTests
{
    [Fact]
    public void PageOneShowsTheNewestLines()
    {
        var log = Build(40);

        log.ScrollToPage(1);

        Assert.Equal(0, log.Scroll.Offset);
    }

    [Fact]
    public void PageTwoSkipsExactlyOnePage()
    {
        var log = Build(40);

        log.ScrollToPage(2);

        Assert.Equal(ChatLog.LinesPerPage, log.Scroll.Offset);
    }

    [Fact]
    public void APageBeyondTheHistoryClampsToTheOldestLines()
    {
        var log = Build(40);

        log.ScrollToPage(99);

        Assert.Equal(40 - ChatLog.LinesPerPage, log.Scroll.Offset);
    }

    private static ChatLog Build(int lines)
    {
        var log = new ChatLog();

        for (var index = 0; index < lines; index++)
        {
            log.Add([new TextRun($"line {index}", ComponentRuns.DefaultColour, false)], index, ChatLog.MessageTag.System, null, null);
        }

        return log;
    }
}
