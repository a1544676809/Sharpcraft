// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// T-691: proves the DSL-baked EnderDragon/Endermite/Nautilus parts are value-identical to the legacy records and that
/// their deeply nested order/parent links and metadata remain unchanged.
/// </summary>
public sealed class EnderDragonEndermiteNautilusModelDefinitionTests
{
    [Fact]
    public void EnderDragonPartsMatchLegacyRecords()
    {
        var expected = ExpectedDragonParts();
        AssertPartsMatch(expected, EnderDragonModelDefinition.Definition.Parts);
    }

    [Fact]
    public void EndermitePartsMatchLegacyRecords()
    {
        var expected = new[]
        {
            PlayerModelDefinition.FromVanilla("segment0", 0f, 21f, -3.5f, -2f, 0f, -1f, 4f, 3f, 2f, 0, 0),
            PlayerModelDefinition.FromVanilla("segment1", 0f, 20f, 0f, -3f, 0f, -2.5f, 6f, 4f, 5f, 0, 5),
            PlayerModelDefinition.FromVanilla("segment2", 0f, 21f, 3f, -1.5f, 0f, -0.5f, 3f, 3f, 1f, 0, 14),
            PlayerModelDefinition.FromVanilla("segment3", 0f, 22f, 4f, -0.5f, 0f, -0.5f, 1f, 2f, 1f, 0, 18)
        };
        AssertPartsMatch(expected, EndermiteModelDefinition.Definition.Parts);
    }

