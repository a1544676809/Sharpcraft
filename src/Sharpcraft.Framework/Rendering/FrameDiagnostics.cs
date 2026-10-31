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

    /// <summary>Draw every chunk without the frustum test, the fallback when GPU culling is unavailable.</summary>
    public bool DisableCulling { get; init; }
}
