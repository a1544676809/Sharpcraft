// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// BookModel: two lids, a seam, two page blocks and two flip-page planes. The model texture is 64 by
/// 32; the lectern renderer applies the outer book transform in world space.
/// </summary>
/// <remarks>
/// Every part value below is from
/// <c>ref://mc-26.3-analysis/src/net/minecraft/client/model/object/book/BookModel.java</c>
/// (<c>createBodyLayer</c>, lines 35-53). The static pose follows <c>setupAnim</c> lines 55-68 and
/// <c>State.forAnimation</c> lines 70-73.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.object.book.BookModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/object/book/BookModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class BookModelDefinition
{
    /// <summary>Declared texture width, 64 pixels.</summary>
    public const int TextureWidth = 64;

    /// <summary>Declared texture height, 32 pixels.</summary>
    public const int TextureHeight = 32;

    /// <summary>Reference texture path used by both the lectern and enchanting-table renderers.</summary>
    public const string TexturePath = "entity/enchantment/enchanting_table_book";

    private static readonly VanillaMeshDefinition BookMesh = BuildBookMesh();

    // BookModel parts are authored around the book's own origin; modelHeight 0 preserves that
    // block-entity model space instead of applying the entity feet conversion.
    private static readonly EntityModelPart[] Parts =
        [.. VanillaMeshBake.Bake(BookMesh, TextureWidth, TextureHeight, modelHeight: 0f)];

    private static readonly CompiledEntityModel BookCompiled =
        VanillaMeshBakery.Build(BookMesh, TextureWidth, TextureHeight, modelHeight: 0f, texturePath: TexturePath);

    /// <summary>Baked book definition shared by lecterns and enchanting tables.</summary>
    public static readonly EntityModelDefinition Definition = new(
        name: "book",
        textureWidth: TextureWidth,
        textureHeight: TextureHeight,
        parts: Parts,
        faceTransforms: PlayerModelDefinition.DefaultFaceTransforms,
        texturePath: TexturePath,
        compiled: BookCompiled);

    /// <summary>Builds the static pose for one <see cref="BookModelState"/>.</summary>
    /// <param name="state">Openness and page-flip values.</param>
    public static EntityPose CreatePose(BookModelState state)
    {
        var pose = new EntityPose();
        var openness = state.Openness;
        var page1 = openness - (openness * 2f * state.PageFlip1);
        var page2 = openness - (openness * 2f * state.PageFlip2);
        var pageX = MathF.Sin(openness);

        // BookModel.setupAnim: leftLid.yRot = PI + openness; rightLid.yRot = -openness;
        // leftPages/rightPages yRot = +/-openness. This block-entity model has no entity
        // scale(-1,-1,1), so PoseOffset stores the negated reference X/Y angles; PartFrame
        // subtracts them from the base part rotations.
        pose.Set("left_lid", new PoseOffset(Vector3.Zero, new Vector3(0f, -(MathF.PI + openness), 0f), Vector3.Zero));
        pose.Set("right_lid", new PoseOffset(Vector3.Zero, new Vector3(0f, openness, 0f), Vector3.Zero));
        pose.Set("left_pages", new PoseOffset(new Vector3(pageX, 0f, 0f), new Vector3(0f, -openness, 0f), Vector3.Zero));
        pose.Set("right_pages", new PoseOffset(new Vector3(pageX, 0f, 0f), new Vector3(0f, openness, 0f), Vector3.Zero));
        pose.Set("flip_page1", new PoseOffset(new Vector3(pageX, 0f, 0f), new Vector3(0f, -page1, 0f), Vector3.Zero));
        pose.Set("flip_page2", new PoseOffset(new Vector3(pageX, 0f, 0f), new Vector3(0f, -page2, 0f), Vector3.Zero));
        return pose;
    }

    /// <summary>
    /// Reference <c>BookModel.State</c>. <see cref="ForAnimation"/> mirrors the source formula
    /// <c>(sin(progress * 0.02) * 0.1 + 1.25) * openness</c>.
    /// </summary>
    [Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.object.book.BookModel.State", "ref://mc-26.3-analysis/src/net/minecraft/client/model/object/book/BookModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
    public readonly record struct BookModelState(float Openness, float PageFlip1, float PageFlip2)
    {
        public static BookModelState ForAnimation(float progress, float pageFlip1, float pageFlip2, float openness) =>
            new((MathF.Sin(progress * 0.02f) * 0.1f + 1.25f) * openness, pageFlip1, pageFlip2);
    }

    private static VanillaMeshDefinition BuildBookMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);

        // left_lid: texOffs(0,0), box(-6,-5,-0.005,6,10,0.005), PartPose.offset(0,0,-1); ref://mc-26.3-analysis/src/net/minecraft/client/model/object/book/BookModel.java:38-40.
        root.AddOrReplaceChild("left_lid", VanillaPartPose.Offset(0f, 0f, -1f),
            new VanillaCubeDefinition("left_lid", 0, 0, -6f, -5f, -0.005f, 6f, 10f, 0.005f, MirrorX: false));

        // right_lid: texOffs(16,0), box(0,-5,-0.005,6,10,0.005), PartPose.offset(0,0,1); ref://mc-26.3-analysis/src/net/minecraft/client/model/object/book/BookModel.java:41-43.
        root.AddOrReplaceChild("right_lid", VanillaPartPose.Offset(0f, 0f, 1f),
            new VanillaCubeDefinition("right_lid", 16, 0, 0f, -5f, -0.005f, 6f, 10f, 0.005f, MirrorX: false));

        // seam: texOffs(12,0), box(-1,-5,0,2,10,0.005), PartPose.rotation(0, PI/2, 0); ref://mc-26.3-analysis/src/net/minecraft/client/model/object/book/BookModel.java:44-46.
        root.AddOrReplaceChild("seam", VanillaPartPose.OffsetAndRotation(0f, 0f, 0f, 0f, MathF.PI / 2f, 0f),
            new VanillaCubeDefinition("seam", 12, 0, -1f, -5f, 0f, 2f, 10f, 0.005f, MirrorX: false));

        // left_pages: texOffs(0,10), box(0,-4,-0.99,5,8,1), PartPose.ZERO; ref://mc-26.3-analysis/src/net/minecraft/client/model/object/book/BookModel.java:47.
        root.AddOrReplaceChild("left_pages", VanillaPartPose.Zero,
            new VanillaCubeDefinition("left_pages", 0, 10, 0f, -4f, -0.99f, 5f, 8f, 1f, MirrorX: false));

        // right_pages: texOffs(12,10), box(0,-4,-0.01,5,8,1), PartPose.ZERO; ref://mc-26.3-analysis/src/net/minecraft/client/model/object/book/BookModel.java:48.
        root.AddOrReplaceChild("right_pages", VanillaPartPose.Zero,
            new VanillaCubeDefinition("right_pages", 12, 10, 0f, -4f, -0.01f, 5f, 8f, 1f, MirrorX: false));

        // flip_page1 and flip_page2: texOffs(24,10), box(0,-4,0,5,8,0.005), PartPose.ZERO; ref://mc-26.3-analysis/src/net/minecraft/client/model/object/book/BookModel.java:49-51.
        root.AddOrReplaceChild("flip_page1", VanillaPartPose.Zero,
            new VanillaCubeDefinition("flip_page1", 24, 10, 0f, -4f, 0f, 5f, 8f, 0.005f, MirrorX: false));
        root.AddOrReplaceChild("flip_page2", VanillaPartPose.Zero,
            new VanillaCubeDefinition("flip_page2", 24, 10, 0f, -4f, 0f, 5f, 8f, 0.005f, MirrorX: false));

        return new VanillaMeshDefinition(root);
    }
}