// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;

/// <summary>
/// One static model cuboid in model-pixel space. This is render data only: it never references an entity or world.
/// </summary>
/// <remarks>
/// The parameterized constructor is internal so model-definition files cannot construct baked geometry directly;
/// the definition DSL and the centralized baker are the only intended construction paths. The public properties remain
/// init-only for record <c>with</c> composition inside the Client assembly and for test inspection.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.geom.ModelPart", "ref://mc-26.3-analysis/src/net/minecraft/client/model/geom/ModelPart.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "One model cuboid/part: pivot, box, UV, overlays")]
public readonly record struct EntityModelPart
{
    public string Name { get; init; }
    public float CentreX { get; init; }
    public float Bottom { get; init; }
    public float Width { get; init; }
    public float Height { get; init; }
    public float Depth { get; init; }
    public int TextureX { get; init; }
    public int TextureY { get; init; }
    public bool Mirror { get; init; }
    public IReadOnlyList<EntityModelLayer>? OverlayLayers { get; init; }
    public float CentreZ { get; init; }
    public float PivotX { get; init; }
    public float PivotY { get; init; }
    public float PivotZ { get; init; }
    public float RotationX { get; init; }
    public float RotationY { get; init; }
    public float RotationZ { get; init; }
    public string? TexturePath { get; init; }
    public float Grow { get; init; }
    public string? Parent { get; init; }
    public EntityGeometryLayerKind Kind { get; init; }
    public Func<EntityRenderState, bool>? VisibleWhen { get; init; }
    public int TextureWidth { get; init; }
    public int TextureHeight { get; init; }
    public bool IncludeInEmissive { get; init; }
    public bool EmissiveOnly { get; init; }
    public string? EmissiveTexturePath { get; init; }
    public Func<EntityRenderState, System.Numerics.Vector3>? TintSelector { get; init; }
    public Func<EntityRenderState, string?>? TextureSelector { get; init; }
    public string? PoseSource { get; init; }
    public System.Numerics.Vector3? GrowXyz { get; init; }
    public Func<EntityRenderState, System.Numerics.Vector3>? RotationSelector { get; init; }
    public int UvWidth { get; init; }
    public int UvHeight { get; init; }
    public int UvDepth { get; init; }
    public Func<EntityRenderState, float>? YOffsetSelector { get; init; }

    internal EntityModelPart(
        string Name,
        float CentreX,
        float Bottom,
        float Width,
        float Height,
        float Depth,
        int TextureX = 0,
        int TextureY = 0,
        bool Mirror = false,
        IReadOnlyList<EntityModelLayer>? OverlayLayers = null,
        float CentreZ = 0f,
        float PivotX = 0f,
        float PivotY = 0f,
        float PivotZ = 0f,
        float RotationX = 0f,
        float RotationY = 0f,
        float RotationZ = 0f,
        string? TexturePath = null,
        float Grow = 0f,
        string? Parent = null,
        EntityGeometryLayerKind Kind = EntityGeometryLayerKind.Cutout,
        Func<EntityRenderState, bool>? VisibleWhen = null,
        int TextureWidth = 0,
        int TextureHeight = 0,
        bool IncludeInEmissive = true,
        bool EmissiveOnly = false,
        string? EmissiveTexturePath = null,
        Func<EntityRenderState, System.Numerics.Vector3>? TintSelector = null,
        Func<EntityRenderState, string?>? TextureSelector = null,
        string? PoseSource = null,
        System.Numerics.Vector3? GrowXyz = null,
        Func<EntityRenderState, System.Numerics.Vector3>? RotationSelector = null,
        int UvWidth = 0,
        int UvHeight = 0,
        int UvDepth = 0,
        Func<EntityRenderState, float>? YOffsetSelector = null)
    {
        this.Name = Name;
        this.CentreX = CentreX;
        this.Bottom = Bottom;
        this.Width = Width;
        this.Height = Height;
        this.Depth = Depth;
        this.TextureX = TextureX;
        this.TextureY = TextureY;
        this.Mirror = Mirror;
        this.OverlayLayers = OverlayLayers;
        this.CentreZ = CentreZ;
        this.PivotX = PivotX;
        this.PivotY = PivotY;
        this.PivotZ = PivotZ;
        this.RotationX = RotationX;
        this.RotationY = RotationY;
        this.RotationZ = RotationZ;
        this.TexturePath = TexturePath;
        this.Grow = Grow;
        this.Parent = Parent;
        this.Kind = Kind;
        this.VisibleWhen = VisibleWhen;
        this.TextureWidth = TextureWidth;
        this.TextureHeight = TextureHeight;
        this.IncludeInEmissive = IncludeInEmissive;
        this.EmissiveOnly = EmissiveOnly;
        this.EmissiveTexturePath = EmissiveTexturePath;
        this.TintSelector = TintSelector;
        this.TextureSelector = TextureSelector;
        this.PoseSource = PoseSource;
        this.GrowXyz = GrowXyz;
        this.RotationSelector = RotationSelector;
        this.UvWidth = UvWidth;
        this.UvHeight = UvHeight;
        this.UvDepth = UvDepth;
        this.YOffsetSelector = YOffsetSelector;
    }

    /// <summary>Returns a copy with the metadata fields used by model-definition layers overridden.</summary>
    public EntityModelPart With(
        string? Name = null,
        string? TexturePath = null,
        EntityGeometryLayerKind? Kind = null,
        string? PoseSource = null,
        bool? EmissiveOnly = null,
        bool? IncludeInEmissive = null,
        string? EmissiveTexturePath = null,
        Func<EntityRenderState, System.Numerics.Vector3>? TintSelector = null)
    {
        return new EntityModelPart(
            Name ?? this.Name,
            CentreX,
            Bottom,
            Width,
            Height,
            Depth,
            TextureX,
            TextureY,
            Mirror,
            OverlayLayers,
            CentreZ,
            PivotX,
            PivotY,
            PivotZ,
            RotationX,
            RotationY,
            RotationZ,
            TexturePath ?? this.TexturePath,
            Grow,
            Parent,
            Kind ?? this.Kind,
            VisibleWhen,
            TextureWidth,
            TextureHeight,
            IncludeInEmissive ?? this.IncludeInEmissive,
            EmissiveOnly ?? this.EmissiveOnly,
            EmissiveTexturePath ?? this.EmissiveTexturePath,
            TintSelector ?? this.TintSelector,
            TextureSelector,
            PoseSource ?? this.PoseSource,
            GrowXyz,
            RotationSelector,
            UvWidth,
            UvHeight,
            UvDepth,
            YOffsetSelector);
    }
}

