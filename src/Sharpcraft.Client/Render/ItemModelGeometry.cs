// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Client.Render;

/// <summary>
/// One quad produced by an element face: four corners in model blocks plus the sprite-local UV of each corner.
/// </summary>
/// <remarks>
/// Corner order follows the reference <c>FaceInfo</c> tables, so corners 0–3 are (minU,minV), (minU,maxV),
/// (maxU,maxV), (maxU,minV) and triangles are 0-1-2 and 0-2-3.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.block.model.BakedQuad", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/block/model/BakedQuad.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "four corners plus UVs and tint index; per-vertex lighting and shade flags are not modelled")]
public readonly record struct ItemModelQuad(    string? Texture,
    Vector3 Corner0,
    Vector3 Corner1,
    Vector3 Corner2,
    Vector3 Corner3,
    Vector2 Uv0,
    Vector2 Uv1,
    Vector2 Uv2,
    Vector2 Uv3,
    int TintIndex,
    ModelFaceDirection Direction)
{
    /// <summary>Corner at <paramref name="index"/> (0–3).</summary>
    public Vector3 Corner(int index) => index switch
    {
        0 => Corner0,
        1 => Corner1,
        2 => Corner2,
        _ => Corner3
    };

    /// <summary>UV at <paramref name="index"/> (0–3).</summary>
    public Vector2 Uv(int index) => index switch
    {
        0 => Uv0,
        1 => Uv1,
        2 => Uv2,
        _ => Uv3
    };
}

/// <summary>
/// Turns cuboid elements into quads. Positions and UVs follow the reference <c>FaceBakery</c>: element pixels are
/// divided by sixteen, and model UV units are divided by sixteen as well.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.block.model.FaceBakery", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/block/model/FaceBakery.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "element faces only; element rotation and shade overrides are not modelled")]
public static class ItemModelGeometry
{
    /// <summary>Model units per block, used for both positions and UVs.</summary>
    public const float PixelsPerBlock = 16f;

    /// <summary>
    /// Reference <c>FaceInfo</c> corner selection: each direction lists its four corners as a min/max pick per axis,
    /// where 1 means "take the max". The order is the one the reference bakes, so UV index mapping lines up.
    /// </summary>
    private static readonly (byte X, byte Y, byte Z)[] DownPicks = [(0, 0, 1), (0, 0, 0), (1, 0, 0), (1, 0, 1)];
    private static readonly (byte X, byte Y, byte Z)[] UpPicks = [(0, 1, 0), (0, 1, 1), (1, 1, 1), (1, 1, 0)];
    private static readonly (byte X, byte Y, byte Z)[] NorthPicks = [(1, 1, 0), (1, 0, 0), (0, 0, 0), (0, 1, 0)];
    private static readonly (byte X, byte Y, byte Z)[] SouthPicks = [(0, 1, 1), (0, 0, 1), (1, 0, 1), (1, 1, 1)];
    private static readonly (byte X, byte Y, byte Z)[] WestPicks = [(0, 1, 0), (0, 0, 0), (0, 0, 1), (0, 1, 1)];
    private static readonly (byte X, byte Y, byte Z)[] EastPicks = [(1, 1, 1), (1, 0, 1), (1, 0, 0), (1, 1, 0)];

    private static (byte X, byte Y, byte Z)[] CornersFor(ModelFaceDirection direction) => direction switch
    {
        ModelFaceDirection.Down => DownPicks,
        ModelFaceDirection.Up => UpPicks,
        ModelFaceDirection.North => NorthPicks,
        ModelFaceDirection.South => SouthPicks,
        ModelFaceDirection.West => WestPicks,
        _ => EastPicks
    };

    /// <summary>
    /// Builds every drawable quad of one element. Faces missing from the element are skipped, as the reference does.
    /// </summary>
    public static void Append(List<ItemModelQuad> output, ItemModelElement element)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(element);

        var from = element.From / PixelsPerBlock;
        var to = element.To / PixelsPerBlock;
        Span<Vector3> positions = stackalloc Vector3[4];
        Span<Vector2> uvs = stackalloc Vector2[4];

        foreach (var (direction, face) in element.Faces)
        {
            var picks = CornersFor(direction);
            for (var index = 0; index < 4; index++)
            {
                var (x, y, z) = picks[index];
                positions[index] = new Vector3(
                    x == 0 ? from.X : to.X,
                    y == 0 ? from.Y : to.Y,
                    z == 0 ? from.Z : to.Z);
                // Reference UVs: index 0/1 take minU, 2/3 take maxU; index 0/3 take minV, 1/2 take maxV.
                uvs[index] = new Vector2(
                    (index is 0 or 1 ? face.Uv.X : face.Uv.Z) / PixelsPerBlock,
                    (index is 0 or 3 ? face.Uv.Y : face.Uv.W) / PixelsPerBlock);
            }

            output.Add(new ItemModelQuad(
                face.Texture,
                positions[0], positions[1], positions[2], positions[3],
                uvs[0], uvs[1], uvs[2], uvs[3],
                face.TintIndex,
                direction));
        }
    }

    /// <summary>Builds the quads of every element of a model.</summary>
    public static List<ItemModelQuad> Build(IReadOnlyList<ItemModelElement> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);
        var quads = new List<ItemModelQuad>(elements.Count * 6);
        foreach (var element in elements)
        {
            Append(quads, element);
        }

        return quads;
    }

    /// <summary>
    /// Moves every corner by <paramref name="matrix"/> and leaves the UVs untouched, which is how the reference applies
    /// the item display transform and the item entity placement on top of the baked model.
    /// </summary>
    public static List<ItemModelQuad> Transform(IReadOnlyList<ItemModelQuad> quads, Matrix4x4 matrix)
    {
        ArgumentNullException.ThrowIfNull(quads);
        var transformed = new List<ItemModelQuad>(quads.Count);
        foreach (var quad in quads)
        {
            transformed.Add(quad with
            {
                Corner0 = Vector3.Transform(quad.Corner0, matrix),
                Corner1 = Vector3.Transform(quad.Corner1, matrix),
                Corner2 = Vector3.Transform(quad.Corner2, matrix),
                Corner3 = Vector3.Transform(quad.Corner3, matrix)
            });
        }

        return transformed;
    }
}
