// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Client.Render;

/// <summary>
/// TextureTransform.setupGlintTexturing: M = T(-o0, o1, 0) * Rz(pi/18) * S(scale), with
/// o0 = millis % 110000 / 110000, o1 = millis % 30000 / 30000. The CPU applies it to layer UVs once per frame;
/// the geometry is unchanged and the result is wrapped exactly like the reference glint shader.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.rendertype.TextureTransform", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/rendertype/TextureTransform.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Full)]
public static class GlintTextureTransform
{
    /// <summary>Item glint scale.</summary>
    public const float ItemScale = 8f;

    /// <summary>Entity glint scale.</summary>
    public const float EntityScale = 0.5f;

    /// <summary>Armor glint scale.</summary>
    public const float ArmorScale = 0.16f;

    private static readonly float Cos = MathF.Cos(MathF.PI / 18f);
    private static readonly float Sin = MathF.Sin(MathF.PI / 18f);

    /// <summary>Source offset pair for a millis value, preserving Java remainder signs for negative millis.</summary>
    public static (float U, float V) Offsets(long millis)
    {
        var offsetU = (millis % 110000L) / 110000f;
        var offsetV = (millis % 30000L) / 30000f;
        return (offsetU, offsetV);
    }

    /// <summary>Applies the source glint texture matrix to one UV and wraps to [0, 1).</summary>
    public static (float U, float V) Apply(float u, float v, long millis, float scale)
    {
        var (offsetU, offsetV) = Offsets(millis);
        var localU = u - MathF.Floor(u);
        var localV = v - MathF.Floor(v);
        var rotatedU = (Cos * localU) - (Sin * localV);
        var rotatedV = (Sin * localU) + (Cos * localV);
        var glintU = (rotatedU * scale) - offsetU;
        var glintV = (rotatedV * scale) + offsetV;
        return (Fract(glintU), Fract(glintV));
    }

    /// <summary>Applies the transform with the row-vector matrix and returns the wrapped UV.</summary>
    public static Vector2 Apply(Vector2 uv, long millis, float scale) =>
        new(Apply(uv.X, uv.Y, millis, scale).U, Apply(uv.X, uv.Y, millis, scale).V);

    private static float Fract(float value) => value - MathF.Floor(value);
}
