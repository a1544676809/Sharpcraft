// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Common.Tests.Network;

/// <summary>
/// A plain server message is sent as a bare string component (docs/modules/ui.md 8.6). Refusing that shape dropped every
/// such message, which is why this is pinned down by the exact bytes a server produced.
/// </summary>
public sealed class SystemChatShapeTests
{
    [Fact]
    public void ABareStringMessageIsRead()
    {
        // 08 = string tag, 000D = length 13, then the text, then the overlay flag.
        var body = BuildBareString("probe-tellraw", overlay: false);

        var status = ChatPackets.TryReadSystemChatStyled(
            body, out var runs, out var clicks, out var hovers, out var overlay);

        Assert.Equal(DecodeStatus.Success, status);
        Assert.Equal("probe-tellraw", string.Concat(runs.Select(run => run.Text)));
        Assert.Single(clicks);
        Assert.Single(hovers);
        Assert.False(overlay);
    }

    [Fact]
    public void TheOverlayFlagIsReadAfterABareString()
    {
        var body = BuildBareString("above", overlay: true);

        Assert.Equal(
            DecodeStatus.Success,
            ChatPackets.TryReadSystemChatStyled(body, out _, out _, out _, out var overlay));

        Assert.True(overlay);
    }

    private static byte[] BuildBareString(string text, bool overlay)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        var body = new byte[3 + bytes.Length + 1];
        body[0] = 8;
        body[1] = (byte)(bytes.Length >> 8);
        body[2] = (byte)(bytes.Length & 0xFF);
        bytes.CopyTo(body, 3);
        body[^1] = overlay ? (byte)1 : (byte)0;
        return body;
    }
}
