// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Builds the static player model from <see cref="PlayerModelDefinition"/> and the default skin. This is render-only:
/// it consumes <see cref="EntityRenderState"/> and never touches world or entity simulation.
/// </summary>
public sealed class EntityModelMesher
{
    private readonly DynamicGeometry _geometry = new();
    private readonly DynamicGeometry[] _layerGeometries = [new(), new(), new()];

    private static readonly EntityGeometryLayerKind[] LayerKinds =
        [EntityGeometryLayerKind.Cutout, EntityGeometryLayerKind.Translucent, EntityGeometryLayerKind.Emissive];

    /// <summary>Merged geometry of every layer, kept for callers that draw entities in one pass.</summary>
    public DynamicGeometry Build(ReadOnlySpan<EntityRenderState> entities, SpriteRect skin, int skinWidth, int skinHeight, IReadOnlyDictionary<string, FaceUvTransform>? transforms = null, string? onlyPart = null, string? excludedPart = null, EntitySkinAtlas? skinAtlas = null, EntityVariantTable? variants = null)
    {
        var layers = BuildLayers(entities, skin, skinWidth, skinHeight, transforms, onlyPart, excludedPart, skinAtlas, variants);
        _geometry.Clear();
        var totalVertexFloats = 0;
        var totalIndexCount = 0;
        foreach (var layer in layers)
        {
            totalVertexFloats += layer.Geometry.VertexFloats;
            totalIndexCount += layer.Geometry.IndexCount;
        }

        _geometry.Reserve(totalVertexFloats, totalIndexCount);
        var vertexBase = 0;
        var indexOffset = 0;
        foreach (var layer in layers)
        {
            var geometry = layer.Geometry;
            Array.Copy(geometry.Vertices, 0, _geometry.Vertices, vertexBase, geometry.VertexFloats);
            for (var i = 0; i < geometry.IndexCount; i++)
            {
                _geometry.Indices[indexOffset + i] = (uint)(geometry.Indices[i] + (vertexBase / 14));
            }

            vertexBase += geometry.VertexFloats;
            indexOffset += geometry.IndexCount;
        }

        _geometry.VertexFloats = totalVertexFloats;
        _geometry.IndexCount = totalIndexCount;
        return _geometry;
    }

    /// <summary>
    /// Builds one geometry per reference render type, in draw order (cutout, translucent, emissive).
    /// </summary>
    public IReadOnlyList<EntityGeometryLayer> BuildLayers(ReadOnlySpan<EntityRenderState> entities, SpriteRect skin, int skinWidth, int skinHeight, IReadOnlyDictionary<string, FaceUvTransform>? transforms = null, string? onlyPart = null, string? excludedPart = null, EntitySkinAtlas? skinAtlas = null, EntityVariantTable? variants = null)
    {
        var layers = new List<EntityGeometryLayer>(3);
        for (var i = 0; i < LayerKinds.Length; i++)
        {
            var kind = LayerKinds[i];
            var geometry = _layerGeometries[i];
            var emissivePass = kind == EntityGeometryLayerKind.Emissive;
            BuildInto(geometry, kind, entities, skin, skinWidth, skinHeight, transforms, onlyPart, excludedPart, skinAtlas, variants, emissivePass);
            if (geometry.IndexCount > 0)
            {
                layers.Add(new EntityGeometryLayer(kind.ToString().ToLowerInvariant(), kind, i, geometry));
            }
        }

        return layers;
    }

