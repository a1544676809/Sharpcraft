// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Imaging;

namespace Sharpcraft.Client.Ui;

/// <summary>
/// A glyph sheet loaded from the game assets: where each character sits, how wide it is, and how far the pen
/// advances (docs/modules/ui.md 5).
/// </summary>
/// <remarks>
/// The sheet is a grid of equally sized cells, which is how the legacy font texture is laid out: a sixteen by
/// sixteen grid of eight pixel cells, slot order running left to right then top to bottom, so a character's slot
/// index is its code point.
/// <para>
/// A glyph's drawn width is not its cell width. The rightmost column that carries any ink defines it, which is
/// why a period advances much less than an m. The advance adds one pixel of spacing, matching the reference
/// behaviour; getting that wrong makes text look cramped or loose rather than obviously broken, so it is pinned
/// by tests.
/// </para>
/// <para>
/// Nothing here touches the GPU or the assets themselves: the caller decodes the sheet and hands over pixels,
/// which keeps the metrics testable without any of the game's files.
/// </para>
/// </remarks>
public sealed class BitmapFont
{
    /// <summary>Line height the reference uses for the default font.</summary>
    public const int DefaultHeight = 8;

    /// <summary>Distance from the top of the line to the baseline.</summary>
    public const int DefaultAscent = 7;

    /// <summary>Columns in the legacy sheet.</summary>
    public const int SheetColumns = 16;

    private readonly Dictionary<char, Glyph> _glyphs = [];
    private readonly RgbaImage _sheet;
    private readonly int _sheetWidth;
    private readonly int _sheetHeight;
    private readonly int _cellWidth;
    private readonly int _cellHeight;

    private BitmapFont(RgbaImage sheet, int height, int ascent, int columns)
    {
        _sheet = sheet;
        _sheetWidth = sheet.Width;
        _sheetHeight = sheet.Height;
        _cellWidth = Math.Max(1, sheet.Width / columns);

        // The legacy sheet uses square cells, so the cell height follows the cell width.
        _cellHeight = _cellWidth;
        Height = height;
        Ascent = ascent;

        var scale = (float)height / _cellHeight;
        var rows = sheet.Height / _cellHeight;
        var slots = columns * rows;

        for (var slot = 0; slot < slots; slot++)
        {
            var slotX = slot % columns;
            var slotY = slot / columns;
            var width = MeasureGlyphWidth(sheet, slotX, slotY);
            var advance = (int)(0.5f + (width * scale)) + 1;
            _glyphs[(char)slot] = new Glyph(slotX, slotY, advance, width);
        }
    }

    /// <summary>The decoded sheet, so it can be uploaded as a texture unchanged.</summary>
    public RgbaImage Sheet => _sheet;

    /// <summary>Line height in font pixels.</summary>
    public int Height { get; }

    /// <summary>Baseline distance from the top of the line, in font pixels.</summary>
    public int Ascent { get; }

    /// <summary>Cell width on the sheet, in pixels.</summary>
    public int CellWidth => _cellWidth;

    /// <summary>Cell height on the sheet, in pixels.</summary>
    public int CellHeight => _cellHeight;

    /// <summary>One character's placement and metrics.</summary>
    /// <param name="SlotX">Column of the cell on the sheet.</param>
    /// <param name="SlotY">Row of the cell on the sheet.</param>
    /// <param name="Advance">How far the pen moves after drawing, in font pixels.</param>
    /// <param name="Width">Inked width of the glyph, in sheet pixels.</param>
    public readonly record struct Glyph(int SlotX, int SlotY, int Advance, int Width);

    /// <summary>Builds a font from a decoded sheet laid out in the legacy grid.</summary>
    /// <param name="sheet">Decoded sheet, RGBA.</param>
    /// <param name="height">Line height to report, in font pixels.</param>
    /// <param name="ascent">Baseline distance.</param>
    /// <param name="columns">Grid columns; the legacy sheet has sixteen.</param>
    public static BitmapFont FromSheet(RgbaImage sheet, int height = DefaultHeight, int ascent = DefaultAscent, int columns = SheetColumns)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        if (columns <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(columns), "the grid needs at least one column");
        }

        if (sheet.Width % columns != 0)
        {
            throw new ArgumentException("the sheet width must divide evenly by the column count", nameof(sheet));
        }

        return new BitmapFont(sheet, height, ascent, columns);
    }

    /// <summary>Looks up a character.</summary>
    /// <param name="character">Character to find.</param>
    /// <param name="glyph">Metrics when present.</param>
    public bool TryGet(char character, out Glyph glyph) => _glyphs.TryGetValue(character, out glyph);

    /// <summary>Width of a string in font pixels, using each glyph's advance.</summary>
    /// <param name="text">Text to measure.</param>
    public int MeasureWidth(ReadOnlySpan<char> text)
    {
        var width = 0;

        for (var index = 0; index < text.Length; index++)
        {
            if (_glyphs.TryGetValue(text[index], out var glyph))
            {
                width += glyph.Advance;
            }
        }

        return width;
    }

    /// <summary>Texture coordinates covering one glyph cell, in the range zero to one.</summary>
    /// <param name="glyph">Glyph whose cell is wanted.</param>
    public (float U0, float V0, float U1, float V1) Uv(in Glyph glyph) =>
        (
            (float)(glyph.SlotX * _cellWidth) / _sheetWidth,
            (float)(glyph.SlotY * _cellHeight) / _sheetHeight,
            (float)((glyph.SlotX + 1) * _cellWidth) / _sheetWidth,
            (float)((glyph.SlotY + 1) * _cellHeight) / _sheetHeight);

    private int MeasureGlyphWidth(RgbaImage sheet, int slotX, int slotY)
    {
        // Rightmost inked column wins, scanning right to left, which is how the reference derives a glyph width.
        for (var column = _cellWidth - 1; column >= 0; column--)
        {
            var x = (slotX * _cellWidth) + column;

            for (var y = 0; y < _cellHeight; y++)
            {
                var pixelY = (slotY * _cellHeight) + y;
                var offset = (((pixelY * sheet.Width) + x) * RgbaImage.BytesPerPixel) + 3;

                if (sheet.Pixels[offset] != 0)
                {
                    return column + 1;
                }
            }
        }

        return 0;
    }
}
