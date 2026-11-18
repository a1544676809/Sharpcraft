// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Reference;
using Sharpcraft.Common.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Draws the bell from its source-frame static model: a bell_body box hanging from the block ceiling
/// and a bell_base child carried by the body. The source swing animation (tick / click-direction driven)
/// is a renderer state rotation and is intentionally not part of the static model; see
/// <c>SIM-BE-BELL-NO-SHAKE</c> in <c>docs/modules/render.md</c>.
/// </summary>
/// <remarks>
/// The static body/base geometry is jar-verified keyed 1e-5 + UV half against
/// <c>bell-bone-raw-vectors.reference.txt</c> (RawRenderUnitProbe, BELL layer). Source:
/// <c>ref://mc-26.3-analysis/src/net/minecraft/client/model/object/bell/BellModel.java</c>.
/// </remarks>
[ReferenceClass("net.minecraft.client.renderer.blockentity.BellRenderer", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/BellRenderer.java", Status = ReferenceStatus.Partial, Note = "Static body and base boxes from the raw source-frame model; the shake animation driven by ticks and click direction is not wired.")]
[Sharpcraft.Common.Diagnostics.Simplification("SIM-BE-BELL-NO-SHAKE", "T-47", Note = "Static bell geometry only; the reference's tick and click-direction driven shake animation is not wired.")]
public sealed class BellMesher
{
    /// <summary>One sixteenth of a block, the model's unit.</summary>
    public const float Pixel = 1f / 16f;

    // Static-geometry constants retained for the golden-vector documentation tests. The model now
    // owns the actual geometry; these describe the same jar source cube bounds.
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
    /// <summary>The bell texture is 32 by 32.</summary>
    public const int TextureSize = 32;

    private readonly EntityModelMesher _mesher = new();
    private readonly DynamicGeometry _geometry = new();

    /// <summary>Entities drawn by the last call.</summary>
    public int LastEntityCount { get; private set; }

    /// <summary>Builds bell geometry for every bell in the span.</summary>
    public DynamicGeometry Build(ReadOnlySpan<BlockEntityRenderState> entities, SpriteRect sprite) =>
        Build(entities, sprite, null);

    /// <summary>Builds bell geometry, resolving the sprite from the block entity atlas.</summary>
    public DynamicGeometry Build(ReadOnlySpan<BlockEntityRenderState> entities, ItemAtlas atlas) =>
        Build(entities, null, atlas);

    private DynamicGeometry Build(
        ReadOnlySpan<BlockEntityRenderState> entities,
        SpriteRect? fallbackSprite,
        ItemAtlas? atlas)
    {
        _geometry.Clear();
        // Two boxes = 48 vertices / 72 indices per bell. Reserve the whole batch before appending:
        // DynamicGeometry.Reserve does not copy existing content, so mid-loop growth would drop earlier bells.
        _geometry.Reserve(entities.Length * 48 * ChunkMesher.FloatsPerVertex, entities.Length * 72);
        LastEntityCount = 0;

        var sprite = fallbackSprite;
        if (sprite is null)
        {
            if (atlas is null)
                throw new InvalidOperationException("either a sprite or an atlas is required");
            sprite = ChestAtlas.SpriteForBell(atlas);
            if (sprite is null)
                return _geometry;
        }

        var definition = BellModelDefinition.Definition;
        for (var index = 0; index < entities.Length; index++)
        {
            var entity = entities[index];
            if (entity.TypeId != VanillaBlockEntityTypes.Bell)
                continue;

            // Static bind pose: the shake rotation is applied by the renderer state and is not wired.
            var state = new EntityRenderState(
                Id: index, TypeId: entity.TypeId,
                X: entity.X, Y: entity.Y, Z: entity.Z,
                Yaw: 180f, Pitch: 0f);
            var built = _mesher.BuildModel(definition, [state], sprite, definition.TextureWidth, definition.TextureHeight,
                modelScale: 1f);
            // BuildModel's WriteVertex fixes sky:1 / block:0. Restore this block's packed light, matching
            // the old AppendBox path (sky = Sky/15, block = Block/15) so night/cave lighting is preserved.
            ApplyLight(built, entity.Sky, entity.Block);
            AppendGeometry(_geometry, built);
            LastEntityCount++;
        }

        return _geometry;
    }

    /// <summary>
    /// Overwrites the packed light bytes of every vertex with this block's sky/block levels. BuildModel
    /// writes sky:1/block:0; the light layout packs sky in bits 16-23 and block in bits 24-31 of the
    /// sprite word (TerrainVertexWriter), so only those two bytes change -- sprite id (bits 0-15) stays.
    /// </summary>
    private static void ApplyLight(DynamicGeometry geometry, byte sky, byte block)
    {
        var skyChannel = Channel(sky / 15f);
        var blockChannel = Channel(block / 15f);
        var count = geometry.VertexFloats / ChunkMesher.FloatsPerVertex;
        for (var vertex = 0; vertex < count; vertex++)
        {
            var index = (vertex * ChunkMesher.FloatsPerVertex) + 5;
            var packed = BitConverter.SingleToUInt32Bits(geometry.Vertices[index]);
            packed &= 0x0000FFFFu; // clear sky + block, keep sprite id
            packed |= (uint)skyChannel << 16;
            packed |= (uint)blockChannel << 24;
            geometry.Vertices[index] = BitConverter.UInt32BitsToSingle(packed);
        }
    }

    private static int Channel(float value) =>
        (int)Math.Clamp(MathF.Round(value * 255f), 0f, 255f);

    private static void AppendGeometry(DynamicGeometry target, DynamicGeometry source)
    {
        if (source.VertexFloats == 0)
            return;
        var vertexFloats = target.VertexFloats + source.VertexFloats;
        var indexCount = target.IndexCount + source.IndexCount;
        target.Reserve(vertexFloats, indexCount);
        var vertexBase = (uint)(target.VertexFloats / ChunkMesher.FloatsPerVertex);
        Array.Copy(source.Vertices, 0, target.Vertices, target.VertexFloats, source.VertexFloats);
        for (var i = 0; i < source.IndexCount; i++)
            target.Indices[target.IndexCount + i] = source.Indices[i] + vertexBase;
        target.VertexFloats = vertexFloats;
        target.IndexCount = indexCount;
    }
}
