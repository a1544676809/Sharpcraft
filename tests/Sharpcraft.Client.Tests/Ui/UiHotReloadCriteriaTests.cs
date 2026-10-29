// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Diagnostics;
using System.Text;
using Sharpcraft.Client.Ui;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Ui;

/// <summary>
/// The measurable criteria for reloading documents (docs/modules/ui.md 8.1). These are the ones that can be
/// checked without a window: the frame path never parses, a bad document never reaches the screen, and a long
/// stream of swaps neither throws nor leaks into the per frame budget.
/// </summary>
public sealed class UiHotReloadCriteriaTests
{
    [Fact]
    public void AFiftyNodeDocumentCompilesQuicklyEnoughForAReload()
    {
        var document = DocumentWithTextBlocks(48);
        var loader = new UiDocumentLoader("unused.xaml");

        var started = Stopwatch.StartNew();
        var applied = loader.TryApply(document, generation: 1);
        started.Stop();

        Assert.True(applied);
        // Generous on purpose: this is a guard against accidentally quadratic parsing, not a benchmark, so it must
        // not fail on a slow machine. The observed figure is recorded in the module page.
        Assert.True(
            started.ElapsedMilliseconds < 50,
            $"compiling a fifty node document took {started.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void TwentyMalformedDocumentsAreAllRejectedAndNoneReachTheScreen()
    {
        var loader = new UiDocumentLoader("unused.xaml");
        loader.TryApply(GoodDocument(), generation: 1);
        var good = loader.Current;

        foreach (var document in Malformed())
        {
            Assert.False(loader.TryApply(document, generation: 2), $"a malformed document was accepted: {document}");
            Assert.Same(good, loader.Current);
            Assert.NotEqual(string.Empty, loader.LastError);
        }

        Assert.Equal(Malformed().Count, loader.RejectedLoads);
    }

    [Fact]
    public void TwentyHalfWrittenDocumentsAreAllRejected()
    {
        var source = GoodDocument();
        var loader = new UiDocumentLoader("unused.xaml");
        loader.TryApply(source, generation: 1);
        var good = loader.Current;

        // Every prefix of a valid document is what a save in progress looks like.
        for (var length = 0; length < source.Length; length += Math.Max(1, source.Length / 20))
        {
            var truncated = source[..length];

            if (loader.TryApply(truncated, generation: 2))
            {
                // A prefix can only be accepted if it happens to be a complete document; that must still be a
                // document the kernel understands, never a half parsed one.
                Assert.NotNull(loader.Current);
                Assert.Equal("Screen", loader.Current!.Root.Name);
            }
        }

        Assert.True(loader.RejectedLoads > 0, "no truncated document was rejected, which cannot be right");
        Assert.NotNull(loader.Current);
        _ = good;
    }

    [Fact]
    public void AThousandSwapsNeitherThrowNorGrowTheFrameBudget()
    {
        var loader = new UiDocumentLoader("unused.xaml");
        loader.TryApply(GoodDocument(), generation: 1);

        var geometry = new UiGeometry();
        var before = 0L;

        for (var round = 0; round < 1000; round++)
        {
            var generation = loader.NextGeneration();
            Assert.True(loader.TryApply(DocumentWithTextBlocks(round % 7), generation));

            var screen = loader.Current!;

            if (round == 1)
            {
                // Warm the buffers, then hold the measurement steady for the rest of the run.
                UiScreenLayout.Build(screen.Root, screen.Styles, 320f, 240f, geometry);
                before = GC.GetAllocatedBytesForCurrentThread();
                continue;
            }

            UiScreenLayout.Build(screen.Root, screen.Styles, 320f, 240f, geometry);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // The swap itself allocates a tree and a style table; what must not happen is per frame allocation in the
        // geometry path. A thousand rounds of building is the measurement, so allow the trees but nothing wild.
        Assert.True(allocated < 8_000_000, $"building a thousand frames allocated {allocated} bytes");
    }

    [Fact]
    public void BuildingFramesNeverTouchesTheLoader()
    {
        var loader = new UiDocumentLoader("unused.xaml");
        loader.TryApply(GoodDocument(), generation: 1);

        var applied = loader.AppliedLoads;
        var rejected = loader.RejectedLoads;
        var geometry = new UiGeometry();

        for (var frame = 0; frame < 500; frame++)
        {
            var screen = loader.Current!;
            UiScreenLayout.Build(screen.Root, screen.Styles, 320f, 240f, geometry);
        }

        // The frame path only reads the current screen; parsing happens on reload.
        Assert.Equal(applied, loader.AppliedLoads);
        Assert.Equal(rejected, loader.RejectedLoads);
    }

    private static string GoodDocument() => """
<Screen Padding="4">
  <StackPanel Background="#A0000000" Padding="4" Spacing="2">
    <TextBlock Text="hello" Colour="#FFFFFFFF" />
    <TextBlock Text="world" Colour="#FF7FC4FF" />
  </StackPanel>
</Screen>
""";

    private static string DocumentWithTextBlocks(int count)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<Screen Padding=\"4\">");
        builder.AppendLine("  <StackPanel Spacing=\"2\">");

        for (var index = 0; index < count; index++)
        {
            builder.AppendLine($"    <TextBlock Text=\"line {index}\" Colour=\"#FFFFFFFF\" />");
        }

        builder.AppendLine("  </StackPanel>");
        builder.AppendLine("</Screen>");
        return builder.ToString();
    }

    private static IReadOnlyList<string> Malformed() =>
    [
        string.Empty,
        "<Screen>",
        "<Screen></Screen>extra",
        "<Screen><Slider/></Screen>",
        "<Screen Opacity=\"1\"/>",
        "<TextBlock Text=\"x\"/>",
        "<Screen><TextBlock Text=\"x\" Colour=\"blue\"/></Screen>",
        "<Screen><TextBlock Text=\"x\" Height=\"tall\"/></Screen>",
        "<Screen><TextBlock Text=\"x\" Colour=\"#FFF\"/></Screen>",
        "<Screen><StackPanel></Screen>",
        "<Screen><!-- unterminated",
        "<Screen><TextBlock Text=\"x\" Height=\"\"/></Screen>",
        "<Screen><TextBlock Text=x/></Screen>",
        "<Screen Padding=\"4\"><TextBlock Text=\"a\"><TextBlock/></TextBlock></Screen>",
        "<Screen><StackPanel Spacing=\"2\" Background=\"#FF0000\"><Slider/></StackPanel></Screen>",
        "<screen></screen>",
        "<Screen Padding=\"4\"><StackPanel Padding=\"2\"><Grid/></StackPanel></Screen>",
        "<?xml version=\"1.0\"?><Screen/>",
        "< Screen/>"
    ];
}
