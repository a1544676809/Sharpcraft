// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Client.Render;

/// <summary>One of the six face directions a model element can define.</summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.core.Direction", "ref://mc-26.3-analysis/src/net/minecraft/core/Direction.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "The six axis directions used by model faces")]
public enum ModelFaceDirection : byte
{
    /// <summary>Negative Y.</summary>
    Down = 0,

    /// <summary>Positive Y.</summary>
    Up = 1,

    /// <summary>Negative Z.</summary>
    North = 2,

    /// <summary>Positive Z.</summary>
    South = 3,

    /// <summary>Negative X.</summary>
    West = 4,

    /// <summary>Positive X.</summary>
    East = 5
}

/// <summary>
/// One face of a model element. <see cref="Texture"/> is the resolved texture path after following <c>#variable</c>
/// indirection; <see cref="Uv"/> is in model texture pixels.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.resources.model.cuboid.CuboidFace", "ref://mc-26.3-analysis/src/net/minecraft/client/resources/model/cuboid/CuboidFace.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public readonly record struct ItemModelFace(
    string? Texture,
    Vector4 Uv,
    int RotationDegrees = 0,
    int TintIndex = -1,
    ModelFaceDirection? CullFace = null);

/// <summary>
/// One cuboid of an item model's <c>elements</c> array, in model pixels (0-16 per block) exactly as written in the JSON.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.resources.model.cuboid.CuboidDefinition", "ref://mc-26.3-analysis/src/net/minecraft/client/resources/model/cuboid/CuboidDefinition.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "from/to plus per-face data; element rotation is not modelled yet")]
public sealed class ItemModelElement
{
    /// <summary>Creates one element from its JSON bounds and resolved faces.</summary>
    public ItemModelElement(Vector3 from, Vector3 to, IReadOnlyDictionary<ModelFaceDirection, ItemModelFace> faces)
    {
        ArgumentNullException.ThrowIfNull(faces);
        From = from;
        To = to;
        Faces = faces;
    }

    /// <summary>Lower corner in model pixels.</summary>
    public Vector3 From { get; }

    /// <summary>Upper corner in model pixels.</summary>
    public Vector3 To { get; }

    /// <summary>Faces that are actually drawn; absent directions are omitted, as in the JSON.</summary>
    public IReadOnlyDictionary<ModelFaceDirection, ItemModelFace> Faces { get; }

    /// <summary>
    /// Reference <c>FaceBakery.defaultFaceUV</c>: the UV a face gets when the JSON omits <c>uv</c>. The element corners
    /// are projected onto the face plane in model pixels (0-16), exactly as the reference does.
    /// </summary>
    public static Vector4 AutoUv(ModelFaceDirection direction, Vector3 from, Vector3 to) => direction switch
    {
        ModelFaceDirection.Down => new Vector4(from.X, 16f - to.Z, to.X, 16f - from.Z),
        ModelFaceDirection.Up => new Vector4(from.X, from.Z, to.X, to.Z),
        ModelFaceDirection.North => new Vector4(16f - to.X, 16f - to.Y, 16f - from.X, 16f - from.Y),
        ModelFaceDirection.South => new Vector4(from.X, 16f - to.Y, to.X, 16f - from.Y),
        ModelFaceDirection.West => new Vector4(from.Z, 16f - to.Y, to.Z, 16f - from.Y),
        ModelFaceDirection.East => new Vector4(16f - to.Z, 16f - to.Y, 16f - from.Z, 16f - from.Y),
        _ => default
    };
}
