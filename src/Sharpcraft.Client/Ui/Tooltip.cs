// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Network;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Ui;

/// <summary>
/// Draws the box that appears beside the pointer when a chat run has hover text (docs/modules/ui.md 8.6).
/// </summary>
/// <remarks>
/// The colours are the reference's: a near black background with a purple frame. The box is kept inside the canvas so a
/// run near an edge does not put its tip off screen.
/// </remarks>
public static class Tooltip
{
    /// <summary>Background of the box.</summary>
    public const uint BackgroundColour = 0xF0100010u;

    /// <summary>Top and left edge of the frame.</summary>
    public const uint FrameStartColour = 0x505000FFu;

    /// <summary>Bottom and right edge of the frame.</summary>
    public const uint FrameEndColour = 0x5028007Fu;

    /// <summary>Padding between the frame and the text.</summary>
    public const float Padding = 3f;

    /// <summary>Height of one line of tip text.</summary>
    public const float LineHeight = 9f;

    /// <summary>
    /// Widest a tip may be before it wraps, which is the reference's own limit.
    /// </summary>
    public const float MaxWidth = 210f;

    /// <summary>
    /// Adds the box to the interface geometry.
    /// </summary>
    /// <param name="geometry">Receives the quads.</param>
    /// <param name="font">Glyph metrics.</param>
    /// <param name="runs">Tip text.</param>
    /// <param name="pointerX">Pointer x in canvas pixels.</param>
    /// <param name="pointerY">Pointer y in canvas pixels.</param>
    /// <param name="canvasWidth">Canvas width, used to keep the box on screen.</param>
    /// <param name="canvasHeight">Canvas height, used to keep the box on screen.</param>
    /// <param name="sprites">GUI sprites to draw the frame from, or null to fall back to a plain rectangle.</param>
    /// <remarks>
    /// The reference draws the frame from two nine slice sprites at <c>x-3-9, y-3-9, w+6+18, h+6+18</c>, where x,y,w,h
    /// is the text area. The corners keep their pixels, which is what gives the frame its shape; a rectangle cannot.
    /// </remarks>
    public static void Draw(
        UiGeometry geometry,
        BitmapFont font,
        TextRun[] runs,
        float pointerX,
        float pointerY,
        float canvasWidth,
        float canvasHeight,
        GuiSpriteAtlas? sprites = null)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(font);

        if (runs.Length == 0)
        {
            return;
        }

        var lines = Wrap(runs, font, MaxWidth);
        var textWidth = 0f;

        foreach (var line in lines)
        {
            var lineWidth = 0f;

            foreach (var run in line)
            {
                lineWidth += font.MeasureWidth(run.Text);
            }

            textWidth = MathF.Max(textWidth, lineWidth);
        }

        var textHeight = lines.Count * LineHeight;

        // Outer box is the text plus the reference's three pixel padding on every side.
        var boxWidth = textWidth + (Padding * 2f);
        var boxHeight = textHeight + (Padding * 2f);

        // The sprites reach nine more pixels beyond that on every side.
        var margin = sprites is null ? 0f : 9f;
        var totalWidth = boxWidth + (margin * 2f);
        var totalHeight = boxHeight + (margin * 2f);

        var left = MathF.Min(pointerX + 6f, canvasWidth - totalWidth - 2f);
        var top = MathF.Min(pointerY - totalHeight - 6f, canvasHeight - totalHeight - 2f);
        left = MathF.Max(2f, left);
        top = MathF.Max(2f, top);

        var textLeft = left + margin + Padding;
        var textTop = top + margin + Padding;

        if (sprites is null)
        {
            geometry.AddRectangle(left, top, boxWidth, boxHeight, BackgroundColour);
            geometry.AddRectangle(left, top, boxWidth, 1f, FrameStartColour);
            geometry.AddRectangle(left, top + boxHeight - 1f, boxWidth, 1f, FrameEndColour);
            geometry.AddRectangle(left, top, 1f, boxHeight, FrameStartColour);
            geometry.AddRectangle(left + boxWidth - 1f, top, 1f, boxHeight, FrameEndColour);
        }
        else
        {
            DrawSprite(
                geometry,
                sprites.Background,
                GuiSpriteAtlas.BackgroundIndex,
                sprites.Border,
                left,
                top,
                totalWidth,
                totalHeight);

            DrawSprite(
                geometry,
                sprites.Frame,
                GuiSpriteAtlas.FrameIndex,
                sprites.Border,
                left,
                top,
                totalWidth,
                totalHeight);
        }

