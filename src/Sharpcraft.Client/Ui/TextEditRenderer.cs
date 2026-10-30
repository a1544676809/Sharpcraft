// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Ui;

/// <summary>
/// Draws a single line of editable text: the value, the selection and the caret (docs/modules/ui.md 5.2).
/// </summary>
/// <remarks>
/// The caret has two shapes in the reference, and the difference is not cosmetic: while the caret sits inside the
/// text it is a vertical bar one pixel wide, and once it is at the end it becomes an underscore drawn with the font's
/// own glyph, so that the end of the line looks like a place to type rather than a thin line in empty space. Both
/// blink with the same rhythm, and the phase starts when the field gains focus, not when the application starts.
/// </remarks>
public static class TextEditRenderer
{
    /// <summary>How long the caret stays on, and then off.</summary>
    public const long BlinkIntervalMs = 300;

    /// <summary>Colour of ordinary text, the reference's own.</summary>
    public const uint TextColour = 0xFFE0E0E0u;

    /// <summary>Colour behind a selection.</summary>
    public const uint SelectionColour = 0xFF0000FFu;

    /// <summary>Height of a single line box.</summary>
    public const float LineHeight = 9f;

    /// <summary>True while the caret should be shown.</summary>
    /// <param name="elapsedMs">Milliseconds since the field gained focus.</param>
    public static bool IsCaretVisible(long elapsedMs) => (elapsedMs / BlinkIntervalMs) % 2 == 0;

    /// <summary>True when the caret is inside the text, which makes it a vertical bar.</summary>
    /// <param name="model">Editing state.</param>
    /// <remarks>
    /// The reference also uses the vertical shape when the value is full, because there is no room to type and the
    /// bar is the honest signal for that.
    /// </remarks>
    public static bool UsesInsertCaret(TextEditModel model) =>
        model.CaretPos < model.Length || model.Length >= model.MaxLength;

    /// <summary>Horizontal offset of a caret position.</summary>
    /// <param name="font">Glyph metrics.</param>
    /// <param name="value">The value being edited.</param>
    /// <param name="position">Caret position.</param>
    public static float CaretOffset(BitmapFont font, string value, int position)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(value);

        var clamped = Math.Clamp(position, 0, value.Length);
        return font.MeasureWidth(value.AsSpan(0, clamped));
    }

    /// <summary>
    /// Caret position nearest a horizontal offset, which is how a click places the caret.
    /// </summary>
    /// <param name="font">Glyph metrics.</param>
    /// <param name="value">The value being edited.</param>
    /// <param name="offset">Distance from the left edge of the text.</param>
    public static int PositionAtOffset(BitmapFont font, string value, float offset)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(value);

        var pen = 0f;

        for (var index = 0; index < value.Length; index++)
        {
            var advance = font.AdvanceOf(value[index]);

            // Inside the left half of a glyph the caret belongs before it, in the right half after it.
            if (offset < pen + (advance / 2f))
            {
                return index;
            }

            pen += advance;
        }

        return value.Length;
    }

    /// <summary>Draws the value, its selection and the caret.</summary>
    /// <param name="geometry">Receives the quads.</param>
    /// <param name="font">Glyph metrics.</param>
    /// <param name="model">Editing state.</param>
    /// <param name="x">Left edge of the text.</param>
    /// <param name="y">Top edge of the line box.</param>
    /// <param name="focused">True while the field has focus, which is when the caret blinks.</param>
    /// <param name="elapsedMs">Milliseconds since the field gained focus.</param>
    public static void Draw(
        UiGeometry geometry,
        BitmapFont font,
        TextEditModel model,
        float x,
        float y,
        bool focused,
        long elapsedMs)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(model);

        var value = model.Value;

        if (model.HasSelection)
        {
            var start = CaretOffset(font, value, model.SelectionStart);
            var end = CaretOffset(font, value, model.SelectionEnd);
            geometry.AddRectangle(x + start, y - 1f, Math.Max(1f, end - start), LineHeight + 2f, SelectionColour);
        }

        UiScreenLayout.AddText(geometry, font, value, x, y, 1f, TextColour);

        if (!focused || !IsCaretVisible(elapsedMs))
        {
            return;
        }

        var caretX = x + CaretOffset(font, value, model.CaretPos);

        if (UsesInsertCaret(model))
        {
            // Vertical bar: one pixel wide, from just above the line to its foot, as the reference draws it.
            geometry.AddRectangle(caretX, y - 1f, 1f, LineHeight + 2f, TextColour);
        }
        else
        {
            // At the end the reference draws the font's underscore, so the caret sits on the baseline.
            UiScreenLayout.AddText(geometry, font, "_", caretX, y, 1f, TextColour);
        }
    }
}
