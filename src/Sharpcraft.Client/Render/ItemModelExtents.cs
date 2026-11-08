// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Model-space extents of a resolved item model, the input <c>ItemEntityRenderer</c> needs for
/// <c>minOffsetY = -bbox.minY + 0.0625</c> and for the <c>modelDepth &gt; 0.0625</c> layout branch.
/// </summary>
/// <remarks>
/// The reference derives this from <c>ItemStackRenderState.visitExtents</c>, which walks the submitted quads. We bake
/// geometry instead of quads, so the same answer comes from the baked cuboids.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.item.ItemStackRenderState", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/item/ItemStackRenderState.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "getModelBoundingBox/visitExtents equivalent computed from baked geometry")]
public readonly record struct ItemModelExtents(Vector3 Min, Vector3 Max)
{
    /// <summary>
    /// Depth of a generated flat item. Reference <c>ItemModelGenerator</c> extrudes every layer between
    /// <c>MIN_Z = 7.5</c> and <c>MAX_Z = 8.5</c> model pixels, i.e. one sixteenth of a block.
    /// </summary>
    public const float FlatItemDepth = 1f / 16f;

    /// <summary>
    /// Extents of a generated flat item: the reference box runs from <c>(0,0,7.5)</c> to <c>(16,16,8.5)</c> model pixels
    /// with UVs <c>0,0 - 16,16</c>.
    /// </summary>
    public static readonly ItemModelExtents FlatItem = new(
        new Vector3(0f, 0f, 7.5f / 16f),
        new Vector3(1f, 1f, 8.5f / 16f));

    /// <summary>
    /// The reference threshold that separates a flat item from a genuinely three-dimensional one. A generated item sits
    /// exactly at this depth, so the comparison is strictly greater and flat drops stay in a single column.
    /// </summary>
    public const float FlatItemThreshold = 0.0625f;

    /// <summary>Depth (Z size) of the extents.</summary>
    public float Depth => Max.Z - Min.Z;

    /// <summary>True when the model is deeper than a generated flat item, which switches item entities to the scattered layout.</summary>
    public bool IsDeeperThanFlatItem => Depth > FlatItemThreshold;

    /// <summary>
    /// The reference visits extents *after* <c>LayerRenderState.applyTransform</c>, so the display transform (including
    /// its −0.5 centring and scale) is part of the box the item entity layout measures. This transforms the eight
    /// corners and re-boxes them, exactly like the reference collecting transformed corner points.
    /// </summary>
    public ItemModelExtents Transformed(Matrix4x4 matrix)
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        for (var corner = 0; corner < 8; corner++)
        {
            var point = new Vector3(
                (corner & 1) == 0 ? Min.X : Max.X,
                (corner & 2) == 0 ? Min.Y : Max.Y,
                (corner & 4) == 0 ? Min.Z : Max.Z);
            var transformed = Vector3.Transform(point, matrix);
            min = Vector3.Min(min, transformed);
            max = Vector3.Max(max, transformed);
        }

        return new ItemModelExtents(min, max);
    }

    /// <summary>
    /// Extents of a resolved item model in model space (pixels over sixteen, Y up). Cuboid models use their
    /// <c>elements</c>; models without elements — generated and handheld sprites — use <see cref="FlatItem"/>.
    /// </summary>
    public static ItemModelExtents FromResolution(ItemModelResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        var cuboids = resolution.Cuboids;
        if (cuboids.Count == 0)
        {
            return FlatItem;
        }

        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var element in cuboids)
        {
            min = Vector3.Min(min, Vector3.Min(element.From, element.To));
            max = Vector3.Max(max, Vector3.Max(element.From, element.To));
        }

        return new ItemModelExtents(min / 16f, max / 16f);
    }

    /// <summary>Extents for a display context: the model box after its <c>ItemTransform</c>, matching the reference.</summary>
    public static ItemModelExtents ForDisplay(ItemModelResolution resolution, ItemDisplayContext context)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        var baseExtents = FromResolution(resolution);
        return resolution.Display.TryGetValue(context, out var transform)
            ? baseExtents.Transformed(transform.ToMatrix())
            : baseExtents.Transformed(ItemDisplayTransform.None.ToMatrix());
    }

    /// <summary>
    /// Baked cuboids live in entity space, where the model's feet are <see cref="ModelHeightPixels"/> pixels up. This is
    /// the bakery's default <c>modelHeight</c>, and it has to be removed to get back to model space.
    /// </summary>
    public const float ModelHeightPixels = 24f;

    /// <summary>
    /// Extents of a baked model, taken as the axis-aligned box of its cuboids and returned in the reference's model
    /// space: pixels divided by sixteen, with the Y axis negated back from our y-up storage.
    /// </summary>
    public static ItemModelExtents FromModel(EntityModelDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var compiled = definition.Compiled;
        if (compiled is null || compiled.Cubes.Count == 0)
        {
            return FlatItem;
        }

        var scale = definition.Scale / 16f;
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var cube in compiled.Cubes)
        {
            var halfWidth = cube.Width * 0.5f;
            var halfDepth = cube.Depth * 0.5f;
            var low = new Vector3(cube.CentreX - halfWidth, cube.Bottom, cube.CentreZ - halfDepth);
            var high = new Vector3(cube.CentreX + halfWidth, cube.Bottom + cube.Height, cube.CentreZ + halfDepth);
            min = Vector3.Min(min, low);
            max = Vector3.Max(max, high);
        }

        // Shift out of entity space, scale into blocks, then mirror Y back to the reference model frame.
        var yLow = -(max.Y - ModelHeightPixels) * scale;
        var yHigh = -(min.Y - ModelHeightPixels) * scale;
        return new ItemModelExtents(
            new Vector3(min.X * scale, yLow, min.Z * scale),
            new Vector3(max.X * scale, yHigh, max.Z * scale));
    }
}
