// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Ui;
using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Client.Tests.Ui;

/// <summary>
/// Deleting a chat line by signature and the background opacity setting (docs/modules/ui.md 8.8). The reference removes the
/// message whose signature a <c>delete_chat</c> packet names, so the line has to carry one.
/// </summary>
public sealed class ChatDeleteAndOpacityTests
{
    [Fact]
    public void ALineWithMatchingSignatureIsRemoved()
    {
        var log = new ChatLog();
        var signature = new byte[] { 7, 7, 7 };
        log.Add([new TextRun("secret", ComponentRuns.DefaultColour, false)], 0, ChatLog.MessageTag.System, null, null, signature);

        Assert.True(log.RemoveBySignature(signature));
        Assert.Equal(0, log.Count);
    }

    [Fact]
    public void AnUnknownSignatureRemovesNothing()
    {
        var log = new ChatLog();
        log.Add([new TextRun("kept", ComponentRuns.DefaultColour, false)], 0, ChatLog.MessageTag.System, null, null, [1]);

        Assert.False(log.RemoveBySignature([2, 2]));
        Assert.Equal(1, log.Count);
    }

    [Fact]
    public void LinesWithoutASignatureAreNeverDeleted()
    {
        var log = new ChatLog();
        log.Add([new TextRun("unsigned", ComponentRuns.DefaultColour, false)], 0);

        Assert.False(log.RemoveBySignature([]));
        Assert.Equal(1, log.Count);
    }

    [Fact]
    public void TheBackgroundOpacityDefaultsToAHalf()
    {
        Assert.InRange(ChatLog.BackgroundOpacity, 0f, 1f);
    }
}
