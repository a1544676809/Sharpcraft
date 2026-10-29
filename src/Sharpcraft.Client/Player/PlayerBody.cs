// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Client.Player;

/// <summary>One tick of intent, produced by the input layer and consumed by the player.</summary>
/// <param name="Forward">−1 backward, +1 forward.</param>
/// <param name="Strafe">−1 left, +1 right.</param>
/// <param name="Jump">True while the jump action is held.</param>
/// <param name="Sprint">True while the sprint action is held.</param>
public readonly record struct MoveIntent(float Forward, float Strafe, bool Jump, bool Sprint)
{
    /// <summary>No input at all.</summary>
    public static MoveIntent None => default;

    /// <summary>Clamps the direction to the unit disc, matching vanilla's diagonal handling.</summary>
    public (float Forward, float Strafe) Normalized()
    {
        var length = MathF.Sqrt((Forward * Forward) + (Strafe * Strafe));
        return length > 1f ? (Forward / length, Strafe / length) : (Forward, Strafe);
    }
}

/// <summary>
/// Player motion: AABB, gravity, jump and per-axis terrain collision
/// (docs/modules/player.md 3-5). The magnitudes are vanilla-scale but not yet verified against a
/// reference run, so tests assert ranges and relations rather than exact digits.
/// </summary>
public sealed class PlayerBody
{
    /// <summary>Gravity per tick squared.</summary>
    public const float Gravity = 0.08f;

    /// <summary>Vertical drag applied every tick.</summary>
    public const float VerticalDrag = 0.98f;

    /// <summary>Horizontal damping once the player is on the ground.</summary>
    public const float GroundFriction = 0.91f;

    /// <summary>Walk speed in blocks per tick.</summary>
    public const float WalkSpeed = 0.1f;

    /// <summary>Sprint multiplier on the walk speed.</summary>
    public const float SprintMultiplier = 1.3f;

    /// <summary>Initial upward velocity of a jump.</summary>
    public const float JumpVelocity = 0.42f;

    /// <summary>Terminal vertical speed.</summary>
    public const float TerminalVelocity = 3.92f;

    /// <summary>Player width and depth.</summary>
    public const float Width = 0.6f;

    /// <summary>Player height.</summary>
    public const float Height = 1.8f;

    /// <summary>Eye height above the feet.</summary>
    public const float EyeHeight = 1.62f;

    private readonly Func<float, float, float, bool> _isSolid;

    /// <summary>Creates a body at a position; the solidity probe decides what blocks movement.</summary>
    /// <param name="feet">Feet position in world space.</param>
    /// <param name="isSolid">Probe: block coordinates → solid?</param>
    public PlayerBody(Vector3 feet, Func<float, float, float, bool> isSolid)
    {
        ArgumentNullException.ThrowIfNull(isSolid);
        Feet = feet;
        _isSolid = isSolid;
    }

    /// <summary>Feet position.</summary>
    public Vector3 Feet { get; private set; }

    /// <summary>Eye position, used as the camera anchor.</summary>
    public Vector3 Eye => Feet + new Vector3(0f, EyeHeight, 0f);

    /// <summary>Velocity in blocks per tick.</summary>
    public Vector3 Velocity { get; private set; }

    /// <summary>True when the body is standing on solid ground.</summary>
    public bool OnGround { get; private set; }

    /// <summary>
    /// Advances the body one tick. <paramref name="yaw"/> is the horizontal facing in radians, where 0
    /// looks towards −Z (vanilla convention).
    /// </summary>
    /// <param name="intent">Movement intent for this tick.</param>
    /// <param name="yaw">Facing, in radians.</param>
    public void Tick(MoveIntent intent, float yaw)
    {
        var (forward, strafe) = intent.Normalized();
        var speed = intent.Sprint ? WalkSpeed * SprintMultiplier : WalkSpeed;

        // Rotate the intent into world space: forward is (−sin yaw, 0, −cos yaw), right is (cos, 0, −sin).
        var sin = MathF.Sin(yaw);
        var cos = MathF.Cos(yaw);
        var desiredX = ((forward * -sin) + (strafe * cos)) * speed;
        var desiredZ = ((forward * -cos) + (strafe * -sin)) * speed;

        var velocity = OnGround
            ? new Vector3(desiredX, Velocity.Y, desiredZ)
            : new Vector3(
                Velocity.X + ((desiredX - Velocity.X) * 0.02f),
                Velocity.Y,
                Velocity.Z + ((desiredZ - Velocity.Z) * 0.02f));

        if (intent.Jump && OnGround)
        {
            velocity = velocity with { Y = JumpVelocity };
            OnGround = false;
        }

        // Vanilla order: move with the current velocity, then integrate gravity and drag. Doing it the
        // other way round shortens every jump by about a third (measured: 0.91 vs 1.25 blocks).
        Velocity = velocity;
        Move();

        Velocity = Velocity with { Y = (Velocity.Y - Gravity) * VerticalDrag };
        if (Velocity.Y < -TerminalVelocity)
        {
            Velocity = Velocity with { Y = -TerminalVelocity };
        }

        if (OnGround)
        {
            Velocity = new Vector3(Velocity.X * GroundFriction, Velocity.Y, Velocity.Z * GroundFriction);
        }
    }

