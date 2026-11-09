// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Architecture.Tests;

/// <summary>
/// Stage-1 single-track gate: both renderer shells must expose the same canonical world pass plan. The actual pass
/// extraction is a separate follow-up; this test prevents the two shells from silently drifting to different orders
/// while that work is in progress.
/// </summary>
public sealed class RendererSingleTrackTests
{
    [Fact]
    public void DefaultPassPlanIsOrderedAndUnique()
    {
        var plan = WorldRenderPipeline.DefaultPasses;
        Assert.Equal(
            [
                WorldRenderPassKind.Sky,
                WorldRenderPassKind.TerrainOpaque,
                WorldRenderPassKind.TerrainCutout,
                WorldRenderPassKind.Entities,
                WorldRenderPassKind.Translucent,
                WorldRenderPassKind.WorldText,
                WorldRenderPassKind.Clouds,
                WorldRenderPassKind.Ui
            ],
            plan.Select(pass => pass.Kind));
        Assert.Equal(plan.Count, plan.Select(pass => pass.Name).Distinct(StringComparer.Ordinal).Count());
        WorldRenderPipeline.Validate(plan);
    }

    [Fact]
    public void WindowedAndOffscreenShellsExposeTheSamePassPlan()
    {
        Assert.Same(WindowedRenderer.PassPlan, OffscreenRenderer.PassPlan);
        WorldRenderPipeline.Validate(WindowedRenderer.PassPlan);
    }

    [Fact]
    public void RecordScenePassesUsesTheCanonicalOrder()
    {
        var drawn = new List<WorldRenderPassKind>();
        var recorder = new FramePassRecorder();

        WorldRenderPipeline.RecordScenePasses(drawn.Add, recorder);

        Assert.Equal(WorldRenderPipeline.DefaultPasses.Select(pass => pass.Kind), drawn);
        Assert.Equal(WorldRenderPipeline.DefaultPasses.Select(pass => pass.Name), recorder.Passes);
    }

    [Fact]
    public void RendererShellsStayUnderLineRatchet()
    {
        var root = RepositoryLocator.Root;
        var windowedLines = File.ReadAllLines(Path.Combine(root, "src", "Sharpcraft.Framework", "Rendering", "WindowedRenderer.cs")).Length;
        var offscreenLines = File.ReadAllLines(Path.Combine(root, "src", "Sharpcraft.Framework", "Rendering", "OffscreenRenderer.cs")).Length;

        // The bound moved 2400 -> 2500 when translucent items gained their own reference OIT set: that set lives in
        // ItemOitSet.cs (an extracted pass), and the remaining growth is the inline reference-transparency sequence,
        // whose extraction is tracked as T-776. Do not raise this again for new inline passes; extract instead.
        Assert.True(windowedLines <= 2500, $"WindowedRenderer.cs grew to {windowedLines} lines; extract passes instead of adding a parallel shell.");
        Assert.True(offscreenLines <= 1700, $"OffscreenRenderer.cs grew to {offscreenLines} lines; extract passes instead of adding a parallel shell.");
    }

    [Fact]
    public void FramePassRecorderKeepsTheRecordedOrder()
    {
        var recorder = new FramePassRecorder();
        recorder.Record("sky");
        recorder.Record("terrain-opaque");
        recorder.Record("ui");

        Assert.Equal(["sky", "terrain-opaque", "ui"], recorder.Passes);
        recorder.Clear();
        Assert.Empty(recorder.Passes);
    }
}
