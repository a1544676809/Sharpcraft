// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Independent source-formula expectations for banner vertex samples. This helper does NOT call
/// production rendering code: it reconstructs the reference vertex positions directly from the
/// documented source constants and PoseStack order, then applies the raw source-frame scale.
///
/// Derivation (all values read from the 26.3 jar, not from our implementation):
///  - BannerModel.java:28 pole box addBox(-1,-42,-1, 2,42,2); :32 bar box
///    addBox(-10, standing?-44:-20.5, standing?-1:9.5, 20,2,2); both PartPose.ZERO.
///  - BannerFlagModel.java:26 flag box addBox(-10,0,-2, 20,40,1), pose offset (0,-44,0)/(0,-20.5,10.5).
///  - BannerRenderer.java:41 MODEL_SCALE=(2/3,-2/3,-2/3), :42 MODEL_TRANSLATION=(0.5,0,0.5),
///    :239-249 modelTransformation = Transformation(MODEL_TRANSLATION, YP.rotationDegrees(-angle), MODEL_SCALE).
///  - ModelPart.render applies the 1/16 model scale after the outer pose.
///  - ModelPart.Cube DOWN face first vertex is (maxX, minY, maxZ) in model pixels (y-down).
///
/// The vertex our raw mesher emits first for the body is the pole's DOWN-face first vertex
/// (maxX,minY,maxZ) = (1,-42,1) px standing. For the flag it is the flag box's DOWN-face first
/// vertex (maxX,minY,maxZ) = (10,0,-1) px plus the pose offset, then the source idle xRot applied by
/// setupAnim. Cross-check: the standing angle-0 body value equals the banner-vectors.golden.txt
/// 'standing' group's pole bar-corner after the same transform (see BannerSourceFormulaCrossCheck).
/// </summary>
internal static class BannerSourceFormula
{
    public const float Pixel = 1f / 16f;
    public const float Scale = 2f / 3f;

    /// <summary>
    /// Reference modelTransformation apply: scale -> rotateY(-angle) -> translate MODEL_TRANSLATION,
    /// with the 1/16 ModelPart scale folded in first (innermost). Column-vector order.
    /// </summary>
    public static Vector3 Transform(Vector3 modelPixel, float angleDegrees)
    {
        var model = modelPixel * Pixel;
        var s = new Vector3(Scale * model.X, -Scale * model.Y, -Scale * model.Z);
        var radians = -angleDegrees * (MathF.PI / 180f);
        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);
        var rotated = new Vector3(s.X * cos + s.Z * sin, s.Y, -s.X * sin + s.Z * cos);
        return rotated + new Vector3(0.5f, 0f, 0.5f);
    }

    /// <summary>Ground body first vertex: pole DOWN-face corner (1,-42,1) px, rotation = segment*22.5.</summary>
    public static Vector3 GroundBodyVertex(byte segment)
    {
        var angle = segment * 22.5f;
        if (angle >= 180f) angle -= 360f;
        return Transform(new Vector3(1f, -42f, 1f), angle);
    }

    /// <summary>
    /// Flag first vertex: flag box addBox(-10,0,-2,20,40,1) with pose offset (0,-44,0) standing or
    /// (0,-20.5,10.5) wall; DOWN-face corner (maxX,minY,maxZ) relative to the flag pivot is (10,0,-1) px.
    /// setupAnim sets xRot=(-0.0125+0.01*cos(2*pi*phase))*pi about the pose pivot (0,offsetY,offsetZ).
    /// </summary>
    public static Vector3 FlagVertex(bool standing, float phase)
    {
        var pivotY = standing ? -44f : -20.5f;
        var pivotZ = standing ? 0f : 10.5f;
        // corner relative to pivot, then rotate about X by the source idle angle, then add pivot, then outer pose.
        var corner = new Vector3(10f, 0f, -1f);
        var xRot = (-0.0125f + 0.01f * MathF.Cos(2f * MathF.PI * phase)) * MathF.PI;
        var cos = MathF.Cos(xRot);
        var sin = MathF.Sin(xRot);
        var rotated = new Vector3(corner.X, corner.Y * cos - corner.Z * sin, corner.Y * sin + corner.Z * cos);
        var modelPixel = rotated + new Vector3(0f, pivotY, pivotZ);
        var angle = standing ? 0f : 0f; // wall flag sampled at facing south -> toYRot 0
        return Transform(modelPixel, angle);
    }

    /// <summary>Wall body first vertex: bar DOWN-face corner (10,-20.5,10.5) px, rotation = facing.toYRot().</summary>
    public static Vector3 WallBodyVertex(byte facing)
    {
        var angle = facing switch
        {
            2 => 180f, // north  -> toYRot 180
            3 => 0f,   // south
            4 => 90f,  // west
            5 => 270f, // east
            _ => 180f
        };
        // wall bar box addBox(-10,-20.5,9.5, 20,2,2): DOWN-face corner (maxX,minY,maxZ)=(10,-20.5,11.5) px.
        return Transform(new Vector3(10f, -20.5f, 11.5f), angle);
    }
}
