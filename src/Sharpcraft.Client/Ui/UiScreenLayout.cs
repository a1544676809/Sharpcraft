// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using Sharpcraft.Common.Diagnostics;
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
public readonly record struct UiStyle(
    uint Colour,
    float Height,
    float Padding,
    float Spacing,
    float Width = -1f,
    float Margin = 0f,
    byte Anchor = 0,
    bool Horizontal = false,
    bool Shadow = true,
    string? BindingKey = null);

/// <summary>
/// Numbers, anchors, orientations and bindings read once per element (docs/modules/ui.md 10).
/// </summary>
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

        var horizontal = node.Attributes.TryGetValue("Orientation", out var orientation) &&
                         ParseOrientation(orientation);
        var anchor = node.Attributes.TryGetValue("Anchor", out var anchorText)
            ? ParseAnchor(anchorText)
            : (byte)0;
        var shadow = !node.Attributes.TryGetValue("Shadow", out var shadowText) ||
                     ParseBool(shadowText);
        var bindingKey = node.Attributes.TryGetValue("Text", out var textValue)
            ? ExtractBindingKey(textValue)
            : null;

        _styles[node] = new UiStyle(
            colour,
            ReadFloat(node, "Height", -1f),
            ReadFloat(node, "Padding", 0f),
            ReadFloat(node, "Spacing", 0f),
            ReadFloat(node, "Width", -1f),
            ReadFloat(node, "Margin", 0f),
            anchor,
            horizontal,
            shadow,
            bindingKey);

        for (var index = 0; index < node.Children.Count; index++)
        {
            Walk(node.Children[index]);
        }
    }

    private static string? ExtractBindingKey(string text)
    {
        if (!text.Contains('{') && !text.Contains('}'))
        {
            return null;
        }

        if (text.Length > 10 && text.StartsWith("{Binding ", StringComparison.Ordinal) && text[^1] == '}')
        {
            var key = text[9..^1].Trim();
            if (key.Length > 0 && !key.Contains('{') && !key.Contains('}'))
            {
                return key;
            }
        }

        throw new FormatException($"invalid binding expression: {text}");
    }

    private static bool ParseOrientation(string value) => value switch
    {
        "Vertical" => false,
        "Horizontal" => true,
        _ => throw new FormatException($"invalid Orientation: {value}")
    };

    private static bool ParseBool(string value) => value switch
    {
        "true" or "True" => true,
        "false" or "False" => false,
        _ => throw new FormatException($"invalid boolean: {value}")
    };

    private static byte ParseAnchor(string value) => value switch
    {
        "TopLeft" => UiScreenLayout.AnchorTopLeft,
        "TopCenter" => UiScreenLayout.AnchorTopCenter,
        "TopRight" => UiScreenLayout.AnchorTopRight,
        "CenterLeft" => UiScreenLayout.AnchorCenterLeft,
        "Center" => UiScreenLayout.AnchorCenter,
        "CenterRight" => UiScreenLayout.AnchorCenterRight,
        "BottomLeft" => UiScreenLayout.AnchorBottomLeft,
        "BottomCenter" => UiScreenLayout.AnchorBottomCenter,
        "BottomRight" => UiScreenLayout.AnchorBottomRight,
        _ => throw new FormatException($"invalid Anchor: {value}")
    };

    private static float ReadFloat(UiNode node, string name, float fallback) =>
        node.Attributes.TryGetValue(name, out var value)
            ? float.Parse(value, CultureInfo.InvariantCulture)
            : fallback;
}

/// <summary>
/// Turns a parsed document into interface geometry (docs/modules/ui.md 6).
/// </summary>
public static class UiScreenLayout
{
    /// <summary>Nine-point screen anchor constants matching vanilla relative screen placement.</summary>
    public const byte AnchorNone = 0;
    public const byte AnchorTopLeft = 1;
    public const byte AnchorTopCenter = 2;
    public const byte AnchorTopRight = 3;
    public const byte AnchorCenterLeft = 4;
    public const byte AnchorCenter = 5;
    public const byte AnchorCenterRight = 6;
    public const byte AnchorBottomLeft = 7;
    public const byte AnchorBottomCenter = 8;
    public const byte AnchorBottomRight = 9;

