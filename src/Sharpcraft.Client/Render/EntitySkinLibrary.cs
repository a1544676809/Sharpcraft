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
    private readonly Dictionary<string, TextureData> _images;

    private EntitySkinLibrary(Dictionary<string, TextureData> images)
    {
        _images = images;
        Atlas = images.Count == 0 ? null : EntitySkinAtlas.FromTextures(images);
    }

    public EntitySkinAtlas? Atlas { get; private set; }

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

        // Equipment assets parsed from the resource pack can reference textures outside the static catalog. LoadTexture
        // already de-duplicates by path, so the asset paths can be appended without a separate set.
        foreach (var path in EquipmentTextureCatalog.Assets.TexturePaths)
        {
            LoadTexture(path, images, resources, playerOverride);
        }

        foreach (var pair in TrimTextureCatalog.Load(resources))
        {
            images.TryAdd(pair.Key, pair.Value);
        }

        return new EntitySkinLibrary(images);
    }

    /// <summary>
    /// Adds one texture after construction and rebuilds the atlas. Used for player skins, which arrive from the
    /// network after the initial resource pass; returns false when the path is already present.
    /// </summary>
    public bool TryAddTexture(string path, TextureData image)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(image);

        if (!_images.TryAdd(path, image))
        {
            return false;
        }

        Atlas = EntitySkinAtlas.FromTextures(_images);
        return true;
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
