// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// Per frame CPU cost of the stages the windowed loop records, accumulated for the diagnostic line.
/// </summary>
/// <remarks>
/// Principle 13: this type is only referenced from the windowed loop, only reports when
/// <see cref="FrameDiagnostics.FrameCpuTiming"/> is set, and can be deleted together with those two call sites without
/// changing what the renderer draws. GPU time is deliberately absent: it cannot be measured per stage without timestamp
/// queries, so the pass skips in <see cref="FrameDiagnostics"/> answer that part by differencing frame time.
/// </remarks>
internal sealed class FrameCpuTimings
{
    /// <summary>The stages the loop can time without touching the backend.</summary>
    private enum Stage
    {
        Frame,
        Updates,
        PrepPump,
        PrepUi,
        PrepEntities,
        Acquire,
        Camera,
        Scene,
        SceneOpen,
        SceneSky,
        SceneCelestial,
        CelBuild,
        CelCamera,
        CelDisc,
        SceneTerrain,
        SceneEntities,
        Oit,
        Text,
        Clouds,
        Ui,
        PresentRecord,
        Submit,
        SubmitFinish,
        SubmitQueue,
        Swap,
        OnFrame,
        FrameGap,
        PipelineWait,
        Count
    }

    private static readonly string[] Names =
    [
        // The order MUST match the Stage enum, because the report prints by index. It did not before T-817: the labels
        // scene-terrain, scene-entities, cel-build, cel-camera and cel-disc were rotated by three against their buckets,
        // so every quoted cel-* figure before this change names the wrong stage.
        "frame", "updates", "prep-pump", "prep-ui", "prep-entities", "acquire", "camera", "scene", "scene-open",
        "scene-sky", "scene-celestial", "cel-build", "cel-camera", "cel-disc", "scene-terrain", "scene-entities",
        "oit", "text", "clouds", "ui", "present-rec", "submit", "sub-finish", "sub-queue", "swap", "onframe", "frame-gap",
        "pipeline-wait"
    ];

    private readonly long[] _windowTicks = new long[(int)Stage.Count];
    private readonly long[] _totalTicks = new long[(int)Stage.Count];
    private int _windowFrames;
    private int _frames;

    /// <summary>
    /// The thread that recorded the first stage of the window, and whether any stage since came from another.
    /// </summary>
    /// <remarks>
    /// Once recording moves to its own thread the stages no longer share a clock or a call stack, and a
    /// figure that silently mixed two threads would read as one thread doing all the work. Nothing records
    /// from a second thread yet; the flag exists so the moment that changes the report says so instead of
    /// quietly lying (T-837).
    /// </remarks>
    private int _ownerThreadId = -1;

    private bool _mixedThreads;

    /// <summary>Records one stage of the current frame.</summary>
    /// <param name="stage">Stage number, as used by <see cref="Begin"/> and <see cref="Add"/>.</param>
    /// <param name="ticks">Elapsed stopwatch ticks for that stage.</param>
    public void Add(int stage, long ticks)
    {
        var thread = Environment.CurrentManagedThreadId;
        if (_ownerThreadId < 0)
        {
            _ownerThreadId = thread;
        }
        else if (_ownerThreadId != thread)
        {
            _mixedThreads = true;
        }

        _windowTicks[stage] += ticks;
        _totalTicks[stage] += ticks;
    }

    private readonly long[] _abTicks = new long[2];
    private readonly int[] _abFrames = new int[2];

    /// <summary>
    /// Records one A/B sample of a span, tagged with the group the frame belonged to.
    /// </summary>
    /// <param name="group">A/B group: 0 for the normal behaviour, 1 for the toggled one.</param>
    /// <param name="ticks">Elapsed ticks for the span being compared.</param>
    /// <remarks>
    /// Comparing two runs has produced a misleading answer three times in this work, because the world differs
    /// between runs by more than the effect being measured. Alternating inside one process keeps the scene fixed
    /// for both halves, which is the only way the delta means anything (T-828).
    /// </remarks>
    public void AddAbSample(int group, long ticks)
    {
        if ((uint)group >= 2u)
        {
            return;
        }

        _abTicks[group] += ticks;
        _abFrames[group]++;
    }

    private void WriteAbSamples()
    {
        if (_abFrames[0] == 0 || _abFrames[1] == 0)
        {
            return;
        }

        var normal = Milliseconds(_abTicks[0] / _abFrames[0]);
        var toggled = Milliseconds(_abTicks[1] / _abFrames[1]);
        Console.WriteLine(
            $"ab-cpu: n={_abFrames[0]}/{_abFrames[1]} normal={normal:F3} toggled={toggled:F3} delta={(toggled - normal):F3}");
        Array.Clear(_abTicks);
        Array.Clear(_abFrames);
    }

    /// <summary>Counts one frame into the window.</summary>
    public void EndFrame()
    {
        _windowFrames++;
        _frames++;
    }

    /// <summary>Prints the mean of the frames since the last print, then starts a new window.</summary>
    /// <param name="suffix">Extra fields to append, or null for none.</param>
    /// <returns>True when a line was printed, false when the window was empty.</returns>
    public bool WriteLine(string? suffix)
    {
        if (_windowFrames == 0)
        {
            return false;
        }

        var builder = new System.Text.StringBuilder("frame-cpu:");
        builder.Append(" n=").Append(_windowFrames);

        for (var i = 0; i < (int)Stage.Count; i++)
        {
            builder.Append(' ').Append(Names[i]).Append('=').Append(Milliseconds(_windowTicks[i] / _windowFrames).ToString("F3"));
        }

        if (suffix is not null)
        {
            builder.Append(' ').Append(suffix);
        }

        builder.Append(_mixedThreads ? " threads=mixed" : " threads=one");

        Console.WriteLine(builder.ToString());
        WriteAbSamples();
        Array.Clear(_windowTicks);
        _windowFrames = 0;
        _mixedThreads = false;
        return true;
    }

