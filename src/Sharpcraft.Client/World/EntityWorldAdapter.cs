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
/// Terrain that has not been downloaded reads as air, matching the vanilla client, whose block lookup falls
/// back to void air for a missing chunk. Vanilla never relies on that fallback to hold a player in place:
/// the server keeps the chunks around every player loaded and streams them, so the client's view is always
/// populated ahead of the player. Treating unknown terrain as solid instead would build an invisible wall at
/// the edge of whatever happens to be downloaded (docs/specs/network/play-movement.md 7.12).
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
    public bool IsSolid(int x, int y, int z) => _world.GetBlockState(x, y, z) != 0;

    /// <inheritdoc />
    public float Friction(int x, int y, int z) => DefaultFriction;
}
