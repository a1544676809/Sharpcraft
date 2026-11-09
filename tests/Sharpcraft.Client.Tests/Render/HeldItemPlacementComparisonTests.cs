// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// End-to-end comparison of the held-item placement against the reference implementation.
/// <para>
/// The reference side is computed by the shipped JOML build
/// (tools/Sharpcraft.ItemRender.Audit/truth/CompareProbe.java), replaying
/// <c>ItemInHandLayer.submitArmWithItem</c> for an adult right hand with an identity arm pose and a generic generated
/// item display transform (translation (0, 3, 1) px, scale 0.55):
/// </para>
/// <code>
///   reference itemCentre = (-0.375000, 0.687500, -0.312500)
///   reference corner     = (-0.650000, 0.962500, -0.037500)
/// </code>
/// <para>
/// Sharpcraft's frame is the reference frame mirrored in X/Y, scaled by the model scale (0.9 for adult humanoids) and
/// lifted so the feet sit at zero, so the same points should land at:
/// </para>
/// <code>
///   expected ours centre = (0.337500, 0.731250, -0.281250)
///   expected ours corner = (0.585000, 0.483750, -0.033750)
/// </code>
/// </summary>
public sealed class HeldItemPlacementComparisonTests
{
    private static readonly ItemDisplayTransform ProbeDisplay =
        new(Vector3.Zero, new Vector3(0f, 3f / 16f, 1f / 16f), new Vector3(0.55f));

    [Fact]
    public void TheHandFrameMatchesTheReferencePivotAfterConversion()
    {
        // Reference arm pivot (-5, 2, 0) px, converted by K = Mirror · S(0.9) · T(0, 24/16·0.9, 0).
        var expected = ReferenceFrame.ToSharpcraft(new Vector3(-5f / 16f, 2f / 16f, 0f), modelScale: 0.9f);
        var actual = EntityModelMesher.PartWorldMatrix(
            EntityModelResolver.Resolve(HumanoidMobModelDefinition.ZombieEntityTypeId),
            Entity(),
            "right_arm").Translation;

        Assert.True(Close(expected, actual), $"hand frame {Show(actual)} expected {Show(expected)}");
    }

    [Fact]
    public void TheHeldItemLandsWhereTheReferencePutsIt()
    {
        var entity = Entity();
        var hand = EntityModelMesher.PartWorldMatrix(
            EntityModelResolver.Resolve(HumanoidMobModelDefinition.ZombieEntityTypeId),
            entity,
            "right_arm");

        var anchor = EntityModelResolver.AnchorFor(ProbeModel(), offhand: false, baby: false);
        // AnchorFor already applies the display transform for the hand context, so it is used as-is here.
        var world = anchor * hand;

        var centre = Vector3.Transform(new Vector3(0.5f, 0.5f, 0.5f), world);
        var corner = Vector3.Transform(new Vector3(1f, 0f, 0f), world);

        Assert.True(Close(centre, new Vector3(0.3375f, 0.73125f, -0.28125f)), $"centre {Show(centre)}");
        Assert.True(Close(corner, new Vector3(0.585f, 0.48375f, -0.03375f)), $"corner {Show(corner)}");
    }

    private static ItemModelResolution ProbeModel() =>
        new(
            "minecraft:item/probe",
            new Dictionary<string, string>(),
            new Dictionary<ItemDisplayContext, ItemDisplayTransform>
            {
                [ItemDisplayContext.ThirdPersonRightHand] = ProbeDisplay
            });

    private static EntityRenderState Entity() =>
        new(1, HumanoidMobModelDefinition.ZombieEntityTypeId, 0f, 0f, 0f, 0f, 0f);

    private static bool Close(Vector3 a, Vector3 b) => (a - b).Length() < 1e-3f;

    private static string Show(Vector3 v) =>
        string.Create(CultureInfo.InvariantCulture, $"({v.X:F5},{v.Y:F5},{v.Z:F5})");
}