    private DynamicGeometry BuildInto(DynamicGeometry target, EntityGeometryLayerKind? kindFilter, ReadOnlySpan<EntityRenderState> entities, SpriteRect skin, int skinWidth, int skinHeight, IReadOnlyDictionary<string, FaceUvTransform>? transforms = null, string? onlyPart = null, string? excludedPart = null, EntitySkinAtlas? skinAtlas = null, EntityVariantTable? variants = null, bool emissivePass = false)
    {
        target.Clear();
        var cubeCount = 0;
        foreach (var entity in entities)
        {
            var model = ResolveModel(entity, variants);
            foreach (var part in model.Parts)
            {
                cubeCount += 1 + (part.OverlayLayers?.Count ?? 0);
            }
        }

        target.Reserve((cubeCount + entities.Length * 2) * 24 * 14, (cubeCount + entities.Length * 2) * 36);
        var vertexFloats = 0;
        var indexCount = 0;

        foreach (var entity in entities)
        {
            var model = ResolveModel(entity, variants);
            if (model is null)
            {
                continue;
            }

            if (emissivePass && model.EmissiveTexturePath is null)
            {
                continue;
            }

            var px = (1f / 16f) * EntityPlaceholderMesher.ModelScale * model.Scale;
            var feet = Vector3.Zero;
            var entityTransform = EntityModelTransform.From(entity);
            var sprite = skin;
            var skinWidthForModel = skinWidth;
            var skinHeightForModel = skinHeight;
            if (emissivePass)
            {
                if (skinAtlas?.Get(model.EmissiveTexturePath) is not { } eyeSprite)
                {
                    continue;
                }

                sprite = eyeSprite;
                skinWidthForModel = eyeSprite.Width;
                skinHeightForModel = eyeSprite.Height;
            }
            else if (skinAtlas?.Get(model.TexturePath) is { } atlasSprite)
            {
                sprite = atlasSprite;
                skinWidthForModel = atlasSprite.Width;
                skinHeightForModel = atlasSprite.Height;
            }

            foreach (var part in model.Parts)
            {
                var centreX = part.CentreX * px;
                var centreZ = part.CentreZ * px;
                var bottom = part.Bottom * px;
                var halfW = part.Width * px * 0.5f;
                var halfD = part.Depth * px * 0.5f;
                var grow = part.Grow * px;
                var min = new Vector3(feet.X + centreX - halfW - grow, feet.Y + bottom - grow, feet.Z + centreZ - halfD - grow);
                var max = new Vector3(feet.X + centreX + halfW + grow, feet.Y + bottom + (part.Height * px) + grow, feet.Z + centreZ + halfD + grow);
                if (onlyPart is not null && part.Name != onlyPart) continue;
                if (excludedPart is not null && part.Name == excludedPart) continue;
                if (!entity.HasChest && (part.Name == "right_chest" || part.Name == "left_chest")) continue;
                if (!entity.HasEgg && part.Name == "egg_belly") continue;
                if (part.Width == 0f && part.Height == 0f && part.Depth == 0f)
                {
                    // Group node (the reference has empty ModelParts such as wolf "head"/"tail"); it only exists to
                    // carry a pivot for its children.
                    continue;
                }

                var partSprite = sprite;
                var partSkinWidth = skinWidthForModel;
                var partSkinHeight = skinHeightForModel;
                if (!emissivePass && part.TexturePath is { Length: > 0 } partPath && skinAtlas?.Get(partPath) is { } partAtlas)
                {
                    partSprite = partAtlas;
                    partSkinWidth = partAtlas.Width;
                    partSkinHeight = partAtlas.Height;
                }

                AddPart(part, target, kindFilter, emissivePass, min, max, partSprite, partSkinWidth, partSkinHeight, feet, entityTransform, PartMatrix(part, model.Parts, px), transforms, model.FaceTransforms, entity, skinAtlas, px, ref vertexFloats, ref indexCount);
            }
        }

        target.VertexFloats = vertexFloats;
        target.IndexCount = indexCount;
        return target;
    }

    private static EntityModelDefinition ResolveModel(EntityRenderState entity, EntityVariantTable? variants) =>
        variants is null
            ? EntityModelResolver.Resolve(entity.TypeId)
            : variants.Resolve(entity.TypeId, entity.VariantId);

