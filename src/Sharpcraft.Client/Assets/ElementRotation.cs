// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Common.Reference;

namespace Sharpcraft.Client.Assets;

/// <summary>
/// Turns a point about an element's origin. With <c>rescale</c> the result is stretched back so the
/// element keeps its size instead of shrinking as it turns, matching the reference's rule: every
/// axis is scaled by the inverse of the largest component of that axis after rotation.
/// </summary>
[ReferenceClass("net.minecraft.client.resources.model.cuboid.CuboidRotation", "ref://mc-26.3-analysis/src/net/minecraft/client/resources/model/cuboid/CuboidRotation.java")]
public static class ElementRotation
{
    /// <summary>Turns a point about an origin.</summary>
    /// <param name="position">Point to turn, in block units.</param>
    /// <param name="origin">Centre of rotation, in block units.</param>
    /// <param name="axis">Rotation axis: "x", "y" or "z".</param>
    /// <param name="angleDegrees">Angle in degrees.</param>
    /// <param name="rescale">Whether to undo the shrinking the turn introduces.</param>
    public static Vector3 Apply(Vector3 position, Vector3 origin, string axis, float angleDegrees, bool rescale)
    {
        var radians = angleDegrees * (MathF.PI / 180f);
        var unit = axis switch
        {
            "x" => Vector3.UnitX,
            "z" => Vector3.UnitZ,
            _ => Vector3.UnitY
        };

        // The reference builds its matrix with JOML, whose positive angle turns the opposite way
        // from System.Numerics, so negate here rather than mirroring every caller.
        var rotation = Quaternion.CreateFromAxisAngle(unit, -radians);
        var offset = Vector3.Transform(position - origin, rotation);

        if (rescale)
        {
            offset = new Vector3(
                offset.X * ScaleFactor(rotation, Vector3.UnitX),
                offset.Y * ScaleFactor(rotation, Vector3.UnitY),
                offset.Z * ScaleFactor(rotation, Vector3.UnitZ));
        }

        return origin + offset;
    }

    private static float ScaleFactor(Quaternion rotation, Vector3 axis)
    {
        var turned = Vector3.Transform(axis, rotation);
        var max = MathF.Max(MathF.Max(MathF.Abs(turned.X), MathF.Abs(turned.Y)), MathF.Abs(turned.Z));
        return 1f / max;
    }
}
