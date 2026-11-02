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

    public DynamicGeometry Build(ReadOnlySpan<EntityRenderState> entities, SpriteRect skin, int skinWidth, int skinHeight, IReadOnlyDictionary<string, FaceUvTransform>? transforms = null, string? onlyPart = null, string? excludedPart = null, EntitySkinAtlas? skinAtlas = null)
    {
        _geometry.Clear();
        var cubeCount = 0;
        foreach (var entity in entities)
        {
            var model = EntityModelResolver.Resolve(entity.TypeId);
            foreach (var part in model.Parts)
            {
                cubeCount += 1 + (part.OverlayLayers?.Count ?? 0);
            }
        }

        _geometry.Reserve((cubeCount + entities.Length * 2) * 24 * 14, (cubeCount + entities.Length * 2) * 36);
        var vertexFloats = 0;
        var indexCount = 0;

        foreach (var entity in entities)
        {
            var model = EntityModelResolver.Resolve(entity.TypeId);
            if (model is null)
            {
                continue;
            }

            var px = (1f / 16f) * EntityPlaceholderMesher.ModelScale * model.Scale;
            var feet = new Vector3(entity.X, entity.Y, entity.Z);
            var sprite = skin;
            var skinWidthForModel = skinWidth;
            var skinHeightForModel = skinHeight;
            if (skinAtlas?.Get(model.TexturePath) is { } atlasSprite)
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
                var min = new Vector3(feet.X + centreX - halfW, feet.Y + bottom, feet.Z + centreZ - halfD);
                var max = new Vector3(feet.X + centreX + halfW, feet.Y + bottom + (part.Height * px), feet.Z + centreZ + halfD);
                if (onlyPart is not null && part.Name != onlyPart) continue;
                if (excludedPart is not null && part.Name == excludedPart) continue;
                AddPart(part, min, max, sprite, skinWidthForModel, skinHeightForModel, feet, transforms, model.FaceTransforms, entity, skinAtlas, px, ref vertexFloats, ref indexCount);
            }
        }

        _geometry.VertexFloats = vertexFloats;
        _geometry.IndexCount = indexCount;
        return _geometry;
    }

    /// <summary>
    /// Emits the six faces in the reference <c>ModelPart.Cube</c> order. The UV corners below are the exact
    /// Polygon constructor values; no per-face rectangle flip or 180-degree rotation is applied.
    /// </summary>
    private void AddPart(
        EntityModelPart part,
        Vector3 min,
        Vector3 max,
        SpriteRect skin,
        int skinWidth,
        int skinHeight,
        Vector3 feet,
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
        if (part.RotationX != 0f || part.RotationY != 0f || part.RotationZ != 0f)
        {
            var pivot = feet + new Vector3(part.PivotX * px, part.PivotY * px, part.PivotZ * px);
            Vector3 Apply(Vector3 point) => RotateAround(point, pivot, part.RotationX, part.RotationY, part.RotationZ);
            t0 = Apply(t0); t1 = Apply(t1); t2 = Apply(t2); t3 = Apply(t3);
            l0 = Apply(l0); l1 = Apply(l1); l2 = Apply(l2); l3 = Apply(l3);
        }
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

        var swapBodyDownUp = true;
        var d0u = swapBodyDownUp ? u22 : u2; var d0v = swapBodyDownUp ? v1 : v0;
        var d1u = swapBodyDownUp ? u2 : u1; var d1v = swapBodyDownUp ? v1 : v0;
        var d2u = swapBodyDownUp ? u2 : u1; var d2v = swapBodyDownUp ? v0 : v1;
        var d3u = swapBodyDownUp ? u22 : u2; var d3v = swapBodyDownUp ? v0 : v1;
        var u0u = swapBodyDownUp ? u2 : u22; var u0v = swapBodyDownUp ? v0 : v1;
        var u1u = swapBodyDownUp ? u1 : u2; var u1v = swapBodyDownUp ? v0 : v1;
        var u2u = swapBodyDownUp ? u1 : u2; var u2v = swapBodyDownUp ? v1 : v0;
        var u3u = swapBodyDownUp ? u2 : u22; var u3v = swapBodyDownUp ? v1 : v0;

        AddFace(l1, l0, t0, t1, d0u, d0v, d1u, d1v, d2u, d2v, d3u, d3v, skin, skinWidth, skinHeight, ref vertexFloats, ref indexCount, T("DOWN")); // DOWN
        AddFace(t2, t3, l3, l2, u0u, u0v, u1u, u1v, u2u, u2v, u3u, u3v, skin, skinWidth, skinHeight, ref vertexFloats, ref indexCount, T("UP")); // UP
        AddFace(t0, l0, l3, t3, u3, v2, u2, v2, u2, v1, u3, v1, skin, skinWidth, skinHeight, ref vertexFloats, ref indexCount, T("WEST")); // WEST
        AddFace(t1, t0, t3, t2, u2, v2, u1, v2, u1, v1, u2, v1, skin, skinWidth, skinHeight, ref vertexFloats, ref indexCount, T("NORTH")); // NORTH
        AddFace(l1, t1, t2, l2, u1, v2, u0, v2, u0, v1, u1, v1, skin, skinWidth, skinHeight, ref vertexFloats, ref indexCount, T("EAST")); // EAST
        AddFace(l0, l1, l2, l3, u4, v2, u3, v2, u3, v1, u4, v1, skin, skinWidth, skinHeight, ref vertexFloats, ref indexCount, T("SOUTH")); // SOUTH

        if (part.OverlayLayers is not null)
        {
            foreach (var layer in part.OverlayLayers)
            {
                if (!LayerVisible(layer.Name, renderState))
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
                    part.RotationZ);
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

                AddPart(overlay, overlayMin, overlayMax, overlaySkin, overlayWidth, overlayHeight, feet, transforms, defaultTransforms, renderState, skinAtlas, px, ref vertexFloats, ref indexCount);
            }
        }
    }

    private static Vector3 RotateAround(Vector3 point, Vector3 pivot, float rx, float ry, float rz)
    {
        var local = point - pivot;
        if (rz != 0f) local = Vector3.Transform(local, Matrix4x4.CreateRotationZ(rz));
        if (ry != 0f) local = Vector3.Transform(local, Matrix4x4.CreateRotationY(ry));
        if (rx != 0f) local = Vector3.Transform(local, Matrix4x4.CreateRotationX(rx));
        return pivot + local;
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
        FaceUvTransform transform)
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
        WriteVertex(_geometry.Vertices, ref vertexFloats, a, au / skinWidth, av / skinHeight, skin);
        WriteVertex(_geometry.Vertices, ref vertexFloats, b, bu / skinWidth, bv / skinHeight, skin);
        WriteVertex(_geometry.Vertices, ref vertexFloats, c, cu / skinWidth, cv / skinHeight, skin);
        WriteVertex(_geometry.Vertices, ref vertexFloats, d, du / skinWidth, dv / skinHeight, skin);

        _geometry.Indices[indexCount++] = first;
        _geometry.Indices[indexCount++] = first + 1;
        _geometry.Indices[indexCount++] = first + 2;
        _geometry.Indices[indexCount++] = first;
        _geometry.Indices[indexCount++] = first + 2;
        _geometry.Indices[indexCount++] = first + 3;
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

    private static void WriteVertex(float[] destination, ref int offset, Vector3 position, float u, float v, SpriteRect skin)
    {
        destination[offset++] = position.X;
        destination[offset++] = position.Y;
        destination[offset++] = position.Z;
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
