// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// One block entity to draw, in world space.
/// </summary>
/// <param name="X">Block X of the entity's block.</param>
/// <param name="Y">Block Y of the entity's block.</param>
/// <param name="Z">Block Z of the entity's block.</param>
/// <param name="TypeId">Registry id of the block entity type.</param>
public readonly record struct BlockEntityRenderState(float X, float Y, float Z, int TypeId);

/// <summary>
/// Placeholder geometry for block entities until real models are available.
/// </summary>
/// <remarks>
/// A block entity is drawn as a box inset inside its block, which is enough to see that the stage runs and to judge where
/// each one sits. Sizes come from the reference chest model (14 by 14 by 14 pixels of sixteen, so 0.875 blocks) with other
/// types falling back to a slightly smaller generic box (docs/specs/render/pipeline.md, block entity stage).
/// </remarks>
public sealed class BlockEntityPlaceholderMesher
{
    /// <summary>Chest sized entities, the reference chest is fourteen of sixteen pixels.</summary>
    public const float ChestSize = 14f / 16f;

    /// <summary>Fallback size for types without a known model.</summary>
    public const float GenericSize = 12f / 16f;

    /// <summary>Type id treated as a chest until per-type sizes are extracted.</summary>
    public const int ChestTypeId = 1;

    private readonly DynamicGeometry _geometry = new();

    /// <summary>Entities drawn by the last call.</summary>
    public int LastEntityCount { get; private set; }

    /// <summary>Builds the placeholder geometry.</summary>
    /// <param name="entities">Block entities in world space.</param>
    /// <param name="sprite">Atlas rect used for the faces.</param>
    public DynamicGeometry Build(ReadOnlySpan<BlockEntityRenderState> entities, SpriteRect sprite)
    {
        _geometry.Clear();
        LastEntityCount = 0;

        if (entities.Length == 0)
        {
            return _geometry;
        }

        const int verticesPerBox = 24;
        const int indicesPerBox = 36;
        _geometry.Reserve(entities.Length * verticesPerBox * 12, entities.Length * indicesPerBox);

        var vertexFloats = 0;
        var indexCount = 0;

        for (var index = 0; index < entities.Length; index++)
        {
            var entity = entities[index];
            var size = entity.TypeId == ChestTypeId ? ChestSize : GenericSize;

            // Sit the box on the floor of its block, centred horizontally, as the reference chest does.
            var feet = new Vector3(entity.X + 0.5f, entity.Y + (1f - size) / 2f, entity.Z + 0.5f);

            EntityPlaceholderMesher.AppendBox(
                _geometry,
                feet,
                size,
                size,
                0.72f,
                0.52f,
                0.28f,
                sprite,
                ref vertexFloats,
                ref indexCount,
                size);

            LastEntityCount++;
        }

        _geometry.VertexFloats = vertexFloats;
        _geometry.IndexCount = indexCount;
        return _geometry;
    }
}
