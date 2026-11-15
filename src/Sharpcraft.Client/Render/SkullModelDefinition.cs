// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>Supported vanilla skull model families for the static head slice.</summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.level.block.SkullBlock.Type", "ref://mc-26.3-analysis/src/net/minecraft/world/level/block/SkullBlock.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "static skull families only; player/dragon/piglin are not drawn")]
public enum SkullType : byte
{
    None = 0,
    Skeleton = 1,
    WitherSkeleton = 2,
    Zombie = 3,
    Creeper = 4,
    Player = 5,
    Piglin = 6
}

/// <summary>
/// The shared SkullModel head. All four supported skull families use the same 8x8x8 head cube
/// (<c>createMobHeadLayer</c>) and differ only by texture.
/// </summary>
/// <remarks>
/// Source: <c>ref://mc-26.3-analysis/src/net/minecraft/client/model/object/skull/SkullModel.java</c>
/// (<c>createHeadModel</c> lines 19-24, <c>createMobHeadLayer</c> lines 36-39). The head box is
/// <c>(-4,-8,-4,8,8,8)</c> at texture offset (0,0), declared texture 64x32. The block entity
/// renderer applies its outer pose after <see cref="EntityModelMesher.BuildModel"/>.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.object.skull.SkullModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/object/skull/SkullModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class SkullModelDefinition
{
    /// <summary>Texture width, 64 pixels.</summary>
    public const int TextureWidth = 64;

    /// <summary>Texture height, 32 pixels.</summary>
    public const int TextureHeight = 32;

    /// <summary>Default/skeleton texture path.</summary>
    public const string SkeletonTexture = "entity/skeleton/skeleton";

    /// <summary>Wither skeleton texture path.</summary>
    public const string WitherSkeletonTexture = "entity/skeleton/wither_skeleton";

    /// <summary>Zombie texture path.</summary>
    public const string ZombieTexture = "entity/zombie/zombie";

    /// <summary>Creeper texture path.</summary>
    public const string CreeperTexture = "entity/creeper/creeper";

    /// <summary>Piglin head texture path.</summary>
    public const string PiglinTexture = "entity/piglin/piglin";

    /// <summary>Default player skin texture used when no profile skin is available.</summary>
    public static readonly string DefaultPlayerSkinTexture = DefaultPlayerSkin.Default.Path;

    private static readonly VanillaMeshDefinition SkullMesh = BuildSkullMesh();
    private static readonly VanillaMeshDefinition PlayerMesh = BuildPlayerSkullMesh();

    private static readonly EntityModelPart[] Parts =
        [.. VanillaMeshBake.Bake(SkullMesh, TextureWidth, TextureHeight, modelHeight: 0f)];

    private static readonly CompiledEntityModel SkullCompiled =
        VanillaMeshBakery.Build(SkullMesh, TextureWidth, TextureHeight, modelHeight: 0f, texturePath: SkeletonTexture);

    private static readonly EntityModelPart[] PlayerParts =
        [.. VanillaMeshBake.Bake(PlayerMesh, 64, 64, modelHeight: 0f)];

    private static readonly CompiledEntityModel PlayerCompiled =
        VanillaMeshBakery.Build(PlayerMesh, 64, 64, modelHeight: 0f, texturePath: DefaultPlayerSkinTexture);

    /// <summary>Baked head definition shared by all supported skull types.</summary>
    public static readonly EntityModelDefinition Definition = new(
        name: "skull",
        textureWidth: TextureWidth,
        textureHeight: TextureHeight,
        parts: Parts,
        faceTransforms: PlayerModelDefinition.DefaultFaceTransforms,
        texturePath: SkeletonTexture,
        compiled: SkullCompiled)
    {
        // VanillaMeshBake converts the reference y-down model to y-up. SkullBlockRenderer's source
        // outer pose includes the model's scale(-1,-1,1), so shift the baked head by its source Y
        // origin before that pose to keep the final head above the block origin.
        // ref://mc-26.3-analysis/src/net/minecraft/client/model/object/skull/SkullModel.java:19-24
        // ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/SkullBlockRenderer.java:164-168
        LocalTransform = System.Numerics.Matrix4x4.CreateTranslation(0f, -0.5f, 0f)
    };

    /// <summary>Player head definition with the humanoid head + hat overlay.</summary>
    public static readonly EntityModelDefinition PlayerDefinition = new(
        name: "player_skull",
        textureWidth: 64,
        textureHeight: 64,
        parts: PlayerParts,
        faceTransforms: PlayerModelDefinition.DefaultFaceTransforms,
        texturePath: DefaultPlayerSkinTexture,
        compiled: PlayerCompiled)
    {
        // ref://mc-26.3-analysis/src/net/minecraft/client/model/object/skull/SkullModel.java:26-34
        // ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/SkullBlockRenderer.java:164-168
        LocalTransform = System.Numerics.Matrix4x4.CreateTranslation(0f, -0.5f, 0f)
    };

    /// <summary>Selects the baked definition for a skull type.</summary>
    public static EntityModelDefinition DefinitionFor(SkullType type) => type switch
    {
        SkullType.Player => PlayerDefinition,
        SkullType.Piglin => PiglinModelDefinition.Head,
        _ => Definition
    };

    /// <summary>Maps a block path to the supported static skull family, or <see cref="SkullType.None"/>.</summary>
    public static SkullType TypeOfPath(string? path) => path switch
    {
        "skeleton_skull" or "skeleton_wall_skull" => SkullType.Skeleton,
        "wither_skeleton_skull" or "wither_skeleton_wall_skull" => SkullType.WitherSkeleton,
        "zombie_head" or "zombie_wall_head" => SkullType.Zombie,
        "creeper_head" or "creeper_wall_head" => SkullType.Creeper,
        "player_head" or "player_wall_head" => SkullType.Player,
        "piglin_head" or "piglin_wall_head" => SkullType.Piglin,
        _ => SkullType.None
    };

    /// <summary>Texture path for one supported skull type.</summary>
    public static string? TextureFor(SkullType type) => type switch
    {
        SkullType.Skeleton => SkeletonTexture,
        SkullType.WitherSkeleton => WitherSkeletonTexture,
        SkullType.Zombie => ZombieTexture,
        SkullType.Creeper => CreeperTexture,
        SkullType.Player => DefaultPlayerSkinTexture,
        SkullType.Piglin => PiglinTexture,
        _ => null
    };

    private static VanillaMeshDefinition BuildSkullMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        // head: texOffs(0,0), addBox(-4,-8,-4,8,8,8), PartPose.ZERO;
        // ref://mc-26.3-analysis/src/net/minecraft/client/model/object/skull/SkullModel.java:19-24,36-39.
        root.AddOrReplaceChild("head", VanillaPartPose.Zero,
            new VanillaCubeDefinition("head", 0, 0, -4f, -8f, -4f, 8f, 8f, 8f, MirrorX: false));
        return new VanillaMeshDefinition(root);
    }

    private static VanillaMeshDefinition BuildPlayerSkullMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        // SkullModel.createHumanoidHeadLayer: head plus a 0.25-grow hat overlay at texOffs(32,0).
        // ref://mc-26.3-analysis/src/net/minecraft/client/model/object/skull/SkullModel.java:26-34.
        root.AddOrReplaceChild("head", VanillaPartPose.Zero,
            new VanillaCubeDefinition("head", 0, 0, -4f, -8f, -4f, 8f, 8f, 8f,
                MirrorX: false,
                Overlays: [new("hat", 0.25f, 32, 0)]));
        return new VanillaMeshDefinition(root);
    }
}
