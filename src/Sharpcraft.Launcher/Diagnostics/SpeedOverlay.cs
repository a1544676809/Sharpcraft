// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Ui;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Launcher.Diagnostics;

/// <summary>
/// Live walking speed readout, drawn over the frame so it can be compared with the reference client on screen.
/// </summary>
/// <remarks>
/// Principle 13: this lives in the host's diagnostics folder and is off unless the host enables it. The game is not
/// modified; the speed comes from the player position the session already exposes, sampled once per frame.
/// </remarks>
public sealed class SpeedOverlay
{
    private Vector3? _previous;
    private double _previousSeconds;
    private float _smoothed;

    /// <summary>True when the host asked for the overlay.</summary>
    public static bool Enabled => Environment.GetEnvironmentVariable("MC_SPEED_OVERLAY") == "1";

    /// <summary>Feeds one frame's player position.</summary>
    /// <param name="position">Current player position.</param>
    /// <param name="seconds">Seconds since the window started.</param>
    public void Update(Vector3 position, double seconds)
    {
        if (_previous is { } previous)
        {
            var elapsed = seconds - _previousSeconds;

            if (elapsed > 0.001)
            {
                // Horizontal only, which is what the reference reports for walking; vertical motion is falling, not speed.
                var dx = position.X - previous.X;
                var dz = position.Z - previous.Z;
                var instant = (float)(Math.Sqrt((dx * dx) + (dz * dz)) / elapsed);

                // A short exponential average so a single slow frame does not flicker the reading.
                _smoothed = _smoothed == 0f ? instant : (_smoothed * 0.8f) + (instant * 0.2f);
            }
        }

        _previous = position;
        _previousSeconds = seconds;
    }

    /// <summary>Draws the readout into the frame's UI geometry.</summary>
    /// <param name="geometry">UI geometry for this frame.</param>
    /// <param name="font">Font to draw with.</param>
    /// <param name="canvasHeight">Virtual canvas height, so the line sits above the hotbar.</param>
    public void Draw(UiGeometry geometry, BitmapFont font, float canvasHeight)
    {
        var text = $"speed {_smoothed:F2} b/s  (reference 4.32)";
        UiScreenLayout.AddText(geometry, font, text, 8f, canvasHeight - 30f, 1f, 0xFFFFD040u);
    }
}
