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
/// Draws the static base of a standing or wall banner: the pole/bar body and the flag mesh from
/// <see cref="BannerModelDefinition"/>, tinted by the banner's dye colour. Patterns and the flag
/// phase animation are deliberately outside this slice.
/// </summary>
/// <remarks>
/// Source: <c>ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/BannerRenderer.java</c>
/// (<c>MODEL_SCALE</c>/<c>MODEL_TRANSLATION</c> lines 45-47, <c>createGroundTransformation</c> /
/// <c>createWallTransformation</c> lines 239-249, and submit lines 124-143). The base texture is
/// <c>entity/banner/banner_base</c> (64 by 64). DyeColor's diffuse values are pinned in
/// <c>ref://mc-26.3-analysis/src/net/minecraft/world/item/DyeColor.java:32-47</c>.
/// </remarks>
[ReferenceClass("net.minecraft.client.renderer.blockentity.BannerRenderer", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/BannerRenderer.java", Kind = ReferenceKind.Adapter, Status = ReferenceStatus.Partial)]
[Sharpcraft.Common.Diagnostics.Simplification("SIM-BE-BANNER-PATTERNS", "T-49", Note = "Only the base banner body/flag is drawn; the pattern overlays and the separate base tint layer are not wired.")]
[Sharpcraft.Common.Diagnostics.Simplification("SIM-BE-BANNER-PHASE", "T-49", Note = "Banner flag phase is fixed at zero; BannerRenderer's animated phase is not wired.")]
public sealed class BannerRenderer : IBlockEntityRenderer
{
    /// <summary>Reference <c>BannerRenderer.SIZE</c> / <c>MODEL_SCALE</c>.</summary>
    public const float ModelScale = 2f / 3f;

    /// <summary>Reference <c>MODEL_TRANSLATION</c>.</summary>
    public const float ModelTranslation = 0.5f;

    private readonly EntityModelMesher _mesher = new();
    private readonly DynamicGeometry _geometry = new();

    /// <inheritdoc />
    public bool Handles(int typeId) => VanillaBlockEntityTypes.IsBanner(typeId);

    /// <inheritdoc />
    public BlockEntityRenderMode RenderMode => BlockEntityRenderMode.Solid;

    /// <inheritdoc />
    public int LastEntityCount { get; private set; }

    /// <inheritdoc />
    public DynamicGeometry Build(ReadOnlySpan<BlockEntityRenderState> entities, ItemAtlas atlas)
    {
        ArgumentNullException.ThrowIfNull(atlas);
        _geometry.Clear();
        LastEntityCount = 0;

        for (var index = 0; index < entities.Length; index++)
        {
            var entity = entities[index];
            if (!Handles(entity.TypeId))
            {
                continue;
            }

            var attachment = entity.BannerAttachment == BannerAttachment.Wall
                ? BannerAttachment.Wall
                : BannerAttachment.Ground;
            var transform = ModelTransform(attachment, entity.BannerRotation, entity.X, entity.Y, entity.Z);
            var tint = TintFor(entity.BannerColor);
            var state = new EntityRenderState(
                Id: index,
                TypeId: entity.TypeId,
                X: 0f,
                Y: 0f,
                Z: 0f,
                Yaw: 180f,
                Pitch: 0f,
                Tint: tint);

            var body = _mesher.BuildModel(
                BannerModelDefinition.BodyFor(attachment),
                state,
                atlas.Get(BannerModelDefinition.TexturePath)!,
                BannerModelDefinition.TextureWidth,
                BannerModelDefinition.TextureHeight,
                modelScale: 1f);
            var bodyIndexCount = body.IndexCount;
            AppendTransformed(body, transform);

            var flag = _mesher.BuildModel(
                BannerModelDefinition.FlagFor(attachment),
                state,
                atlas.Get(BannerModelDefinition.TexturePath)!,
                BannerModelDefinition.TextureWidth,
                BannerModelDefinition.TextureHeight,
                modelScale: 1f);
            var flagIndexCount = flag.IndexCount;
            AppendTransformed(flag, transform);

            if (bodyIndexCount + flagIndexCount > 0)
            {
                LastEntityCount++;
            }
        }

        return _geometry;
    }

    /// <summary>
    /// Source <c>BannerRenderer.modelTransformation</c>: translation (0.5, 0, 0.5), Y rotation
    /// <c>-degrees</c>, scale (2/3, -2/3, -2/3), followed by the block origin translation.
    /// </summary>
    public static Matrix4x4 ModelTransform(
        BannerAttachment attachment,
        byte rotation,
        float blockX,
        float blockY,
        float blockZ)
    {
        var degrees = attachment == BannerAttachment.Ground
            ? GroundDegrees(rotation)
            : WallDegrees(rotation);

        // The source MODEL_SCALE is (0.6667, -0.6667, -0.6667) in the reference's y-down model
        // frame. EntityModelMesher returns y-up part coordinates, so the source sign is converted
        // back to a positive Y scale here; the source translation/rotation and x/z scale are kept.
        return Matrix4x4.CreateScale(ModelScale)
            * Matrix4x4.CreateRotationY(-degrees * (MathF.PI / 180f))
            * Matrix4x4.CreateTranslation(ModelTranslation, 0f, ModelTranslation)
            * Matrix4x4.CreateTranslation(blockX, blockY, blockZ);
    }

    /// <summary>
    /// <c>RotationSegment.convertToDegrees(segment)</c> normalized to [-180, 180). The source
    /// transformation negates this value.
    /// </summary>
    public static float GroundDegrees(byte segment)
    {
        var normalized = (segment & 15) * 22.5f;
        return normalized >= 180f ? normalized - 360f : normalized;
    }

    /// <summary>
    /// <c>Direction.toYRot</c> for the reference Direction ordinal. Banner's wall transform negates
    /// this value; the horizontal ordinals are north 180, south 0, west 90, east 270.
    /// </summary>
    public static float WallDegrees(byte facing) => facing switch
    {
        2 => 180f, // north
        3 => 0f,   // south
        4 => 90f,  // west
        5 => 270f, // east
        _ => 180f
    };

    /// <summary>
    /// DyeColor texture diffuse RGB, normalized. The packed values are the source table entries
    /// 0xFFFFFF, 0xF9801D, ... 0x1D1D21.
    /// </summary>
    public static Vector3 TintFor(byte color)
    {
        var rgb = DyeColours[(color & 15)];
        return new Vector3(
            ((rgb >> 16) & 0xFF) / 255f,
            ((rgb >> 8) & 0xFF) / 255f,
            (rgb & 0xFF) / 255f);
    }

    private static readonly int[] DyeColours =
    [
        16383998, // white
        16351261, // orange
        13061821, // magenta
        3847130,  // light blue
        16701501, // yellow
        8439583,  // lime
        15961002, // pink
        4673362,  // gray
        10329495, // light gray
        1481884,  // cyan
        8991416,  // purple
        3949738,  // blue
        8606770,  // brown
        6192150,  // green
        11546150, // red
        1908001   // black
    ];

    private void AppendTransformed(DynamicGeometry source, Matrix4x4 transform)
    {
        if (source.VertexFloats == 0)
        {
            return;
        }

        var vertexBase = (uint)(_geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
        var target = _geometry;
        var neededVertexFloats = target.VertexFloats + source.VertexFloats;
        var neededIndexCount = target.IndexCount + source.IndexCount;
        target.Reserve(neededVertexFloats, neededIndexCount);

        for (var vertex = 0; vertex < source.VertexFloats; vertex += ChunkMesher.FloatsPerVertex)
        {
            var position = Vector3.Transform(
                new Vector3(source.Vertices[vertex], source.Vertices[vertex + 1], source.Vertices[vertex + 2]),
                transform);
            target.Vertices[target.VertexFloats + vertex] = position.X;
            target.Vertices[target.VertexFloats + vertex + 1] = position.Y;
            target.Vertices[target.VertexFloats + vertex + 2] = position.Z;
            for (var field = 3; field < ChunkMesher.FloatsPerVertex; field++)
            {
                target.Vertices[target.VertexFloats + vertex + field] = source.Vertices[vertex + field];
            }
        }

        // The source scale flips Y in its y-down frame; this renderer converts that to a positive
        // Y scale for the y-up baked model, so winding is not mirrored.
        var mirror = transform.GetDeterminant() < 0f;
        for (var index = 0; index < source.IndexCount; index += 3)
        {
            target.Indices[target.IndexCount + index] = source.Indices[index] + vertexBase;
            var second = source.Indices[index + 1] + vertexBase;
            var third = source.Indices[index + 2] + vertexBase;
            target.Indices[target.IndexCount + index + 1] = mirror ? third : second;
            target.Indices[target.IndexCount + index + 2] = mirror ? second : third;
        }

        target.VertexFloats = neededVertexFloats;
        target.IndexCount = neededIndexCount;
    }
}