    /// <summary>
    /// Emits the six faces in the reference <c>ModelPart.Cube</c> order. The UV corners below are the exact
    /// Polygon constructor values; no per-face rectangle flip or 180-degree rotation is applied.
    /// </summary>
    private void AddPart(
        EntityModelPart part,
        DynamicGeometry target,
        EntityGeometryLayerKind? kindFilter,
        bool emissivePass,
        Vector3 min,
        Vector3 max,
        SpriteRect skin,
        int skinWidth,
        int skinHeight,
        Vector3 feet,
        EntityModelTransform entityTransform,
        Matrix4x4 partMatrix,
        IReadOnlyDictionary<string, FaceUvTransform>? transforms,
        IReadOnlyDictionary<string, FaceUvTransform> defaultTransforms,
        EntityRenderState renderState,
        EntitySkinAtlas? skinAtlas,
        float px,
        ref int vertexFloats,
        ref int indexCount)
    {
        var d = part.Depth;
        var w = part.Width;
        var h = part.Height;
        var tx = part.TextureX;
        var ty = part.TextureY;

        var u0 = tx;
        var u1 = tx + d;
        var u2 = tx + d + w;
        var u22 = tx + d + (2f * w);
        var u3 = tx + d + w + d;
        var u4 = tx + d + w + d + w;
        var v0 = ty;
        var v1 = ty + d;
        var v2 = ty + d + h;

        var t0 = new Vector3(min.X, min.Y, min.Z);
        var t1 = new Vector3(max.X, min.Y, min.Z);
        var t2 = new Vector3(max.X, max.Y, min.Z);
        var t3 = new Vector3(min.X, max.Y, min.Z);
        var l0 = new Vector3(min.X, min.Y, max.Z);
        var l1 = new Vector3(max.X, min.Y, max.Z);
        var l2 = new Vector3(max.X, max.Y, max.Z);
        var l3 = new Vector3(min.X, max.Y, max.Z);
        // The part matrix already carries this part's own rotation and the whole parent chain, so the corners only
        // need the matrix and then the entity-level transform.
        Vector3 ToWorld(Vector3 point) => entityTransform.Apply(Vector3.Transform(point, partMatrix));
        t0 = ToWorld(t0); t1 = ToWorld(t1); t2 = ToWorld(t2); t3 = ToWorld(t3);
        l0 = ToWorld(l0); l1 = ToWorld(l1); l2 = ToWorld(l2); l3 = ToWorld(l3);
        FaceUvTransform T(string face)
        {
            var fallback = defaultTransforms.TryGetValue(face, out var defaultTransform)
                ? defaultTransform
                : FaceUvTransform.Identity;
            var result = transforms is not null && transforms.TryGetValue(part.Name + "." + face, out var value)
                ? value
                : fallback;
            // CubeListBuilder.mirror flips U for every face of the part.
            return part.Mirror ? result with { FlipH = !result.FlipH } : result;
        }

        var emitBase = emissivePass || kindFilter is null || part.Kind == kindFilter;
        var swapBodyDownUp = true;
        var d0u = swapBodyDownUp ? u22 : u2; var d0v = swapBodyDownUp ? v1 : v0;
        var d1u = swapBodyDownUp ? u2 : u1; var d1v = swapBodyDownUp ? v1 : v0;
        var d2u = swapBodyDownUp ? u2 : u1; var d2v = swapBodyDownUp ? v0 : v1;
        var d3u = swapBodyDownUp ? u22 : u2; var d3v = swapBodyDownUp ? v0 : v1;
        var u0u = swapBodyDownUp ? u2 : u22; var u0v = swapBodyDownUp ? v0 : v1;
        var u1u = swapBodyDownUp ? u1 : u2; var u1v = swapBodyDownUp ? v0 : v1;
        var u2u = swapBodyDownUp ? u1 : u2; var u2v = swapBodyDownUp ? v1 : v0;
        var u3u = swapBodyDownUp ? u2 : u22; var u3v = swapBodyDownUp ? v1 : v0;

        if (emitBase)
        {
        AddFace(l1, l0, t0, t1, d0u, d0v, d1u, d1v, d2u, d2v, d3u, d3v, skin, skinWidth, skinHeight, ref vertexFloats, ref indexCount, T("DOWN"), target); // DOWN
        AddFace(t2, t3, l3, l2, u0u, u0v, u1u, u1v, u2u, u2v, u3u, u3v, skin, skinWidth, skinHeight, ref vertexFloats, ref indexCount, T("UP"), target); // UP
        AddFace(t0, l0, l3, t3, u3, v2, u2, v2, u2, v1, u3, v1, skin, skinWidth, skinHeight, ref vertexFloats, ref indexCount, T("WEST"), target); // WEST
        AddFace(t1, t0, t3, t2, u2, v2, u1, v2, u1, v1, u2, v1, skin, skinWidth, skinHeight, ref vertexFloats, ref indexCount, T("NORTH"), target); // NORTH
        AddFace(l1, t1, t2, l2, u1, v2, u0, v2, u0, v1, u1, v1, skin, skinWidth, skinHeight, ref vertexFloats, ref indexCount, T("EAST"), target); // EAST
        AddFace(l0, l1, l2, l3, u4, v2, u3, v2, u3, v1, u4, v1, skin, skinWidth, skinHeight, ref vertexFloats, ref indexCount, T("SOUTH"), target); // SOUTH
        }


        if (!emissivePass && part.OverlayLayers is not null)
        {
            foreach (var layer in part.OverlayLayers)
            {
                if (!LayerVisible(layer.Name, renderState))
                {
                    continue;
                }

                if (kindFilter is not null && layer.Kind != kindFilter)
                {
                    continue;
                }

                var grow = layer.Grow * px;
                var overlay = new EntityModelPart(
                    part.Name,
                    part.CentreX,
                    part.Bottom,
                    part.Width,
                    part.Height,
                    part.Depth,
                    layer.TextureX,
                    layer.TextureY,
                    layer.Mirror || part.Mirror,
                    null,
                    part.CentreZ,
                    part.PivotX,
                    part.PivotY,
                    part.PivotZ,
                    part.RotationX,
                    part.RotationY,
                    part.RotationZ,
                    Kind: layer.Kind);
                var overlayMin = new Vector3(min.X - grow, min.Y - grow, min.Z - grow);
                var overlayMax = new Vector3(max.X + grow, max.Y + grow, max.Z + grow);
                var overlaySkin = skin;
                var overlayWidth = skinWidth;
                var overlayHeight = skinHeight;
                if (layer.TexturePath is { Length: > 0 } layerPath && skinAtlas?.Get(layerPath) is { } layerSprite)
                {
                    overlaySkin = layerSprite;
                    overlayWidth = layerSprite.Width;
                    overlayHeight = layerSprite.Height;
                }

                AddPart(overlay, target, kindFilter, emissivePass, overlayMin, overlayMax, overlaySkin, overlayWidth, overlayHeight, feet, entityTransform, partMatrix, transforms, defaultTransforms, renderState, skinAtlas, px, ref vertexFloats, ref indexCount);
            }
        }
    }

