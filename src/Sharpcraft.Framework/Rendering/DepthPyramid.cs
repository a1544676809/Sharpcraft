// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// Hierarchical depth pyramid: each level halves the resolution and keeps the farthest depth of its children.
/// </summary>
/// <remarks>
/// Occlusion culling asks "is the whole region under this box already farther than the box's nearest point". Keeping the
/// farthest depth per region makes that test conservative: a region that is partly nearer will report itself as nearer and
/// the box is kept, which is the only safe direction. This is the CPU reference the compute pass will be checked against.
/// </remarks>
public sealed class DepthPyramid
{
    private readonly float[][] _levels;

    private DepthPyramid(float[][] levels, int width, int height)
    {
        _levels = levels;
        Width = width;
        Height = height;
    }

    /// <summary>Width of the base level.</summary>
    public int Width { get; }

    /// <summary>Height of the base level.</summary>
    public int Height { get; }

    /// <summary>Number of levels, including the base.</summary>
    public int LevelCount => _levels.Length;

    /// <summary>One level, in pixels, starting at zero.</summary>
    /// <param name="level">Level index.</param>
    public ReadOnlySpan<float> this[int level] => _levels[level];

    /// <summary>Width of a level, at least one pixel.</summary>
    /// <param name="level">Level index.</param>
    public int WidthOf(int level) => Math.Max(1, Width >> level);

    /// <summary>Height of a level, at least one pixel.</summary>
    /// <param name="level">Level index.</param>
    public int HeightOf(int level) => Math.Max(1, Height >> level);

    /// <summary>Builds the pyramid from a base depth buffer, zero-to-one depths.</summary>
    /// <param name="depth">Base level depths, row major.</param>
    /// <param name="width">Base width.</param>
    /// <param name="height">Base height.</param>
    public static DepthPyramid Build(ReadOnlySpan<float> depth, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        if (depth.Length < width * height)
        {
            throw new ArgumentException("depth buffer is smaller than the stated size", nameof(depth));
        }

        var levels = new List<float[]>();
        var current = depth[..(width * height)].ToArray();
        levels.Add(current);

        var levelWidth = width;
        var levelHeight = height;

        while (levelWidth > 1 || levelHeight > 1)
        {
            var nextWidth = Math.Max(1, levelWidth / 2);
            var nextHeight = Math.Max(1, levelHeight / 2);
            var next = new float[nextWidth * nextHeight];

            for (var y = 0; y < nextHeight; y++)
            {
                for (var x = 0; x < nextWidth; x++)
                {
                    // Four children; farthest wins, which is what keeps the occlusion test conservative.
                    var farthest = 0f;

                    for (var dy = 0; dy < 2; dy++)
                    {
                        for (var dx = 0; dx < 2; dx++)
                        {
                            var sourceX = Math.Min(levelWidth - 1, (x * 2) + dx);
                            var sourceY = Math.Min(levelHeight - 1, (y * 2) + dy);
                            farthest = Math.Max(farthest, current[(sourceY * levelWidth) + sourceX]);
                        }
                    }

                    next[(y * nextWidth) + x] = farthest;
                }
            }

            levels.Add(next);
            current = next;
            levelWidth = nextWidth;
            levelHeight = nextHeight;
        }

        return new DepthPyramid([.. levels], width, height);
    }

    /// <summary>
    /// Chooses the finest level whose single texel still covers the given rectangle, and returns that texel's depth.
    /// </summary>
    /// <param name="minX">Rectangle minimum x, zero-to-one.</param>
    /// <param name="minY">Rectangle minimum y, zero-to-one.</param>
    /// <param name="maxX">Rectangle maximum x, zero-to-one.</param>
    /// <param name="maxY">Rectangle maximum y, zero-to-one.</param>
    /// <param name="farthest">Farthest depth covering the rectangle.</param>
    /// <returns>False when the rectangle cannot be covered by a single texel.</returns>
    public bool TryCoveringDepth(float minX, float minY, float maxX, float maxY, out float farthest)
    {
        farthest = float.MaxValue;

        if (maxX <= minX || maxY <= minY)
        {
            return false;
        }

        var pixelMinX = (int)MathF.Floor(minX * Width);
        var pixelMaxX = (int)MathF.Ceiling(maxX * Width) - 1;
        var pixelMinY = (int)MathF.Floor(minY * Height);
        var pixelMaxY = (int)MathF.Ceiling(maxY * Height) - 1;

        pixelMinX = Math.Clamp(pixelMinX, 0, Width - 1);
        pixelMaxX = Math.Clamp(pixelMaxX, 0, Width - 1);
        pixelMinY = Math.Clamp(pixelMinY, 0, Height - 1);
        pixelMaxY = Math.Clamp(pixelMaxY, 0, Height - 1);

        if (pixelMaxX < pixelMinX || pixelMaxY < pixelMinY)
        {
            return false;
        }

        // Walk up the pyramid until one texel spans the whole rectangle.
        for (var level = 0; level < LevelCount; level++)
        {
            var levelWidth = WidthOf(level);
            var levelHeight = HeightOf(level);
            var scaleX = (float)levelWidth / Width;
            var scaleY = (float)levelHeight / Height;

            var left = (int)MathF.Floor(pixelMinX * scaleX);
            var right = (int)MathF.Floor(pixelMaxX * scaleX);
            var top = (int)MathF.Floor(pixelMinY * scaleY);
            var bottom = (int)MathF.Floor(pixelMaxY * scaleY);

            if (left != right || top != bottom)
            {
                continue;
            }

            farthest = this[level][(top * levelWidth) + left];
            return true;
        }

        return false;
    }
}