    /// <summary>Height of one text line when no font is supplied.</summary>
    public const float LineHeight = 12f;

    /// <summary>Advance per placeholder character.</summary>
    public const float CharacterAdvance = 7f;

    /// <summary>Width of one placeholder character cell.</summary>
    public const float CharacterWidth = 5f;

    /// <summary>Height of one placeholder character cell.</summary>
    public const float CharacterHeight = 7f;

    /// <summary>The screen shown when no document is supplied, so the layer is visible while it is built.</summary>
    public const string DefaultDocument = """
<Screen Padding="8">
  <!-- A HUD panel sits behind its text rather than tinting the whole screen. -->
  <StackPanel Background="#A0000000" Padding="6" Spacing="2">
    <TextBlock Text="SHARPCRAFT" Colour="#FFFFFFFF" />
    <TextBlock Text="ui kernel online" Colour="#FF7FC4FF" />
    <TextBlock Text="glyphs from the game font sheet" Colour="#FFB0B0B0" />
  </StackPanel>
</Screen>
""";

    /// <summary>Builds the geometry for one frame. Allocates nothing once the buffers are warm.</summary>
    /// <param name="screen">Parsed root <c>Screen</c> node.</param>
    /// <param name="styles">Pre-compiled style table from <see cref="UiStyles.Compile"/>.</param>
    /// <param name="width">Canvas width in virtual GUI pixels.</param>
    /// <param name="height">Canvas height in virtual GUI pixels.</param>
    /// <param name="geometry">Reusable geometry buffer cleared and populated in place.</param>
    /// <param name="font">Optional bitmap font; when <c>null</c>, placeholder bars are emitted.</param>
    /// <param name="textScale">Scale factor applied to bitmap font metrics.</param>
    /// <param name="resolveBinding">
    /// Optional resolver for whole-string <c>{Binding Key}</c> expressions on <c>TextBlock</c>.
    /// When <c>null</c> or when it returns <c>null</c>, the bound text falls back to <see cref="string.Empty"/>.
    /// </param>
    [HotPath("per-frame UI layout")]
    public static void Build(
        UiNode screen,
        UiStyles styles,
        float width,
        float height,
        UiGeometry geometry,
        BitmapFont? font = null,
        float textScale = 1f,
        Func<string, string?>? resolveBinding = null)
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

        var pad = style.Padding;
        var innerWidth = Math.Max(0f, width - (pad * 2f));
        var innerHeight = Math.Max(0f, height - (pad * 2f));
        var cursor = pad;
        var spacing = style.Spacing;

