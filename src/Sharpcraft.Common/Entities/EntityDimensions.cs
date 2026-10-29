// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Common.Entities;

/// <summary>
/// Collision box of an entity type. Mirrors vanilla's record: width, height, eye height, and a note on
/// whether a pose may change it later (docs/modules/entities.md 3).
/// </summary>
/// <param name="Width">Full width and depth.</param>
/// <param name="Height">Full height.</param>
/// <param name="EyeHeight">Eye offset above the feet.</param>
public readonly record struct EntityDimensions(float Width, float Height, float EyeHeight)
{
    /// <summary>Vanilla derives the eye height from the body height when a type does not override it.</summary>
    /// <param name="width">Full width and depth.</param>
    /// <param name="height">Full height.</param>
    public static EntityDimensions WithDefaultEyeHeight(float width, float height) =>
        new(width, height, height * 0.85f);

    /// <summary>Player dimensions: 0.6 wide, 1.8 tall, eyes at 1.62 (explicitly set, not derived).</summary>
    public static EntityDimensions Player => new(0.6f, 1.8f, 1.62f);

    /// <summary>Builds the box for a feet position, exactly as vanilla's <c>makeBoundingBox</c> does.</summary>
    public Aabb MakeBoundingBox(Vector3 feet)
    {
        var half = Width / 2f;
        return new Aabb(
            new Vector3(feet.X - half, feet.Y, feet.Z - half),
            new Vector3(feet.X + half, feet.Y + Height, feet.Z + half));
    }
}