    /// <summary>Prints the whole run's mean, for the shut down summary.</summary>
    public void WriteSummary()
    {
        if (_frames == 0)
        {
            return;
        }

        var builder = new System.Text.StringBuilder($"window: cpu per frame over {_frames} frames:");

        for (var i = 0; i < (int)Stage.Count; i++)
        {
            builder.Append(' ').Append(Names[i]).Append('=').Append(Milliseconds(_totalTicks[i] / _frames).ToString("F3"));
        }

        builder.Append(" ms");
        Console.WriteLine(builder.ToString());
    }

    /// <summary>Stage number for the whole frame, used as the denominator of the report.</summary>
    public static int FrameStage => (int)Stage.Frame;

    /// <summary>Stage number for the geometry update block.</summary>
    public static int UpdatesStage => (int)Stage.Updates;

    /// <summary>Stage number for the encoder finish inside the submit (T-819).</summary>
    public static int SubmitFinishStage => (int)Stage.SubmitFinish;

    /// <summary>Stage number for the queue submit inside the submit (T-819).</summary>
    public static int SubmitQueueStage => (int)Stage.SubmitQueue;

    /// <summary>Stage number for the diagnostic report, the window pump and the loop head (T-817).</summary>
    public static int PrepPumpStage => (int)Stage.PrepPump;

    /// <summary>Time from the end of the previous frame to just before the window pump.</summary>
    public static int FrameGapStage => (int)Stage.FrameGap;

    /// <summary>
    /// Time the submitting side spent waiting for the recording side to hand over a frame (T-837).
    /// </summary>
    /// <remarks>
    /// Zero while recording and submitting share a thread, which they still do. It becomes the number that
    /// decides whether the split earned anything: if it grows to match the recording time, the two sides are
    /// not overlapping and the pipeline is a failure, not a saving.
    /// </remarks>
    public static int PipelineWaitStage => (int)Stage.PipelineWait;

    /// <summary>Stage number for the interface upload and its orthographic matrix (T-817).</summary>
    public static int PrepUiStage => (int)Stage.PrepUi;

    /// <summary>Stage number for the dynamic and entity geometry uploads (T-817).</summary>
    public static int PrepEntitiesStage => (int)Stage.PrepEntities;

    /// <summary>Stage number for the surface acquire and frame pacing block.</summary>
    public static int AcquireStage => (int)Stage.Acquire;

    /// <summary>Stage number for the camera sample.</summary>
    public static int CameraStage => (int)Stage.Camera;

    /// <summary>Stage number for the scene pass recording.</summary>
    public static int SceneStage => (int)Stage.Scene;

    /// <summary>Opening the scene pass, writing the frame uniforms and setting up the frustum (T-806).</summary>
    public static int SceneOpenStage => (int)Stage.SceneOpen;

    /// <summary>The gradient sky draw (T-806).</summary>
    public static int SceneSkyStage => (int)Stage.SceneSky;

    /// <summary>The sun, moon and stars (T-806).</summary>
    public static int SceneCelestialStage => (int)Stage.SceneCelestial;

    /// <summary>The opaque and cutout terrain draws (T-806).</summary>
    public static int SceneTerrainStage => (int)Stage.SceneTerrain;

    /// <summary>The entity layer draws (T-806).</summary>
    public static int SceneEntitiesStage => (int)Stage.SceneEntities;

    /// <summary>Building the celestial draw lists for this frame (T-807).</summary>
    public static int CelestialBuildStage => (int)Stage.CelBuild;

    /// <summary>Moving the celestial draw lists with the camera delta (T-807).</summary>
    public static int CelestialCameraStage => (int)Stage.CelCamera;

    /// <summary>The vanilla sky disc state update, which rewrites its uniforms (T-807).</summary>
    public static int CelestialDiscStage => (int)Stage.CelDisc;

    /// <summary>Stage number for the reference OIT recording.</summary>
    public static int OitStage => (int)Stage.Oit;

    /// <summary>Stage number for the world text pass.</summary>
    public static int TextStage => (int)Stage.Text;

    /// <summary>Stage number for the cloud pass.</summary>
    public static int CloudsStage => (int)Stage.Clouds;

    /// <summary>Stage number for the interface pass.</summary>
    public static int UiStage => (int)Stage.Ui;

    /// <summary>Stage number for the present copy recording.</summary>
    public static int PresentRecordStage => (int)Stage.PresentRecord;

    /// <summary>Stage number for the command submission.</summary>
    public static int SubmitStage => (int)Stage.Submit;

    /// <summary>Surface present plus the acquired image release: the wait the swapchain imposes (T-803).</summary>
    public static int SwapStage => (int)Stage.Swap;

    /// <summary>The host's per-frame callback, which used to sit inside the present bucket (T-803).</summary>
    public static int OnFrameStage => (int)Stage.OnFrame;

    /// <summary>Converts stopwatch ticks to milliseconds.</summary>
    /// <param name="ticks">Stopwatch ticks.</param>
    /// <returns>The same duration in milliseconds.</returns>
    public static double Milliseconds(long ticks) => ticks * 1000d / System.Diagnostics.Stopwatch.Frequency;
}