    private static Matrix4x4 RotationZyx(float rx, float ry, float rz) =>
        Matrix4x4.CreateRotationZ(rz) * Matrix4x4.CreateRotationY(ry) * Matrix4x4.CreateRotationX(rx);

    /// <summary>
    /// Matrix that maps a point expressed relative to <paramref name="part"/>'s origin into entity space. This is the
    /// frame other parts' pivots are expressed in, not the frame a part's own cube is stored in.
    /// </summary>
    private static Matrix4x4 PartFrame(EntityModelPart part, IReadOnlyList<EntityModelPart> parts, float px)
    {
        var pivot = new Vector3(part.PivotX * px, part.PivotY * px, part.PivotZ * px);
        // Row-vector convention: "rotate, then translate" is Rotation * Translation.
        var local = RotationZyx(part.RotationX, part.RotationY, part.RotationZ) * Matrix4x4.CreateTranslation(pivot);

        if (part.Parent is not { Length: > 0 } parentName)
        {
            return local;
        }

        foreach (var candidate in parts)
        {
            if (string.Equals(candidate.Name, parentName, StringComparison.Ordinal))
            {
                return local * PartFrame(candidate, parts, px);
            }
        }

        return local;
    }

    /// <summary>
    /// Matrix that maps a part's stored cube coordinates into entity space.
    /// </summary>
    /// <remarks>
    /// Root parts keep absolute model-space cubes (from <c>FromVanilla</c>), so they are rotated about their pivot with
    /// the extra translate back. Child parts keep cubes relative to their own origin (from <c>FromVanillaChild</c>), so
    /// their frame is already the storage transform.
    /// </remarks>
    private static Matrix4x4 PartMatrix(EntityModelPart part, IReadOnlyList<EntityModelPart> parts, float px)
    {
        var frame = PartFrame(part, parts, px);
        if (part.Parent is { Length: > 0 })
        {
            return frame;
        }

        var pivot = new Vector3(part.PivotX * px, part.PivotY * px, part.PivotZ * px);
        return Matrix4x4.CreateTranslation(-pivot) * frame;
    }

