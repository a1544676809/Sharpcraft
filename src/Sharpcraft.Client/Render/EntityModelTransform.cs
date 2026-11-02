// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Client.Render;

/// <summary>
/// One entity-level transform from model-local space to world space. The mesher builds vertices around the entity
/// origin and applies this once, instead of adding feet coordinates into every part.
/// </summary>
public readonly record struct EntityModelTransform(
    Vector3 Translation,
    float YawDegrees,
    float PitchDegrees,
    float Scale = 1f)
{
    public static EntityModelTransform From(Sharpcraft.Client.World.EntityRenderState state) =>
        new(new Vector3(state.X, state.Y, state.Z), state.Yaw, state.Pitch);

    public Vector3 Apply(Vector3 modelPosition)
    {
        var scaled = modelPosition * Scale;
        if (YawDegrees != 0f)
        {
            scaled = Vector3.Transform(scaled, Matrix4x4.CreateRotationY(YawDegrees * (MathF.PI / 180f)));
        }

        if (PitchDegrees != 0f)
        {
            scaled = Vector3.Transform(scaled, Matrix4x4.CreateRotationX(PitchDegrees * (MathF.PI / 180f)));
        }

        return Translation + scaled;
    }
}
