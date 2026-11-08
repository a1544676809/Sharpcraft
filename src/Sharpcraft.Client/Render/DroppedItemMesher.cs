// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Draws dropped items: resolves each item entity's stack to its item model, bakes the model's quads, then applies the
/// reference grounding, bob, spin and stack layout before writing vertices.
/// </summary>
/// <remarks>
/// The bob phase is <c>random.nextFloat() * 2π</c> on the reference entity and is never synced, so two vanilla clients
/// already disagree on it. We derive a stable per-entity phase from the entity id: uniformly distributed like the
/// reference, deterministic within one client so a drop does not jitter frame to frame.
/// </remarks>
[Sharpcraft.Common.Diagnostics.Simplification("SIM-ITEM-DROP-DISPLAY-GUI", "T-761")]
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.entity.ItemEntityRenderer", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/entity/ItemEntityRenderer.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "model cuboids and flat sprites only; special renderers are not drawn here")]
public sealed class DroppedItemMesher
{
    /// <summary>Floats per vertex, matching <see cref="EntityModelMesher"/>'s layout.</summary>
    public const int VertexStride = 14;

    /// <summary>
    /// Reference <c>ItemEntityRenderState.getRenderedAmount</c>: a stack is drawn as 1..5 items depending on how many it
    /// holds, not as one item per unit.
    /// </summary>
    public static int RenderedAmount(int stackCount) => stackCount switch
    {
        <= 1 => 1,
        <= 16 => 2,
        <= 32 => 3,
        <= 48 => 4,
        _ => 5
    };

    /// <summary>Highest number of items any stack is drawn as.</summary>
    public const int MaxDrawnCount = 5;

    /// <summary>Layer name reported back to the renderer.</summary>
    public const string LayerName = "dropped_item";

    private readonly DynamicGeometry _geometry = new();
    private readonly List<ItemModelQuad> _quads = [];

    /// <summary>
    /// Builds one geometry layer for every dropped item in <paramref name="entities"/>, or null when there is nothing to
    /// draw. UVs are sprite-local in 0..1, which is what the item atlas shader expects.
    /// </summary>
    public EntityGeometryLayer? Build(IReadOnlyList<EntityRenderState> entities, ISpriteAtlas itemAtlas)
    {
        ArgumentNullException.ThrowIfNull(itemAtlas);
        if (EntityModelResolver.ItemModels is not { } models || EntityModelResolver.Items is not { } items)
        {
            return null;
        }

        _geometry.Clear();
        var vertexFloats = 0;
        var indexCount = 0;

        foreach (var entity in entities)
        {
            if (entity.TypeId != EntityTracker.ItemEntityTypeId || entity.ItemEntityItemId < 0)
            {
                continue;
            }

            if (items.ById(entity.ItemEntityItemId) is not { } item)
            {
                continue;
            }

            if (models.Resolve(item.Id.ToString()) is not { } resolution)
            {
                continue;
            }

            _quads.Clear();
            CollectQuads(_quads, resolution);
            if (_quads.Count == 0)
            {
                continue;
            }

            var extents = ItemModelExtents.ForDisplay(resolution, ItemDisplayContext.Ground);
            var ground = resolution.Display.TryGetValue(ItemDisplayContext.Ground, out var display)
                ? display.ToMatrix()
                : ItemDisplayTransform.None.ToMatrix();
            var root = ItemEntityRenderer.RootTransform(entity.AgeInTicks, PhaseOf(entity.Id), extents);
            var scattered = ItemEntityRenderer.UsesScatteredLayout(extents);
            var count = RenderedAmount(entity.ItemEntityCount);

            for (var itemIndex = 0; itemIndex < count; itemIndex++)
            {
                var offset = scattered
                    ? ItemEntityRenderer.ThickItemOffset(
                        RandomUnit(entity.Id, itemIndex, 0),
                        RandomUnit(entity.Id, itemIndex, 1),
                        RandomUnit(entity.Id, itemIndex, 2))
                    : ItemEntityRenderer.ThinItemOffset(
                        extents,
                        itemIndex,
                        count,
                        RandomUnit(entity.Id, itemIndex, 0),
                        RandomUnit(entity.Id, itemIndex, 1));

                var world = Matrix4x4.CreateTranslation(offset)
                    * ground
                    * root
                    * Matrix4x4.CreateTranslation(entity.X, entity.Y, entity.Z);

                foreach (var quad in _quads)
                {
                    var sprite = itemAtlas.Get(quad.Texture);
                    if (sprite is null)
                    {
                        continue;
                    }

                    AppendQuad(quad, world, sprite, ref vertexFloats, ref indexCount);
                }
            }
        }

        if (indexCount == 0)
        {
            return null;
        }

        _geometry.VertexFloats = vertexFloats;
        _geometry.IndexCount = indexCount;
        return new EntityGeometryLayer(LayerName, EntityGeometryLayerKind.Cutout, 900, _geometry, usesItemAtlas: true);
    }