    [Fact]
    public void NautilusPartsMatchLegacyRecords()
    {
        var expected = new[]
        {
            PlayerModelDefinition.FromVanilla("root", 0f, 29f, -6f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("shell", "root", 0f, -13f, 5f, -7f, -10f, -7f, 14f, 10f, 16f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("shell_mid", "shell", 0f, 0f, 0f, -7f, 0f, -7f, 14f, 8f, 20f, 0, 26),
            PlayerModelDefinition.FromVanillaChild("shell_back", "shell", 0f, 0f, 0f, -7f, 0f, 6f, 14f, 8f, 0f, 48, 26),
            PlayerModelDefinition.FromVanillaChild("body", "root", 0f, -8.5f, 12.3f, -5f, -4.51f, -3f, 10f, 8f, 14f, 0, 54),
            PlayerModelDefinition.FromVanillaChild("body_back", "body", 0f, 0f, 0f, -5f, -4.51f, 7f, 10f, 8f, 0f, 0, 76),
            PlayerModelDefinition.FromVanillaChild("upper_mouth", "body", 0f, -2.51f, 7f, -5f, -2f, 0f, 10f, 4f, 4f, 54, 54, grow: -0.001f),
            PlayerModelDefinition.FromVanillaChild("inner_mouth", "body", 0f, -0.51f, 7.5f, -3f, -2f, -0.5f, 6f, 4f, 4f, 54, 70),
            PlayerModelDefinition.FromVanillaChild("lower_mouth", "body", 0f, 1.49f, 7f, -5f, -1.98f, 0f, 10f, 4f, 4f, 54, 62, grow: -0.001f)
        };
        AssertPartsMatch(expected, NautilusModelDefinition.Definition.Parts);
    }

    [Fact]
    public void NestedNamesRemain()
    {
        Assert.Equal(66, EnderDragonModelDefinition.Definition.Parts.Count);
        Assert.Equal("head", EnderDragonModelDefinition.Definition.Parts[0].Name);
        Assert.Equal("neck0", EnderDragonModelDefinition.Definition.Parts[8].Name);
        Assert.Equal("body", EnderDragonModelDefinition.Definition.Parts[42].Name);
        Assert.Equal(4, EndermiteModelDefinition.Definition.Parts.Count);
        Assert.Equal(9, NautilusModelDefinition.Definition.Parts.Count);
    }

    private static EntityModelPart[] ExpectedDragonParts()
    {
        var parts = new List<EntityModelPart>
        {
            PlayerModelDefinition.FromVanilla("head", 0f, 20f, -62f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("upperlip", "head", 0f, 0f, 0f, -6f, -1f, -24f, 12f, 5f, 16f, 176, 44),
            PlayerModelDefinition.FromVanillaChild("upperhead", "head", 0f, 0f, 0f, -8f, -8f, -10f, 16f, 16f, 16f, 112, 30),
            PlayerModelDefinition.FromVanillaChild("scale_left", "head", 0f, 0f, 0f, -5f, -12f, -4f, 2f, 4f, 6f, 0, 0, mirror: true),
            PlayerModelDefinition.FromVanillaChild("nostril_left", "head", 0f, 0f, 0f, -5f, -3f, -22f, 2f, 2f, 4f, 112, 0, mirror: true),
            PlayerModelDefinition.FromVanillaChild("scale_right", "head", 0f, 0f, 0f, 3f, -12f, -4f, 2f, 4f, 6f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("nostril_right", "head", 0f, 0f, 0f, 3f, -3f, -22f, 2f, 2f, 4f, 112, 0),
            PlayerModelDefinition.FromVanillaChild("jaw", "head", 0f, 4f, -8f, -6f, 0f, -16f, 12f, 4f, 16f, 176, 65)
        };
        for (var i = 0; i < 5; i++) AddSpine(parts, "neck" + i, 20f, -12f - (i * 10f));
        for (var i = 0; i < 12; i++) AddSpine(parts, "tail" + i, 10f, 60f + (i * 10f));
        parts.Add(PlayerModelDefinition.FromVanilla("body", 0f, 20f, 8f, -12f, -16f, -16f, 24f, 24f, 64f, 0, 0));
        parts.Add(PlayerModelDefinition.FromVanillaChild("body_scale_1", "body", 0f, 0f, 0f, -1f, -22f, -10f, 2f, 6f, 12f, 220, 53));
        parts.Add(PlayerModelDefinition.FromVanillaChild("body_scale_2", "body", 0f, 0f, 0f, -1f, -22f, 10f, 2f, 6f, 12f, 220, 53));
        parts.Add(PlayerModelDefinition.FromVanillaChild("body_scale_3", "body", 0f, 0f, 0f, -1f, -22f, 30f, 2f, 6f, 12f, 220, 53));
        parts.Add(PlayerModelDefinition.FromVanillaChild("left_wing", "body", 12f, -15f, -6f, 0f, -4f, -4f, 56f, 8f, 8f, 112, 88, mirror: true));
        parts.Add(PlayerModelDefinition.FromVanillaChild("left_wing_skin", "body", 12f, -15f, -6f, 0f, 0f, 2f, 56f, 0f, 56f, -56, 88, mirror: true));
        parts.Add(PlayerModelDefinition.FromVanillaChild("left_wing_tip", "left_wing", 56f, 0f, 0f, 0f, -2f, -2f, 56f, 4f, 4f, 112, 136, mirror: true));
        parts.Add(PlayerModelDefinition.FromVanillaChild("left_wing_tip_skin", "left_wing", 56f, 0f, 0f, 0f, 0f, 2f, 56f, 0f, 56f, -56, 144, mirror: true));
        parts.Add(PlayerModelDefinition.FromVanillaChild("right_wing", "body", -12f, -15f, -6f, -56f, -4f, -4f, 56f, 8f, 8f, 112, 88));
        parts.Add(PlayerModelDefinition.FromVanillaChild("right_wing_skin", "body", -12f, -15f, -6f, -56f, 0f, 2f, 56f, 0f, 56f, -56, 88));
        parts.Add(PlayerModelDefinition.FromVanillaChild("right_wing_tip", "right_wing", -56f, 0f, 0f, -56f, -2f, -2f, 56f, 4f, 4f, 112, 136));
        parts.Add(PlayerModelDefinition.FromVanillaChild("right_wing_tip_skin", "right_wing", -56f, 0f, 0f, -56f, 0f, 2f, 56f, 0f, 56f, -56, 144));
        AddLeg(parts, "left_front", 12f, 0f, -6f, -4f, -4f, -4f, 8f, 24f, 8f, 112, 104, -3f, -1f, -3f, 6f, 24f, 6f, 226, 138, 23f, 0f, -4f, 0f, -12f, 8f, 4f, 16f, 144, 104, 1.3f, -0.5f, 0.75f);
        AddLeg(parts, "right_front", -12f, 0f, -6f, -4f, -4f, -4f, 8f, 24f, 8f, 112, 104, -3f, -1f, -3f, 6f, 24f, 6f, 226, 138, 23f, 0f, -4f, 0f, -12f, 8f, 4f, 16f, 144, 104, 1.3f, -0.5f, 0.75f);
        AddLeg(parts, "left_hind", 16f, -4f, 34f, -8f, -4f, -8f, 16f, 32f, 16f, 0, 0, -6f, -2f, 0f, 12f, 32f, 12f, 196, 0, 31f, 4f, -9f, 0f, -20f, 18f, 6f, 24f, 112, 0, 1.0f, 0.5f, 0.75f);
        AddLeg(parts, "right_hind", -16f, -4f, 34f, -8f, -4f, -8f, 16f, 32f, 16f, 0, 0, -6f, -2f, 0f, 12f, 32f, 12f, 196, 0, 31f, 4f, -9f, 0f, -20f, 18f, 6f, 24f, 112, 0, 1.0f, 0.5f, 0.75f);
        return [.. parts];
    }

    private static void AddSpine(List<EntityModelPart> parts, string name, float pivotY, float pivotZ)
    {
        parts.Add(PlayerModelDefinition.FromVanilla(name, 0f, pivotY, pivotZ, -5f, -5f, -5f, 10f, 10f, 10f, 192, 104));
        parts.Add(PlayerModelDefinition.FromVanillaChild(name + "_scale", name, 0f, 0f, 0f, -1f, -9f, -3f, 2f, 4f, 6f, 48, 0));
    }

    private static void AddLeg(List<EntityModelPart> parts, string prefix, float pivotX, float pivotY, float pivotZ,
        float mainMinX, float mainMinY, float mainMinZ, float mainW, float mainH, float mainD, int mainU, int mainV,
        float tipMinX, float tipMinY, float tipMinZ, float tipW, float tipH, float tipD, int tipU, int tipV,
        float footPivotY, float footPivotZ, float footMinX, float footMinY, float footMinZ, float footW, float footH, float footD, int footU, int footV,
        float mainRotX, float tipRotX, float footRotX)
    {
        parts.Add(PlayerModelDefinition.FromVanillaChild(prefix + "_leg", "body", pivotX, pivotY, pivotZ, mainMinX, mainMinY, mainMinZ, mainW, mainH, mainD, mainU, mainV, rotationX: mainRotX));
        parts.Add(PlayerModelDefinition.FromVanillaChild(prefix + "_leg_tip", prefix + "_leg", 0f, 20f, -1f, tipMinX, tipMinY, tipMinZ, tipW, tipH, tipD, tipU, tipV, rotationX: tipRotX));
        parts.Add(PlayerModelDefinition.FromVanillaChild(prefix + "_foot", prefix + "_leg_tip", 0f, footPivotY, footPivotZ, footMinX, footMinY, footMinZ, footW, footH, footD, footU, footV, rotationX: footRotX));
    }

    private static void AssertPartsMatch(EntityModelPart[] expected, IReadOnlyList<EntityModelPart> actual)
    {
        Assert.Equal(expected.Length, actual.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            ModelPartAssertions.AssertEqual(expected[i], actual[i]);
        }
    }
}
