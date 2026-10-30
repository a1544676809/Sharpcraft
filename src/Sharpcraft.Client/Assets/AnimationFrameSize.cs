// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Text.Json;

namespace Sharpcraft.Client.Assets;

/// <summary>
/// The frame size a sprite declares in its <c>.mcmeta</c> (docs/analysis/assets/texture-atlas-sizing.md §1).
/// </summary>
/// <remarks>
/// An animated sheet stacks its frames vertically, and the frame size comes from <c>animation.width</c> and
/// <c>animation.height</c>. Guessing it as a square of the image width is only right for the common case, so a pack with
/// wider or narrower frames would be sliced wrong.
/// </remarks>
public readonly record struct AnimationFrameSize(int Width, int Height)
{
    /// <summary>Reads the frame size, or null when the file declares none or cannot be parsed.</summary>
    /// <param name="metadata">Contents of the sprite's <c>.mcmeta</c>, or null.</param>
    public static AnimationFrameSize? TryRead(byte[]? metadata)
    {
        if (metadata is null || metadata.Length == 0)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(metadata);

            if (!document.RootElement.TryGetProperty("animation", out var animation) ||
                animation.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var width = ReadInt(animation, "width");
            var height = ReadInt(animation, "height");

            if (width <= 0 || height <= 0)
            {
                return null;
            }

            return new AnimationFrameSize(width, height);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static int ReadInt(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : 0;
}
