// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Hands each visible block entity to the renderer registered for its type and concatenates what
/// they build into one geometry. This is the single entry point the frame path uses; it never asks
/// what kind of block entity it is looking at.
/// </summary>
[Sharpcraft.Common.Diagnostics.Simplification("SIM-BE-SINGLE-NOCULL", "T-45", Note = "One no-cull pipeline covers the whole block entity segment; the reference picks a render type per renderer and entitySolid (conduit, decorated pot) culls.")]
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.blockentity.BlockEntityRenderDispatcher", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/BlockEntityRenderDispatcher.java")]
public sealed class BlockEntityRenderDispatcher
{
    private const int MaxRenderers = 16;

    private readonly IBlockEntityRenderer[] _renderers = BlockEntityRenderers.Create();
    private readonly DynamicGeometry?[] _parts = new DynamicGeometry?[MaxRenderers];
    private readonly DynamicGeometry _geometry = new();

    /// <summary>Whether any registered renderer handles the given type.</summary>
    public bool Handles(int typeId) => BlockEntityRenderers.Handles(typeId, _renderers);

    /// <summary>Builds the geometry of every entity a renderer claims, or null when none do.</summary>
    public DynamicGeometry? Build(
        ReadOnlySpan<BlockEntityRenderState> entities,
        ItemAtlas atlas,
        ItemAtlas? itemAtlas = null)
    {
        _geometry.Clear();

        var count = 0;
        var totalVertices = 0;
        var totalIndices = 0;
        foreach (var renderer in _renderers)
        {
            var part = renderer.Build(entities, atlas, itemAtlas);
            if (part is null || renderer.LastEntityCount == 0)
            {
                continue;
            }

            _parts[count++] = part;
            totalVertices += part.VertexFloats;
            totalIndices += part.IndexCount;
        }

        if (count == 0)
        {
            return null;
        }

        // One reserve for the whole frame, before any copy: Reserve reallocates rather than growing
        // in place, so growing between copies would discard what was already written.
        _geometry.Reserve(totalVertices, totalIndices);

        var vertexFloats = 0;
        var indexCount = 0;
        for (var part = 0; part < count; part++)
        {
            var source = _parts[part]!;
            Array.Copy(source.Vertices, 0, _geometry.Vertices, vertexFloats, source.VertexFloats);

            var baseVertex = (uint)(vertexFloats / ChunkMesher.FloatsPerVertex);
            for (var index = 0; index < source.IndexCount; index++)
            {
                _geometry.Indices[indexCount + index] = source.Indices[index] + baseVertex;
            }

            vertexFloats += source.VertexFloats;
            indexCount += source.IndexCount;
            _parts[part] = null;
        }

        _geometry.VertexFloats = vertexFloats;
        _geometry.IndexCount = indexCount;
        return _geometry;
    }
}