/// <summary>
/// An outer skin layer around one base cuboid, expressed as a uniform growth and its own texture offset.
/// </summary>
public readonly record struct EntityModelLayer(
    string Name,
    float Grow,
    int TextureX,
    int TextureY,
    bool Mirror = false,
    string? TexturePath = null,
    EntityGeometryLayerKind Kind = EntityGeometryLayerKind.Cutout);

/// <summary>
/// Static model definitions consumed by the entity mesher. The values follow the reference
/// `HumanoidModel.createMesh` player parts (docs/modules/entity-render.md).
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.PlayerModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/PlayerModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Merged, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Helper to build humanoid/player part geometry")]
public static class PlayerModelDefinition
{
    /// <summary>Model height in pixels: head top is 32 pixels above the feet.</summary>
    public const float ModelPixels = 32f;

    /// <summary>Final per-face UV transforms calibrated in the sandbox (identical for every adult part).</summary>
    public static readonly IReadOnlyDictionary<string, FaceUvTransform> DefaultFaceTransforms = new Dictionary<string, FaceUvTransform>(StringComparer.Ordinal)
    {
        ["DOWN"] = new(180, false, false),
        ["UP"] = new(180, false, false),
        ["WEST"] = new(0, true, false),
        ["NORTH"] = new(0, true, false),
        ["EAST"] = new(0, true, false),
        ["SOUTH"] = new(0, true, false)
    };

    /// <summary>
    /// Declarative DSL tree for the six adult player parts (T-671). The root group's direct children are baked as root
    /// parts via <c>VanillaMeshBake.BakeRoot</c>, producing exactly the legacy flat part records.
    /// </summary>
    private static readonly VanillaMeshDefinition AdultMesh = BuildAdultMesh();

    /// <summary>The declarative adult mesh, shared with humanoid families that replace only one part (Piglin).</summary>
    internal static VanillaMeshDefinition AdultMeshDefinition => AdultMesh;

    /// <summary>The baked adult player parts, identical to the former hand-written flat array.</summary>
    public static readonly EntityModelPart[] AdultParts = [.. VanillaMeshBake.Bake(AdultMesh, 0, 0)];

    private static readonly CompiledEntityModel AdultCompiled =
        VanillaMeshBakery.Build(AdultMesh, 64, 64);

    private static VanillaMeshDefinition BuildAdultMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 0f, 0f),
            new VanillaCubeDefinition("head", 0, 0, -4f, -8f, -4f, 8f, 8f, 8f,
                Overlays: [new("hat", 0.5f, 32, 0, Kind: EntityGeometryLayerKind.Translucent)]));
        root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 0f, 0f),
            new VanillaCubeDefinition("body", 16, 16, -4f, 0f, -2f, 8f, 12f, 4f,
                Overlays: [new("jacket", 0.25f, 16, 32, Kind: EntityGeometryLayerKind.Translucent)]));
        root.AddOrReplaceChild("left_arm", VanillaPartPose.Offset(5f, 2f, 0f),
            new VanillaCubeDefinition("left_arm", 32, 48, -1f, -2f, -2f, 4f, 12f, 4f,
                Overlays: [new("left_sleeve", 0.25f, 48, 48, Kind: EntityGeometryLayerKind.Translucent)]));
        root.AddOrReplaceChild("right_arm", VanillaPartPose.Offset(-5f, 2f, 0f),
            new VanillaCubeDefinition("right_arm", 40, 16, -3f, -2f, -2f, 4f, 12f, 4f,
                Overlays: [new("right_sleeve", 0.25f, 40, 32, Kind: EntityGeometryLayerKind.Translucent)]));
        root.AddOrReplaceChild("left_leg", VanillaPartPose.Offset(1.9f, 12f, 0f),
            new VanillaCubeDefinition("left_leg", 16, 48, -2f, 0f, -2f, 4f, 12f, 4f,
                Overlays: [new("left_pants", 0.25f, 0, 48, Kind: EntityGeometryLayerKind.Translucent)]));
        root.AddOrReplaceChild("right_leg", VanillaPartPose.Offset(-1.9f, 12f, 0f),
            new VanillaCubeDefinition("right_leg", 0, 16, -2f, 0f, -2f, 4f, 12f, 4f,
                Overlays: [new("right_pants", 0.25f, 0, 32, Kind: EntityGeometryLayerKind.Translucent)]));
        return new VanillaMeshDefinition(root);
    }

    public static readonly EntityModelDefinition Definition =
        new("player", 64, 64, AdultParts, DefaultFaceTransforms, "entity/player/wide/steve",
            compiled: AdultCompiled);

}


