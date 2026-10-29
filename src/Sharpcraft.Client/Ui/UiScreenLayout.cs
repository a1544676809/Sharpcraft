// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Ui;

/// <summary>Parses the colour values the interface documents use.</summary>
public static class UiColours
{
    /// <summary>Parses <c>#RRGGBB</c> or <c>#AARRGGBB</c> into the packed word the vertex expects.</summary>
    /// <param name="text">Colour text.</param>
    /// <exception cref="FormatException">The text is not a colour in either accepted form.</exception>
    public static uint Parse(string text)
    {
        if (text.Length is not (7 or 9) || text[0] != '#')
        {
            throw new FormatException($"colour must be #RRGGBB or #AARRGGBB, found {text}");
        }

        if (!uint.TryParse(text[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
        {
            throw new FormatException($"colour has non hexadecimal digits: {text}");
        }

        if (text.Length == 7)
        {
            // Six digits carry no alpha, and an interface colour without one means fully opaque.
            return ((value >> 16) & 0xFFu) | (value & 0xFF00u) | ((value & 0xFFu) << 16) | 0xFF000000u;
        }

        var alpha = (value >> 24) & 0xFFu;
        var red = (value >> 16) & 0xFFu;
        var green = (value >> 8) & 0xFFu;
        var blue = value & 0xFFu;
        return red | (green << 8) | (blue << 16) | (alpha << 24);
    }
}

/// <summary>Pre-parsed values for one element, so building a frame does not touch strings.</summary>
/// <param name="Colour">Packed colour, zero when the element has none.</param>
/// <param name="Height">Requested height, or a negative value when absent.</param>
/// <param name="Padding">Inner padding.</param>
/// <param name="Spacing">Gap after the element inside a panel.</param>
public readonly record struct UiStyle(uint Colour, float Height, float Padding, float Spacing);

/// <summary>
/// Numbers and colours read once per element (docs/modules/ui.md 10).
/// </summary>
/// <remarks>
/// Parsing text on every frame would allocate and show up in the per frame budget, so the document is walked once
/// when it is loaded and every element's parsed values are kept here. Building a frame then only does arithmetic.
/// </remarks>
public sealed class UiStyles
{
    private readonly Dictionary<UiNode, UiStyle> _styles = new(ReferenceEqualityComparer.Instance);

    /// <summary>Reads the values for every element in the tree.</summary>
    /// <param name="root">Parsed root element.</param>
    public static UiStyles Compile(UiNode root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var styles = new UiStyles();
        styles.Walk(root);
        return styles;
    }

    /// <summary>Values of one element.</summary>
    /// <param name="node">Element.</param>
    public UiStyle Of(UiNode node) => _styles[node];

    private void Walk(UiNode node)
    {
        var colour = node.Attributes.TryGetValue("Colour", out var colourText)
            ? UiColours.Parse(colourText)
            : node.Attributes.TryGetValue("Background", out var backgroundText)
                ? UiColours.Parse(backgroundText)
                : 0u;

        _styles[node] = new UiStyle(
            colour,
            ReadFloat(node, "Height", -1f),
            ReadFloat(node, "Padding", 0f),
            ReadFloat(node, "Spacing", 0f));

        foreach (var child in node.Children)
        {
            Walk(child);
        }
    }

    private static float ReadFloat(UiNode node, string name, float fallback) =>
        node.Attributes.TryGetValue(name, out var value)
            ? float.Parse(value, CultureInfo.InvariantCulture)
            : fallback;
}

/// <summary>
/// Turns a parsed document into interface geometry (docs/modules/ui.md 6).
/// </summary>
/// <remarks>
/// One pass, no measurement pass: heights come from the document or from the element kind, so a screen's layout
/// never depends on a sibling's layout beyond the running cursor. Text is drawn as one cell per character until
/// the bitmap font lands, which keeps batching and layout verifiable in the meantime.
/// </remarks>
public static class UiScreenLayout
{
    /// <summary>Height of one text line in virtual pixels.</summary>
    public const float LineHeight = 12f;

    /// <summary>Advance per character, in virtual pixels.</summary>
    public const float CharacterAdvance = 7f;

    /// <summary>Size of one character cell.</summary>
    public const float CharacterWidth = 5f;

    /// <summary>Height of one character cell.</summary>
    public const float CharacterHeight = 7f;

    /// <summary>The screen shown when no document is supplied, so the layer is visible while it is built.</summary>
    public const string DefaultDocument = """
<Screen Padding="8">
  <!-- A HUD panel sits behind its text rather than tinting the whole screen; until documents ship with the
       asset pack this is also the smoke test for the layout kernel. -->
  <StackPanel Background="#A0000000" Padding="6" Spacing="2">
    <TextBlock Text="SHARPCRAFT" Colour="#FFFFFFFF" Height="16" />
    <TextBlock Text="ui kernel online" Colour="#FF7FC4FF" />
    <TextBlock Text="glyphs arrive with the font slice" Colour="#FFB0B0B0" />
  </StackPanel>
</Screen>
""";

    /// <summary>Builds the geometry for one frame. Allocates nothing once the buffers are warm.</summary>
    /// <param name="screen">Parsed root element.</param>
    /// <param name="styles">Pre-parsed values for that tree.</param>
    /// <param name="width">Virtual width.</param>
    /// <param name="height">Virtual height.</param>
    /// <param name="geometry">Receives the quads; cleared first.</param>
    public static void Build(UiNode screen, UiStyles styles, float width, float height, UiGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(screen);
        ArgumentNullException.ThrowIfNull(styles);
        ArgumentNullException.ThrowIfNull(geometry);

        if (screen.Name != "Screen")
        {
            throw new ArgumentException("the root element must be Screen", nameof(screen));
        }

        geometry.Clear();
        geometry.SetScissor(0f, 0f, width, height);

        var style = styles.Of(screen);

        if (style.Colour != 0u)
        {
            geometry.AddRectangle(0f, 0f, width, height, style.Colour);
        }

        var cursor = style.Padding;
        LayoutChildren(screen, styles, style.Padding, ref cursor, width - (style.Padding * 2f), geometry);
    }

    private static void LayoutChildren(
        UiNode parent,
        UiStyles styles,
        float left,
        ref float cursor,
        float available,
        UiGeometry geometry)
    {
        var spacing = styles.Of(parent).Spacing;

        for (var index = 0; index < parent.Children.Count; index++)
        {
            var child = parent.Children[index];

            switch (child.Name)
            {
                case "StackPanel":
                    LayoutPanel(child, styles, left, ref cursor, available, geometry);
                    break;

                case "TextBlock":
                    LayoutText(child, styles, left, ref cursor, geometry);
                    break;

                default:
                    throw new FormatException($"element {child.Name} cannot appear inside {parent.Name}");
            }

            if (index < parent.Children.Count - 1)
            {
                cursor += spacing;
            }
        }
    }

    private static void LayoutPanel(
        UiNode panel,
        UiStyles styles,
        float left,
        ref float cursor,
        float available,
        UiGeometry geometry)
    {
        var style = styles.Of(panel);

        var innerLeft = left + style.Padding;
        var innerWidth = Math.Max(0f, available - (style.Padding * 2f));

        if (style.Colour != 0u)
        {
            // Sized to its content rather than to the row: a HUD panel should frame its text, not tint the width
            // of the screen.
            var panelHeight = MeasurePanel(panel, styles, style.Padding, style.Spacing);
            var contentWidth = MeasurePanelWidth(panel, styles) + (style.Padding * 2f);
            geometry.AddRectangle(left, cursor, Math.Min(available, contentWidth), panelHeight, style.Colour);
        }

        cursor += style.Padding;
        LayoutChildren(panel, styles, innerLeft, ref cursor, innerWidth, geometry);
        cursor += style.Padding;
    }

    private static float MeasurePanelWidth(UiNode node, UiStyles styles)
    {
        if (node.Name == "TextBlock")
        {
            return node.Get("Text").Length * CharacterAdvance;
        }

        var widest = 0f;

        for (var index = 0; index < node.Children.Count; index++)
        {
            widest = Math.Max(widest, MeasurePanelWidth(node.Children[index], styles));
        }

        // The padding comes from the pre-parsed styles: reading the attribute here would parse a string on every
        // frame, which the allocation test catches.
        return widest + (styles.Of(node).Padding * 2f);
    }

    private static float MeasurePanel(UiNode panel, UiStyles styles, float padding, float spacing)
    {
        var total = padding * 2f;

        // Indexed loops throughout: enumerating an IReadOnlyList through its interface allocates an enumerator,
        // which the per frame allocation test catches.
        for (var index = 0; index < panel.Children.Count; index++)
        {
            var child = panel.Children[index];
            total += spacing;

            if (child.Name == "TextBlock")
            {
                var height = styles.Of(child).Height;
                total += height >= 0f ? height : LineHeight;
            }
            else
            {
                var inner = styles.Of(child);
                total += MeasurePanel(child, styles, inner.Padding, inner.Spacing);
            }
        }

        return total;
    }

    private static void LayoutText(UiNode text, UiStyles styles, float left, ref float cursor, UiGeometry geometry)
    {
        var content = text.Get("Text");
        var style = styles.Of(text);
        var colour = style.Colour == 0u ? 0xFFFFFFFFu : style.Colour;
        var height = style.Height >= 0f ? style.Height : LineHeight;

        // One cell per visible character; the advance is fixed until real glyph metrics arrive with the font.
        var x = left;

        // Indexed rather than foreach: enumerating a string allocates a CharEnumerator, which would show up in
        // the per frame budget.
        for (var index = 0; index < content.Length; index++)
        {
            var character = content[index];

            if (character != ' ')
            {
                // Placeholder glyph: a narrow bar per character, so the block reads as a line of text rather
                // than as a row of squares until real glyphs arrive.
                geometry.AddRectangle(
                    x,
                    cursor + ((height - CharacterHeight) / 2f),
                    CharacterWidth,
                    CharacterHeight,
                    colour);
            }

            x += CharacterAdvance;
        }

        cursor += height;
    }
}
