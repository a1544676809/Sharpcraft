// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>DataComponents.KINETIC_WEAPON</c> (26.3) reduced to what the client animation reads, plus the per-tier
/// values registered by <c>Item.Properties.spear(...)</c>.
/// </summary>
/// <remarks>
/// The component is not present in <c>items/*.json</c> — it is assembled in code from
/// <c>spear(material, attackDuration, damageMultiplier, delay, dismountTime, dismountThreshold, knockbackTime,
/// knockbackThreshold, damageTime, damageThreshold)</c>, whose tick fields are
/// <c>(int)(seconds * 20)</c> and whose <c>forwardMovement</c> is the constant <c>0.38</c>. Only the tick counts and
/// <c>forwardMovement</c> reach the animation; the sound, damage and condition thresholds are combat-only.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.item.component.KineticWeapon", "ref://mc-26.3-analysis/src/net/minecraft/world/item/component/KineticWeapon.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Only the fields the client animation reads: tick counts and forward movement")]
public readonly record struct KineticWeapon(
    int DelayTicks,
    int DismountMaxTicks,
    int KnockbackMaxTicks,
    int DamageMaxTicks,
    float ForwardMovement);

/// <summary>Per-tier kinetic weapon values for the seven spears of 26.3.</summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.item.Item", "ref://mc-26.3-analysis/src/net/minecraft/world/item/Item.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Item.Properties.spear(...) kinetic weapon constants per tier")]
public static class KineticWeapons
{
    /// <summary>Reference <c>spear(...)</c>: the forward movement every spear shares.</summary>
    public const float SpearForwardMovement = 0.38f;

    private static readonly Dictionary<string, KineticWeapon> Spears = new(StringComparer.Ordinal)
    {
        // delay, dismountTime, knockbackTime, damageTime in seconds; the reference multiplies by 20 and truncates.
        ["wooden_spear"] = Spear(0.75f, 5.0f, 10.0f, 15.0f),
        ["stone_spear"] = Spear(0.70f, 4.5f, 9.0f, 13.75f),
        ["copper_spear"] = Spear(0.65f, 4.0f, 8.25f, 12.5f),
        ["iron_spear"] = Spear(0.60f, 2.5f, 6.75f, 11.25f),
        ["golden_spear"] = Spear(0.70f, 3.5f, 8.5f, 13.75f),
        ["diamond_spear"] = Spear(0.50f, 3.0f, 6.5f, 10.0f),
        ["netherite_spear"] = Spear(0.40f, 2.5f, 5.5f, 8.75f),
    };

    /// <summary>Resolves the kinetic weapon for an item name such as <c>iron_spear</c>, or null when it has none.</summary>
    public static KineticWeapon? ForItem(string? itemName) =>
        itemName is not null && Spears.TryGetValue(itemName, out var weapon) ? weapon : null;

    private static KineticWeapon Spear(float delay, float dismountTime, float knockbackTime, float damageTime) =>
        new(
            (int)(delay * 20f),
            (int)(dismountTime * 20f),
            (int)(knockbackTime * 20f),
            (int)(damageTime * 20f),
            SpearForwardMovement);
}
