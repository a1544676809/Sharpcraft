// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Network;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Ui;

/// <summary>
/// The transient line a server shows above the hotbar, which is what the overlay flag on a system message means
/// (docs/modules/ui.md 8.4).
/// </summary>
/// <remarks>
/// It is not a chat line: it is centred, it has no history, it has no backdrop either - the reference draws plain text
/// there - and it disappears on its own after three seconds.
/// </remarks>
public sealed class ActionBar
{
    /// <summary>Ticks a message stays before it has fully faded.</summary>
    public const int LifetimeTicks = 60;

    /// <summary>Ticks the fade itself takes; the rest of the lifetime is at full opacity.</summary>
    public const int FadeTicks = 20;

    /// <summary>Distance from the bottom of the screen to the message.</summary>
    public const float BottomInset = 68f;

    /// <summary>Height of one line, used for the backdrop.</summary>
    public const float LineHeight = 9f;

    private TextRun[] _runs = [];
    private int _remaining;

    /// <summary>True while something is on screen.</summary>
    public bool IsVisible => _remaining > 0 && _runs.Length > 0;

    /// <summary>
    /// True when the text colour cycles instead of being white.
    /// </summary>
    /// <remarks>
    /// The reference's <c>set_overlay_message</c> arrives with <c>animate = true</c>, so the action bar cycles hue from the
    /// moment it appears. Drawing it white is a visible difference, which is why this defaults to true.
    /// </remarks>
    public bool AnimateColour { get; set; } = true;

    /// <summary>Ticks left before the message has faded out.</summary>
    public int RemainingTicks => _remaining;

    /// <summary>Replaces the message and restarts its life.</summary>
    /// <param name="runs">Text to show, already flattened.</param>
    public void Set(ReadOnlySpan<TextRun> runs)
    {
        if (runs.Length == 0)
        {
            Clear();
            return;
        }

        _runs = runs.ToArray();
        _remaining = LifetimeTicks;
    }

    /// <summary>Forgets the message immediately.</summary>
    public void Clear()
    {
        _runs = [];
        _remaining = 0;
    }

    /// <summary>Advances one tick.</summary>
    public void Tick()
    {
        if (_remaining > 0)
        {
            _remaining--;
        }
    }

    /// <summary>
    /// Opacity for the current tick, which is full until the last second and then falls linearly.
    /// </summary>
    /// <param name="remaining">Ticks left.</param>
    /// <remarks>
    /// The reference computes the alpha as <c>remaining * 255 / 20</c> and clamps it, so anything above twenty ticks is
    /// fully opaque and the fade occupies the final twenty.
    /// </remarks>
    public static float AlphaFor(int remaining)
    {
        if (remaining <= 0)
        {
            return 0f;
        }

        var alpha = remaining * 255f / FadeTicks;

        return alpha > 255f ? 1f : alpha / 255f;
    }

    /// <summary>Adds the message to the interface geometry, centred above the hotbar.</summary>
    /// <param name="geometry">Receives the quads.</param>
    /// <param name="font">Glyph metrics.</param>
    /// <param name="canvasWidth">Width of the interface canvas.</param>
    /// <param name="canvasHeight">Height of the interface canvas.</param>
    /// <summary>
    /// Cycles the colour the way the reference does: hue advances with elapsed ticks, saturation 0.7, value 0.6.
    /// </summary>
    /// <param name="ticks">Ticks since the message appeared.</param>
    /// <param name="alpha">Alpha in the range zero to one.</param>
    public static uint CycledColour(int ticks, float alpha) =>
        HsvToArgb(ticks / 50f, 0.7f, 0.6f, alpha);

    /// <summary>Converts HSV to packed ARGB, matching the reference's own conversion.</summary>
    /// <param name="hue">Hue; the whole part is discarded.</param>
    /// <param name="saturation">Saturation, zero to one.</param>
    /// <param name="value">Value, zero to one.</param>
    /// <param name="alpha">Alpha, zero to one.</param>
    public static uint HsvToArgb(float hue, float saturation, float value, float alpha)
    {
        var h = hue - MathF.Floor(hue);
        var i = (int)(h * 6f) % 6;
        var f = (h * 6f) - MathF.Floor(h * 6f);
        var p = value * (1f - saturation);
        var q = value * (1f - (saturation * f));
        var t = value * (1f - (saturation * (1f - f)));

        var (red, green, blue) = i switch
        {
            0 => (value, t, p),
            1 => (q, value, p),
            2 => (p, value, t),
            3 => (p, q, value),
            4 => (t, p, value),
            _ => (value, p, q)
        };

        var a = (uint)Math.Clamp((int)((alpha * 255f) + 0.5f), 0, 255);
        var r = (uint)Math.Clamp((int)((red * 255f) + 0.5f), 0, 255);
        var g = (uint)Math.Clamp((int)((green * 255f) + 0.5f), 0, 255);
        var b = (uint)Math.Clamp((int)((blue * 255f) + 0.5f), 0, 255);

        return (a << 24) | (r << 16) | (g << 8) | b;
    }

    public void Draw(UiGeometry geometry, BitmapFont font, float canvasWidth, float canvasHeight)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(font);

        var alpha = AlphaFor(_remaining);

        if (alpha <= 0f || _runs.Length == 0)
        {
            return;
        }

        var width = 0f;

        foreach (var run in _runs)
        {
            width += font.MeasureWidth(run.Text);
        }

        var left = MathF.Floor((canvasWidth - width) / 2f);
        var top = canvasHeight - BottomInset - 4f;

        var pen = left;

        foreach (var run in _runs)
        {
            UiScreenLayout.AddText(
                geometry,
                font,
                run.Text,
                pen,
                top,
                1f,
                AnimateColour
                    ? CycledColour(LifetimeTicks - _remaining, alpha * (byte.MaxValue / 255f))
                    : Pack(run.Colour, alpha * (byte.MaxValue / 255f)),
                shadow: false);

            pen += font.MeasureWidth(run.Text);
        }
    }

    private static uint Pack(uint argb, float alpha)
    {
        var byteAlpha = (byte)Math.Clamp(alpha * 255f, 0f, 255f);
        return (argb & 0x00FFFFFFu) | ((uint)byteAlpha << 24);
    }
}
