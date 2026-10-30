// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Ui;

/// <summary>
/// The chat prompt box at the foot of the screen (docs/modules/ui.md 8.4).
/// </summary>
/// <remarks>
/// Placement is expressed against the canvas height every frame, never against a size captured at startup, because
/// the interface scale changes with the window and a captured height leaves the box floating somewhere above the
/// bottom after a resize.
/// </remarks>
public static class ChatPrompt
{
    /// <summary>Horizontal inset of the box.</summary>
    public const float InsetX = 2f;

    /// <summary>Height of the box.</summary>
    public const float Height = 12f;

    /// <summary>Distance from the bottom edge of the canvas to the bottom of the box.</summary>
    public const float BottomInset = 2f;

    /// <summary>Background of the box.</summary>
    public const uint Background = 0x80000000u;

    /// <summary>Ink colour.</summary>
    public const uint Ink = 0xFFFFFFFFu;

    /// <summary>Caret colour.</summary>
    public const uint Caret = 0xFFE0E0E0u;

    /// <summary>Draws the prompt.</summary>
    /// <param name="geometry">Receives the quads.</param>
    /// <param name="font">Glyph metrics, or null to draw only the box.</param>
    /// <param name="canvasWidth">Virtual width.</param>
    /// <param name="canvasHeight">Virtual height.</param>
    /// <param name="model">Editing state of the prompt.</param>
    /// <param name="elapsedMs">Milliseconds since the prompt gained focus, which drives the caret blink.</param>
    public static void Draw(
        UiGeometry geometry,
        BitmapFont? font,
        float canvasWidth,
        float canvasHeight,
        TextEditModel model,
        long elapsedMs)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(model);

        var top = canvasHeight - BottomInset - Height;
        geometry.AddRectangle(InsetX, top, canvasWidth - (InsetX * 2f), Height, Background);

        if (font is null)
        {
            return;
        }

        // The prompt always has focus while it is open, which is what makes the caret blink.
        // The reference centres the eight pixel glyph box in a twelve pixel field, so the text sits two pixels down.
        TextEditRenderer.Draw(geometry, font, model, InsetX + 2f, top + 2f, focused: true, elapsedMs);
    }
}