    /// <summary>Fills <paramref name="output"/> with the model's quads, falling back to the generated flat sprite box.</summary>
    public static void CollectQuads(List<ItemModelQuad> output, ItemModelResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(resolution);

        var cuboids = resolution.Cuboids;
        if (cuboids.Count > 0)
        {
            foreach (var element in cuboids)
            {
                ItemModelGeometry.Append(output, element);
            }

            return;
        }

        // No elements: the model is a generated sprite. ItemModelGenerator extrudes each layer between z 7.5 and 8.5.
        var texture = resolution.Textures.TryGetValue("layer0", out var layer0) ? IdentifierPath(layer0) : null;
        if (texture is null)
        {
            return;
        }

        var from = new Vector3(0f, 0f, 7.5f);
        var to = new Vector3(16f, 16f, 8.5f);
        var face = new ItemModelFace(texture, ItemModelElement.AutoUv(ModelFaceDirection.South, from, to));
        var back = new ItemModelFace(texture, ItemModelElement.AutoUv(ModelFaceDirection.North, from, to));
        output.Add(BuildQuad(from, to, ModelFaceDirection.South, face));
        output.Add(BuildQuad(from, to, ModelFaceDirection.North, back));
    }

    /// <summary>
    /// Texture paths a dropped item needs in the item atlas. Element models contribute one path per face; generated
    /// models contribute their <c>layer0</c>.
    /// </summary>
    public static IEnumerable<string> ResolveTextures(EntityRenderState entity)
    {
        if (entity.TypeId != EntityTracker.ItemEntityTypeId || entity.ItemEntityItemId < 0)
        {
            yield break;
        }

        if (EntityModelResolver.ItemModels is not { } models ||
            EntityModelResolver.Items?.ById(entity.ItemEntityItemId) is not { } item ||
            models.Resolve(item.Id.ToString()) is not { } resolution)
        {
            yield break;
        }

        var cuboids = resolution.Cuboids;
        if (cuboids.Count > 0)
        {
            foreach (var element in cuboids)
            {
                foreach (var face in element.Faces.Values)
                {
                    if (face.Texture is { } texture)
                    {
                        yield return texture;
                    }
                }
            }

            yield break;
        }

        foreach (var slot in new[] { "layer0", "layer1" })
        {
            if (resolution.Textures.TryGetValue(slot, out var value) && IdentifierPath(value) is { } path)
            {
                yield return path;
            }
        }
    }

    private static ItemModelQuad BuildQuad(Vector3 from, Vector3 to, ModelFaceDirection direction, ItemModelFace face)
    {
        var element = new ItemModelElement(
            from,
            to,
            new Dictionary<ModelFaceDirection, ItemModelFace> { [direction] = face });
        var quads = new List<ItemModelQuad>(1);
        ItemModelGeometry.Append(quads, element);
        return quads[0];
    }

    private static string? IdentifierPath(string value) =>
        Sharpcraft.Common.Identifiers.Identifier.TryParse(value, out var id) ? id.Path : null;

    /// <summary>Stable per-entity bob phase in [0, 2π), standing in for the reference's unsynced random draw.</summary>
    public static float PhaseOf(int entityId)
    {
        var hash = (uint)entityId * 2654435761u;
        return (hash & 0xFFFFFF) / (float)0x1000000 * ItemEntityRenderer.BobPhasePeriod;
    }

    private static float RandomUnit(int entityId, int itemIndex, int axis)
    {
        var hash = ((uint)entityId * 2246822519u) ^ ((uint)itemIndex * 3266489917u) ^ ((uint)axis * 668265263u);
        hash ^= hash >> 15;
        hash *= 2246822519u;
        hash ^= hash >> 13;
        return (hash & 0xFFFFFF) / (float)0x1000000;
    }

    private void AppendQuad(
        in ItemModelQuad quad,
        in Matrix4x4 world,
        SpriteRect sprite,
        ref int vertexFloats,
        ref int indexCount)
    {
        var first = (uint)(vertexFloats / VertexStride);
        for (var corner = 0; corner < 4; corner++)
        {
            WriteVertex(_geometry.Vertices, ref vertexFloats, Vector3.Transform(quad.Corner(corner), world), quad.Uv(corner), sprite);
        }

        _geometry.Indices[indexCount++] = first;
        _geometry.Indices[indexCount++] = first + 1;
        _geometry.Indices[indexCount++] = first + 2;
        _geometry.Indices[indexCount++] = first;
        _geometry.Indices[indexCount++] = first + 2;
        _geometry.Indices[indexCount++] = first + 3;
    }

    private static void WriteVertex(float[] destination, ref int offset, Vector3 position, Vector2 uv, SpriteRect sprite)
    {
        destination[offset++] = position.X;
        destination[offset++] = position.Y;
        destination[offset++] = position.Z;
        destination[offset++] = 1f;
        destination[offset++] = 1f;
        destination[offset++] = 1f;
        destination[offset++] = uv.X;
        destination[offset++] = uv.Y;
        destination[offset++] = sprite.U0;
        destination[offset++] = sprite.V0;
        destination[offset++] = sprite.U1;
        destination[offset++] = sprite.V1;
        destination[offset++] = 1f;
        destination[offset++] = 0f;
    }
}
