// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.World;
using Sharpcraft.Common.Entities;

namespace Sharpcraft.Client.World;

/// <summary>
/// Adapts <see cref="ClientWorld"/> to what the entity layer needs: which blocks stop movement and how
/// slippery the ground is. Air is state 0 in this world, so solidity is "not air" for now; per-block
/// friction arrives with the block registry (docs/modules/entities.md 1).
///
/// Terrain that has not been downloaded is treated as solid. Reading a missing chunk as air lets the
/// entity walk off the loaded region into space the server does have blocks in, and the server then sees a
/// position whose bounding box overlaps blocks it knows about and pulls the player back
/// (docs/specs/network/play-movement.md 7.10).
/// </summary>
public sealed class EntityWorldAdapter : IEntityWorld
{
    /// <summary>Block friction used until the registry can answer per block.</summary>
    public const float DefaultFriction = 0.6f;

    private readonly ClientWorld _world;

    /// <summary>Wraps a client world.</summary>
    /// <param name="world">World to read blocks from.</param>
    public EntityWorldAdapter(ClientWorld world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
    }

    /// <inheritdoc />
    public bool IsSolid(int x, int y, int z)
    {
        // Unknown terrain blocks movement instead of reading as air; see the class remarks.
        if (!_world.IsChunkLoaded(x, z))
        {
            return true;
        }

        return _world.GetBlockState(x, y, z) != 0;
    }

    /// <inheritdoc />
    public float Friction(int x, int y, int z) => DefaultFriction;
}