    /// <summary>
    /// Moves along each axis in turn, clamping to the contact surface instead of dropping the whole step:
    /// rejecting the step leaves the body hovering a fraction above the floor.
    /// </summary>
    private void Move()
    {
        OnGround = false;

        // Vertical first: landing has to be known before horizontal friction is applied.
        if (Velocity.Y != 0f)
        {
            if (TryMoveAxis(axisY: true, Velocity.Y))
            {
                // Free to move.
            }
            else if (Velocity.Y < 0f)
            {
                OnGround = true;
                Velocity = Velocity with { Y = 0f };
            }
            else
            {
                Velocity = Velocity with { Y = 0f };
            }
        }
        else if (TouchingGround())
        {
            OnGround = true;
        }

        if (Velocity.X != 0f && !TryMoveAxis(axisY: false, Velocity.X, axisX: true))
        {
            Velocity = Velocity with { X = 0f };
        }

        if (Velocity.Z != 0f && !TryMoveAxis(axisY: false, Velocity.Z, axisZ: true))
        {
            Velocity = Velocity with { Z = 0f };
        }
    }

    private bool TryMoveAxis(bool axisY, float delta, bool axisX = false, bool axisZ = false)
    {
        var steps = (int)MathF.Ceiling(MathF.Abs(delta) / 0.5f);
        steps = Math.Max(1, steps);
        var step = delta / steps;

        for (var index = 0; index < steps; index++)
        {
            var candidate = axisY
                ? Feet + new Vector3(0f, step, 0f)
                : axisX
                    ? Feet + new Vector3(step, 0f, 0f)
                    : Feet + new Vector3(0f, 0f, step);

            if (!Intersects(candidate))
            {
                Feet = candidate;
                continue;
            }

            // Clamp to the surface we just hit so the body rests exactly on it.
            Feet = ClampToSurface(candidate, step, axisY, axisX, axisZ);
            return false;
        }

        return true;
    }

    private Vector3 ClampToSurface(Vector3 candidate, float step, bool axisY, bool axisX, bool axisZ)
    {
        if (axisY)
        {
            return step < 0f
                ? Feet with { Y = MathF.Floor(candidate.Y) + 1f }
                : Feet with { Y = MathF.Floor(candidate.Y + Height) - Height };
        }

        const float half = Width / 2f;
        if (axisX)
        {
            return step > 0f
                ? Feet with { X = MathF.Floor(candidate.X + half) - half }
                : Feet with { X = MathF.Floor(candidate.X - half) + 1f + half };
        }

        return step > 0f
            ? Feet with { Z = MathF.Floor(candidate.Z + half) - half }
            : Feet with { Z = MathF.Floor(candidate.Z - half) + 1f + half };
    }

    private bool TouchingGround() => Intersects(Feet + new Vector3(0f, -0.001f, 0f));

    private bool Intersects(Vector3 feet)
    {
        const float half = Width / 2f;
        var minX = feet.X - half;
        var maxX = feet.X + half;
        var minY = feet.Y;
        var maxY = feet.Y + Height;
        var minZ = feet.Z - half;
        var maxZ = feet.Z + half;

        var fromX = (int)MathF.Floor(minX);
        var toX = (int)MathF.Floor(maxX - 1e-4f);
        var fromY = (int)MathF.Floor(minY);
        var toY = (int)MathF.Floor(maxY - 1e-4f);
        var fromZ = (int)MathF.Floor(minZ);
        var toZ = (int)MathF.Floor(maxZ - 1e-4f);

        for (var x = fromX; x <= toX; x++)
        {
            for (var y = fromY; y <= toY; y++)
            {
                for (var z = fromZ; z <= toZ; z++)
                {
                    if (_isSolid(x, y, z))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
}
