// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Common.Reference;
using Sharpcraft.Common.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Builds the static bell geometry: a body hanging below the block's ceiling and a base above it.
/// </summary>
/// <remarks>
/// Geometry follows the reference bell model. In that model's space, one block is sixteen units and
/// the bell body spans five to eleven across, six to thirteen up and five to eleven deep; the base
/// spans four to twelve across, four to six up and four to twelve deep. The reference's swing
/// animation, driven by block entity ticks and click direction, is not wired yet; see
/// <c>SIM-BE-BELL-NO-SHAKE</c> in <c>docs/modules/render.md</c>.
/// </remarks>
[ReferenceClass("net.minecraft.client.renderer.blockentity.BellRenderer", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/BellRenderer.java", Status = ReferenceStatus.Partial, Note = "Static body and base boxes only; the shake animation driven by the block entity's ticks and click direction is not wired.")]
[Sharpcraft.Common.Diagnostics.Simplification("SIM-BE-BELL-NO-SHAKE", "T-47", Note = "Static bell geometry only; the reference's tick and click-direction driven shake animation is not wired.")]
public sealed class BellMesher
{
    /// <summary>One sixteenth of a block, the model's unit.</summary>
    public const float Pixel = 1f / 16f;

    /// <summary>Body width, six pixels.</summary>
    public const float BodyWidth = 6f * Pixel;

    /// <summary>Body height, seven pixels.</summary>
    public const float BodyHeight = 7f * Pixel;

    /// <summary>Body depth, six pixels.</summary>
    public const float BodyDepth = 6f * Pixel;

    /// <summary>Body bottom above the block floor, six pixels.</summary>
    public const float BodyFeetY = 6f * Pixel;

    /// <summary>Base width, eight pixels.</summary>
    public const float BaseWidth = 8f * Pixel;

    /// <summary>Base height, two pixels.</summary>
    public const float BaseHeight = 2f * Pixel;

    /// <summary>Base depth, eight pixels.</summary>
    public const float BaseDepth = 8f * Pixel;

    /// <summary>Base bottom above the block floor, four pixels.</summary>
    public const float BaseFeetY = 4f * Pixel;

    /// <summary>The bell texture is 32 by 32, not the 64 by 64 default block entity sheet size.</summary>
    public const int TextureSize = 32;

    private readonly DynamicGeometry _geometry = new();

    /// <summary>Entities drawn by the last call.</summary>
    public int LastEntityCount { get; private set; }

    /// <summary>Builds bell geometry for every bell in the span.</summary>
    /// <param name="entities">Block entities in world space.</param>
    /// <param name="sprite">Atlas rect used for the faces.</param>
    public DynamicGeometry Build(ReadOnlySpan<BlockEntityRenderState> entities, SpriteRect sprite) =>
        Build(entities, sprite, null);

    /// <summary>Builds bell geometry, resolving the sprite from the block entity atlas.</summary>
    /// <param name="entities">Block entities in world space.</param>
    /// <param name="atlas">Atlas that holds <see cref="ChestAtlas.BellTexture"/>.</param>
    public DynamicGeometry Build(ReadOnlySpan<BlockEntityRenderState> entities, ItemAtlas atlas) =>
        Build(entities, null, atlas);

    private DynamicGeometry Build(
        ReadOnlySpan<BlockEntityRenderState> entities,
        SpriteRect? fallbackSprite,
        ItemAtlas? atlas)
    {
        _geometry.Clear();
        LastEntityCount = 0;

        var sprite = fallbackSprite;
        if (sprite is null)
        {
            if (atlas is null)
            {
                throw new InvalidOperationException("either a sprite or an atlas is required");
            }

            sprite = ChestAtlas.SpriteForBell(atlas);
            if (sprite is null)
            {
                return _geometry;
            }
        }

        if (entities.Length == 0)
        {
            return _geometry;
        }

        const int boxesPerBell = 2;
        const int verticesPerBox = 24;
        const int indicesPerBox = 36;
        _geometry.Reserve(
            entities.Length * boxesPerBell * verticesPerBox * ChunkMesher.FloatsPerVertex,
            entities.Length * boxesPerBell * indicesPerBox);

        var vertexFloats = 0;
        var indexCount = 0;

        for (var index = 0; index < entities.Length; index++)
        {
            var entity = entities[index];
            if (entity.TypeId != VanillaBlockEntityTypes.Bell)
            {
                continue;
            }

            var origin = new Vector3(entity.X, entity.Y, entity.Z);
            var lightSky = entity.Sky / 15f;
            var lightBlock = entity.Block / 15f;

            // The body hangs below the block's ceiling: its bottom is six pixels up and it is seven tall.
            EntityPlaceholderMesher.AppendBox(
                _geometry,
                origin + new Vector3(0.5f, BodyFeetY, 0.5f),
                width: BodyWidth,
                height: BodyHeight,
                red: 1f,
                green: 1f,
                blue: 1f,
                sprite: sprite,
                ref vertexFloats,
                ref indexCount,
                depth: BodyDepth,
                sky: lightSky,
                block: lightBlock,
                texU: 0,
                texV: 0,
                texDx: 6,
                texDy: 7,
                texDz: 6,
                textureSize: TextureSize);

            // The base caps the body: its bottom is four pixels up and it is two tall.
            EntityPlaceholderMesher.AppendBox(
                _geometry,
                origin + new Vector3(0.5f, BaseFeetY, 0.5f),
                width: BaseWidth,
                height: BaseHeight,
                red: 1f,
                green: 1f,
                blue: 1f,
                sprite: sprite,
                ref vertexFloats,
                ref indexCount,
                depth: BaseDepth,
                sky: lightSky,
                block: lightBlock,
                texU: 0,
                texV: 13,
                texDx: 8,
                texDy: 2,
                texDz: 8,
                textureSize: TextureSize);

            LastEntityCount++;
        }

        _geometry.VertexFloats = vertexFloats;
        _geometry.IndexCount = indexCount;
        return _geometry;
    }
}