        for (var index = 0; index < screen.Children.Count; index++)
        {
            var child = screen.Children[index];
            var childStyle = styles.Of(child);

            if (childStyle.Anchor != AnchorNone)
            {
                var (cw, ch) = MeasureNode(child, styles, font, textScale, resolveBinding);
                var (ax, ay) = ResolveAnchorOrigin(childStyle.Anchor, pad, pad, innerWidth, innerHeight, cw, ch, childStyle.Margin);
                var y = ay;
                EmitNode(child, styles, ax, ref y, Math.Max(cw, innerWidth), geometry, font, textScale, resolveBinding);
                continue;
            }

            EmitNode(child, styles, pad, ref cursor, innerWidth, geometry, font, textScale, resolveBinding);
            if (index < screen.Children.Count - 1)
            {
                cursor += spacing;
            }
        }
    }

    private static (float X, float Y) ResolveAnchorOrigin(
        byte anchor,
        float areaX,
        float areaY,
        float areaW,
        float areaH,
        float elemW,
        float elemH,
        float margin)
    {
        var x = anchor switch
        {
            AnchorTopLeft or AnchorCenterLeft or AnchorBottomLeft => areaX + margin,
            AnchorTopCenter or AnchorCenter or AnchorBottomCenter => areaX + ((areaW - elemW) * 0.5f),
            _ => areaX + areaW - elemW - margin
        };
        var y = anchor switch
        {
            AnchorTopLeft or AnchorTopCenter or AnchorTopRight => areaY + margin,
            AnchorCenterLeft or AnchorCenter or AnchorCenterRight => areaY + ((areaH - elemH) * 0.5f),
            _ => areaY + areaH - elemH - margin
        };
        return (x, y);
    }

    private static void EmitNode(
        UiNode node,
        UiStyles styles,
        float left,
        ref float cursor,
        float available,
        UiGeometry geometry,
        BitmapFont? font,
        float textScale,
        Func<string, string?>? resolveBinding)
    {
        switch (node.Name)
        {
            case "StackPanel":
                LayoutPanel(node, styles, left, ref cursor, available, geometry, font, textScale, resolveBinding);
                break;

            case "TextBlock":
                LayoutText(node, styles, left, ref cursor, geometry, font, textScale, resolveBinding);
                break;

            case "Spacer":
                LayoutSpacer(node, styles, left, ref cursor, geometry);
                break;

            default:
                throw new FormatException($"element {node.Name} cannot appear inside parent");
        }
    }

    private static void LayoutSpacer(UiNode spacer, UiStyles styles, float left, ref float cursor, UiGeometry geometry)
    {
        var style = styles.Of(spacer);
        var w = style.Width >= 0f ? style.Width : 0f;
        var h = style.Height >= 0f ? style.Height : 0f;
        if (style.Colour != 0u && w > 0f && h > 0f)
        {
            geometry.AddRectangle(left, cursor, w, h, style.Colour);
        }

        cursor += h;
    }

    private static void LayoutChildren(
        UiNode parent,
        UiStyles styles,
        float left,
        ref float cursor,
        float available,
        UiGeometry geometry,
        BitmapFont? font,
        float textScale,
        Func<string, string?>? resolveBinding)
    {
        var parentStyle = styles.Of(parent);
        var spacing = parentStyle.Spacing;

        if (parentStyle.Horizontal)
        {
            var penX = left;
            var maxH = 0f;
            for (var index = 0; index < parent.Children.Count; index++)
            {
                var child = parent.Children[index];
                var rowY = cursor;
                var (cw, ch) = MeasureNode(child, styles, font, textScale, resolveBinding);
                EmitNode(child, styles, penX, ref rowY, cw, geometry, font, textScale, resolveBinding);
                penX += cw + (index < parent.Children.Count - 1 ? spacing : 0f);
                if (ch > maxH)
                {
                    maxH = ch;
                }
            }

            cursor += maxH;
            return;
        }

        for (var index = 0; index < parent.Children.Count; index++)
        {
            EmitNode(parent.Children[index], styles, left, ref cursor, available, geometry, font, textScale, resolveBinding);
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
        UiGeometry geometry,
        BitmapFont? font,
        float textScale,
        Func<string, string?>? resolveBinding)
    {
        var style = styles.Of(panel);
        var innerLeft = left + style.Padding;
        var innerWidth = Math.Max(0f, available - (style.Padding * 2f));

        if (style.Colour != 0u)
        {
            var (panelWidth, panelHeight) = MeasureNode(panel, styles, font, textScale, resolveBinding);
            geometry.AddRectangle(left, cursor, Math.Min(available, panelWidth), panelHeight, style.Colour);
        }

        cursor += style.Padding;
        LayoutChildren(panel, styles, innerLeft, ref cursor, innerWidth, geometry, font, textScale, resolveBinding);
        cursor += style.Padding;
    }

    private static (float Width, float Height) MeasureNode(
        UiNode node,
        UiStyles styles,
        BitmapFont? font,
        float textScale,
        Func<string, string?>? resolveBinding)
    {
        var style = styles.Of(node);
        if (node.Name == "TextBlock")
        {
            var text = ResolveText(node, style, resolveBinding);
            var w = font is null ? text.Length * CharacterAdvance : font.MeasureWidth(text) * textScale;
            return (w, LineHeightOf(style, font, textScale));
        }

        if (node.Name == "Spacer")
        {
            return (style.Width >= 0f ? style.Width : 0f, style.Height >= 0f ? style.Height : 0f);
        }

        var contentW = 0f;
        var contentH = 0f;
        var count = node.Children.Count;

        for (var index = 0; index < count; index++)
        {
            var (cw, ch) = MeasureNode(node.Children[index], styles, font, textScale, resolveBinding);
            if (style.Horizontal)
            {
                contentW += cw + (index < count - 1 ? style.Spacing : 0f);
                if (ch > contentH)
                {
                    contentH = ch;
                }
            }
            else
            {
                if (cw > contentW)
                {
                    contentW = cw;
                }

                contentH += ch + (index < count - 1 ? style.Spacing : 0f);
            }
        }

        var totalW = style.Width >= 0f ? style.Width : contentW + (style.Padding * 2f);
        var totalH = style.Height >= 0f ? style.Height : contentH + (style.Padding * 2f);
        return (totalW, totalH);
    }

    private static string ResolveText(UiNode node, UiStyle style, Func<string, string?>? resolveBinding) =>
        style.BindingKey is { } key
            ? resolveBinding?.Invoke(key) ?? string.Empty
            : node.Get("Text");

    private static float LineHeightOf(UiStyle style, BitmapFont? font, float textScale)
    {
        if (style.Height >= 0f)
        {
            return style.Height;
        }

        return font is null ? LineHeight : font.Height * textScale;
    }

    private static uint ShadowOf(uint colour) =>
        (colour & 0xFF000000u)
        | ((colour >> 2) & 0x003F3F3Fu);

    /// <summary>Adds one line of text as textured quads, starting at <paramref name="x"/> and <paramref name="y"/>.</summary>
    public static void AddText(
        UiGeometry geometry,
        BitmapFont font,
        string content,
        float x,
        float y,
        float textScale,
        uint colour) =>
        AddText(geometry, font, content.AsSpan(), x, y, textScale, colour);

    /// <summary>Adds one run of text, given as a span so wrapped chat lines need no substring.</summary>
    public static void AddText(
        UiGeometry geometry,
        BitmapFont font,
        ReadOnlySpan<char> content,
        float x,
        float y,
        float textScale,
        uint colour,
        bool shadow = true)
    {
        var cellWidth = font.CellWidth * font.Scale * textScale;
        var cellHeight = font.CellHeight * font.Scale * textScale;
        var pen = x;
        var shadowColour = ShadowOf(colour);

        for (var index = 0; index < content.Length; index++)
        {
            var character = content[index];

            if (font.TryGet(character, out var glyph) && character != ' ')
            {
                var (u0, v0, u1, v1) = font.Uv(glyph);
                if (shadow)
                {
                    geometry.AddTexturedRectangle(
                        pen + textScale,
                        y + textScale,
                        cellWidth,
                        cellHeight,
                        u0,
                        v0,
                        u1,
                        v1,
                        shadowColour);
                }

                geometry.AddTexturedRectangle(pen, y, cellWidth, cellHeight, u0, v0, u1, v1, colour);
            }

            pen += font.AdvanceOf(character) * textScale;
        }
    }

    private static void LayoutText(
        UiNode text,
        UiStyles styles,
        float left,
        ref float cursor,
        UiGeometry geometry,
        BitmapFont? font,
        float textScale,
        Func<string, string?>? resolveBinding)
    {
        var style = styles.Of(text);
        var content = ResolveText(text, style, resolveBinding);
        var colour = style.Colour == 0u ? 0xFFFFFFFFu : style.Colour;
        var lineHeight = LineHeightOf(style, font, textScale);

        if (font is null)
        {
            var barPen = left;

            for (var index = 0; index < content.Length; index++)
            {
                if (content[index] != ' ')
                {
                    geometry.AddRectangle(
                        barPen,
                        cursor + ((lineHeight - CharacterHeight) / 2f),
                        CharacterWidth,
                        CharacterHeight,
                        colour);
                }

                barPen += CharacterAdvance;
            }

            cursor += lineHeight;
            return;
        }

        AddText(geometry, font, content, left, cursor + ((lineHeight - (font.CellHeight * font.Scale * textScale)) / 2f), textScale, colour, style.Shadow);
        cursor += lineHeight;
    }
}
