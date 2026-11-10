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
        Acquire,
        Camera,
        Scene,
        Oit,
        Text,
        Clouds,
        Ui,
        PresentRecord,
        Submit,
        Present,
        Count
    }

    private static readonly string[] Names =
    [
        "frame", "updates", "acquire", "camera", "scene", "oit", "text", "clouds", "ui", "present-rec", "submit",
        "present"
    ];

    private readonly long[] _windowTicks = new long[(int)Stage.Count];
    private readonly long[] _totalTicks = new long[(int)Stage.Count];
    private int _windowFrames;
    private int _frames;

    /// <summary>Records one stage of the current frame.</summary>
    /// <param name="stage">Stage number, as used by <see cref="Begin"/> and <see cref="Add"/>.</param>
    /// <param name="ticks">Elapsed stopwatch ticks for that stage.</param>
    public void Add(int stage, long ticks)
    {
        _windowTicks[stage] += ticks;
        _totalTicks[stage] += ticks;
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

        Console.WriteLine(builder.ToString());
        Array.Clear(_windowTicks);
        _windowFrames = 0;
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

    /// <summary>Stage number for the surface acquire and frame pacing block.</summary>
    public static int AcquireStage => (int)Stage.Acquire;

    /// <summary>Stage number for the camera sample.</summary>
    public static int CameraStage => (int)Stage.Camera;

    /// <summary>Stage number for the scene pass recording.</summary>
    public static int SceneStage => (int)Stage.Scene;

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

    /// <summary>Stage number for the swapchain present call.</summary>
    public static int PresentStage => (int)Stage.Present;

    /// <summary>Converts stopwatch ticks to milliseconds.</summary>
    /// <param name="ticks">Stopwatch ticks.</param>
    /// <returns>The same duration in milliseconds.</returns>
    public static double Milliseconds(long ticks) => ticks * 1000d / System.Diagnostics.Stopwatch.Frequency;
}
