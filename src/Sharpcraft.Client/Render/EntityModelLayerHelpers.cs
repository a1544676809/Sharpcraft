// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;

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
                OverlayLayers = slot == 5 ? HatOnly(part) : null,
                TextureWidth = 64,
                TextureHeight = 32,
                VisibleWhen = state => EntityModelResolver.ResolveEquipmentTexture(state, kind, slot) is not null
            });
        }

        return [.. result];
    }

    /// <summary>Keeps only the reference hat box overlay (the helmet's outer layer); its texture follows the armor sprite.</summary>
    private static IReadOnlyList<EntityModelLayer>? HatOnly(EntityModelPart part)
    {
        if (part.OverlayLayers is not { } layers)
        {
            return null;
        }

        foreach (var layer in layers)
        {
            if (string.Equals(layer.Name, "hat", StringComparison.Ordinal))
            {
                return [layer];
            }
        }

        return null;
    }

    private static readonly Vector3 BabyOuterGrow = new(-0.1f, 0.5f, 0.3f);
    private static readonly Vector3 BabyInnerGrow = new(-0.1f, 0.3f, 0.3f);

    /// <summary>Baby humanoid armor layer for one slot: dedicated baby mesh parts with per-axis deformation.</summary>
    public static EntityModelPart[] WithBabyArmorSlot(IReadOnlyList<EntityModelPart> parts, EquipmentAssetKind kind, int slot, string fallbackTexture)
    {
        string[] visible = slot switch
        {
            5 => ["head"],
            4 => ["body", "left_arm", "right_arm"],
            3 => ["left_leg", "right_leg", "waist"],
            2 => ["right_foot", "left_foot"],
            _ => []
        };
        var baseGrow = slot == 3 ? BabyInnerGrow : BabyOuterGrow;
        var legInset = new Vector3(0.1f, 0.1f, 0.1f);
        var baseBottoms = new Dictionary<string, float>(StringComparer.Ordinal);
        foreach (var basePart in parts)
        {
            if (!basePart.Name.StartsWith("equipment_", StringComparison.Ordinal))
            {
                baseBottoms[basePart.Name] = basePart.Bottom;
            }
        }

        var result = new List<EntityModelPart>(parts.Count + visible.Length);
        result.AddRange(parts);
        foreach (var part in BabyHumanoidArmorParts.Parts)
        {
            if (Array.IndexOf(visible, part.Name) < 0)
            {
                continue;
            }

            var grow = part.Name is "left_leg" or "right_leg" or "waist" ? baseGrow - legInset : baseGrow;
            var poseSource = BabyPoseSource(part.Name);
            var bottom = baseBottoms.TryGetValue(poseSource, out var baseBottom) ? baseBottom : part.Bottom;
            result.Add(part with
            {
                Name = "equipment_" + kind + "_" + slot + "_" + part.Name,
                Bottom = bottom,
                PoseSource = poseSource,
                TexturePath = fallbackTexture,
                TextureSelector = state => EntityModelResolver.ResolveEquipmentTexture(state, kind, slot),
                Grow = 0f,
                GrowXyz = grow,
                TextureWidth = 64,
                TextureHeight = 64,
                VisibleWhen = state => EntityModelResolver.ResolveEquipmentTexture(state, kind, slot) is not null
            });
        }

        return [.. result];
    }

    private static string BabyPoseSource(string name) => name switch
    {
        "waist" => "body",
        "right_foot" => "left_leg",
        "left_foot" => "right_leg",
        _ => name
    };

    /// <summary>Copies the base parts with a crackiness overlay texture (wolf armor durability levels).</summary>
    public static EntityModelPart[] WithCrackiness(IReadOnlyList<EntityModelPart> parts)
    {
        var result = new List<EntityModelPart>(parts.Count * 2);
        result.AddRange(parts);
        foreach (var part in parts)
        {
            if (part.Name.StartsWith("equipment_", StringComparison.Ordinal) || part.Name.StartsWith("crackiness_", StringComparison.Ordinal))
            {
                continue;
            }

            result.Add(part with
            {
                Name = "crackiness_" + part.Name,
                PoseSource = part.PoseSource ?? part.Name,
                TextureSelector = static state => state.Crackiness switch
                {
                    1 => "entity/wolf/wolf_armor_crackiness_low",
                    2 => "entity/wolf/wolf_armor_crackiness_medium",
                    3 => "entity/wolf/wolf_armor_crackiness_high",
                    _ => null
                },
                Grow = part.Grow + 0.21f,
                OverlayLayers = null,
                VisibleWhen = static state => state.Crackiness > 0
            });
        }

        return [.. result];
    }

    /// <summary>
    /// Adds the two elytra wing boxes (reference <c>ElytraModel.createLayer</c>: pivot ±5, box 10x20x2 at tex 22,0).
    /// Positioned relative to the body top so it works for any humanoid model; wing rotation is driven by
    /// <see cref="ElytraAnimationState"/>. Rendered when the chest slot holds an elytra (glider).
    /// </summary>
    public static EntityModelPart[] WithElytra(IReadOnlyList<EntityModelPart> parts, bool baby = false)
    {
        var result = new List<EntityModelPart>(parts.Count + 2);
        result.AddRange(parts);
        EntityModelPart body = default;
        foreach (var part in parts)
        {
            if (part.Name == "body")
            {
                body = part;
                break;
            }
        }

        if (baby)
        {
            // Reference ELYTRA_BABY = ElytraModel.createLayer().apply(ElytraModel.BABY_TRANSFORMER). Unlike a plain
            // 0.5 scale, MeshTransformer.scaling(0.5F) also applies yOffset = 24.016 * (1 - 0.5) = 12.008 after the
            // scale. In the model's y-down frame the wing box is therefore y 12.008..22.008 and the pivot is y 12.008;
            // converting to our y-up frame (modelHeight 24) gives bottom ≈ 2 and pivotY ≈ 12.
            result.Add(new EntityModelPart(
                "equipment_Elytra_4_left_wing",
                CentreX: 0f,
                Bottom: 2f,
                Width: 5f,
                Height: 10f,
                Depth: 1f,
                TextureX: 22,
                TextureY: 0,
                CentreZ: body.CentreZ + 2.5f,
                PivotX: -2.5f,
                PivotY: 12f,
                PivotZ: body.CentreZ + 2f,
                Grow: 0.5f,
                Kind: EntityGeometryLayerKind.Elytra,
                TexturePath: "entity/equipment/wings/elytra",
                TextureWidth: 64,
                TextureHeight: 32,
                VisibleWhen: static state => EntityModelResolver.ResolveEquipmentTexture(state, EquipmentAssetKind.Elytra, 4) is not null,
                RotationSelector: static state => new System.Numerics.Vector3(-state.ElytraRotX, -state.ElytraRotY, state.ElytraRotZ)));
            result.Add(new EntityModelPart(
                "equipment_Elytra_4_right_wing",
                CentreX: 0f,
                Bottom: 2f,
                Width: 5f,
                Height: 10f,
                Depth: 1f,
                TextureX: 22,
                TextureY: 0,
                Mirror: true,
                CentreZ: body.CentreZ + 2.5f,
                PivotX: 2.5f,
                PivotY: 12f,
                PivotZ: body.CentreZ + 2f,
                Grow: 0.5f,
                Kind: EntityGeometryLayerKind.Elytra,
                TexturePath: "entity/equipment/wings/elytra",
                TextureWidth: 64,
                TextureHeight: 32,
                VisibleWhen: static state => EntityModelResolver.ResolveEquipmentTexture(state, EquipmentAssetKind.Elytra, 4) is not null,
                RotationSelector: static state => new System.Numerics.Vector3(-state.ElytraRotX, state.ElytraRotY, -state.ElytraRotZ)));
            return [.. result];
        }

        // Wings hang from the body's top (shoulder line) down 20 px, in the same model space as the body.
        // FromVanilla with modelHeight = bodyTop reproduces the reference ElytraModel box (bottom = modelHeight - 20,
        // pivotY = modelHeight) without relying on an absolute modelHeight. The pivot Z is shifted by the WingsLayer
        // translate(0,0,0.125) (+2 model px) so both the pivot and the box move back together.
        var bodyTop = body.Bottom + body.Height;
        var wingPivotZ = body.CentreZ + 2f;
        result.Add(PlayerModelDefinition.FromVanilla(
            "equipment_Elytra_4_left_wing", 5f, 0f, wingPivotZ, -10f, 0f, 0f, 10f, 20f, 2f, 22, 0,
            modelHeight: bodyTop,
            texturePath: "entity/equipment/wings/elytra", grow: 1f, kind: EntityGeometryLayerKind.Elytra)
            with
            {
                TextureWidth = 64,
                TextureHeight = 32,
                VisibleWhen = static state => EntityModelResolver.ResolveEquipmentTexture(state, EquipmentAssetKind.Elytra, 4) is not null,
                RotationSelector = static state => new System.Numerics.Vector3(-state.ElytraRotX, -state.ElytraRotY, state.ElytraRotZ)
            });
        result.Add(PlayerModelDefinition.FromVanilla(
            "equipment_Elytra_4_right_wing", -5f, 0f, wingPivotZ, 0f, 0f, 0f, 10f, 20f, 2f, 22, 0, mirror: true,
            modelHeight: bodyTop,
            texturePath: "entity/equipment/wings/elytra", grow: 1f, kind: EntityGeometryLayerKind.Elytra)
            with
            {
                TextureWidth = 64,
                TextureHeight = 32,
                VisibleWhen = static state => EntityModelResolver.ResolveEquipmentTexture(state, EquipmentAssetKind.Elytra, 4) is not null,
                RotationSelector = static state => new System.Numerics.Vector3(-state.ElytraRotX, state.ElytraRotY, -state.ElytraRotZ)
            });
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
