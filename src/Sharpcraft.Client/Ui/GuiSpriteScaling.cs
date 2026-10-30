// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Text.Json;

namespace Sharpcraft.Client.Ui;

/// <summary>How a GUI sprite scales into its destination box.</summary>
public enum GuiSpriteScalingType
{
    /// <summary>The whole sprite is stretched; this is the default.</summary>
    Stretch,

    /// <summary>The sprite is repeated.</summary>
    Tile,

    /// <summary>Corners keep their pixels while edges and middle stretch.</summary>
    NineSlice
}

/// <summary>Corner sizes of a nine slice, per side (docs/analysis/gui/tooltip-background.md).</summary>
/// <param name="Left">Left border.</param>
/// <param name="Top">Top border.</param>
/// <param name="Right">Right border.</param>
/// <param name="Bottom">Bottom border.</param>
public readonly record struct GuiSpriteBorder(int Left, int Top, int Right, int Bottom)
{
    /// <summary>The same size on all four sides.</summary>
    /// <param name="size">Size in sprite pixels.</param>
    public static GuiSpriteBorder Uniform(int size) => new(size, size, size, size);
}

/// <summary>
/// The scaling a GUI sprite declares in its <c>.mcmeta</c> (docs/analysis/gui/tooltip-background.md).
/// </summary>
/// <remarks>
/// The reference has three forms: <c>stretch</c> with no fields, <c>tile</c> with a nominal width and height, and
/// <c>nine_slice</c> with a nominal size, per-side borders and an optional inner stretch. The nominal size is what the
/// sprite is treated as, which is not necessarily the image's pixel size, so a resource pack can re-scale the art.
/// </remarks>
public sealed record GuiSpriteScaling
{
    /// <summary>Scaling form.</summary>
    public GuiSpriteScalingType Type { get; init; } = GuiSpriteScalingType.Stretch;

    /// <summary>Nominal sprite width, or 0 when the file does not declare one.</summary>
    public int Width { get; init; }

    /// <summary>Nominal sprite height, or 0 when the file does not declare one.</summary>
    public int Height { get; init; }

    /// <summary>Corner sizes, used by nine slice only.</summary>
    public GuiSpriteBorder Border { get; init; }

    /// <summary>True when the middle region stretches rather than tiles.</summary>
    public bool StretchInner { get; init; }

    /// <summary>Parses the <c>gui.scaling</c> block of an <c>.mcmeta</c> file.</summary>
    /// <param name="json">File contents.</param>
    /// <returns>The scaling, or null when the file carries none or the block is invalid.</returns>
    public static GuiSpriteScaling? TryParse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);

            if (!document.RootElement.TryGetProperty("gui", out var gui) ||
                !gui.TryGetProperty("scaling", out var scaling))
            {
                return null;
            }

            var type = scaling.TryGetProperty("type", out var typeElement)
                ? typeElement.GetString() ?? "stretch"
                : "stretch";

            var width = ReadInt(scaling, "width");
            var height = ReadInt(scaling, "height");

            if (type == "stretch")
            {
                return new GuiSpriteScaling { Type = GuiSpriteScalingType.Stretch, Width = width, Height = height };
            }

            if (type == "tile")
            {
                return new GuiSpriteScaling { Type = GuiSpriteScalingType.Tile, Width = width, Height = height };
            }

            if (type != "nine_slice")
            {
                return null;
            }

            var border = ReadBorder(scaling);

            if (border is not { } sides)
            {
                return null;
            }

            // The reference rejects a nine slice whose borders leave no centre to stretch.
            if (sides.Left + sides.Right >= width || sides.Top + sides.Bottom >= height)
            {
                return null;
            }

            var stretchInner = scaling.TryGetProperty("stretch_inner", out var stretchElement) &&
                               stretchElement.ValueKind == JsonValueKind.True;

            return new GuiSpriteScaling
            {
                Type = GuiSpriteScalingType.NineSlice,
                Width = width,
                Height = height,
                Border = sides,
                StretchInner = stretchInner
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static int ReadInt(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : 0;

    private static GuiSpriteBorder? ReadBorder(JsonElement scaling)
    {
        if (!scaling.TryGetProperty("border", out var border))
        {
            return null;
        }

        // A single number means the same size on every side; an object gives each side separately.
        if (border.ValueKind == JsonValueKind.Number && border.TryGetInt32(out var uniform))
        {
            return GuiSpriteBorder.Uniform(uniform);
        }

        if (border.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new GuiSpriteBorder(
            ReadInt(border, "left"),
            ReadInt(border, "top"),
            ReadInt(border, "right"),
            ReadInt(border, "bottom"));
    }
}
