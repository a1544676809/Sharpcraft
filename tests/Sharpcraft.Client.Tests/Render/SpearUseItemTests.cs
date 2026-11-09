// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// The spear use-item animation (<c>SpearAnimations.thirdPersonUseItem</c> via <c>ArmPose.animateUseItem</c>), pinned
/// against reference matrices captured with the shipped JOML build
/// (tools/Sharpcraft.ItemRender.Audit/truth/SpearUseProbe.java).
/// <para>
/// Scenario: wooden spear (<c>delay 0.75s → 15 ticks</c>, dismount 100, knockback 200, damage 300,
/// <c>forward_movement 0.38</c>), <c>ticksUsingItem = 30</c>, no swing, adult right hand, generated-item display.
/// The probe reports <c>raise = 1</c>, <c>sway = 0</c>, <c>lower = 0</c>, <c>raiseBack = 0</c>,
/// <c>swayIntensity = 0</c>.
/// </para>
/// </summary>
public sealed class SpearUseItemTests
{
    private const float TicksUsingItem = 30f;

    private static readonly Matrix4x4 Reference = new(
        0f, 0.18811f, 0.51683f, 0f,
        0f, 0.51683f, -0.18811f, 0f,
        -0.55f, 0f, 0f, 0f,
        -0.0375f, 0.85809f, -0.33293f, 1f);

    [Fact]
    public void TheUseParamsMatchTheReferenceProgressValues()
    {
        var weapon = KineticWeapons.ForItem("wooden_spear");
        Assert.NotNull(weapon);

        var parameters = SpearAnimations.UseParams.FromKineticWeapon(weapon!.Value, TicksUsingItem);

        Assert.Equal(15, weapon.Value.DelayTicks);
        Assert.Equal(100, weapon.Value.DismountMaxTicks);
        Assert.Equal(200, weapon.Value.KnockbackMaxTicks);
        Assert.Equal(300, weapon.Value.DamageMaxTicks);
        Assert.Equal(1f, parameters.RaiseProgress, 5);
        Assert.Equal(0f, parameters.SwayProgress, 5);
        Assert.Equal(0f, parameters.LowerProgress, 5);
        Assert.Equal(0f, parameters.RaiseBackProgress, 5);
        Assert.Equal(0f, parameters.SwayIntensity, 5);
    }

    [Fact]
    public void TheUseItemMatrixMatchesTheReferenceWorld()
    {
        var weapon = KineticWeapons.ForItem("wooden_spear")!.Value;
        var parameters = SpearAnimations.UseParams.FromKineticWeapon(weapon, TicksUsingItem);
        var useItem = SpearAnimations.ThirdPersonUseItemMatrix(parameters, weapon, swingAnimation: 0f, invert: 1);

        var display = new ItemDisplayTransform(Vector3.Zero, new Vector3(0f, 3f / 16f, 1f / 16f), new Vector3(0.55f)).ToMatrix();
        var anchor = ReferenceFrame.HeldItemAnchor(ReferenceFrame.AdultOffset, 0.9f);
        var hand = EntityModelMesher.PartWorldMatrix(
            EntityModelResolver.Resolve(HumanoidMobModelDefinition.ZombieEntityTypeId),
            new EntityRenderState(1, HumanoidMobModelDefinition.ZombieEntityTypeId, 0f, 0f, 0f, 0f, 0f),
            "right_arm",
            null);

        var world = display * useItem * anchor * hand;
        var expected = Reference * ReferenceFrame.Frame(0.9f);

        foreach (var probe in new[] { new Vector3(0.5f, 0.5f, 0.5f), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ })
        {
            var want = Vector3.Transform(probe, expected);
            var got = Vector3.Transform(probe, world);
            Assert.True((want - got).Length() < 1e-3f, $"{probe}: {Show(got)} expected {Show(want)}");
        }
    }

    [Fact]
    public void TheSpearTableMatchesTheReferenceTiers()
    {
        Assert.Equal(15, KineticWeapons.ForItem("wooden_spear")!.Value.DelayTicks);
        Assert.Equal(14, KineticWeapons.ForItem("stone_spear")!.Value.DelayTicks);
        Assert.Equal(13, KineticWeapons.ForItem("copper_spear")!.Value.DelayTicks);
        Assert.Equal(12, KineticWeapons.ForItem("iron_spear")!.Value.DelayTicks);
        Assert.Equal(14, KineticWeapons.ForItem("golden_spear")!.Value.DelayTicks);
        Assert.Equal(10, KineticWeapons.ForItem("diamond_spear")!.Value.DelayTicks);
        Assert.Equal(8, KineticWeapons.ForItem("netherite_spear")!.Value.DelayTicks);
        Assert.Null(KineticWeapons.ForItem("iron_sword"));
        Assert.Equal(0.38f, KineticWeapons.SpearForwardMovement);
    }

    private static string Show(Vector3 v) => string.Create(CultureInfo.InvariantCulture, $"({v.X:F5},{v.Y:F5},{v.Z:F5})");
}
