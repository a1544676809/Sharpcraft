// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Hands each visible block entity to the renderer registered for its type and concatenates what
/// they build into one geometry, with solid renderers first and no-cull renderers second. This is the
/// single entry point the frame path uses; it never asks what kind of block entity it is looking at.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.blockentity.BlockEntityRenderDispatcher", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/BlockEntityRenderDispatcher.java")]
public sealed class BlockEntityRenderDispatcher
{
    private const int MaxRenderers = 16;

    private readonly IBlockEntityRenderer[] _renderers;
    private readonly DynamicGeometry?[] _parts = new DynamicGeometry?[MaxRenderers];
    private readonly bool[] _solidParts = new bool[MaxRenderers];
    private readonly DynamicGeometry _geometry = new();

    /// <summary>Creates a dispatcher with the shipped renderer table.</summary>
    public BlockEntityRenderDispatcher()
        : this(BlockEntityRenderers.Create())
    {
    }

    /// <summary>Creates a dispatcher with an explicit renderer table; used by grouping tests.</summary>
    internal BlockEntityRenderDispatcher(IBlockEntityRenderer[] renderers)
    {
        ArgumentNullException.ThrowIfNull(renderers);
        if (renderers.Length > MaxRenderers)
        {
            throw new ArgumentException($"At most {MaxRenderers} block entity renderers are supported.", nameof(renderers));
        }

        _renderers = renderers;
    }

    /// <summary>Whether any registered renderer handles the given type.</summary>
    public bool Handles(int typeId) => BlockEntityRenderers.Handles(typeId, _renderers);

    /// <summary>Prints one line per renderer; the host turns this on for diagnostics only.</summary>
    public bool Trace { get; set; }

    /// <summary>Builds the geometry of every entity a renderer claims, or null when none do.</summary>
    public BlockEntityGeometry? Build(
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
            if (Trace)
            {
                Console.WriteLine(
                    $"be {renderer.GetType().Name}: entities={renderer.LastEntityCount} index={part?.IndexCount ?? 0}");
            }

            if (part is null || renderer.LastEntityCount == 0)
            {
                continue;
            }

            _parts[count] = part;
            _solidParts[count] = renderer.RenderMode == BlockEntityRenderMode.Solid;
            count++;
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
        var solidIndexCount = 0;

        // Solid geometry occupies the leading index range, no-cull geometry follows it. A two-pass
        // copy over the same part list keeps the single concatenation path.
        for (var pass = 0; pass < 2; pass++)
        {
            var solidPass = pass == 0;
            for (var part = 0; part < count; part++)
            {
                if (_solidParts[part] != solidPass)
                {
                    continue;
                }

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

            if (solidPass)
            {
                solidIndexCount = indexCount;
            }
        }

        _geometry.VertexFloats = vertexFloats;
        _geometry.IndexCount = indexCount;
        return new BlockEntityGeometry(_geometry, solidIndexCount);
    }
}
