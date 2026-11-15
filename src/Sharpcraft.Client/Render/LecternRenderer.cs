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
/// Draws the static book on a lectern. The book model's page state is fixed to the reference
/// lectern's bind/animation state; the lectern block entity transform places and rotates the book
/// above the desk.
/// </summary>
/// <remarks>
/// Source: <c>ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/LecternRenderer.java</c>
/// (<c>extractRenderState</c> lines 32-42, <c>submit</c> lines 44-53) and
/// <c>ref://mc-26.3-analysis/src/net/minecraft/client/model/object/book/BookModel.java</c>
/// (<c>createBodyLayer</c> lines 35-53, <c>setupAnim</c> lines 55-68, <c>State.forAnimation</c>
/// lines 70-73). The outer pose is translation (0.5, 1.0625, 0.5), Y rotation -yRot,
/// Z rotation 67.5 degrees, then translation (0, -0.125, 0), where
/// <c>yRot = facing.getClockWise().toYRot()</c>.
/// </remarks>
[ReferenceClass("net.minecraft.client.renderer.blockentity.LecternRenderer", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/LecternRenderer.java", Status = ReferenceStatus.Partial, Note = "Static book state only; lectern page animation is not wired.")]
[Sharpcraft.Common.Diagnostics.Simplification("SIM-BE-LECTERN-STATE", "T-47", Note = "The book uses the fixed BOOK_STATE = BookModel.State.forAnimation(0, 0.1, 0.9, 1.2); lectern page-flip/progress animation is not wired.")]
public sealed class LecternRenderer : IBlockEntityRenderer
{
    /// <summary>Tile texture is 64 by 32 pixels.</summary>
    public const int TextureWidth = BookModelDefinition.TextureWidth;

    /// <summary>Tile texture is 64 by 32 pixels.</summary>
    public const int TextureHeight = BookModelDefinition.TextureHeight;

    /// <summary>The fixed lectern book state, matching the reference <c>BOOK_STATE</c>.</summary>
    public static readonly BookModelDefinition.BookModelState BookState =
        BookModelDefinition.BookModelState.ForAnimation(0f, 0.1f, 0.9f, 1.2f);

    private static readonly EntityPose BookPose = BookModelDefinition.CreatePose(BookState);

    private readonly EntityModelMesher _mesher = new();
    private readonly DynamicGeometry _geometry = new();

    /// <summary>Entities drawn by the last call.</summary>
    public int LastEntityCount { get; private set; }

    /// <summary>Whether this renderer handles the lectern block entity type.</summary>
    public bool Handles(int typeId) => VanillaBlockEntityTypes.IsLectern(typeId);

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
            if (entity.TypeId != VanillaBlockEntityTypes.Lectern || !entity.HasBook)
            {
                continue;
            }

            // Build the book in model space; the source renderer's outer PoseStack transform is
            // applied below so the exact translation/rotation order is preserved.
            var modelState = new EntityRenderState(
                Id: index,
                TypeId: VanillaBlockEntityTypes.Lectern,
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

    /// <summary>Reference renderer pose: translate, rotate Y, rotate Z, then translate Y.</summary>
    private void ApplyOuterPose(int startFloats, int endFloats, BlockEntityRenderState entity)
    {
        var radians = MathF.PI / 180f;
        var yRot = ClockwiseYRot(entity.Facing);
        var center = new Vector3(entity.X + 0.5f, entity.Y + 1.0625f, entity.Z + 0.5f);
        for (var vertex = startFloats; vertex < endFloats; vertex += ChunkMesher.FloatsPerVertex)
        {
            var position = new Vector3(_geometry.Vertices[vertex], _geometry.Vertices[vertex + 1], _geometry.Vertices[vertex + 2]);
            // PoseStack operations apply to the vertex in reverse call order: the final translate is
            // first, then rotate Z, then rotate Y, then the block-centre translate.
            position += new Vector3(0f, -0.125f, 0f);
            position = Vector3.Transform(position, Matrix4x4.CreateRotationZ(67.5f * radians));
            position = Vector3.Transform(position, Matrix4x4.CreateRotationY(-yRot * radians));
            position += center;
            _geometry.Vertices[vertex] = position.X;
            _geometry.Vertices[vertex + 1] = position.Y;
            _geometry.Vertices[vertex + 2] = position.Z;
        }
    }

    /// <summary>
    /// <c>facing.getClockWise().toYRot()</c> for the horizontal Direction ordinal used by
    /// <see cref="FrameCommand"/>: north 2, south 3, west 4, east 5.
    /// Ref: <c>ref://mc-26.3-analysis/src/net/minecraft/core/Direction.java:33-38,66-77,311-312</c>
    /// and <c>ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/LecternRenderer.java:40-41</c>.
    /// </summary>
    public static float ClockwiseYRot(byte facing) => facing switch
    {
        2 => 270f, // north -> east
        3 => 90f,  // south -> west
        4 => 180f, // west -> north
        5 => 0f,   // east -> south
        _ => 180f
    };

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