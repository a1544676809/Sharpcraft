// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Centralised vanilla <c>MeshTransformer</c> bake for already-flattened <see cref="EntityModelPart"/> geometry.
/// </summary>
/// <remarks>
/// <para>
/// The reference <c>MeshTransformer.scaling(factor)</c> scales every model vertex by <c>factor</c> and then translates
/// the result down by <c>yOffset = 24.016f * (1f - factor)</c> in the y-down model frame. Our parts are stored y-up
/// (<see cref="EntityModelPart.Bottom"/> and <see cref="EntityModelPart.PivotY"/> are measured from the feet), so a
/// y-down coordinate maps to <c>modelHeight - yDown</c>. Applying the reference transform therefore gives
/// <c>yUp' = modelHeight - (yDown * factor + yOffset)</c> where <c>yDown = modelHeight - yUp</c>.
/// </para>
/// <para>
/// Cube sizes (and the per-part <see cref="EntityModelPart.Grow"/>) scale by <c>factor</c>; rotations are unchanged by a
/// uniform scale. Because the physical box shrinks but the reference texture sampling keeps the original cube UV
/// rectangle, <see cref="EntityModelPart.UvWidth"/>/<see cref="EntityModelPart.UvHeight"/>/
/// <see cref="EntityModelPart.UvDepth"/> are pinned to the original cube dimensions whenever the factor is not one. This
/// is exactly the T-662/T-663 baby-elytra fix expressed once instead of hand-copied per model.
/// </para>
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.geom.builders.MeshTransformer", "ref://mc-26.3-analysis/src/net/minecraft/client/model/geom/builders/MeshTransformer.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Centralised vanilla MeshTransformer scaling bake for flattened EntityModelPart geometry")]
public static partial class VanillaMeshBake
{
    /// <summary>Reference pivot height constant used by <c>MeshTransformer.scaling</c>.</summary>
    public const float ReferencePivotHeight = 24.016f;

    /// <summary>Applies the reference scaling transform to every part and returns a new array (input is not mutated).</summary>
    public static EntityModelPart[] Scale(IReadOnlyList<EntityModelPart> parts, float factor, float modelHeight = 24f)
    {
        ArgumentNullException.ThrowIfNull(parts);
        if (!(factor > 0f))
        {
            throw new ArgumentOutOfRangeException(nameof(factor), factor, "scale factor must be positive");
        }

        var result = new EntityModelPart[parts.Count];
        for (var i = 0; i < parts.Count; i++)
        {
            result[i] = Scale(parts[i], factor, modelHeight);
        }

        return result;
    }

    /// <summary>Applies the reference scaling transform to one part.</summary>
    public static EntityModelPart Scale(in EntityModelPart part, float factor, float modelHeight = 24f)
    {
        if (!(factor > 0f))
        {
            throw new ArgumentOutOfRangeException(nameof(factor), factor, "scale factor must be positive");
        }

        // Identity must be byte-for-byte, including any pre-existing UV overrides and selector delegates.
        if (factor == 1f)
        {
            return part;
        }

        var yOffset = ReferencePivotHeight * (1f - factor);

        EntityModelPart result = part with
        {
            CentreX = part.CentreX * factor,
            CentreZ = part.CentreZ * factor,
            Bottom = ScaleYUp(part.Bottom, factor, modelHeight, yOffset),
            PivotX = part.PivotX * factor,
            PivotY = ScaleYUp(part.PivotY, factor, modelHeight, yOffset),
            PivotZ = part.PivotZ * factor,
            Width = part.Width * factor,
            Height = part.Height * factor,
            Depth = part.Depth * factor,
            Grow = part.Grow * factor,
            GrowXyz = part.GrowXyz is { } growXyz
                ? new Vector3(growXyz.X * factor, growXyz.Y * factor, growXyz.Z * factor)
                : null,
            // Keep sampling the reference cube's UV rectangle even though the geometry shrank. If a part already
            // carries an explicit UV override (for example a previously baked layer), preserve that override instead
            // of overwriting it with the already-scaled geometry size.
            UvWidth = part.UvWidth > 0 ? part.UvWidth : RoundDimension(part.Width),
            UvHeight = part.UvHeight > 0 ? part.UvHeight : RoundDimension(part.Height),
            UvDepth = part.UvDepth > 0 ? part.UvDepth : RoundDimension(part.Depth)
        };

        return result;
    }

