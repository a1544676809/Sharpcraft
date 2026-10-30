// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Imaging;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Ui;

/// <summary>
/// The two GUI sprites a tooltip is drawn from, with the scaling each one declares
/// (docs/analysis/gui/tooltip-background.md).
/// </summary>
/// <remarks>
/// The scaling travels with the texture because a resource pack may change either one, and a nine slice with a
/// different border or a plain stretch must render accordingly rather than being forced through one rule.
/// </remarks>
public sealed record GuiSpriteAtlas(
    TextureData Background,
    TextureData Frame,
    GuiSpriteScaling BackgroundScaling,
    GuiSpriteScaling FrameScaling)
{
    /// <summary>Index of the background sprite in the renderer's list.</summary>
    public const int BackgroundIndex = 0;

    /// <summary>Index of the frame sprite in the renderer's list.</summary>
    public const int FrameIndex = 1;

    /// <summary>Builds a pair whose scaling is a uniform nine slice border, for tests and simple packs.</summary>
    /// <param name="background">Background texture.</param>
    /// <param name="frame">Frame texture.</param>
    /// <param name="border">Corner size in sprite pixels.</param>
    public static GuiSpriteAtlas Uniform(TextureData background, TextureData frame, int border)
    {
        var scaling = new GuiSpriteScaling
        {
            Type = GuiSpriteScalingType.NineSlice,
            Width = Math.Max(background.Width, frame.Width),
            Height = Math.Max(background.Height, frame.Height),
            Border = GuiSpriteBorder.Uniform(border)
        };

        return new GuiSpriteAtlas(background, frame, scaling, scaling);
    }

    /// <summary>The textures in the order the renderer must bind them.</summary>
    public IReadOnlyList<TextureData> Textures => [Background, Frame];
}
