// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>Clones a model's geometry as a collar overlay: same parts, collar texture, collar-colour tint, only while tamed.</summary>
public static class EntityModelLayerHelpers
{
    public static EntityModelPart[] WithCollar(IReadOnlyList<EntityModelPart> parts, string texturePath) =>
        WithCopy(parts, texturePath, static state => state.HasCollar, 0.1f,
            static state => state.CollarTint == default ? Vector3.One : state.CollarTint);

    public static EntityModelPart[] WithCopy(IReadOnlyList<EntityModelPart> parts, string texturePath, Func<EntityRenderState, bool> visible, float inflate = 0.05f, Func<EntityRenderState, Vector3>? tint = null)
    {
        var result = new List<EntityModelPart>(parts.Count * 2);
        result.AddRange(parts);
        foreach (var part in parts)
        {
            result.Add(part with
            {
                Name = "copylayer_" + part.Name,
                PoseSource = part.PoseSource ?? part.Name,
                TexturePath = texturePath,
                Grow = part.Grow + inflate,
                TintSelector = tint,
                VisibleWhen = visible
            });
        }

        return [.. result];
    }

    private static bool static_hasCollar(EntityRenderState state) => state.HasCollar;

    public static EntityModelPart[] WithEquipment(IReadOnlyList<EntityModelPart> parts, EquipmentAssetKind kind, int slot, string fallbackTexture, bool overlay = false, bool baseCopy = true)
    {
        var result = new List<EntityModelPart>(parts.Count * 2);
        result.AddRange(parts);
        var inflate = kind == EquipmentAssetKind.WolfBody ? 0.2f : 0.05f;
        const int textureWidth = 0;
        const int textureHeight = 0;
        if (baseCopy)
        {
            foreach (var part in parts)
            {
                result.Add(part with
                {
                    Name = "equipment_" + kind + "_" + slot + (overlay ? "_overlay_" : "_") + part.Name,
                    PoseSource = part.PoseSource ?? part.Name,
                    TexturePath = fallbackTexture,
                    TextureSelector = state => EntityModelResolver.ResolveEquipmentTexture(state, kind, slot, overlay),
                    Grow = part.Grow + inflate,
                    TextureWidth = textureWidth,
                    TextureHeight = textureHeight,
                    VisibleWhen = state => EntityModelResolver.ResolveEquipmentTexture(state, kind, slot, overlay) is not null
                });
            }
        }

        foreach (var part in EquipmentModelParts.For(kind))
        {
            result.Add(part with
            {
                Name = "equipment_extra_" + kind + "_" + slot + "_" + part.Name,
                TexturePath = fallbackTexture,
                TextureSelector = state => EntityModelResolver.ResolveEquipmentTexture(state, kind, slot, overlay),
                Grow = part.Grow + inflate,
                TextureWidth = textureWidth,
                TextureHeight = textureHeight,
                VisibleWhen = state => EntityModelResolver.ResolveEquipmentTexture(state, kind, slot, overlay) is not null
            });
        }

        return [.. result];
    }

    /// <summary>Humanoid armor layer for one slot: clones only that slot's parts with the outer/inner deformation.</summary>
    public static EntityModelPart[] WithArmorSlot(IReadOnlyList<EntityModelPart> parts, EquipmentAssetKind kind, int slot, string fallbackTexture)
    {
        string[] visible = slot switch
        {
            5 => ["head"],
            4 => ["body", "left_arm", "right_arm"],
            3 => ["body", "left_leg", "right_leg"],
            2 => ["left_leg", "right_leg"],
            _ => []
        };
        var grow = slot == 3 ? 0.5f : 1.0f; // leggings use the inner deformation, everything else the outer one
        var result = new List<EntityModelPart>(parts.Count + visible.Length);
        result.AddRange(parts);
        foreach (var part in parts)
        {
            if (Array.IndexOf(visible, part.Name) < 0)
            {
                continue;
            }

            result.Add(part with
            {
                Name = "equipment_" + kind + "_" + slot + "_" + part.Name,
                PoseSource = part.PoseSource ?? part.Name,
                TexturePath = fallbackTexture,
                TextureSelector = state => EntityModelResolver.ResolveEquipmentTexture(state, kind, slot),
                Grow = grow,
                OverlayLayers = null,
                TextureWidth = 64,
                TextureHeight = 32,
                VisibleWhen = state => EntityModelResolver.ResolveEquipmentTexture(state, kind, slot) is not null
            });
        }

        return [.. result];
    }

    public static EntityModelPart[] WithHorseMarkings(IReadOnlyList<EntityModelPart> parts, string texturePrefix, bool baby)
    {
        (int Id, string Key)[] markings = [(1, "white"), (2, "whitefield"), (3, "whitedots"), (4, "blackdots")];
        var result = new List<EntityModelPart>(parts.Count * (markings.Length + 1));
        result.AddRange(parts);
        foreach (var (id, key) in markings)
        {
            var path = texturePrefix + key + (baby ? "_baby" : string.Empty);
            foreach (var part in parts)
            {
                result.Add(part with
                {
                    Name = "marking_" + key + "_" + part.Name,
                    PoseSource = part.PoseSource ?? part.Name,
                    TexturePath = path,
                    Grow = part.Grow + 0.05f,
                    VisibleWhen = state => state.Markings == id
                });
            }
        }

        return [.. result];
    }
}






