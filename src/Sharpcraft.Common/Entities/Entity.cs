// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Common.Entities;

/// <summary>
/// Base entity: position, velocity, collision box and the per-axis move that resolves terrain.
/// The movement order and the collision flags follow vanilla's <c>Entity.move</c>
/// (docs/modules/entities.md 4).
/// </summary>
public abstract class Entity
{
    /// <summary>Largest per-axis step before a move is split, so fast entities cannot tunnel.</summary>
    public const float MaxAxisStep = 0.5f;

    /// <summary>Creates an entity at a feet position.</summary>
    /// <param name="world">World the entity collides against.</param>
    /// <param name="feet">Initial feet position.</param>
    protected Entity(IEntityWorld world, Vector3 feet)
    {
        World = world ?? throw new ArgumentNullException(nameof(world));
        Position = feet;
    }

    /// <summary>World used for collisions and friction.</summary>
    public IEntityWorld World { get; }

    /// <summary>Feet position.</summary>
    public Vector3 Position { get; protected set; }

    /// <summary>Velocity in blocks per tick, as vanilla's <c>deltaMovement</c>.</summary>
    public Vector3 DeltaMovement { get; protected set; }

    /// <summary>Bounding box of this entity at its current position.</summary>
    public Aabb BoundingBox => Dimensions.MakeBoundingBox(Position);

    /// <summary>Eye position; the camera anchor.</summary>
    public Vector3 EyePosition => Position + new Vector3(0f, Dimensions.EyeHeight, 0f);

    /// <summary>Dimensions, which subclasses fix for their type.</summary>
    public abstract EntityDimensions Dimensions { get; }

    /// <summary>True when the entity rests on a solid block.</summary>
    public bool OnGround { get; private set; }

    /// <summary>True when the last move was stopped on a horizontal axis.</summary>
    public bool HorizontalCollision { get; private set; }

    /// <summary>True when the last move was stopped on the vertical axis.</summary>
    public bool VerticalCollision { get; private set; }

    /// <summary>Height the entity can step up without jumping.</summary>
    public virtual float StepHeight => 0.6f;

    /// <summary>Replaces the velocity.</summary>
    /// <param name="deltaMovement">New velocity in blocks per tick.</param>
    protected void SetDeltaMovement(Vector3 deltaMovement) => DeltaMovement = deltaMovement;

    /// <summary>Adds to the velocity.</summary>
    /// <param name="delta">Velocity change in blocks per tick.</param>
    protected void AddDeltaMovement(Vector3 delta) => DeltaMovement += delta;

    /// <summary>
    /// Applies a displacement with per-axis collision resolution: Y first, then X, then Z. A blocked axis
    /// clears only that axis and clamps the position to the contact surface; the other axes still apply
    /// (docs/modules/entities.md 4).
    /// </summary>
    /// <param name="delta">Requested displacement in blocks.</param>
    public void Move(Vector3 delta)
    {
        HorizontalCollision = false;
        VerticalCollision = false;
        OnGround = false;

        if (TryMoveAxis(new Vector3(0f, delta.Y, 0f), axis: Axis.Y))
        {
            // Moved freely; landing is decided by the ground probe below.
        }
        else
        {
            VerticalCollision = true;
            if (delta.Y < 0f)
            {
                OnGround = true;
            }

            DeltaMovement = DeltaMovement with { Y = 0f };
        }

        if (!TryMoveAxis(new Vector3(delta.X, 0f, 0f), axis: Axis.X))
        {
            HorizontalCollision = true;
            DeltaMovement = DeltaMovement with { X = 0f };
        }

        if (!TryMoveAxis(new Vector3(0f, 0f, delta.Z), axis: Axis.Z))
        {
            HorizontalCollision = true;
            DeltaMovement = DeltaMovement with { Z = 0f };
        }

        if (!OnGround && TouchesGround())
        {
            OnGround = true;
        }
    }

    /// <summary>Moves the entity to a position, ignoring terrain; used by the simulation entry points.</summary>
    /// <param name="feet">New feet position.</param>
    public void SetPosition(Vector3 feet) => Position = feet;

    /// <summary>True when the block below the feet is solid.</summary>
    protected bool TouchesGround()
    {
        var box = Dimensions.MakeBoundingBox(Position + new Vector3(0f, -0.001f, 0f));
        return IntersectsWorld(box);
    }

    private enum Axis
    {
        X,
        Y,
        Z
    }

    private bool TryMoveAxis(Vector3 delta, Axis axis)
    {
        var component = axis switch { Axis.X => delta.X, Axis.Y => delta.Y, _ => delta.Z };
        if (component == 0f)
        {
            return true;
        }

        var steps = Math.Max(1, (int)MathF.Ceiling(MathF.Abs(component) / MaxAxisStep));
        var step = delta / steps;

        for (var index = 0; index < steps; index++)
        {
            var candidate = Position + step;
            if (IntersectsWorld(Dimensions.MakeBoundingBox(candidate)))
            {
                Position = ClampToContactSurface(candidate, component, axis);
                return false;
            }

            Position = candidate;
        }

        return true;
    }

    private Vector3 ClampToContactSurface(Vector3 candidate, float component, Axis axis)
    {
        var dimensions = Dimensions;
        var half = dimensions.Width / 2f;

        return axis switch
        {
            Axis.Y => component < 0f
                ? Position with { Y = MathF.Floor(candidate.Y) + 1f }
                : Position with { Y = MathF.Floor(candidate.Y + dimensions.Height) - dimensions.Height },
            Axis.X => component > 0f
                ? Position with { X = MathF.Floor(candidate.X + half) - half }
                : Position with { X = MathF.Floor(candidate.X - half) + 1f + half },
            _ => component > 0f
                ? Position with { Z = MathF.Floor(candidate.Z + half) - half }
                : Position with { Z = MathF.Floor(candidate.Z - half) + 1f + half }
        };
    }

    private bool IntersectsWorld(Aabb box)
    {
        var fromX = (int)MathF.Floor(box.Min.X);
        var toX = (int)MathF.Floor(box.Max.X - 1e-4f);
        var fromY = (int)MathF.Floor(box.Min.Y);
        var toY = (int)MathF.Floor(box.Max.Y - 1e-4f);
        var fromZ = (int)MathF.Floor(box.Min.Z);
        var toZ = (int)MathF.Floor(box.Max.Z - 1e-4f);

        for (var x = fromX; x <= toX; x++)
        {
            for (var y = fromY; y <= toY; y++)
            {
                for (var z = fromZ; z <= toZ; z++)
                {
                    if (World.IsSolid(x, y, z))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
}
