// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Ui;
using Sharpcraft.Common.Network;

namespace Sharpcraft.Launcher.Diagnostics;

/// <summary>
/// The single place where the host turns environment switches into plain values the game can use.
/// </summary>
/// <remarks>
/// Principle 13: the game assemblies read no environment, so deleting this file (and the folder) removes the debugging
/// behaviour without touching the game. Every setter here has a game-side default that means "debug off".
/// </remarks>
public static class HostDebugOptions
{
    /// <summary>Applies every switch that the host owns.</summary>
    public static void Apply()
    {
        MinecraftLoginClient.LoginNameOverride = Environment.GetEnvironmentVariable("MC_LOGIN_NAME");
        PlaySession.ChunkDumpDirectory = Environment.GetEnvironmentVariable("MC_DUMP_CHUNK_DIR");
        PackWatcher.IsEnabled = Environment.GetEnvironmentVariable(PackWatcher.SwitchName) == "1";

        if (double.TryParse(
                Environment.GetEnvironmentVariable("MC_TEXT_BACKGROUND_OPACITY"),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var opacity))
        {
            ChatLog.BackgroundOpacity = Math.Clamp((float)opacity, 0f, 1f);
        }
    }

    /// <summary>
    /// The frame options this host debug configuration asks for. Every field defaults to the plain frame, so a host that
    /// passes nothing renders exactly as production does.
    /// </summary>
    public static Sharpcraft.Framework.Rendering.FrameDiagnostics ForFrame()
    {
        static float Number(string name, float fallback) =>
            float.TryParse(Environment.GetEnvironmentVariable(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value
                : fallback;

        static bool Flag(string name) => Environment.GetEnvironmentVariable(name) == "1";

        return new Sharpcraft.Framework.Rendering.FrameDiagnostics
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
            DisableCulling = Flag("MC_CULL_DISABLE")
        };
    }

    /// <summary>True when the mesh queue should ignore the nearest-first ordering.</summary>
    public static bool FifoMeshOrder => Environment.GetEnvironmentVariable("MC_MESH_FIFO") == "1";
}
