// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Framework.Tests.Rendering;

/// <summary>
/// Pins the reversed-Z depth-bounds packing and composite depth selection (docs/specs/render/oit-reference.md 3.1, 3.4,
/// 4). The engine is reversed-Z, so the nearest translucent fragment has the LARGEST device depth and MAX blending must
/// keep it as the composite's depth; a near translucent layer that fails to become the composite depth is exactly the
/// ordering regression that hides translucency behind another translucent surface.
/// </summary>
public sealed class OitDepthBoundsTests
{
    private const float Near = 0.1f;
    private const float Far = 2000f;

    private static float Device(float distance) => DepthLinearisation.ToDepth(distance, Near, Far);

    [Fact]
    public void ReversedZMakesTheNearPlaneOneAndTheFarPlaneZero()
    {
        Assert.Equal(1f, Device(Near), 3);
        Assert.Equal(0f, Device(Far), 3);
        // Distance grows away from the camera, so window depth falls.
        Assert.True(Device(5f) > Device(20f));
        Assert.True(Device(20f) > Device(200f));
    }

    [Fact]
    public void CompositeDepthIsTheNearestTransluentsDeviceDepth()
    {
        // A near slab and a far slab of water (alpha below the opaque threshold) on the same pixel.
        var near = OitDepthBounds.Encode(5f, Device(5f), 0.7f);
        var far = OitDepthBounds.Encode(20f, Device(20f), 0.7f);
        var bounds = OitDepthBounds.Merge(OitDepthBounds.Empty, near);
        bounds = OitDepthBounds.Merge(bounds, far);

        var culled = OitDepthBounds.Cull(bounds);

        // Reversed-Z: the near surface has the larger device depth, so MAX keeps it in b and the composite writes it.
        Assert.Equal(Device(5f), culled.ClosestDevice, 6);
        Assert.Equal(Device(5f), OitDepthBounds.CompositeDepth(culled), 6);
        Assert.True(culled.ClosestDevice > far.ClosestDevice);
    }

    [Fact]
    public void AddingANearerLayerMovesTheCompositeDepthNearer()
    {
        var farOnly = OitDepthBounds.Merge(OitDepthBounds.Empty, OitDepthBounds.Encode(30f, Device(30f), 0.7f));
        var withNear = OitDepthBounds.Merge(farOnly, OitDepthBounds.Encode(5f, Device(5f), 0.7f));

        Assert.Equal(Device(30f), OitDepthBounds.CompositeDepth(OitDepthBounds.Cull(farOnly)), 6);
        Assert.Equal(Device(5f), OitDepthBounds.CompositeDepth(OitDepthBounds.Cull(withNear)), 6);
        Assert.True(OitDepthBounds.CompositeDepth(OitDepthBounds.Cull(withNear))
            > OitDepthBounds.CompositeDepth(OitDepthBounds.Cull(farOnly)));
    }

    [Fact]
    public void CullClipsTheFurthestBoundToTheNearestOpaqueSurface()
    {
        // An opaque-ish fragment (alpha > 0.99) at 8 blocks sits in front of a translucent fragment at 40 blocks; the
        // far bound must be clipped to the opaque surface so layers behind it never pollute the transmittance.
        var opaque = OitDepthBounds.Encode(8f, Device(8f), 1f);
        var translucentBehind = OitDepthBounds.Encode(40f, Device(40f), 0.7f);
        var bounds = OitDepthBounds.Merge(OitDepthBounds.Empty, opaque);
        bounds = OitDepthBounds.Merge(bounds, translucentBehind);

        var culled = OitDepthBounds.Cull(bounds);

        Assert.Equal(8f, culled.FurthestLinear, 4);
        // The opaque fragment's own distance is the closest bound.
        Assert.Equal(8f, -culled.NegClosestLinear, 4);
    }

    [Fact]
    public void OpaqueSentinelLeavesTheFarBoundUnclipped()
    {
        // No fragment is fully opaque: the sentinel must not clip the far bound (65504 is far beyond any scene depth).
        var bounds = OitDepthBounds.Encode(25f, Device(25f), 0.7f);
        var culled = OitDepthBounds.Cull(bounds);

        Assert.Equal(25f, culled.FurthestLinear, 4);
        Assert.Equal(OitDepthBounds.OpaqueSentinel, bounds.NegClosestOpaqueLinear);
    }

    [Fact]
    public void EmptyPixelKeepsTheSentinelDepth()
    {
        // With no translucent fragment the composite depth stays at the negative sentinel, which clamps to the far
        // plane when written, so an empty pixel never occludes anything drawn later.
        var empty = OitDepthBounds.Cull(OitDepthBounds.Empty);

        Assert.Equal(OitDepthBounds.OpaqueSentinel, OitDepthBounds.CompositeDepth(empty));
        Assert.True(OitDepthBounds.CompositeDepth(empty) < 0f);
    }
}
