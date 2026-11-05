// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Client.Render;

/// <summary>
/// One entity-level transform from model-local space to world space. The mesher builds vertices around the entity
/// origin and applies this once, instead of adding feet coordinates into every part.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.entity.LivingEntityRenderer", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/entity/LivingEntityRenderer.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Merged, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Entity-level transform chain (scale/rotate/feet offset, docs/specs/render/entity-transform.md)")]
public readonly record struct EntityModelTransform(
    Vector3 Translation,
    float YawDegrees,
    float PitchDegrees,
    float Scale = 1f,
    float ScaleXZ = 1f,
    float ScaleY = 1f,
    bool HasAttachRotation = false,
    Quaternion AttachRotation = default,
    Vector3 AttachPivot = default)
{
    public static EntityModelTransform From(Sharpcraft.Client.World.EntityRenderState state)
    {
        var size = state.CubeSize <= 0 ? 1 : state.CubeSize;
        var squish = state.Squish / ((size * 0.5f) + 1f);
        var width = 1f / (squish + 1f);
        var hasAttach = state.AttachFace >= 0;
        return new EntityModelTransform(
            new Vector3(state.X, state.Y, state.Z),
            // Reference LivingEntityRenderer.submit applies only rotateDegrees(YP, 180 - bodyRot) to the whole entity;
            // body tilt comes from model parts, never from an entity-level pitch (docs/specs/render/entity-transform.md).
            180f - state.Yaw,
            0f,
            ScaleXZ: width * size,
            ScaleY: (1f / width) * size,
            Scale: state.Scale,
            HasAttachRotation: hasAttach,
            AttachRotation: hasAttach ? AttachQuaternion(state.AttachFace) : default,
            AttachPivot: new Vector3(0f, 0.5f, 0f));
    }

    /// <summary>Reference <c>Direction.getRotation()</c> for the opposite of the attach face (JOML rotationXYZ order).</summary>
    private static Quaternion AttachQuaternion(int attachFace)
    {
        var opposite = attachFace switch
        {
            0 => 1, // down -> up
            1 => 0, // up -> down
            2 => 3, // north -> south
            3 => 2, // south -> north
            4 => 5, // west -> east
            5 => 4, // east -> west
            _ => 1
        };

        var (x, y, z) = opposite switch
        {
            0 => (MathF.PI, 0f, 0f),
            1 => (0f, 0f, 0f),
            2 => (MathF.PI / 2f, 0f, MathF.PI),
            3 => (MathF.PI / 2f, 0f, 0f),
            4 => (MathF.PI / 2f, 0f, MathF.PI / 2f),
            _ => (MathF.PI / 2f, 0f, -MathF.PI / 2f)
        };

        var matrix = Matrix4x4.CreateRotationX(x) * Matrix4x4.CreateRotationY(y) * Matrix4x4.CreateRotationZ(z);
        return Quaternion.CreateFromRotationMatrix(matrix);
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

        if (HasAttachRotation)
        {
            var relative = scaled - AttachPivot;
            scaled = AttachPivot + Vector3.Transform(relative, AttachRotation);
        }

        return Translation + scaled;
    }
}
