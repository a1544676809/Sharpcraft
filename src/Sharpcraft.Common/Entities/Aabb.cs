// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Common.Entities;

/// <summary>
/// Axis-aligned bounding box in world space. Entity boxes are built from the feet position, so the box
/// spans <c>y … y + height</c> (docs/modules/entities.md 3).
/// </summary>
/// <param name="Min">Corner with the smallest coordinates.</param>
/// <param name="Max">Corner with the largest coordinates.</param>
public readonly record struct Aabb(Vector3 Min, Vector3 Max)
{
    /// <summary>True when the two boxes overlap on every axis.</summary>
    public bool Intersects(in Aabb other) =>
        Min.X < other.Max.X && Max.X > other.Min.X &&
        Min.Y < other.Max.Y && Max.Y > other.Min.Y &&
        Min.Z < other.Max.Z && Max.Z > other.Min.Z;

    /// <summary>Returns the box moved by a delta.</summary>
    public Aabb Offset(Vector3 delta) => new(Min + delta, Max + delta);

    /// <summary>Returns the smallest box containing both.</summary>
    public Aabb Union(in Aabb other) => new(Vector3.Min(Min, other.Min), Vector3.Max(Max, other.Max));

    /// <summary>Size on each axis.</summary>
    public Vector3 Size => Max - Min;
}
