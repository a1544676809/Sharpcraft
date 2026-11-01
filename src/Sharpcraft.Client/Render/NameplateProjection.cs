// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Projects an entity nameplate anchor to screen space. This module only supplies the projection; the UI owns text layout
/// and drawing (docs/specs/render/pipeline.md §6).
/// </summary>
public static class NameplateProjection
{
    /// <summary>Vertical offset above the entity's eye/head used by the reference nameplate anchor.</summary>
    public const float HeadOffset = 0.6f;

    /// <summary>
    /// Projects the nameplate anchor above an entity to normalized screen coordinates.
    /// </summary>
    /// <param name="feet">Entity feet position in world space.</param>
    /// <param name="height">Entity height in blocks.</param>
    /// <param name="viewProjection">Combined camera view-projection.</param>
    /// <param name="screen">Normalized screen position, origin top-left.</param>
    /// <param name="depth">Normalized device depth for sorting/occlusion checks.</param>
    /// <returns>False when the anchor is behind the camera.</returns>
    public static bool TryProject(
        Vector3 feet,
        float height,
        Matrix4x4 viewProjection,
        out Vector2 screen,
        out float depth)
    {
        var anchor = feet + new Vector3(0f, height + HeadOffset, 0f);
        var clip = Vector4.Transform(new Vector4(anchor, 1f), viewProjection);
        if (clip.W <= 1e-6f)
        {
            screen = Vector2.NaN;
            depth = float.NaN;
            return false;
        }

        screen = new Vector2(
            ((clip.X / clip.W) * 0.5f) + 0.5f,
            (0.5f - ((clip.Y / clip.W) * 0.5f)));
        depth = clip.Z / clip.W;
        return true;
    }
}
