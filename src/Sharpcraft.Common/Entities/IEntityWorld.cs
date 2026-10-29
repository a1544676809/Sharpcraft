// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Entities;

/// <summary>
/// What an entity needs from the world: which blocks stop it and how slippery the block below is.
/// Kept as an interface so the entity layer never learns about block registries, and so tests describe
/// terrain with a few lines instead of building a world (docs/modules/entities.md 1).
/// </summary>
public interface IEntityWorld
{
    /// <summary>True when the block at the given coordinates blocks movement.</summary>
    /// <param name="x">Block x.</param>
    /// <param name="y">Block y.</param>
    /// <param name="z">Block z.</param>
    bool IsSolid(int x, int y, int z);

    /// <summary>
    /// Friction of the block at the given coordinates. Vanilla's default block friction is 0.6; ice is
    /// 0.98 and slime blocks 0.8 (docs/modules/entities.md 3).
    /// </summary>
    /// <param name="x">Block x.</param>
    /// <param name="y">Block y.</param>
    /// <param name="z">Block z.</param>
    float Friction(int x, int y, int z);
}

/// <summary>A world where nothing blocks movement; useful as a null object and in tests.</summary>
public sealed class EmptyEntityWorld : IEntityWorld
{
    /// <summary>Shared instance.</summary>
    public static EmptyEntityWorld Instance { get; } = new();

    /// <inheritdoc />
    public bool IsSolid(int x, int y, int z) => false;

    /// <inheritdoc />
    public float Friction(int x, int y, int z) => 0.6f;
}
