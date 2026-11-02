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
    /// <summary>Vertical offset above the entity's head used by the reference nameplate anchor.</summary>
    public const float HeadOffset = 0.5f;

    /// <summary>The reference nameplate text scale in blocks per font pixel.</summary>
    public const float WorldTextScale = 0.025f;

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
    /// <summary>
    /// Projects a camera-facing text plane at the nameplate anchor and returns its normalized screen size.
    /// The plane uses the reference <see cref="WorldTextScale"/> so the text keeps the vanilla world size.
    /// </summary>
    public static bool TryProjectBillboard(
        Vector3 feet,
        float height,
        float textWidthPixels,
        float textHeightPixels,
        Vector3 eye,
        Vector3 target,
        Matrix4x4 viewProjection,
        out Vector2 screen,
        out float screenWidth,
        out float screenHeight)
    {
        var anchor = feet + new Vector3(0f, height + HeadOffset, 0f);
        var forward = target - eye;
        if (forward.LengthSquared() < 1e-8f)
        {
            forward = -Vector3.UnitZ;
        }

        forward = Vector3.Normalize(forward);
        var right = Vector3.Cross(forward, Vector3.UnitY);
        if (right.LengthSquared() < 1e-8f)
        {
            right = Vector3.UnitX;
        }

        right = Vector3.Normalize(right);
        var up = Vector3.Normalize(Vector3.Cross(right, forward));
        var halfWidth = textWidthPixels * WorldTextScale * 0.5f;
        var worldHeight = textHeightPixels * WorldTextScale;

        if (!TryToScreen(anchor, viewProjection, out screen)
            || !TryToScreen(anchor - (right * halfWidth), viewProjection, out var left)
            || !TryToScreen(anchor + (right * halfWidth), viewProjection, out var rightPoint)
            || !TryToScreen(anchor + (up * worldHeight), viewProjection, out var top))
        {
            screen = Vector2.NaN;
            screenWidth = float.NaN;
            screenHeight = float.NaN;
            return false;
        }

        screenWidth = Vector2.Distance(left, rightPoint);
        screenHeight = Vector2.Distance(screen, top);
        return true;
    }

    private static bool TryToScreen(Vector3 world, Matrix4x4 viewProjection, out Vector2 screen)
    {
        var clip = Vector4.Transform(new Vector4(world, 1f), viewProjection);
        if (clip.W <= 1e-6f)
        {
            screen = Vector2.NaN;
            return false;
        }

        screen = new Vector2(
            ((clip.X / clip.W) * 0.5f) + 0.5f,
            (0.5f - ((clip.Y / clip.W) * 0.5f)));
        return true;
    }}
