// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// Extra outputs and tweaks a host may ask a frame for, all off by default.
/// </summary>
/// <remarks>
/// Principle 13: the renderer never reads the environment. The host parses its switches and fills this record, so dropping
/// the diagnostic features is a matter of not passing it.
/// </remarks>
public sealed record FrameDiagnostics
{
    /// <summary>Fog start distance in blocks.</summary>
    public float FogStart { get; init; } = 64f;

    /// <summary>Fog end distance in blocks.</summary>
    public float FogEnd { get; init; } = 160f;

    /// <summary>Read the depth buffer back with the colour, for culling comparisons.</summary>
    public bool ReadDepth { get; init; }

    /// <summary>Build the occlusion pyramid and compare it against the software one.</summary>
    public bool BuildPyramid { get; init; }

    /// <summary>Render and compare a motion vector target.</summary>
    public bool MotionVectors { get; init; }

    /// <summary>Horizontal shift applied to the motion vector comparison.</summary>
    public float MotionShiftX { get; init; }

    /// <summary>Draw the opaque pass with the solid shader even when the translucent layer is present.</summary>
    public bool AlphaOne { get; init; }

    /// <summary>Fixed GUI scale, or zero to pick the largest that fits.</summary>
    public int GuiScale { get; init; }

    /// <summary>Log every key event, for diagnosing injected input.</summary>
    public bool InputTrace { get; init; }

    /// <summary>Print frame time percentiles when the window closes.</summary>
    public bool PerformanceSummary { get; init; }

    /// <summary>Print how many chunks the frustum test skipped, once a second.</summary>
    public bool CullStatistics { get; init; }

    /// <summary>Compare consecutive frames pixel by pixel and report how far apart they are at exit.</summary>
    /// <remarks>
    /// Exists because a change to what the renderer records can be argued equal but rarely shown equal: the offscreen
    /// paths build one merged mesh and never touch the paged arena, and two separate runs of the windowed client differ
    /// in what the server has streamed, so neither can answer "did this change a pixel".
    /// <para>
    /// It reports two numbers. When the A/B group changed between the two frames, the difference is the change under
    /// test. When it did not, the same comparison measures the floor set by clouds, particles and everything else that
    /// keeps moving, which is the only thing a difference can be judged against. Both are taken from neighbouring
    /// frames of the same run, so scene motion affects each by the same amount.
    /// </para>
    /// <para>
    /// Off by default and free when off. On, it costs a full frame readback per frame, so a run with it enabled must not
    /// also be used for the frame timings; the readback lands in the frame preparation bucket and will inflate it.
    /// </para>
    /// </remarks>
    public bool PixelCompare { get; init; }

    /// <summary>Print the CPU cost of each recorded frame stage, once a second.</summary>
    /// <remarks>
    /// The stages are the ones the windowed loop can name on its own: geometry updates, surface acquire, camera sample,
    /// scene recording, the reference OIT stages, world text, clouds, interface and the submit/present tail. GPU time is
    /// not included, which is why the pass skips below exist: their frame time differences are the GPU attribution.
    /// </remarks>
    public bool FrameCpuTiming { get; init; }

    /// <summary>Skip the reference OIT stages, leaving the opaque and cutout passes for the measurement.</summary>
    /// <remarks>Measurement only: without the transparency resolve the picture is visibly wrong.</remarks>
    public bool SkipOit { get; init; }

    /// <summary>Skip the cloud draws, for measuring what the cloud passes cost.</summary>
    /// <remarks>Measurement only: clouds disappear from the picture.</remarks>
    public bool SkipClouds { get; init; }

    /// <summary>Skip translucent terrain geometry, for measuring what the transparency layers cost.</summary>
    /// <remarks>Measurement only: water and glass disappear from the picture.</remarks>
    public bool SkipTranslucent { get; init; }

    /// <summary>Skip the opaque and cutout terrain draws, for measuring what the terrain costs (T-801).</summary>
    /// <remarks>Measurement only: the world disappears and the sky remains.</remarks>
    public bool SkipTerrain { get; init; }

    /// <summary>Skip the entity draws, for measuring what they cost (T-801).</summary>
    /// <remarks>Measurement only: entities and their layers disappear.</remarks>
    public bool SkipEntities { get; init; }

    /// <summary>Skip the interface pass, for measuring what the UI costs (T-801).</summary>
    /// <remarks>Measurement only: the interface disappears.</remarks>
    public bool SkipInterface { get; init; }

    /// <summary>Skip the sky geometry (gradient sky, celestial bodies, stars), for measuring what it costs (T-801).</summary>
    /// <remarks>Measurement only: the frame shows the flat clear colour instead.</remarks>
    public bool SkipSky { get; init; }

    /// <summary>Load the colour and depth targets instead of clearing them (T-801).</summary>
    /// <remarks>Measurement only: the frame accumulates on top of the previous one, which is what the timing asks for.</remarks>
    public bool SkipClear { get; init; }

    /// <summary>Skip the copy of the finished frame into the swapchain image (T-801).</summary>
    /// <remarks>Measurement only: the window keeps showing the previous image.</remarks>
    public bool SkipPresentCopy { get; init; }

    /// <summary>Skip the celestial bodies (sun, moon, stars, sky discs), for measuring what they cost (T-807).</summary>
    /// <remarks>Measurement only: the gradient sky still draws.</remarks>
    public bool SkipCelestial { get; init; }

    /// <summary>
    /// Flips both A/B groups every N frames and labels each report line with its group, so a comparison happens inside one
    /// process and machine drift cancels out (T-801).
    /// </summary>
    /// <remarks>
    /// Zero disables it. Group A draws everything, group B skips what the skip switches ask for, so the difference between
    /// the two averages is the cost of the skipped work even when the machine slows down or speeds up mid-run.
    /// </remarks>
    public int AbToggleFrames { get; init; }

    /// <summary>Draw every chunk without the frustum test, the fallback when GPU culling is unavailable.</summary>
    public bool DisableCulling { get; init; }

    /// <summary>
    /// Which GPU to render on: null or <c>high</c> for the most powerful adapter, <c>low</c> for the power saving one,
    /// <c>list</c> to print every adapter, or a case insensitive substring of an adapter name.
    /// </summary>
    /// <remarks>
    /// The renderers resolve this once, before the device is created, and print the adapter they picked. A value that
    /// matches no adapter falls back to <c>high</c> with a warning, and an unavailable preference falls back to the
    /// binding's default request, so a single GPU machine is unaffected.
    /// </remarks>
    public string? Gpu { get; init; }

    /// <summary>
    /// Records only the first N reference transparency stages, for measuring what each one costs.
    /// </summary>
    /// <remarks>
    /// Zero means "no limit" and records the whole sequence, so the default frame is unchanged. Stages in order: 1 depth
    /// bounds, 2 opaque-depth cull, 3 transmittance, 4 accumulate, 5 composite. Stages that do not run leave the picture
    /// without transparency, which is expected: this is a measurement switch, not a rendering mode.
    /// </remarks>
    public int OitStages { get; init; }
}
