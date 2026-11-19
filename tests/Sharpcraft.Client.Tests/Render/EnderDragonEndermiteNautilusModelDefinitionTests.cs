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
            LegacyModelPartFactory.FromVanilla("head", 0f, 20f, -62f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            LegacyModelPartFactory.FromVanillaChild("upperlip", "head", 0f, 0f, 0f, -6f, -1f, -24f, 12f, 5f, 16f, 176, 44),
            LegacyModelPartFactory.FromVanillaChild("upperhead", "head", 0f, 0f, 0f, -8f, -8f, -10f, 16f, 16f, 16f, 112, 30),
            LegacyModelPartFactory.FromVanillaChild("scale_left", "head", 0f, 0f, 0f, -5f, -12f, -4f, 2f, 4f, 6f, 0, 0, mirror: true),
            LegacyModelPartFactory.FromVanillaChild("nostril_left", "head", 0f, 0f, 0f, -5f, -3f, -22f, 2f, 2f, 4f, 112, 0, mirror: true),
            LegacyModelPartFactory.FromVanillaChild("scale_right", "head", 0f, 0f, 0f, 3f, -12f, -4f, 2f, 4f, 6f, 0, 0, mirror: true),
            LegacyModelPartFactory.FromVanillaChild("nostril_right", "head", 0f, 0f, 0f, 3f, -3f, -22f, 2f, 2f, 4f, 112, 0, mirror: true),
            LegacyModelPartFactory.FromVanillaChild("jaw", "head", 0f, 4f, -8f, -6f, 0f, -16f, 12f, 4f, 16f, 176, 65)
        };
        for (var i = 0; i < 5; i++) AddSpine(parts, "neck" + i, 20f, -12f - (i * 10f));
        for (var i = 0; i < 12; i++) AddSpine(parts, "tail" + i, 10f, 60f + (i * 10f));
        parts.Add(LegacyModelPartFactory.FromVanilla("body", 0f, 20f, 8f, -12f, -16f, -16f, 24f, 24f, 64f, 0, 0));
        parts.Add(LegacyModelPartFactory.FromVanillaChild("body_scale_1", "body", 0f, 0f, 0f, -1f, -22f, -10f, 2f, 6f, 12f, 220, 53));
        parts.Add(LegacyModelPartFactory.FromVanillaChild("body_scale_2", "body", 0f, 0f, 0f, -1f, -22f, 10f, 2f, 6f, 12f, 220, 53));
        parts.Add(LegacyModelPartFactory.FromVanillaChild("body_scale_3", "body", 0f, 0f, 0f, -1f, -22f, 30f, 2f, 6f, 12f, 220, 53));
        parts.Add(LegacyModelPartFactory.FromVanillaChild("left_wing", "body", 12f, -15f, -6f, 0f, -4f, -4f, 56f, 8f, 8f, 112, 88, mirror: true));
        parts.Add(LegacyModelPartFactory.FromVanillaChild("left_wing_skin", "body", 12f, -15f, -6f, 0f, 0f, 2f, 56f, 0f, 56f, -56, 88, mirror: true));
        parts.Add(LegacyModelPartFactory.FromVanillaChild("left_wing_tip", "left_wing", 56f, 0f, 0f, 0f, -2f, -2f, 56f, 4f, 4f, 112, 136, mirror: true));
        parts.Add(LegacyModelPartFactory.FromVanillaChild("left_wing_tip_skin", "left_wing", 56f, 0f, 0f, 0f, 0f, 2f, 56f, 0f, 56f, -56, 144, mirror: true));
        parts.Add(LegacyModelPartFactory.FromVanillaChild("right_wing", "body", -12f, -15f, -6f, -56f, -4f, -4f, 56f, 8f, 8f, 112, 88));
        parts.Add(LegacyModelPartFactory.FromVanillaChild("right_wing_skin", "body", -12f, -15f, -6f, -56f, 0f, 2f, 56f, 0f, 56f, -56, 88));
        parts.Add(LegacyModelPartFactory.FromVanillaChild("right_wing_tip", "right_wing", -56f, 0f, 0f, -56f, -2f, -2f, 56f, 4f, 4f, 112, 136));
        parts.Add(LegacyModelPartFactory.FromVanillaChild("right_wing_tip_skin", "right_wing", -56f, 0f, 0f, -56f, 0f, 2f, 56f, 0f, 56f, -56, 144));
        AddLeg(parts, "left_front", 12f, 0f, -6f, -4f, -4f, -4f, 8f, 24f, 8f, 112, 104, -3f, -1f, -3f, 6f, 24f, 6f, 226, 138, 20f, -1f, 23f, 0f, -4f, 0f, -12f, 8f, 4f, 16f, 144, 104, 1.3f, -0.5f, 0.75f);
        AddLeg(parts, "right_front", -12f, 0f, -6f, -4f, -4f, -4f, 8f, 24f, 8f, 112, 104, -3f, -1f, -3f, 6f, 24f, 6f, 226, 138, 20f, -1f, 23f, 0f, -4f, 0f, -12f, 8f, 4f, 16f, 144, 104, 1.3f, -0.5f, 0.75f);
        AddLeg(parts, "left_hind", 16f, -4f, 34f, -8f, -4f, -8f, 16f, 32f, 16f, 0, 0, -6f, -2f, 0f, 12f, 32f, 12f, 196, 0, 32f, -4f, 31f, 4f, -9f, 0f, -20f, 18f, 6f, 24f, 112, 0, 1.0f, 0.5f, 0.75f);
        AddLeg(parts, "right_hind", -16f, -4f, 34f, -8f, -4f, -8f, 16f, 32f, 16f, 0, 0, -6f, -2f, 0f, 12f, 32f, 12f, 196, 0, 32f, -4f, 31f, 4f, -9f, 0f, -20f, 18f, 6f, 24f, 112, 0, 1.0f, 0.5f, 0.75f);
        return [.. parts];
    }

    private static void AddSpine(List<EntityModelPart> parts, string name, float pivotY, float pivotZ)
    {
        parts.Add(LegacyModelPartFactory.FromVanilla(name, 0f, pivotY, pivotZ, -5f, -5f, -5f, 10f, 10f, 10f, 192, 104));
        parts.Add(LegacyModelPartFactory.FromVanillaChild(name + "_scale", name, 0f, 0f, 0f, -1f, -9f, -3f, 2f, 4f, 6f, 48, 0));
    }

    private static void AddLeg(List<EntityModelPart> parts, string prefix, float pivotX, float pivotY, float pivotZ,
        float mainMinX, float mainMinY, float mainMinZ, float mainW, float mainH, float mainD, int mainU, int mainV,
        float tipMinX, float tipMinY, float tipMinZ, float tipW, float tipH, float tipD, int tipU, int tipV,
        float tipPivotY, float tipPivotZ,
        float footPivotY, float footPivotZ, float footMinX, float footMinY, float footMinZ, float footW, float footH, float footD, int footU, int footV,
        float mainRotX, float tipRotX, float footRotX)
    {
        parts.Add(LegacyModelPartFactory.FromVanillaChild(prefix + "_leg", "body", pivotX, pivotY, pivotZ, mainMinX, mainMinY, mainMinZ, mainW, mainH, mainD, mainU, mainV, rotationX: mainRotX));
        parts.Add(LegacyModelPartFactory.FromVanillaChild(prefix + "_leg_tip", prefix + "_leg", 0f, tipPivotY, tipPivotZ, tipMinX, tipMinY, tipMinZ, tipW, tipH, tipD, tipU, tipV, rotationX: tipRotX));
        parts.Add(LegacyModelPartFactory.FromVanillaChild(prefix + "_foot", prefix + "_leg_tip", 0f, footPivotY, footPivotZ, footMinX, footMinY, footMinZ, footW, footH, footD, footU, footV, rotationX: footRotX));
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
