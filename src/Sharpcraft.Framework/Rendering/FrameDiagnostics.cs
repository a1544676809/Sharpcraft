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

    /// <summary>
    /// Write GPU timestamps around every frame stage and print them once a second.
    /// </summary>
    /// <remarks>
    /// Diagnostic only (T-800): with the switch off no query set exists and nothing is written. The device requests the
    /// timestamp query feature only when this is set, and an adapter without that feature logs one line and continues.
    /// </remarks>
    public bool GpuTimings { get; init; }
}
