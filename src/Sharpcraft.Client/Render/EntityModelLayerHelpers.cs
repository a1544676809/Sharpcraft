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

    /// <summary>
    /// Adds a tree-derived collar layer without flattening the base model. Cat uses the reference 0.01 growth;
    /// Wolf's collar layer re-renders the same geometry with the collar texture and therefore uses zero growth.
    /// </summary>
    public static CompiledEntityModel WithCollar(
        CompiledEntityModel model,
        string texturePath,
        float inflate,
        Func<EntityRenderState, bool>? visible = null)
    {
        var visibleWhen = visible ?? (static state => state.HasCollar);
        var tint = (EntityRenderState state) =>
            state.CollarTint == default ? Vector3.One : state.CollarTint;
        var clonedParts = new List<EntityModelPartNode>();

        EntityModelPartNode CloneNode(EntityModelPartNode source, string? parent)
        {
            var name = "collar_" + source.Name;
            var cubes = source.Cubes
                .Select(cube => cube with
                {
                    Name = "collar_" + cube.Name,
                    TexturePath = texturePath,
                    Grow = cube.Grow + inflate,
                    GrowXyz = cube.GrowXyz is { } grow
                        ? new Vector3(grow.X + inflate, grow.Y + inflate, grow.Z + inflate)
                        : null
                })
                .ToArray();
            var children = source.Children.Select(child => CloneNode(child, name)).ToArray();
            var node = new EntityModelPartNode(
                name,
                parent,
                source.PivotX,
                source.PivotY,
                source.PivotZ,
                source.RotationX,
                source.RotationY,
                source.RotationZ,
                cubes,
                children,
                visibleWhen: state => visibleWhen(state) && (source.VisibleWhen?.Invoke(state) ?? true),
                rotationSelector: source.RotationSelector,
                yOffsetSelector: source.YOffsetSelector,
                poseSource: source.PoseSource ?? source.Name,
                tintSelector: tint);
            clonedParts.Add(node);
            return node;
        }

        var children = new List<EntityModelPartNode>(model.Root.Children.Count * 2);
        children.AddRange(model.Root.Children);
        foreach (var child in model.Root.Children)
        {
            children.Add(CloneNode(child, parent: null));
        }

        var root = new EntityModelPartNode(
            model.Root.Name,
            parent: null,
            pivotX: 0f,
            pivotY: 0f,
            pivotZ: 0f,
            rotationX: 0f,
            rotationY: 0f,
            rotationZ: 0f,
            cubes: [],
            children);

        var parts = new List<EntityModelPartNode>(model.Parts.Count + clonedParts.Count);
        parts.AddRange(model.Parts);
        parts.AddRange(clonedParts);
        var cubes = new List<EntityModelCube>(model.Cubes.Count + clonedParts.Sum(part => part.Cubes.Count));
        cubes.AddRange(model.Cubes);
        foreach (var part in clonedParts)
        {
            cubes.AddRange(part.Cubes);
        }

        return new CompiledEntityModel(root, parts, cubes);
    }

    /// <summary>
    /// Builds a tree-derived emissive subset layer using the reference retainExactParts semantics: only the named
    /// source nodes keep their cubes; ancestor group nodes are retained to preserve the pose chain, while selected
    /// nodes do not gain descendants they did not retain.
    /// </summary>
    public static CompiledEntityModel WithEmissiveLayer(
        CompiledEntityModel model,
        string prefix,
        string texturePath,
        IReadOnlySet<string> sourceNames)
    {
        var layerParts = new List<EntityModelPartNode>();

        EntityModelPartNode? CloneNode(EntityModelPartNode source, string? parent)
        {
            var selected = sourceNames.Contains(source.Name);
            var children = new List<EntityModelPartNode>();
            foreach (var child in source.Children)
            {
                var cloned = CloneNode(child, prefix + source.Name);
                if (cloned is not null)
                {
                    children.Add(cloned);
                }
            }

            if (!selected && children.Count == 0)
            {
                return null;
            }

            var cubes = selected
                ? source.Cubes.Select(cube => cube with
                {
                    Name = prefix + cube.Name,
                    EmissiveOnly = true,
                    IncludeInEmissive = true,
                    EmissiveTexturePath = texturePath
                }).ToArray()
                : [];
            var node = new EntityModelPartNode(
                prefix + source.Name,
                parent,
                source.PivotX,
                source.PivotY,
                source.PivotZ,
                source.RotationX,
                source.RotationY,
                source.RotationZ,
                cubes,
                children,
                visibleWhen: source.VisibleWhen,
                rotationSelector: source.RotationSelector,
                yOffsetSelector: source.YOffsetSelector,
                poseSource: source.PoseSource ?? source.Name,
                tintSelector: source.TintSelector);
            layerParts.Add(node);
            return node;
        }

        var children = new List<EntityModelPartNode>(model.Root.Children.Count * 2);
        children.AddRange(model.Root.Children);
        foreach (var child in model.Root.Children)
        {
            var cloned = CloneNode(child, parent: null);
            if (cloned is not null)
            {
                children.Add(cloned);
            }
        }

        var root = new EntityModelPartNode(
            model.Root.Name,
            parent: null,
            pivotX: 0f,
            pivotY: 0f,
            pivotZ: 0f,
            rotationX: 0f,
            rotationY: 0f,
            rotationZ: 0f,
            cubes: [],
            children);

        var parts = new List<EntityModelPartNode>(model.Parts.Count + layerParts.Count);
        parts.AddRange(model.Parts);
        parts.AddRange(layerParts);
        var cubes = new List<EntityModelCube>(model.Cubes.Count + layerParts.Sum(part => part.Cubes.Count));
        cubes.AddRange(model.Cubes);
        foreach (var part in layerParts)
        {
            cubes.AddRange(part.Cubes);
        }

        return new CompiledEntityModel(root, parts, cubes);
    }

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
            // Reference ELYTRA_BABY = ElytraModel.createLayer().apply(ElytraModel.BABY_TRANSFORMER). Build the reference
            // 10x20x2 wings in the ElytraModel frame and run them through the shared vanilla scaling bake (factor 0.5,
            // yOffset = 24.016 * (1 - 0.5) = 12.008). The WingsLayer translate(0,0,0.125) (+2 model px) is applied after
            // the bake so it is not scaled, matching the adult path; the bake pins UvWidth/Height/Depth to the original
            // 10x20x2 cube so baby wings sample the same texels as the adult (T-663). The baked vertical coordinates are
            // snapped to whole model pixels (11.992 -> 12, 1.992 -> 2) so the historical whole-pixel baby pivots stay
            // byte-identical.
            var referenceWings = BuildElytraWings(24f, 0f);
            var layerZ = body.CentreZ + 2f;
            foreach (var wing in VanillaMeshBake.Scale(referenceWings, 0.5f, modelHeight: 24f))
            {
                var isRight = wing.Name == "equipment_Elytra_4_right_wing";
                result.Add(wing with
                {
                    Bottom = MathF.Round(wing.Bottom),
                    PivotY = MathF.Round(wing.PivotY),
                    CentreZ = wing.CentreZ + layerZ,
                    PivotZ = wing.PivotZ + layerZ,
                    TextureWidth = 64,
                    TextureHeight = 32,
                    YOffsetSelector = static state => state.IsCrouching ? -1.5f : 0f,
                    VisibleWhen = static state => EntityModelResolver.ResolveEquipmentTexture(state, EquipmentAssetKind.Elytra, 4) is not null,
                    RotationSelector = isRight
                        ? static state => new System.Numerics.Vector3(-state.ElytraRotX, state.ElytraRotY, -state.ElytraRotZ)
                        : static state => new System.Numerics.Vector3(-state.ElytraRotX, -state.ElytraRotY, state.ElytraRotZ)
                });
            }

            return [.. result];
        }

        // Wings hang from the body's top (shoulder line) down 20 px, in the same model space as the body.
        // FromVanilla with modelHeight = bodyTop reproduces the reference ElytraModel box (bottom = modelHeight - 20,
        // pivotY = modelHeight) without relying on an absolute modelHeight. The pivot Z is shifted by the WingsLayer
        // translate(0,0,0.125) (+2 model px) so both the pivot and the box move back together.
        var bodyTop = body.Bottom + body.Height;
        var wingPivotZ = body.CentreZ + 2f;
        foreach (var wing in BuildElytraWings(bodyTop, wingPivotZ))
        {
            var isRight = wing.Name == "equipment_Elytra_4_right_wing";
            result.Add(wing with
            {
                TextureWidth = 64,
                TextureHeight = 32,
                YOffsetSelector = static state => state.IsCrouching ? -3f : 0f,
                VisibleWhen = static state => EntityModelResolver.ResolveEquipmentTexture(state, EquipmentAssetKind.Elytra, 4) is not null,
                RotationSelector = isRight
                    ? static state => new System.Numerics.Vector3(-state.ElytraRotX, state.ElytraRotY, -state.ElytraRotZ)
                    : static state => new System.Numerics.Vector3(-state.ElytraRotX, -state.ElytraRotY, state.ElytraRotZ)
            });
        }

        return [.. result];
    }

    private static EntityModelPart[] BuildElytraWings(float modelHeight, float wingPivotZ)
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("equipment_Elytra_4_left_wing",
            VanillaPartPose.Offset(5f, 0f, wingPivotZ),
            new VanillaCubeDefinition("equipment_Elytra_4_left_wing", 22, 0, -10f, 0f, 0f, 10f, 20f, 2f,
                Grow: 1f, Kind: EntityGeometryLayerKind.Elytra));
        root.AddOrReplaceChild("equipment_Elytra_4_right_wing",
            VanillaPartPose.Offset(-5f, 0f, wingPivotZ),
            new VanillaCubeDefinition("equipment_Elytra_4_right_wing", 22, 0, 0f, 0f, 0f, 10f, 20f, 2f,
                Mirror: true, Grow: 1f, Kind: EntityGeometryLayerKind.Elytra));
        return [.. VanillaMeshBake.Bake(new VanillaMeshDefinition(root), 0, 0,
            modelHeight: modelHeight, texturePath: "entity/equipment/wings/elytra")];
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
