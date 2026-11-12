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
public sealed class ItemEntityMesher
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
    public const string LayerName = "item_entity";

    private readonly DynamicGeometry _cutoutGeometry = new();
    private readonly DynamicGeometry _translucentGeometry = new();
    private readonly List<ItemModelQuad> _quads = [];
    private readonly List<QuadSection> _sections = [];

    /// <summary>One sub-model's slice of <see cref="_quads"/>, with the transformation that places it.</summary>
    private readonly record struct QuadSection(int Start, int End, Matrix4x4 Transform, ItemModelResolution Model);

    /// <summary>Bounding box of every sub-model after its transformation, which is what the drop layout centres on.</summary>
    private static ItemModelExtents UnionExtents(IReadOnlyList<ItemModelPlacement> placements)
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var placement in placements)
        {
            var extents = ItemModelExtents.ForDisplay(placement.Model, ItemDisplayContext.Ground).Transformed(placement.Transform);
            min = Vector3.Min(min, extents.Min);
            max = Vector3.Max(max, extents.Max);
        }

        return new ItemModelExtents(min, max);
    }

    /// <summary>
    /// Builds the geometry for every dropped item in <paramref name="entities"/>. At most two layers come back, the
    /// cutout one and the blended one, because the reference picks a single render type per item from its sprite alpha
    /// (<c>Transparency.hasTranslucent()</c> selects the translucent sheet). UVs are sprite-local in 0..1, which is what
    /// the item atlas shader expects.
    /// </summary>
    public IReadOnlyList<EntityGeometryLayer> Build(IReadOnlyList<EntityRenderState> entities, ISpriteAtlas itemAtlas)
    {
        ArgumentNullException.ThrowIfNull(itemAtlas);
        if (EntityModelResolver.ItemModels is not { } models || EntityModelResolver.Items is not { } items)
        {
            return [];
        }

        _cutoutGeometry.Clear();
        _translucentGeometry.Clear();
        var cutoutFloats = 0;
        var cutoutIndices = 0;
        var translucentFloats = 0;
        var translucentIndices = 0;
        var atlas = itemAtlas as ItemAtlas;

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

            // A composite item model submits several sub-models (a bed is head + foot), each under its own item-model
            // transformation; the reference draws every one of them.
            var placements = models.ResolveAll(item.Id.ToString());
            if (placements.Count == 0)
            {
                continue;
            }

            _quads.Clear();
            _sections.Clear();

            // A special model (the reference SpecialModelRenderer family) is not an element model: it bakes a ModelPart
            // tree in item space instead. Dropped chests are the first one wired up (T-764 S3).
            var special = placements.Count > 0 ? placements[0].Model.SpecialRendererType : null;
            if (special == "minecraft:chest")
            {
                var start = _quads.Count;
                SpecialModelBake.AppendQuads(
                    _quads,
                    SpecialModelDefinitions.Chest(),
                    SpecialModelDefinitions.ChestTextureWidth,
                    SpecialModelDefinitions.ChestTextureHeight,
                    SpecialModelDefinitions.ChestTexture);

                if (_quads.Count > start)
                {
                    _sections.Add(new QuadSection(start, _quads.Count, Matrix4x4.Identity, placements[0].Model));
                }
            }

            foreach (var placement in placements)
            {
                var start = _quads.Count;
                CollectQuads(_quads, placement.Model, itemAtlas as ItemAtlas);
                if (_quads.Count > start)
                {
                    _sections.Add(new QuadSection(start, _quads.Count, placement.Transform, placement.Model));
                }
            }

            if (_sections.Count == 0)
            {
                continue;
            }

            var grass = models.GrassColormap;
            var extents = UnionExtents(placements);
            var scattered = ItemEntityRenderer.UsesScatteredLayout(extents);
            var count = RenderedAmount(entity.ItemEntityCount);

            for (var itemIndex = 0; itemIndex < count; itemIndex++)
            {
                var offset = ItemEntityRenderer.ItemOffset(
                    itemIndex,
                    scattered,
                    extents,
                    count,
                    RandomUnit(entity.Id, itemIndex, 0),
                    RandomUnit(entity.Id, itemIndex, 1),
                    RandomUnit(entity.Id, itemIndex, 2));

                foreach (var section in _sections)
                {
                    var ground = section.Model.Display.TryGetValue(ItemDisplayContext.Ground, out var display)
                        ? display.ToMatrix()
                        : ItemDisplayTransform.None.ToMatrix();
                    var world = section.Transform * ItemEntityRenderer.Placement(
                        ground,
                        extents,
                        entity.AgeInTicks,
                        PhaseOf(entity.Id),
                        offset,
                        new Vector3(entity.X, entity.Y, entity.Z));

                    // The reference chooses one render type for the whole item: a sprite with partial alpha makes it
                    // translucent, everything else stays on the alpha-tested cutout sheet.
                    var translucent = IsTranslucent(section.Model, atlas);
                    for (var quadIndex = section.Start; quadIndex < section.End; quadIndex++)
                    {
                        var quad = _quads[quadIndex];
                        var sprite = itemAtlas.Get(quad.Texture);
                        if (sprite is null)
                        {
                            continue;
                        }

                        if (translucent)
                        {
                            AppendQuad(_translucentGeometry, quad, world, sprite, section.Model.TintFor(quad.TintIndex, grass), ref translucentFloats, ref translucentIndices);
                        }
                        else
                        {
                            AppendQuad(_cutoutGeometry, quad, world, sprite, section.Model.TintFor(quad.TintIndex, grass), ref cutoutFloats, ref cutoutIndices);
                        }
                    }
                }
            }
        }

        var layers = new List<EntityGeometryLayer>(2);
        if (cutoutIndices > 0)
        {
            _cutoutGeometry.VertexFloats = cutoutFloats;
            _cutoutGeometry.IndexCount = cutoutIndices;
            layers.Add(new EntityGeometryLayer(LayerName, EntityGeometryLayerKind.ItemCutout, 900, _cutoutGeometry, usesItemAtlas: true));
        }

        if (translucentIndices > 0)
        {
            _translucentGeometry.VertexFloats = translucentFloats;
            _translucentGeometry.IndexCount = translucentIndices;
            layers.Add(new EntityGeometryLayer($"{LayerName}_translucent", EntityGeometryLayerKind.ItemTranslucent, 910, _translucentGeometry, usesItemAtlas: true));
        }

        return layers;
    }

    /// <summary>
    /// True when any sprite this model uses has partial alpha, i.e. the reference
    /// <c>Transparency.hasTranslucent()</c> that switches the item to the translucent sheet.
    /// </summary>
    public static bool IsTranslucent(ItemModelResolution resolution, ItemAtlas? atlas)
    {
        if (resolution.ForcesTranslucency)
        {
            // Reference Material.force_translucent: the quad material flag selects the translucent sheet regardless of
            // the sprite alpha (stained glass relies on it).
            return true;
        }

        if (atlas is null)
        {
            return false;
        }

        foreach (var texture in ResolutionTextures(resolution))
        {
            if (atlas.IsTranslucent(texture))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Fills <paramref name="output"/> with the model's quads, falling back to the generated sprite box.</summary>
    public static void CollectQuads(List<ItemModelQuad> output, ItemModelResolution resolution) =>
        CollectQuads(output, resolution, atlas: null);

    /// <summary>
    /// Fills <paramref name="output"/> with the model's quads. When <paramref name="atlas"/> holds the sprite pixels, a
    /// generated model also gets its extruded side faces, which is what gives a flat drop visible thickness.
    /// </summary>
    public static void CollectQuads(
        List<ItemModelQuad> output,
        ItemModelResolution resolution,
        ItemAtlas? atlas)
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

        // No elements: the model is a generated sprite. The reference emits both faces as *unculled* quads, so both are
        // always drawn, and it gives the north face explicitly reversed UVs (16,0,0,16) so that the far face lines up
        // with the near one instead of showing a mirror image. Using the generic auto-UV here (0,0,16,16 for a full
        // element) makes the back face read as a second, mirrored blade.
        var texture = resolution.Textures.TryGetValue("layer0", out var layer0) ? IdentifierPath(layer0) : null;
        if (texture is null)
        {
            return;
        }

        var from = new Vector3(0f, 0f, 7.5f);
        var to = new Vector3(16f, 16f, 8.5f);
        // The reference bakes generated layers with the layer index as the tint index, so layer0 is tint 0; leaving it
        // at -1 silently disables every tint source on flat items such as short_grass.
        const int layerTintIndex = 0;
        var south = new ItemModelFace(texture, new Vector4(0f, 0f, 16f, 16f), TintIndex: layerTintIndex);
        var north = new ItemModelFace(texture, new Vector4(16f, 0f, 0f, 16f), TintIndex: layerTintIndex);
        output.Add(BuildQuad(from, to, ModelFaceDirection.South, south));
        output.Add(BuildQuad(from, to, ModelFaceDirection.North, north));

        if (atlas?.GetTexture(texture) is { } pixels)
        {
            GeneratedItemGeometry.AppendSideFaces(
                output,
                texture,
                pixels.Width,
                pixels.Height,
                (x, y) => x >= 0 && y >= 0 && x < pixels.Width && y < pixels.Height
                    ? pixels.Rgba[(((y * pixels.Width) + x) * 4) + 3]
                    : (byte)0);
        }
    }

    /// <summary>
    /// Appends resolved quads with a caller-supplied transform. Shared by the drop path and the held-item pass so both
    /// emit the identical vertex layout and tinting; returns how many quads were written.
    /// </summary>
    public static int EmitQuads(
        DynamicGeometry geometry,
        List<ItemModelQuad> quads,
        in Matrix4x4 world,
        ISpriteAtlas atlas,
        ItemModelResolution resolution,
        GrassColorMap? grass,
        ref int vertexFloats,
        ref int indexCount)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(quads);
        ArgumentNullException.ThrowIfNull(atlas);
        ArgumentNullException.ThrowIfNull(resolution);

        var written = 0;
        foreach (var quad in quads)
        {
            if (atlas.Get(quad.Texture) is not { } sprite)
            {
                continue;
            }

            EnsureCapacity(geometry, vertexFloats, indexCount + 6);
            var first = (uint)(vertexFloats / VertexStride);
            var tint = resolution.TintFor(quad.TintIndex, grass);
            for (var corner = 0; corner < 4; corner++)
            {
                WriteVertex(
                    geometry.Vertices,
                    ref vertexFloats,
                    Vector3.Transform(quad.Corner(corner), world),
                    quad.Uv(corner),
                    sprite,
                    tint);
            }

            geometry.Indices[indexCount++] = first;
            geometry.Indices[indexCount++] = first + 1;
            geometry.Indices[indexCount++] = first + 2;
            geometry.Indices[indexCount++] = first;
            geometry.Indices[indexCount++] = first + 2;
            geometry.Indices[indexCount++] = first + 3;
            written++;
        }

        return written;
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

        if (EntityModelResolver.ItemModels is not { } models || EntityModelResolver.Items?.ById(entity.ItemEntityItemId) is not { } item)
        {
            yield break;
        }

        // Every sub-model of a composite item contributes sprites (a bed's foot half has its own texture family), so the
        // atlas has to be primed from ResolveAll; asking only the first model leaves the other halves untextured.
        foreach (var placement in models.ResolveAll(item.Id.ToString()))
        {
            foreach (var texture in ResolutionTextures(placement.Model))
            {
                yield return texture;
            }
        }
    }

    /// <summary>
    /// Texture paths one resolved model needs: every face texture for a cuboid model, or the sprite layers of a
    /// generated one. Shared with the held-item pre-pass so both paths prime the same atlas entries.
    /// </summary>
    public static IEnumerable<string> ResolutionTextures(ItemModelResolution resolution)
    {
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

    private static void AppendQuad(
        DynamicGeometry geometry,
        in ItemModelQuad quad,
        in Matrix4x4 world,
        SpriteRect sprite,
        Vector3 tint,
        ref int vertexFloats,
        ref int indexCount)
    {
        // The buffers are owned by this instance and reused, so the capacity has to be grown before writing: a fresh
        // DynamicGeometry starts with empty arrays, and the first dropped item in a session would otherwise write out of
        // bounds. Reserve doubles, so after the first frames this settles to no allocations.
        EnsureCapacity(geometry, vertexFloats, indexCount);

        var first = (uint)(vertexFloats / VertexStride);
        for (var corner = 0; corner < 4; corner++)
        {
            WriteVertex(geometry.Vertices, ref vertexFloats, Vector3.Transform(quad.Corner(corner), world), quad.Uv(corner), sprite, tint);
        }

        geometry.Indices[indexCount++] = first;
        geometry.Indices[indexCount++] = first + 1;
        geometry.Indices[indexCount++] = first + 2;
        geometry.Indices[indexCount++] = first;
        geometry.Indices[indexCount++] = first + 2;
        geometry.Indices[indexCount++] = first + 3;
    }

    /// <summary>
    /// Grows a reused dynamic geometry so four more vertices and six more indices fit. <see cref="DynamicGeometry"/>
    /// starts with empty arrays, so writing a quad before reserving throws — which is exactly what happened the first
    /// time a drop appeared in a live session.
    /// </summary>
    internal static void EnsureCapacity(DynamicGeometry geometry, int vertexFloats, int indexCount)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        const int quadFloats = 4 * VertexStride;
        const int quadIndices = 6;
        if (geometry.Vertices.Length < vertexFloats + quadFloats ||
            geometry.Indices.Length < indexCount + quadIndices)
        {
            geometry.Reserve(vertexFloats + quadFloats, indexCount + quadIndices);
        }
    }

    private static void WriteVertex(float[] destination, ref int offset, Vector3 position, Vector2 uv, SpriteRect sprite, Vector3 tint)
    {
        TerrainVertexWriter.Write(
            destination,
            ref offset,
            position.X,
            position.Y,
            position.Z,
            tint.X,
            tint.Y,
            tint.Z,
            uv.X,
            uv.Y,
            sprite,
            sky: 1f,
            block: 0f);
    }
}
