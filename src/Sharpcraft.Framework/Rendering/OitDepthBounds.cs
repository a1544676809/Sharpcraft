// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// CPU reference of the reference-transparency depth-bounds packing, culling and composite depth selection
/// (docs/specs/render/oit-reference.md 3.1, 3.4 and 4).
/// </summary>
/// <remarks>
/// <para>
/// The DEPTH_BOUNDS stage records, per pixel, four values that MAX-blend across every translucent fragment:
/// <c>r = -closestLinear</c>, <c>g = +furthestLinear</c>, <c>b = closestDevice</c> and
/// <c>a = -closestOpaqueLinear</c> (or <see cref="OpaqueSentinel"/>). The cull clips <c>g</c> to the nearest opaque
/// surface, and the composite writes <c>b</c> as the fragment depth. This type mirrors those steps so the reversed-Z
/// convention they depend on can be pinned by tests: the engine is reversed-Z (near maps to device depth 1, far to 0),
/// so the nearest translucent fragment carries the LARGEST device depth and MAX blending keeps it in <c>b</c>. Getting
/// that direction wrong is exactly what makes a near translucent layer fail to composite over / under a far one.
/// </para>
/// <para>
/// The linear channels are recorded negated for the closest bound so that a single MAX blend resolves both bounds. This
/// mirrors ref://mc-26.3-analysis/src/assets/minecraft/shaders/include/oit_depth_bounds.glsl,
/// core/oit_depth_bounds_cull.fsh and core/oit_composite.fsh; it is an independent implementation, not a translation.
/// </para>
/// </remarks>
public static class OitDepthBounds
{
    /// <summary>Alpha above which a translucent fragment also counts as an opaque occluder for the far-bound clip.</summary>
    public const float OpaqueAlpha = 0.99f;

    /// <summary>Sentinel stored in the opaque channel when nothing in the pixel is fully opaque.</summary>
    public const float OpaqueSentinel = -65504f;

    /// <summary>The four packed depth-bounds channels for one pixel.</summary>
    /// <param name="NegClosestLinear">r: <c>-closestLinear</c>.</param>
    /// <param name="FurthestLinear">g: <c>+furthestLinear</c>.</param>
    /// <param name="ClosestDevice">b: the closest translucent device depth (reversed-Z: the largest device depth).</param>
    /// <param name="NegClosestOpaqueLinear">a: <c>-closestOpaqueLinear</c>, or <see cref="OpaqueSentinel"/>.</param>
    public readonly record struct Packed(
        float NegClosestLinear,
        float FurthestLinear,
        float ClosestDevice,
        float NegClosestOpaqueLinear);

    /// <summary>Packs one translucent fragment's contribution to the depth bounds.</summary>
    /// <param name="linearDepth">View-space distance in blocks (the fragment's fog distance).</param>
    /// <param name="deviceDepth">Reversed-Z device depth in [0, 1] (near is 1, far is 0).</param>
    /// <param name="alpha">Fragment alpha.</param>
    public static Packed Encode(float linearDepth, float deviceDepth, float alpha)
    {
        var opaque = alpha > OpaqueAlpha ? -linearDepth : OpaqueSentinel;
        return new Packed(-linearDepth, linearDepth, deviceDepth, opaque);
    }

    /// <summary>MAX-merges two packed contributions, as the depth-bounds MAX blend does across fragments.</summary>
    public static Packed Merge(Packed a, Packed b) => new(
        MathF.Max(a.NegClosestLinear, b.NegClosestLinear),
        MathF.Max(a.FurthestLinear, b.FurthestLinear),
        MathF.Max(a.ClosestDevice, b.ClosestDevice),
        MathF.Max(a.NegClosestOpaqueLinear, b.NegClosestOpaqueLinear));

    /// <summary>The starting value of a pixel before any fragment: all channels at the negative sentinel.</summary>
    public static Packed Empty => new(OpaqueSentinel, OpaqueSentinel, OpaqueSentinel, OpaqueSentinel);

    /// <summary>
    /// Runs the depth-bounds cull: clip the furthest linear bound to the nearest opaque linear surface. Linear-space
    /// only, so it is independent of the device-depth convention.
    /// </summary>
    public static Packed Cull(Packed bounds)
    {
        var closestOpaqueLinear = -bounds.NegClosestOpaqueLinear;
        return bounds with { FurthestLinear = MathF.Min(bounds.FurthestLinear, closestOpaqueLinear) };
    }

    /// <summary>
    /// The device depth the composite writes for a pixel. It is the closest translucent device depth kept in
    /// <c>b</c>, which under reversed-Z is the largest device depth among the translucent fragments.
    /// </summary>
    public static float CompositeDepth(Packed culledBounds) => culledBounds.ClosestDevice;
}
