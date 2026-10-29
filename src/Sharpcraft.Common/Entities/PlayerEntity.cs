// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Common.Entities;

/// <summary>
/// The player entity: vanilla dimensions and movement speed, plus the mapping from an input intent to the
/// travel input vector (docs/modules/entities.md 5).
/// </summary>
public sealed class PlayerEntity : LivingEntity
{
    /// <summary>Player movement speed attribute value.</summary>
    public const float PlayerMovementSpeed = 0.1f;

    /// <summary>Sprint multiplier on the movement speed.</summary>
    public const float SprintMultiplier = 1.3f;

    /// <summary>Creates a player at a feet position.</summary>
    /// <param name="world">Collision and friction source.</param>
    /// <param name="feet">Initial feet position.</param>
    public PlayerEntity(IEntityWorld world, Vector3 feet)
        : base(world, feet)
    {
    }

    /// <inheritdoc />
    public override EntityDimensions Dimensions => EntityDimensions.Player;

    /// <inheritdoc />
    public override float MovementSpeed => PlayerMovementSpeed * (Sprinting ? SprintMultiplier : 1f);

    /// <summary>True while the sprint action is held.</summary>
    public bool Sprinting { get; set; }

    /// <summary>
    /// Advances the player one tick from an input intent.
    /// </summary>
    /// <param name="forward">−1 backward … +1 forward.</param>
    /// <param name="strafe">−1 left … +1 right.</param>
    /// <param name="jumping">True while jump is held.</param>
    /// <param name="sprinting">True while sprint is held.</param>
    /// <param name="yaw">Facing in radians; 0 looks towards −Z.</param>
    public void Tick(float forward, float strafe, bool jumping, bool sprinting, float yaw)
    {
        Sprinting = sprinting;
        Jumping = jumping;

        // Vanilla's input vector uses −Z for forward, which is also the yaw-0 facing direction.
        Travel(new Vector3(strafe, 0f, -forward), yaw, jumping);
    }
}