        var lineTop = textTop;

        foreach (var line in lines)
        {
            var pen = textLeft;

            foreach (var run in line)
            {
                UiScreenLayout.AddText(geometry, font, run.Text, pen, lineTop, 1f, run.Colour, shadow: false);
                pen += font.MeasureWidth(run.Text);
            }

            lineTop += LineHeight;
        }
    }

    private static void DrawSprite(
        UiGeometry geometry,
        TextureData sprite,
        int spriteIndex,
        int border,
        float left,
        float top,
        float width,
        float height)
    {
        var cells = NineSlice.Cells(left, top, width, height, sprite.Width, sprite.Height, border);

        // The cells work in texels, which is what a nine slice reasons about, but a vertex wants 0..1 coordinates.
        var inverseWidth = 1f / sprite.Width;
        var inverseHeight = 1f / sprite.Height;

        for (var index = 0; index < cells.Count; index++)
        {
            var cell = cells[index];
            geometry.AddSpriteRectangle(
                cell.X,
                cell.Y,
                cell.Width,
                cell.Height,
                cell.U0 * inverseWidth,
                cell.V0 * inverseHeight,
                cell.U1 * inverseWidth,
                cell.V1 * inverseHeight,
                spriteIndex);
        }
    }

    /// <summary>
    /// Breaks the tip into lines no wider than the limit, on word boundaries where possible.
    /// </summary>
    /// <param name="runs">Tip text.</param>
    /// <param name="font">Glyph metrics.</param>
    /// <param name="maxWidth">Widest a line may be.</param>
    /// <remarks>
    /// A word that is itself too wide is broken between characters, because leaving it whole would push the tip past
    /// the limit and off the screen edge. Colours travel with the characters they belong to.
    /// </remarks>
    public static List<TextRun[]> Wrap(TextRun[] runs, BitmapFont font, float maxWidth)
    {
        ArgumentNullException.ThrowIfNull(runs);
        ArgumentNullException.ThrowIfNull(font);

        var lines = new List<TextRun[]>();
        var current = new List<TextRun>();
        var currentWidth = 0f;

        void Break()
        {
            if (current.Count > 0)
            {
                lines.Add([.. current]);
                current.Clear();
                currentWidth = 0f;
            }
        }

        foreach (var run in runs)
        {
            var word = new System.Text.StringBuilder();

            void FlushWord()
            {
                if (word.Length == 0)
                {
                    return;
                }

                var text = word.ToString();
                word.Clear();
                var wordWidth = font.MeasureWidth(text);

                if (currentWidth > 0f && currentWidth + wordWidth > maxWidth)
                {
                    Break();
                }

                if (wordWidth > maxWidth)
                {
                    // A single word longer than a line has to be split between characters.
                    var piece = new System.Text.StringBuilder();
                    var pieceWidth = 0f;

                    foreach (var character in text)
                    {
                        var characterWidth = font.MeasureWidth(character.ToString());

                        if (pieceWidth + characterWidth > maxWidth && piece.Length > 0)
                        {
                            current.Add(new TextRun(piece.ToString(), run.Colour, run.Bold));
                            Break();
                            piece.Clear();
                            pieceWidth = 0f;
                        }

                        piece.Append(character);
                        pieceWidth += characterWidth;
                    }

                    if (piece.Length > 0)
                    {
                        current.Add(new TextRun(piece.ToString(), run.Colour, run.Bold));
                        currentWidth += pieceWidth;
                    }

                    return;
                }

                current.Add(new TextRun(text, run.Colour, run.Bold));
                currentWidth += wordWidth;
            }

            foreach (var character in run.Text)
            {
                if (character == ' ')
                {
                    FlushWord();
                    var spaceWidth = font.MeasureWidth(" ");

                    if (currentWidth + spaceWidth > maxWidth)
                    {
                        Break();
                    }
                    else
                    {
                        current.Add(new TextRun(" ", run.Colour, run.Bold));
                        currentWidth += spaceWidth;
                    }
                }
                else
                {
                    word.Append(character);
                }
            }

            FlushWord();
        }

        Break();
        return lines;
    }
}
