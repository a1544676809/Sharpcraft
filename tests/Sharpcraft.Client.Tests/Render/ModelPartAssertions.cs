// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Shared value-wise oracle for vanilla mesh DSL migrations (T-671, T-672). <c>EntityModelPart</c> is a record, but its
/// <c>OverlayLayers</c> is a reference-typed list: the synthesized record Equals compares two collection instances by
/// reference, so plain record equality is NOT a valid oracle for parts with overlays (two independently built parts
/// hold distinct <c>&lt;&gt;z__ReadOnlySingleElementList</c> instances even when their contents match). This is a
/// pre-existing model-layer design fact. Delegate fields are compared by presence/non-presence only, never identity.
/// </summary>
internal static class ModelPartAssertions
{
    /// <summary>Asserts two parts carry identical values (overlay layers by value, delegate selectors by presence).</summary>
    public static void AssertEqual(EntityModelPart expected, EntityModelPart actual)
    {
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.CentreX, actual.CentreX);
        Assert.Equal(expected.Bottom, actual.Bottom);
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);
        Assert.Equal(expected.Depth, actual.Depth);
        Assert.Equal(expected.TextureX, actual.TextureX);
        Assert.Equal(expected.TextureY, actual.TextureY);
        Assert.Equal(expected.Mirror, actual.Mirror);
        Assert.Equal(expected.CentreZ, actual.CentreZ);
        Assert.Equal(expected.PivotX, actual.PivotX);
        Assert.Equal(expected.PivotY, actual.PivotY);
        Assert.Equal(expected.PivotZ, actual.PivotZ);
        Assert.Equal(expected.RotationX, actual.RotationX);
        Assert.Equal(expected.RotationY, actual.RotationY);
        Assert.Equal(expected.RotationZ, actual.RotationZ);
        Assert.Equal(expected.TexturePath, actual.TexturePath);
        Assert.Equal(expected.Grow, actual.Grow);
        Assert.Equal(expected.Parent, actual.Parent);
        Assert.Equal(expected.Kind, actual.Kind);
        Assert.Equal(expected.TextureWidth, actual.TextureWidth);
        Assert.Equal(expected.TextureHeight, actual.TextureHeight);
        Assert.Equal(expected.UvWidth, actual.UvWidth);
        Assert.Equal(expected.UvHeight, actual.UvHeight);
        Assert.Equal(expected.UvDepth, actual.UvDepth);
        Assert.Equal(expected.IncludeInEmissive, actual.IncludeInEmissive);
        Assert.Equal(expected.EmissiveOnly, actual.EmissiveOnly);
        Assert.Equal(expected.EmissiveTexturePath, actual.EmissiveTexturePath);
        Assert.Equal(expected.PoseSource, actual.PoseSource);
        Assert.Equal(expected.GrowXyz, actual.GrowXyz);

        // OverlayLayers: compare by value; null and empty are both "effectively empty" and treated as equivalent.
        var eOverlays = expected.OverlayLayers;
        var aOverlays = actual.OverlayLayers;
        var eEmpty = eOverlays is null || eOverlays.Count == 0;
        var aEmpty = aOverlays is null || aOverlays.Count == 0;
        if (eEmpty || aEmpty)
        {
            Assert.True(eEmpty && aEmpty, "OverlayLayers presence differs (null/empty)");
        }
        else
        {
            Assert.Equal(eOverlays!.Count, aOverlays!.Count);
            for (var i = 0; i < eOverlays!.Count; i++)
            {
                // EntityModelLayer is a record struct, so this compares all of its fields by value.
                Assert.Equal(eOverlays[i], aOverlays[i]);
            }
        }

        // Delegate selectors: presence/non-presence only, never delegate identity.
        Assert.Equal(expected.VisibleWhen is not null, actual.VisibleWhen is not null);
        Assert.Equal(expected.RotationSelector is not null, actual.RotationSelector is not null);
        Assert.Equal(expected.TintSelector is not null, actual.TintSelector is not null);
        Assert.Equal(expected.TextureSelector is not null, actual.TextureSelector is not null);
        Assert.Equal(expected.YOffsetSelector is not null, actual.YOffsetSelector is not null);
    }
}
