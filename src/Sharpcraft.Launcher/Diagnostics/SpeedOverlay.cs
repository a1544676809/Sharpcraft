// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Ui;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Launcher.Diagnostics;

/// <summary>
/// Walking speed readout, computed the way the reference debug screen does it.
/// </summary>
/// <remarks>
/// <para>
/// The reference keeps <c>lastKnownSpeed</c> as the difference between the current position and the position at the end of
/// the previous tick, then shows its length in blocks per tick. It is sampled once per tick and never smoothed, so the
/// figure holds steady while walking instead of flickering with the frame rate.
/// </para>
/// <para>Principle 13: this lives in the host diagnostics folder and is off unless the host enables it.</para>
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("sharpcraft.SpeedOverlay", "ref://mc-26.3-analysis/src", Kind = Sharpcraft.Common.Reference.ReferenceKind.New, Status = Sharpcraft.Common.Reference.ReferenceStatus.Full, Note = "Sharpcraft host/infra; no reference counterpart")]
public sealed class SpeedOverlay
{
    private long _lastTick = -1;
    private Vector3 _lastTickPosition;
    private float _blocksPerTick;

    /// <summary>True when the host asked for the overlay.</summary>
    public static bool Enabled => Environment.GetEnvironmentVariable("MC_SPEED_OVERLAY") == "1";

    /// <summary>Records the position at the end of a tick, mirroring the reference <c>computeSpeed</c>.</summary>
    /// <param name="position">Position after this tick.</param>
    /// <param name="tickIndex">Session tick counter; the sample is taken only when it advances.</param>
    public void Observe(Vector3 position, long tickIndex)
    {
        if (tickIndex == _lastTick)
        {
            return;
        }

        if (_lastTick >= 0)
        {
            _blocksPerTick = (position - _lastTickPosition).Length();
        }

        _lastTick = tickIndex;
        _lastTickPosition = position;
    }

    /// <summary>Draws the readout in the same units as the reference: blocks per tick.</summary>
    /// <param name="geometry">UI geometry for this frame.</param>
    /// <param name="font">Font to draw with.</param>
    /// <param name="canvasHeight">Virtual canvas height, so the line sits above the hotbar.</param>
    public void Draw(UiGeometry geometry, BitmapFont font, float canvasHeight)
    {
        var text = $"Speed: {_blocksPerTick:F3} blocks/tick  ({_blocksPerTick * 20f:F2} b/s)";
        UiScreenLayout.AddText(geometry, font, text, 8f, canvasHeight - 30f, 1f, 0xFFFFD040u);
    }
}
