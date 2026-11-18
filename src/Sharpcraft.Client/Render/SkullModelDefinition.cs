// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>Supported vanilla skull model families for the static head slice.</summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.level.block.SkullBlock.Type", "ref://mc-26.3-analysis/src/net/minecraft/world/level/block/SkullBlock.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "all seven supported static skull families; model-specific animation/skin cache remain separate")]
public enum SkullType : byte
{
    None = 0,
    Skeleton = 1,
    WitherSkeleton = 2,
    Zombie = 3,
    Creeper = 4,
    Player = 5,
    Piglin = 6,
    Dragon = 7
}

/// <summary>
/// Static skull definitions for the seven supported families: the four mob heads use the shared 8x8x8
/// <c>SkullModel</c> head, Player adds the humanoid hat overlay, Piglin uses its six-box head and Dragon
/// uses the seven-box head/jaw model.
/// </summary>
/// <remarks>
/// Source: <c>ref://mc-26.3-analysis/src/net/minecraft/client/model/object/skull/SkullModel.java</c>
/// (<c>createHeadModel</c> lines 19-24, <c>createMobHeadLayer</c> lines 36-39,
/// <c>createHumanoidHeadLayer</c> lines 26-34), plus the Piglin head and
/// <c>DragonHeadModel</c> definitions. The block entity renderer applies its outer pose after
/// <see cref="EntityModelMesher.BuildModel"/>.
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

    /// <summary>Dragon head texture path.</summary>
    public const string DragonTexture = "entity/enderdragon/dragon";

    /// <summary>Default player skin texture used when no profile skin is available.</summary>
    public static readonly string DefaultPlayerSkinTexture = DefaultPlayerSkin.Default.Path;

    private static readonly VanillaMeshDefinition SkullMesh = BuildSkullMesh();
    private static readonly VanillaMeshDefinition PlayerMesh = BuildPlayerSkullMesh();
    private static readonly VanillaMeshDefinition DragonMesh = BuildDragonSkullMesh();

    private static readonly EntityModelPart[] Parts =
        [.. VanillaMeshBake.Bake(SkullMesh, TextureWidth, TextureHeight, modelHeight: 0f, rawSourceFrame: true)];

    private static readonly CompiledEntityModel SkullCompiled =
        VanillaMeshBakery.Build(SkullMesh, TextureWidth, TextureHeight, modelHeight: 0f, texturePath: SkeletonTexture, rawSourceFrame: true);

    private static readonly EntityModelPart[] PlayerParts =
        [.. VanillaMeshBake.Bake(PlayerMesh, 64, 64, modelHeight: 0f, rawSourceFrame: true)];

    private static readonly CompiledEntityModel PlayerCompiled =
        VanillaMeshBakery.Build(PlayerMesh, 64, 64, modelHeight: 0f, texturePath: DefaultPlayerSkinTexture, rawSourceFrame: true);

    private static readonly EntityModelPart[] DragonParts =
        VanillaMeshBake.Bake(DragonMesh, 256, 256, modelHeight: 0f, rawSourceFrame: true)
            .Select(PinDragonUv).ToArray();

    /// <summary>
    /// Baked head definition shared by the four mob skull types, in the raw source y-down frame.
    /// The renderer's source <c>scale(-1,-1,1)</c> outer pose performs the y-up flip, so no baked
    /// baseline or renderer-local translation is folded in. ref://mc-26.3-analysis/src/net/minecraft/
    /// client/model/object/skull/SkullModel.java:19-24 and SkullBlockRenderer.java:164-168.
    /// </summary>
    public static readonly EntityModelDefinition Definition = new(
        name: "skull",
        textureWidth: TextureWidth,
        textureHeight: TextureHeight,
        parts: Parts,
        faceTransforms: null,
        texturePath: SkeletonTexture,
        compiled: SkullCompiled,
        baselinePx: 0f,
        rawSourceFrame: true);

    /// <summary>Player head definition with the humanoid head + hat overlay, in the raw source frame.</summary>
    public static readonly EntityModelDefinition PlayerDefinition = new(
        name: "player_skull",
        textureWidth: 64,
        textureHeight: 64,
        parts: PlayerParts,
        faceTransforms: null,
        texturePath: DefaultPlayerSkinTexture,
        compiled: PlayerCompiled,
        baselinePx: 0f,
        rawSourceFrame: true);

    /// <summary>Dragon head definition: six head boxes plus the parented jaw.</summary>
    public static readonly EntityModelDefinition DragonDefinition = new(
        name: "dragon_skull",
        textureWidth: 256,
        textureHeight: 256,
        parts: DragonParts,
        faceTransforms: null,
        texturePath: DragonTexture,
        baselinePx: 0f, rawSourceFrame: true)
    {
        // Source head pose uses PartPose.scaled(0.75); the generic scaled bake emits absolute source-frame
        // cuboids, so the outer skull pose applies without a renderer-local part matrix.
        // ref://mc-26.3-analysis/src/net/minecraft/client/model/object/skull/DragonHeadModel.java:14-42
        // ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/SkullBlockRenderer.java:164-168
    };

    /// <summary>Selects the baked definition for a skull type.</summary>
    public static EntityModelDefinition DefinitionFor(SkullType type) => type switch
    {
        SkullType.Player => PlayerDefinition,
        SkullType.Piglin => PiglinModelDefinition.Head,
        SkullType.Dragon => DragonDefinition,
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
        "dragon_head" or "dragon_wall_head" => SkullType.Dragon,
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
        SkullType.Dragon => DragonTexture,
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

    private static EntityModelPart PinDragonUv(EntityModelPart part) => part.Name switch
    {
        // The source head pose is PartPose.scaled(0.75): positions/sizes are scaled, but Cube UV polygons keep
        // the pre-scale cube dimensions (DragonHeadModel.createHeadLayer addBox literals).
        "head" => VanillaMeshBake.PinUvDimensions(part, 12, 5, 16),
        "upper_head" => VanillaMeshBake.PinUvDimensions(part, 16, 16, 16),
        "scale_left" or "scale_right" => VanillaMeshBake.PinUvDimensions(part, 2, 4, 6),
        "nostril_left" or "nostril_right" => VanillaMeshBake.PinUvDimensions(part, 2, 2, 4),
        "jaw" => VanillaMeshBake.PinUvDimensions(part, 12, 4, 16),
        _ => part
    };

    private static VanillaMeshDefinition BuildDragonSkullMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        // DragonHeadModel.createHeadLayer: six head boxes share the scaled head pose; the jaw is a
        // child with an unscaled local offset. ref://mc-26.3-analysis/src/net/minecraft/client/model/object/skull/DragonHeadModel.java:14-42.
        var head = root.AddOrReplaceChild("head",
            VanillaPartPose.Offset(0f, -7.986666f, 0f).Scaled(0.75f),
            new VanillaCubeDefinition("upper_lip", 176, 44, -6f, -1f, -24f, 12f, 5f, 16f, MirrorX: false),
            new VanillaCubeDefinition("upper_head", 112, 30, -8f, -8f, -10f, 16f, 16f, 16f, MirrorX: false),
            new VanillaCubeDefinition("scale_left", 0, 0, -5f, -12f, -4f, 2f, 4f, 6f, Mirror: true, MirrorX: false),
            new VanillaCubeDefinition("nostril_left", 112, 0, -5f, -3f, -22f, 2f, 2f, 4f, Mirror: true, MirrorX: false),
            new VanillaCubeDefinition("scale_right", 0, 0, 3f, -12f, -4f, 2f, 4f, 6f, MirrorX: false),
            new VanillaCubeDefinition("nostril_right", 112, 0, 3f, -3f, -22f, 2f, 2f, 4f, MirrorX: false));
        head.AddOrReplaceChild("jaw", VanillaPartPose.Offset(0f, 4f, -8f),
            new VanillaCubeDefinition("jaw", 176, 65, -6f, 0f, -16f, 12f, 4f, 16f, MirrorX: false));
        return new VanillaMeshDefinition(root);
    }
}
