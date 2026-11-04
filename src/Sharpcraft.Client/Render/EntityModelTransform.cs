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
    float Scale = 1f,
    float ScaleXZ = 1f,
    float ScaleY = 1f)
{
    public static EntityModelTransform From(Sharpcraft.Client.World.EntityRenderState state)
    {
        var size = state.CubeSize <= 0 ? 1 : state.CubeSize;
        var squish = state.Squish / ((size * 0.5f) + 1f);
        var width = 1f / (squish + 1f);
        return new EntityModelTransform(
            new Vector3(state.X, state.Y, state.Z),
            state.Yaw,
            state.Pitch,
            ScaleXZ: width * size,
            ScaleY: (1f / width) * size);
    }

    public Vector3 Apply(Vector3 modelPosition)
    {
        var scaled = new Vector3(
            modelPosition.X * Scale * ScaleXZ,
            modelPosition.Y * Scale * ScaleY,
            modelPosition.Z * Scale * ScaleXZ);
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
