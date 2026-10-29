// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Ui;
using Sharpcraft.Common.Imaging;
using Sharpcraft.Common.Nbt;
using Sharpcraft.Common.Network;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Ui;

/// <summary>
/// Chat display: the fade curve, the history limit, and flattening a component tree into text
/// (docs/modules/ui.md 8.4).
/// </summary>
public sealed class ChatLogTests
{
    [Theory]
    [InlineData(0, 1f)]
    [InlineData(100, 1f)]
    [InlineData(179, 1f)]
    [InlineData(190, 0.25f)]
    [InlineData(195, 0.0625f)]
    [InlineData(200, 0f)]
    [InlineData(250, 0f)]
    public void TheFadeFollowsTheReferenceCurve(long age, float expected)
    {
        // Fully visible until the last tenth of the two hundred ticks, then falling away squared.
        Assert.Equal(expected, ChatLog.AlphaFor(age), 4);
    }

    [Fact]
    public void TheHistoryKeepsTheNewestHundredLines()
    {
        var log = new ChatLog();

        for (var index = 0; index < 150; index++)
        {
            log.Add($"line {index}", tick: index);
        }

        Assert.Equal(ChatLog.MaxHistory, log.Count);

        // The oldest fifty were dropped.
        var geometry = new UiGeometry();
        var drawn = log.Draw(geometry, Font(), screenHeight: 200f, nowTick: 149, fullyVisible: true);
        Assert.Equal(ChatLog.LinesPerPage, drawn);
    }

    [Fact]
    public void AnAgedLineDrawsNothing()
    {
        var log = new ChatLog();
        log.Add("old news", tick: 0);

        var geometry = new UiGeometry();
        var drawn = log.Draw(geometry, Font(), screenHeight: 200f, nowTick: ChatLog.FadeTicks + 1, fullyVisible: false);

        Assert.Equal(0, drawn);
        Assert.Equal(0, geometry.QuadCount);
    }

    [Fact]
    public void OpeningThePromptPinsTheLinesVisible()
    {
        var log = new ChatLog();
        log.Add("still here", tick: 0);

        var geometry = new UiGeometry();
        var drawn = log.Draw(geometry, Font(), screenHeight: 200f, nowTick: ChatLog.FadeTicks + 1, fullyVisible: true);

        Assert.Equal(1, drawn);
        Assert.True(geometry.QuadCount > 0);
    }

    [Fact]
    public void ALongLineWrapsInsteadOfRunningOffTheEdge()
    {
        var log = new ChatLog();
        log.Add(new string('m', 400), tick: 0);

        var geometry = new UiGeometry();
        var drawn = log.Draw(geometry, Font(), screenHeight: 200f, nowTick: 0, fullyVisible: true);

        // The column is three hundred and twenty pixels and each glyph advances six, so a four hundred character
        // line cannot be a single row.
        Assert.True(drawn > 1, $"expected the line to wrap, drew {drawn} rows");
    }

    [Fact]
    public void AComponentTreeFlattensToItsVisibleText()
    {
        var text = new NbtCompound();
        text.Set("text", new NbtString("hello "));

        var child = new NbtCompound();
        child.Set("text", new NbtString("world"));

        var extra = new NbtCompound();
        extra.Set("text", new NbtString("!"));

        var list = new NbtList(NbtTagType.Compound, [child, extra]);
        text.Set("extra", list);

        Assert.Equal("hello world!", ComponentText.Flatten(text));
    }

    [Fact]
    public void ATranslatedComponentFallsBackToItsKeyRatherThanGoingBlank()
    {
        var compound = new NbtCompound();
        compound.Set("translate", new NbtString("chat.type.text"));

        Assert.Equal("chat.type.text", ComponentText.Flatten(compound));
    }

    /// <summary>A sheet whose every glyph advances six pixels, so wrapping has something to measure.</summary>
    private static BitmapFont Font()
    {
        const int size = 128;
        const int cell = 8;
        var sheet = new RgbaImage(size, size, new byte[size * size * RgbaImage.BytesPerPixel]);

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                if (x % cell >= 5)
                {
                    continue;
                }

                sheet.Pixels[(((y * size) + x) * RgbaImage.BytesPerPixel) + 3] = 255;
            }
        }

        return BitmapFont.FromSheet(sheet);
    }
}
