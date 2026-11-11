// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Game;

/// <summary>
/// Explicit, host-provided run options for the game library (AGENTS.md principle 13).
/// </summary>
/// <remarks>
/// The game assemblies must not read the environment. The host (launcher, dev tool or the client's own
/// <c>--debugOptionsFile</c>) interprets its configuration once, before any game code runs, and hands the result
/// here. The snapshot is read-only afterwards; a game process that is given nothing behaves exactly like production.
/// </remarks>
internal sealed class GameOptions
{
    private static readonly GameOptions Empty = new(new Dictionary<string, string>(StringComparer.Ordinal));

    private readonly IReadOnlyDictionary<string, string> _values;

    private GameOptions(IReadOnlyDictionary<string, string> values) => _values = values;

    /// <summary>The current read-only snapshot. Empty until <see cref="Configure"/> runs.</summary>
    internal static GameOptions Current { get; private set; } = Empty;

    /// <summary>Replaces the snapshot once, from the host's explicit configuration.</summary>
    /// <exception cref="InvalidOperationException">The snapshot was already configured.</exception>
    internal static void Configure(IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (!ReferenceEquals(Current, Empty))
        {
            throw new InvalidOperationException("GameOptions is configured exactly once, before any game code runs");
        }

        Current = new GameOptions(new Dictionary<string, string>(values, StringComparer.Ordinal));
    }

    /// <summary>Returns the configured text value, or null when the host did not set it.</summary>
    internal static string? Get(string name) => Current._values.TryGetValue(name, out var value) ? value : null;

    /// <summary>True when the host set the switch to the canonical <c>"1"</c>.</summary>
    internal static bool Flag(string name) => Get(name) == "1";

    /// <summary>
    /// The frame diagnostics the host asked for. Every field defaults to the plain frame, so a host that passes
    /// nothing renders exactly as production does.
    /// </summary>
    internal static FrameDiagnostics ForFrame()
    {
        static float Number(string name, float fallback) =>
            float.TryParse(Get(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;

        return new FrameDiagnostics
        {
            FogStart = Number("MC_FOG_START", 64f),
            FogEnd = Number("MC_FOG_END", 160f),
            ReadDepth = Flag("MC_FRAME_DEPTH"),
            BuildPyramid = Flag("MC_FRAME_PYRAMID"),
            MotionVectors = Flag("MC_FRAME_MOTION"),
            MotionShiftX = Number("MC_FRAME_MOTION_DX", 0f),
            AlphaOne = Flag("MC_FRAME_ALPHA_ONE"),
            GuiScale = (int)Number("MC_GUI_SCALE", 0f),
            InputTrace = Flag("MC_INPUT_TRACE"),
            PerformanceSummary = Flag("MC_PERF_SUMMARY"),
            CullStatistics = Flag("MC_CULL_STATS"),
            DisableCulling = Flag("MC_CULL_DISABLE"),
            FrameCpuTiming = Flag("MC_FRAME_CPU"),
            SkipOit = Flag("MC_SKIP_OIT"),
            SkipClouds = Flag("MC_SKIP_CLOUDS"),
            SkipTranslucent = Flag("MC_SKIP_TRANSLUCENT"),
            Gpu = Get("MC_GPU")
        };
    }
}
