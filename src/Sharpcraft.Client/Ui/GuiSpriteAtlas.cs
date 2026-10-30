// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Imaging;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Ui;

/// <summary>
/// The two GUI sprites a tooltip is drawn from, with the corner size their <c>.mcmeta</c> declares
/// (docs/analysis/gui/tooltip-background.md).
/// </summary>
/// <remarks>
/// The sprites are handed to the renderer as a list, and their positions in that list are what a range's sprite index
/// refers to. Keeping the pair together here means the tooltip never has to know the indices.
/// </remarks>
public sealed record GuiSpriteAtlas(TextureData Background, TextureData Frame, int Border)
{
    /// <summary>Index of the background sprite in the renderer's list.</summary>
    public const int BackgroundIndex = 0;

    /// <summary>Index of the frame sprite in the renderer's list.</summary>
    public const int FrameIndex = 1;

    /// <summary>The textures in the order the renderer must bind them.</summary>
    public IReadOnlyList<TextureData> Textures => [Background, Frame];
}
