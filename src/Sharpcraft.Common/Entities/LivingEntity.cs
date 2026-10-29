// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Common.Entities;

/// <summary>
/// A living entity: adds gravity, jump, block friction and the two air drags on top of
/// <see cref="Entity"/>. The order and the constants are taken from vanilla's
/// <c>LivingEntity.travelInAir</c> and <c>handleRelativeFrictionAndCalculateMovement</c>
/// (docs/modules/entities.md 3).
/// </summary>
public abstract class LivingEntity : Entity
{
    /// <summary>Gravity per tick squared (vanilla <c>DEFAULT_BASE_GRAVITY</c>).</summary>
    public const float BaseGravity = 0.08f;

    /// <summary>Input friction applied to the movement impulse every tick (vanilla <c>INPUT_FRICTION</c>).</summary>
    public const float InputFriction = 0.98f;

    /// <summary>Horizontal push along the facing applied when jumping while sprinting.</summary>
    public const float SprintJumpBoost = 0.2f;

    /// <summary>Upward speed applied by a jump (vanilla <c>BASE_JUMP_POWER</c>).</summary>
    public const float BaseJumpPower = 0.42f;

    /// <summary>Horizontal drag applied every tick (vanilla <c>BASE_HORIZONTAL_AIR_DRAG</c>).</summary>
    public const float HorizontalDrag = 0.91f;

    /// <summary>Vertical drag applied every tick (vanilla <c>BASE_VERTICAL_AIR_DRAG</c>).</summary>
    public const float VerticalDrag = 0.98f;

    /// <summary>Terminal falling speed.</summary>
    public const float TerminalVelocity = 3.92f;

    /// <summary>Default block friction when the world reports nothing special.</summary>
    public const float DefaultBlockFriction = 0.6f;

    /// <summary>Creates a living entity.</summary>
    /// <param name="world">Collision and friction source.</param>
    /// <param name="feet">Initial feet position.</param>
    protected LivingEntity(IEntityWorld world, Vector3 feet)
        : base(world, feet)
    {
    }

    /// <summary>Movement speed attribute, in blocks per tick at full impulse.</summary>
    public abstract float MovementSpeed { get; }

    /// <summary>Movement impulse used while airborne (vanilla <c>getFlyingSpeed</c>).</summary>
    protected virtual float AirImpulse => 0.02f;

    /// <summary>True when the entity is sprinting; drives the air impulse and the sprint jump.</summary>
    protected virtual bool SprintingNow => false;

    /// <summary>True while the entity wants to jump.</summary>
    protected bool Jumping { get; set; }

    /// <summary>
    /// Advances one tick. <paramref name="input"/> is the movement wish in entity space, where
    /// <c>Z = −1</c> means forward at yaw 0; <paramref name="yaw"/> is in radians.
    /// </summary>
    /// <param name="input">Movement wish, each component in −1..1.</param>
    /// <param name="yaw">Facing in radians; 0 looks towards −Z.</param>
    /// <param name="jumping">True while the jump action is held.</param>
    public void Travel(Vector3 input, float yaw, bool jumping)
    {
        // 1) Jump is applied before the move so the first tick already rises.
        if (jumping && OnGround)
        {
            // Vanilla takes the larger of the jump power and the current vertical speed, so a jump cannot
            // slow an entity that is already rising faster (a bounce, a piston launch)
            // (docs/specs/network/play-movement.md 7.15).
            SetDeltaMovement(new Vector3(
                DeltaMovement.X,
                MathF.Max(BaseJumpPower, DeltaMovement.Y),
                DeltaMovement.Z));

            // A sprint jump also pushes along the facing, which is what makes bunny hopping faster than
            // walking instead of slower (docs/specs/network/play-movement.md 7.21).
            if (SprintingNow)
            {
                AddDeltaMovement(new Vector3(MathF.Sin(yaw) * SprintJumpBoost, 0f, -MathF.Cos(yaw) * SprintJumpBoost));
            }
        }

        // 2) Block friction only matters while standing on something; in the air vanilla uses 1.0.
        var blockFriction = OnGround ? FrictionBelow() : 1f;

        // 3) The impulse is scaled by the friction-influenced speed: on slippery blocks the entity
        //    accelerates less per tick but ends up faster, which is the whole point of the cubic term.
        var impulseSpeed = ImpulseSpeed(blockFriction);
        MoveRelative(impulseSpeed, input, yaw);

        // 4) Move first, then integrate: vanilla resolves the displacement using the velocity as it is.
        Move(DeltaMovement);

        // 5) Gravity and drag, applied to the post-move velocity.
        var movement = DeltaMovement;
        var vertical = (movement.Y - BaseGravity) * VerticalDrag;
        if (vertical < -TerminalVelocity)
        {
            vertical = -TerminalVelocity;
        }

        var horizontalScale = blockFriction * HorizontalDrag;
        SetDeltaMovement(new Vector3(movement.X * horizontalScale, vertical, movement.Z * horizontalScale));
    }

    /// <summary>
    /// Block friction of the block below the feet, using the standard friction modifier of 1.
    /// </summary>
    protected float FrictionBelow()
    {
        var below = Position + new Vector3(0f, -0.2f, 0f);
        return World.Friction((int)MathF.Floor(below.X), (int)MathF.Floor(below.Y), (int)MathF.Floor(below.Z));
    }

    /// <summary>
    /// Speed the movement impulse is multiplied by. On the ground this is the movement speed, reduced on
    /// slippery blocks by <c>0.21600002 / friction³</c> (that constant is <c>0.6³</c>); in the air it is a
    /// tenth of the speed.
    /// </summary>
    /// <param name="blockFriction">Friction of the block below, or 1 in the air.</param>
    protected virtual float ImpulseSpeed(float blockFriction)
    {
        if (!OnGround)
        {
            // Vanilla applies a flat air impulse: 0.02, or 0.026 while sprinting. It is not scaled by the
            // movement speed attribute, which is why our first attempt (speed times a tenth) decayed toward
            // walking speed mid-jump (docs/specs/network/play-movement.md 7.21).
            return AirImpulse;
        }

        return blockFriction > DefaultBlockFriction
            ? MovementSpeed * (0.21600002f / (blockFriction * blockFriction * blockFriction))
            : MovementSpeed;
    }

    /// <summary>
    /// Adds the rotated impulse to the velocity, matching vanilla's <c>moveRelative</c>: the input is
    /// normalised only when longer than a unit, then rotated by yaw.
    /// </summary>
    protected void MoveRelative(float speed, Vector3 input, float yaw)
    {
        // The impulse is damped by INPUT_FRICTION before it is applied. This is the factor that makes the
        // documented walking speed come out right: 0.1 * 0.98 / (1 − 0.546) = 4.317 blocks per second.
        var lengthSquared = input.LengthSquared();
        if (lengthSquared < 1e-7f)
        {
            return;
        }

        var scaled = (lengthSquared > 1f ? Vector3.Normalize(input) : input) * (speed * InputFriction);
        var sin = MathF.Sin(yaw);
        var cos = MathF.Cos(yaw);

        AddDeltaMovement(new Vector3(
            (scaled.X * cos) - (scaled.Z * sin),
            scaled.Y,
            (scaled.Z * cos) + (scaled.X * sin)));
    }
}
