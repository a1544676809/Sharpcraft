// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Network;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Ui;

/// <summary>
/// The large centred title and its subtitle, which the title command shows (docs/modules/ui.md 8.7).
/// </summary>
/// <remarks>
/// Unlike the action bar this has no backdrop: the reference draws it as plain white text, once at four times the glyph
/// size and the subtitle at twice, both centred on the middle of the screen. It fades in, holds, then fades out.
/// </remarks>
public sealed class Title
{
    /// <summary>Ticks the title takes to appear.</summary>
    public const int FadeInTicks = 10;

    /// <summary>Ticks the title holds at full opacity.</summary>
    public const int StayTicks = 70;

    /// <summary>Ticks the title takes to disappear.</summary>
    public const int FadeOutTicks = 20;

    /// <summary>Total lifetime in ticks.</summary>
    public const int LifetimeTicks = FadeInTicks + StayTicks + FadeOutTicks;

    /// <summary>Scale of the title.</summary>
    public const float TitleScale = 4f;

    /// <summary>Scale of the subtitle.</summary>
    public const float SubtitleScale = 2f;

    /// <summary>Distance above the centre the title is drawn at, before scaling.</summary>
    public const float TitleOffset = -10f;

    /// <summary>Distance below the centre the subtitle is drawn at, before scaling.</summary>
    public const float SubtitleOffset = 5f;

    private TextRun[] _title = [];
    private TextRun[] _subtitle = [];
    private int _remaining;

    /// <summary>True while anything is on screen.</summary>
    public bool IsVisible => _remaining > 0 && (_title.Length > 0 || _subtitle.Length > 0);

    /// <summary>Ticks left.</summary>
    public int RemainingTicks => _remaining;

    /// <summary>Shows a title and restarts the lifetime.</summary>
    /// <param name="runs">Title text.</param>
    public void Show(ReadOnlySpan<TextRun> runs)
    {
        _title = runs.ToArray();
        _subtitle = [];
        _remaining = LifetimeTicks;
    }

    /// <summary>Shows a subtitle without disturbing the title, which is how the command sends them.</summary>
    /// <param name="runs">Subtitle text.</param>
    public void ShowSubtitle(ReadOnlySpan<TextRun> runs)
    {
        _subtitle = runs.ToArray();

        if (_remaining <= 0)
        {
            _remaining = LifetimeTicks;
        }
    }

    /// <summary>Removes everything immediately.</summary>
    public void Clear()
    {
        _title = [];
        _subtitle = [];
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
    /// Opacity for the current tick: rising during the fade in, full while it holds, falling during the fade out.
    /// </summary>
    /// <param name="remaining">Ticks left.</param>
    public static float AlphaFor(int remaining)
    {
        if (remaining <= 0)
        {
            return 0f;
        }

        if (remaining > FadeOutTicks + StayTicks)
        {
            // Still fading in: the elapsed time is the whole lifetime minus what is left.
            var elapsed = LifetimeTicks - remaining;
            return Math.Clamp(elapsed / (float)FadeInTicks, 0f, 1f);
        }

        if (remaining <= FadeOutTicks)
        {
            return Math.Clamp(remaining / (float)FadeOutTicks, 0f, 1f);
        }

        return 1f;
    }

    /// <summary>Adds the title and subtitle to the interface geometry.</summary>
    /// <param name="geometry">Receives the quads.</param>
    /// <param name="font">Glyph metrics.</param>
    /// <param name="canvasWidth">Width of the interface canvas.</param>
    /// <param name="canvasHeight">Height of the interface canvas.</param>
    public void Draw(UiGeometry geometry, BitmapFont font, float canvasWidth, float canvasHeight)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(font);

        var alpha = AlphaFor(_remaining);

        if (alpha <= 0f)
        {
            return;
        }

        var centreX = canvasWidth / 2f;
        var centreY = canvasHeight / 2f;

        DrawCentred(geometry, font, _title, centreX, centreY, TitleOffset, TitleScale, alpha);
        DrawCentred(geometry, font, _subtitle, centreX, centreY, SubtitleOffset, SubtitleScale, alpha);
    }

    private static void DrawCentred(
        UiGeometry geometry,
        BitmapFont font,
        TextRun[] runs,
        float centreX,
        float centreY,
        float offset,
        float scale,
        float alpha)
    {
        if (runs.Length == 0)
        {
            return;
        }

        var width = 0f;

        foreach (var run in runs)
        {
            width += font.MeasureWidth(run.Text) * scale;
        }

        var pen = MathF.Floor(centreX - (width / 2f));
        var top = centreY + (offset * scale);

        // No backdrop: the reference draws the title as plain text.
        foreach (var run in runs)
        {
            UiScreenLayout.AddText(
                geometry,
                font,
                run.Text,
                pen,
                top,
                scale,
                WithAlpha(run.Colour, alpha),
                shadow: true);

            pen += font.MeasureWidth(run.Text) * scale;
        }
    }

    private static uint WithAlpha(uint colour, float alpha)
    {
        var byteAlpha = (byte)Math.Clamp(alpha * 255f, 0f, 255f);
        return (colour & 0x00FFFFFFu) | ((uint)byteAlpha << 24);
    }
}
