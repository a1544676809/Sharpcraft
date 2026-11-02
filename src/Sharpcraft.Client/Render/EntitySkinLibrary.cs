// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Imaging;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Resource-layer owner for entity skins. It reads every registered model's TexturePath through the ResourceManager and
/// builds one atlas; callers never hard-code individual mob texture paths.
/// </summary>
public sealed class EntitySkinLibrary
{
    private EntitySkinLibrary(EntitySkinAtlas? atlas)
    {
        Atlas = atlas;
    }

    public EntitySkinAtlas? Atlas { get; }

    public static EntitySkinLibrary Load(ResourceManager resources, TextureData? playerOverride = null)
    {
        ArgumentNullException.ThrowIfNull(resources);
        var images = new Dictionary<string, TextureData>(StringComparer.Ordinal);
        foreach (var definition in EntityModelResolver.Definitions)
        {
            foreach (var path in definition.TexturePaths)
            {
                if (images.ContainsKey(path))
                {
                    continue;
                }

                if (path == "entity/player/wide/steve" && playerOverride is not null)
                {
                    images[path] = playerOverride;
                    continue;
                }

                var data = resources.Read(ResourceKind.Texture, new Identifier("minecraft", path));
                if (data is null)
                {
                    continue;
                }

                var image = PngDecoder.Decode(data);
                images[path] = new TextureData(image.Width, image.Height, image.Pixels);
            }
        }

        return new EntitySkinLibrary(images.Count == 0 ? null : EntitySkinAtlas.FromTextures(images));
    }
}
