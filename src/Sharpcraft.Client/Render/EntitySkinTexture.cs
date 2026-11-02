// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>Loads the default player skin; the texture is data only and contains no world/entity reference.</summary>
public static class EntitySkinTexture
{
    public static TextureData? Load(ResourceManager resources, bool slim = false) =>
        LoadPath(resources, slim ? "entity/player/slim/steve" : "entity/player/wide/steve");

    public static TextureData? LoadPath(ResourceManager resources, string path)
    {
        ArgumentNullException.ThrowIfNull(resources);
        var data = resources.Read(ResourceKind.Texture, new Identifier("minecraft", path));
        if (data is null)
        {
            return null;
        }

        var image = Sharpcraft.Common.Imaging.PngDecoder.Decode(data);
        return image.Width > 0 && image.Height > 0
            ? new TextureData(image.Width, image.Height, image.Pixels)
            : null;
    }
}