    /// <summary>Converts a y-up coordinate through the y-down scale + y-offset transform and back to y-up.</summary>
    private static float ScaleYUp(float yUp, float factor, float modelHeight, float yOffset)
    {
        var yDown = modelHeight - yUp;
        return modelHeight - ((yDown * factor) + yOffset);
    }

    private static int RoundDimension(float value) => (int)MathF.Round(value);

    /// <summary>
    /// Bakes a root part from the reference y-down pivot/cube values. This is the exact legacy conversion: the global
    /// X mirror, the y-down to y-up model-height flip, and the pass-through rotations. Root cubes keep an absolute
    /// model-space position.
    /// </summary>
    public static EntityModelPart BakeRoot(
        string name,
        float pivotX,
        float pivotY,
        float pivotZ,
        float minX,
        float minY,
        float minZ,
        float width,
        float height,
        float depth,
        int textureX,
        int textureY,
        bool mirror = false,
        bool mirrorX = true,
        IReadOnlyList<EntityModelLayer>? overlays = null,
        float modelHeight = 24f,
        float rotationX = 0f,
        float rotationY = 0f,
        float rotationZ = 0f,
        string? texturePath = null,
        float grow = 0f,
        EntityGeometryLayerKind kind = EntityGeometryLayerKind.Cutout)
    {
        var sourceCentreX = pivotX + minX + (width * 0.5f);
        var centreX = MathF.Round(mirrorX ? -sourceCentreX : sourceCentreX, 4);
        var bottom = MathF.Round(modelHeight - (pivotY + minY + height), 4);
        var centreZ = MathF.Round(pivotZ + minZ + (depth * 0.5f), 4);
        return new EntityModelPart(
            name,
            centreX,
            bottom,
            width,
            height,
            depth,
            textureX,
            textureY,
            mirror,
            overlays,
            CentreZ: centreZ,
            PivotX: mirrorX ? -pivotX : pivotX,
            PivotY: modelHeight - pivotY,
            PivotZ: pivotZ,
            RotationX: rotationX,
            RotationY: rotationY,
            RotationZ: rotationZ,
            TexturePath: texturePath,
            Grow: grow,
            Kind: kind);
    }

    /// <summary>
    /// Bakes a child part from the reference y-down pivot/cube values, keeping the cube local to
    /// <paramref name="parent"/> (no model-height offset). This is the exact legacy child conversion: only the global X
    /// mirror and Y flip are applied, and X/Y rotations negate while Z is unchanged. The mesher composes the parent chain
    /// afterwards.
    /// </summary>
    public static EntityModelPart BakeChild(
        string name,
        string parent,
        float pivotX,
        float pivotY,
        float pivotZ,
        float minX,
        float minY,
        float minZ,
        float width,
        float height,
        float depth,
        int textureX,
        int textureY,
        bool mirror = false,
        IReadOnlyList<EntityModelLayer>? overlays = null,
        float rotationX = 0f,
        float rotationY = 0f,
        float rotationZ = 0f,
        string? texturePath = null,
        float grow = 0f,
        EntityGeometryLayerKind kind = EntityGeometryLayerKind.Cutout)
    {
        ArgumentException.ThrowIfNullOrEmpty(parent);
        return new EntityModelPart(
            name,
            MathF.Round(-(minX + (width * 0.5f)), 4),
            MathF.Round(-(minY + height), 4),
            width,
            height,
            depth,
            textureX,
            textureY,
            mirror,
            overlays,
            CentreZ: MathF.Round(minZ + (depth * 0.5f), 4),
            PivotX: MathF.Round(-pivotX, 4),
            PivotY: MathF.Round(-pivotY, 4),
            PivotZ: pivotZ,
            RotationX: -rotationX,
            RotationY: -rotationY,
            RotationZ: rotationZ,
            TexturePath: texturePath,
            Grow: grow,
            Parent: parent,
            Kind: kind);
    }
}