    private static bool LayerVisible(string name, EntityRenderState state) => name switch
    {
        "hat" => state.ShowHat,
        "jacket" => state.ShowJacket,
        "left_sleeve" => state.ShowLeftSleeve,
        "right_sleeve" => state.ShowRightSleeve,
        "left_pants" => state.ShowLeftPants,
        "right_pants" => state.ShowRightPants,
        _ => true
    };

    private void AddFace(
        Vector3 a, Vector3 b, Vector3 c, Vector3 d,
        float au, float av, float bu, float bv, float cu, float cv, float du, float dv,
        SpriteRect skin, int skinWidth, int skinHeight,
        ref int vertexFloats, ref int indexCount,
        FaceUvTransform transform,
        DynamicGeometry target)
    {
        var uMin = MathF.Min(MathF.Min(au, bu), MathF.Min(cu, du));
        var uMax = MathF.Max(MathF.Max(au, bu), MathF.Max(cu, du));
        var vMin = MathF.Min(MathF.Min(av, bv), MathF.Min(cv, dv));
        var vMax = MathF.Max(MathF.Max(av, bv), MathF.Max(cv, dv));
        (au, av) = TransformUv(au, av, uMin, uMax, vMin, vMax, transform);
        (bu, bv) = TransformUv(bu, bv, uMin, uMax, vMin, vMax, transform);
        (cu, cv) = TransformUv(cu, cv, uMin, uMax, vMin, vMax, transform);
        (du, dv) = TransformUv(du, dv, uMin, uMax, vMin, vMax, transform);
        var first = (uint)(vertexFloats / 14);
        WriteVertex(target.Vertices, ref vertexFloats, a, au / skinWidth, av / skinHeight, skin);
        WriteVertex(target.Vertices, ref vertexFloats, b, bu / skinWidth, bv / skinHeight, skin);
        WriteVertex(target.Vertices, ref vertexFloats, c, cu / skinWidth, cv / skinHeight, skin);
        WriteVertex(target.Vertices, ref vertexFloats, d, du / skinWidth, dv / skinHeight, skin);

        target.Indices[indexCount++] = first;
        target.Indices[indexCount++] = first + 1;
        target.Indices[indexCount++] = first + 2;
        target.Indices[indexCount++] = first;
        target.Indices[indexCount++] = first + 2;
        target.Indices[indexCount++] = first + 3;
    }

    private static (float U, float V) TransformUv(float u, float v, float uMin, float uMax, float vMin, float vMax, FaceUvTransform t)
    {
        var nu = (u - uMin) / MathF.Max(uMax - uMin, 1e-6f);
        var nv = (v - vMin) / MathF.Max(vMax - vMin, 1e-6f);
        if (t.FlipH) nu = 1f - nu;
        if (t.FlipV) nv = 1f - nv;
        (nu, nv) = (t.Rotation % 360) switch
        {
            90 => (nv, 1f - nu),
            180 => (1f - nu, 1f - nv),
            270 => (1f - nv, nu),
            _ => (nu, nv)
        };
        return (uMin + nu * (uMax - uMin), vMin + nv * (vMax - vMin));
    }

    private static void WriteVertex(float[] destination, ref int offset, Vector3 world, float u, float v, SpriteRect skin)
    {
        destination[offset++] = world.X;
        destination[offset++] = world.Y;
        destination[offset++] = world.Z;
        destination[offset++] = 1f;
        destination[offset++] = 1f;
        destination[offset++] = 1f;
        destination[offset++] = u;
        destination[offset++] = v;
        destination[offset++] = skin.U0;
        destination[offset++] = skin.V0;
        destination[offset++] = skin.U1;
        destination[offset++] = skin.V1;
        destination[offset++] = 1f;
        destination[offset++] = 0f;
    }
}



