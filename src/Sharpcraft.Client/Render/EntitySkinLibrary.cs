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
        EquipmentTextureCatalog.LoadAssets(resources);
        foreach (var definition in EntityModelResolver.Definitions)
        {
            foreach (var path in definition.TexturePaths)
            {
                LoadTexture(path, images, resources, playerOverride);
            }
        }

        foreach (var path in EquipmentTextureCatalog.AllTexturePaths)
        {
            LoadTexture(path, images, resources, playerOverride);
        }

        return new EntitySkinLibrary(images.Count == 0 ? null : EntitySkinAtlas.FromTextures(images));
    }

    private static void LoadTexture(string path, Dictionary<string, TextureData> images, ResourceManager resources, TextureData? playerOverride)
    {
        if (images.ContainsKey(path))
        {
            return;
        }

        if (path == "entity/player/wide/steve" && playerOverride is not null)
        {
            images[path] = playerOverride;
            return;
        }

        var data = resources.Read(ResourceKind.Texture, new Identifier("minecraft", path));
        if (data is null)
        {
            return;
        }

        var image = PngDecoder.Decode(data);
        images[path] = new TextureData(image.Width, image.Height, image.Pixels);
    }
}
