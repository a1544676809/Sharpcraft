// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Client.Assets;

/// <summary>One corner of a model quad: position in block units, texture coordinates in sprite units.</summary>
/// <param name="Position">Corner position, where one is a whole block.</param>
/// <param name="Uv">Texture coordinate, where one is the whole sprite.</param>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.resources.model.geometry.BakedQuad", "ref://mc-26.3-analysis/src/net/minecraft/client/resources/model/geometry/BakedQuad.java")]
public readonly record struct ModelVertex(Vector3 Position, Vector2 Uv);

/// <summary>One face of a model box, ready to be drawn.</summary>
/// <param name="V0">First corner.</param>
/// <param name="V1">Second corner.</param>
/// <param name="V2">Third corner.</param>
/// <param name="V3">Fourth corner.</param>
/// <param name="Direction">Face direction name.</param>
/// <param name="Texture">Texture variable as written, such as <c>#side</c>.</param>
/// <param name="TintIndex">Tint slot, or -1 when none.</param>
/// <param name="CullFace">Direction whose neighbour hides this face, or null.</param>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.resources.model.geometry.BakedQuad", "ref://mc-26.3-analysis/src/net/minecraft/client/resources/model/geometry/BakedQuad.java")]
public sealed record ModelQuad(
    ModelVertex V0,
    ModelVertex V1,
    ModelVertex V2,
    ModelVertex V3,
    string Direction,
    string Texture,
    int TintIndex,
    string? CullFace);

/// <summary>
/// Turns model boxes into quads: for every face the four corners, the texture rectangle and the
/// winding, following the reference's corner tables and its rule for deriving a face's texture
/// rectangle from the box when the model leaves it out
/// (docs/specs/render/block-model-geometry.md).
/// </summary>
/// <remarks>
/// Corners are emitted in the reference's order, which walks each face's perimeter, so the two
/// triangles are 0-1-2 and 0-2-3. Model units are sixteenths of a block and are divided down to block
/// units here; texture coordinates are divided the same way.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.resources.model.cuboid.FaceBakery", "ref://mc-26.3-analysis/src/net/minecraft/client/resources/model/cuboid/FaceBakery.java")]
public static class BlockModelGeometry
{
    private const float PerBlock = 16f;

    /// <summary>Builds every drawable quad of a model's elements.</summary>
    /// <param name="elements">Elements read by <see cref="BlockModelElements"/>.</param>
    /// <returns>Quads in model space, one per face of every box.</returns>
    public static List<ModelQuad> Build(IReadOnlyList<ModelElement> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);

        var quads = new List<ModelQuad>(elements.Count * 6);

        foreach (var element in elements)
        {
            foreach (var face in element.Faces)
            {
                if (FaceCorners(face.Key, element.Box) is not { } corners)
                {
                    continue;
                }

                var uv = face.Value.Uv is { Length: 4 } explicitUv
                    ? explicitUv
                    : DefaultUv(face.Key, element.Box);

                quads.Add(BuildQuad(corners, uv, face.Key, face.Value, element.Rotation));
            }
        }

