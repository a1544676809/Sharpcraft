// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

public sealed class PlayerModelDefinitionTests
{
    [Fact]
    public void TheAdultModelHasTheReferencePartsAndDimensions()
    {
        var parts = PlayerModelDefinition.AdultParts;

        Assert.Equal(6, parts.Length);
        AssertPart(parts, "head", 0f, 24f, 8f, 8f, 8f);
        AssertPart(parts, "body", 0f, 12f, 8f, 12f, 4f);
        AssertPart(parts, "left_arm", -6f, 12f, 4f, 12f, 4f);
        AssertPart(parts, "right_arm", 6f, 12f, 4f, 12f, 4f);
        AssertPart(parts, "left_leg", -1.9f, 0f, 4f, 12f, 4f);
        AssertPart(parts, "right_leg", 1.9f, 0f, 4f, 12f, 4f);
    }

    [Fact]
    public void TheModelIsThirtyTwoPixelsTall()
    {
        var head = PlayerModelDefinition.AdultParts[0];
        Assert.Equal(32f, PlayerModelDefinition.ModelPixels);
        Assert.Equal(32f, head.Bottom + head.Height);
    }

    /// <summary>
    /// T-671: proves the DSL-baked AdultParts are identical to the legacy FromVanilla records, as a first real-model
    /// migration check.
    /// </summary>
    [Fact]
    public void AdultPartsMatchTheLegacyFromVanillaRecords()
    {
        var expected = new[]
        {
            PlayerModelDefinition.FromVanilla("head", 0f, 0f, 0f, -4f, -8f, -4f, 8f, 8f, 8f, 0, 0,
                overlays: [new("hat", 0.5f, 32, 0, Kind: EntityGeometryLayerKind.Translucent)]),
            PlayerModelDefinition.FromVanilla("body", 0f, 0f, 0f, -4f, 0f, -2f, 8f, 12f, 4f, 16, 16,
                overlays: [new("jacket", 0.25f, 16, 32, Kind: EntityGeometryLayerKind.Translucent)]),
            PlayerModelDefinition.FromVanilla("left_arm", 5f, 2f, 0f, -1f, -2f, -2f, 4f, 12f, 4f, 32, 48,
                overlays: [new("left_sleeve", 0.25f, 48, 48, Kind: EntityGeometryLayerKind.Translucent)]),
            PlayerModelDefinition.FromVanilla("right_arm", -5f, 2f, 0f, -3f, -2f, -2f, 4f, 12f, 4f, 40, 16,
                overlays: [new("right_sleeve", 0.25f, 40, 32, Kind: EntityGeometryLayerKind.Translucent)]),
            PlayerModelDefinition.FromVanilla("left_leg", 1.9f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 16, 48,
                overlays: [new("left_pants", 0.25f, 0, 48, Kind: EntityGeometryLayerKind.Translucent)]),
            PlayerModelDefinition.FromVanilla("right_leg", -1.9f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 16,
                overlays: [new("right_pants", 0.25f, 0, 32, Kind: EntityGeometryLayerKind.Translucent)])
        };

        var actual = PlayerModelDefinition.AdultParts;
        Assert.Equal(expected.Length, actual.Length);
        // Record equality is not a valid migration oracle here (OverlayLayers is reference-typed); compare by value.
        for (var i = 0; i < expected.Length; i++)
        {
            AssertPartValueEqual(expected[i], actual[i]);
        }
    }

    /// <summary>
    /// Value-wise oracle for a DSL migration. <c>EntityModelPart</c> is a record, but its <c>OverlayLayers</c> is a
    /// reference-typed list: the synthesized record Equals compares two collection instances by reference, so plain
    /// record equality is NOT a valid oracle for parts with overlays (two independently built parts hold distinct
    /// &lt;&gt;z__ReadOnlySingleElementList instances even when their contents match). This is a pre-existing
    /// model-layer design fact, deliberately reused by future DSL migrations. Delegate fields are compared by
    /// presence/non-presence only, never by identity.
    /// </summary>
    private static void AssertPartValueEqual(EntityModelPart expected, EntityModelPart actual)
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

    private static void AssertPart(EntityModelPart[] parts, string name, float x, float bottom, float width, float height, float depth)
    {
        var part = Assert.Single(parts, candidate => candidate.Name == name);
        Assert.Equal(x, part.CentreX);
        Assert.Equal(bottom, part.Bottom);
        Assert.Equal(width, part.Width);
        Assert.Equal(height, part.Height);
        Assert.Equal(depth, part.Depth);
    }
}
