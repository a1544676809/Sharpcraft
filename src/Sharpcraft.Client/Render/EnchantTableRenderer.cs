// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Common.Reference;
using Sharpcraft.Common.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Draws the floating book above an enchanting table using the shared <see cref="BookModelDefinition"/>.
/// This slice uses the reference's default resting values for time, flip, open and yRot.
/// </summary>
/// <remarks>
/// Source: <c>ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/EnchantTableRenderer.java</c>
/// (<c>extractRenderState</c> lines 34-56, <c>submit</c> lines 58-85). The fixed
/// <c>EnchantingTableBlockEntity</c> fields are the Java defaults in
/// <c>ref://mc-26.3-analysis/src/net/minecraft/world/level/block/entity/EnchantingTableBlockEntity.java:21-30</c>:
/// time = 0, flip = 0, open = 0, yRot = 0. The source outer pose is
/// translate(0.5, 0.75, 0.5), translate(0, 0.1 + sin(time * 0.1) * 0.01, 0), rotate Y -yRot,
/// rotate Z 80 degrees. `ff1`/`ff2` at flip 0 are 0.1/0.9 by the source formula at lines 67-69.
/// </remarks>
[ReferenceClass("net.minecraft.client.renderer.blockentity.EnchantTableRenderer", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/EnchantTableRenderer.java", Status = ReferenceStatus.Partial, Note = "Static default book state only; time/rotation/flip/open animation is not wired.")]
[Sharpcraft.Common.Diagnostics.Simplification("SIM-BE-ENCHANT-BOOK-STATE", "T-47", Note = "Book state is fixed to time=0, flip=0, open=0, yRot=0; enchanting-table book rotation, floating bob and page-flip/open animation are not wired.")]
public sealed class EnchantTableRenderer : IBlockEntityRenderer
{
    /// <summary>Book texture width, 64 pixels.</summary>
    public const int TextureWidth = BookModelDefinition.TextureWidth;

    /// <summary>Book texture height, 32 pixels.</summary>
    public const int TextureHeight = BookModelDefinition.TextureHeight;

    /// <summary>The fixed source-default book state: openness 0, page flips 0.1/0.9.</summary>
    public static readonly BookModelDefinition.BookModelState BookState =
        BookModelDefinition.BookModelState.ForAnimation(0f, 0.1f, 0.9f, 0f);

    private static readonly EntityPose BookPose = BookModelDefinition.CreatePose(BookState);

    private readonly EntityModelMesher _mesher = new();
    private readonly DynamicGeometry _geometry = new();

    /// <summary>Entities drawn by the last call.</summary>
    public int LastEntityCount { get; private set; }

    /// <summary>Whether this renderer handles the enchanting table block entity type.</summary>
    public bool Handles(int typeId) => VanillaBlockEntityTypes.IsEnchantingTable(typeId);

    /// <summary>Builds the book geometry from the block entity atlas.</summary>
    /// <param name="entities">Block entities in world space.</param>
    /// <param name="atlas">Atlas holding <see cref="ChestAtlas.EnchantingTableBookTexture"/>.</param>
    public DynamicGeometry Build(ReadOnlySpan<BlockEntityRenderState> entities, ItemAtlas atlas) =>
        Build(entities, null, atlas);

    /// <summary>Builds the book geometry with an explicit sprite, used by golden tests.</summary>
    /// <param name="entities">Block entities in world space.</param>
    /// <param name="sprite">Book sprite.</param>
    public DynamicGeometry Build(ReadOnlySpan<BlockEntityRenderState> entities, SpriteRect sprite) =>
        Build(entities, sprite, null);

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

            sprite = ChestAtlas.SpriteForEnchantingTableBook(atlas);
            if (sprite is null)
            {
                return _geometry;
            }
        }

        for (var index = 0; index < entities.Length; index++)
        {
            var entity = entities[index];
            if (!Handles(entity.TypeId))
            {
                continue;
            }

            // Build in book-model space, then apply the source PoseStack outer transform below.
            var modelState = new Sharpcraft.Client.World.EntityRenderState(
                Id: index,
                TypeId: entity.TypeId,
                X: 0f,
                Y: 0f,
                Z: 0f,
                Yaw: 180f,
                Pitch: 0f);
            var built = _mesher.BuildModel(
                BookModelDefinition.Definition,
                modelState,
                sprite,
                TextureWidth,
                TextureHeight,
                pose: BookPose,
                modelScale: 1f);
            var start = _geometry.VertexFloats;
            AppendGeometry(_geometry, built);
            ApplyOuterPose(start, _geometry.VertexFloats, entity);
            LastEntityCount++;
        }

        return _geometry;
    }

    /// <summary>
    /// Source renderer pose: translate(0.5, 0.75, 0.5), translate Y by 0.1 (time 0),
    /// rotate Y by -yRot (0), then rotate Z by 80 degrees. The vertex receives the final
    /// translate first under the reference PoseStack order.
    /// </summary>
    private void ApplyOuterPose(int startFloats, int endFloats, BlockEntityRenderState entity)
    {
        var radians = MathF.PI / 180f;
        var center = new Vector3(entity.X + 0.5f, entity.Y + 0.75f + 0.1f, entity.Z + 0.5f);
        for (var vertex = startFloats; vertex < endFloats; vertex += ChunkMesher.FloatsPerVertex)
        {
            var position = new Vector3(_geometry.Vertices[vertex], _geometry.Vertices[vertex + 1], _geometry.Vertices[vertex + 2]);
            position = Vector3.Transform(position, Matrix4x4.CreateRotationZ(80f * radians));
            position = Vector3.Transform(position, Matrix4x4.CreateRotationY(0f));
            position += center;
            _geometry.Vertices[vertex] = position.X;
            _geometry.Vertices[vertex + 1] = position.Y;
            _geometry.Vertices[vertex + 2] = position.Z;
        }
    }

    private static void AppendGeometry(DynamicGeometry target, DynamicGeometry source)
    {
        if (source.VertexFloats == 0)
        {
            return;
        }

        var vertexFloats = target.VertexFloats + source.VertexFloats;
        var indexCount = target.IndexCount + source.IndexCount;
        target.Reserve(vertexFloats, indexCount);
        var vertexBase = (uint)(target.VertexFloats / ChunkMesher.FloatsPerVertex);
        Array.Copy(source.Vertices, 0, target.Vertices, target.VertexFloats, source.VertexFloats);
        for (var i = 0; i < source.IndexCount; i++)
        {
            target.Indices[target.IndexCount + i] = source.Indices[i] + vertexBase;
        }

        target.VertexFloats = vertexFloats;
        target.IndexCount = indexCount;
    }
}