        return quads;
    }

    /// <summary>The four corners of one face, walking its perimeter.</summary>
    /// <param name="direction">Face direction name.</param>
    /// <param name="box">Box the face belongs to.</param>
    private static ((float X, float Y, float Z) A, (float X, float Y, float Z) B, (float X, float Y, float Z) C, (float X, float Y, float Z) D)? FaceCorners(string direction, ModelBox box) =>
        direction switch
        {
            // Down: min corner at max Z, then round the bottom in the reference's order.
            "down" => (
                (box.FromX, box.FromY, box.ToZ),
                (box.FromX, box.FromY, box.FromZ),
                (box.ToX, box.FromY, box.FromZ),
                (box.ToX, box.FromY, box.ToZ)),
            "up" => (
                (box.FromX, box.ToY, box.FromZ),
                (box.FromX, box.ToY, box.ToZ),
                (box.ToX, box.ToY, box.ToZ),
                (box.ToX, box.ToY, box.FromZ)),
            "north" => (
                (box.ToX, box.ToY, box.FromZ),
                (box.ToX, box.FromY, box.FromZ),
                (box.FromX, box.FromY, box.FromZ),
                (box.FromX, box.ToY, box.FromZ)),
            "south" => (
                (box.FromX, box.ToY, box.ToZ),
                (box.FromX, box.FromY, box.ToZ),
                (box.ToX, box.FromY, box.ToZ),
                (box.ToX, box.ToY, box.ToZ)),
            "west" => (
                (box.FromX, box.ToY, box.FromZ),
                (box.FromX, box.FromY, box.FromZ),
                (box.FromX, box.FromY, box.ToZ),
                (box.FromX, box.ToY, box.ToZ)),
            "east" => (
                (box.ToX, box.ToY, box.ToZ),
                (box.ToX, box.FromY, box.ToZ),
                (box.ToX, box.FromY, box.FromZ),
                (box.ToX, box.ToY, box.FromZ)),
            _ => null
        };

    /// <summary>
    /// The texture rectangle a face uses when the model omits one: the box projected onto the face
    /// plane, with the vertical axis flipped where the reference flips it.
    /// </summary>
    /// <param name="direction">Face direction name.</param>
    /// <param name="box">Box the face belongs to.</param>
    public static float[] DefaultUv(string direction, ModelBox box) =>
        direction switch
        {
            "down" => [box.FromX, PerBlock - box.ToZ, box.ToX, PerBlock - box.FromZ],
            "up" => [box.FromX, box.FromZ, box.ToX, box.ToZ],
            "north" => [PerBlock - box.ToX, PerBlock - box.ToY, PerBlock - box.FromX, PerBlock - box.FromY],
            "south" => [box.FromX, PerBlock - box.ToY, box.ToX, PerBlock - box.FromY],
            "west" => [box.FromZ, PerBlock - box.ToY, box.ToZ, PerBlock - box.FromY],
            "east" => [PerBlock - box.ToZ, PerBlock - box.ToY, PerBlock - box.FromZ, PerBlock - box.FromY],
            _ => [0f, 0f, PerBlock, PerBlock]
        };

    private static ModelQuad BuildQuad(
        ((float X, float Y, float Z) A, (float X, float Y, float Z) B, (float X, float Y, float Z) C, (float X, float Y, float Z) D) corners,
        float[] uv,
        string direction,
        ModelFace face,
        ModelRotation? rotation)
    {
        // Corner order maps to the texture rectangle as 0 min/min, 1 min/max, 2 max/max, 3 max/min.
        // A face rotation turns which corner of the rectangle each vertex reads, so the index is
        // advanced by a quarter turn before the rectangle corner is chosen.
        var quarter = ((face.Rotation % 360) + 360) % 360 / 90;
        Vector2 UvAt(int index)
        {
            var cornerIndex = (index + quarter) % 4;
            var u = cornerIndex is 0 or 1 ? uv[0] : uv[2];
            var v = cornerIndex is 0 or 3 ? uv[1] : uv[3];
            return new Vector2(u / PerBlock, v / PerBlock);
        }

        Span<ModelVertex> vertices = stackalloc ModelVertex[4]
        {
            new(Place(corners.A, rotation), UvAt(0)),
            new(Place(corners.B, rotation), UvAt(1)),
            new(Place(corners.C, rotation), UvAt(2)),
            new(Place(corners.D, rotation), UvAt(3))
        };

        return new ModelQuad(vertices[0], vertices[1], vertices[2], vertices[3], direction, face.Texture, face.TintIndex, face.CullFace);
    }

    /// <summary>Scales a corner to block units and turns it about the element's origin when asked.</summary>
    private static Vector3 Place((float X, float Y, float Z) corner, ModelRotation? rotation)
    {
        var position = new Vector3(corner.X / PerBlock, corner.Y / PerBlock, corner.Z / PerBlock);

        if (rotation is not { } turn)
        {
            return position;
        }

        var origin = new Vector3(turn.OriginX / PerBlock, turn.OriginY / PerBlock, turn.OriginZ / PerBlock);
        return ElementRotation.Apply(position, origin, turn.Axis, turn.Angle, turn.Rescale);
    }
}