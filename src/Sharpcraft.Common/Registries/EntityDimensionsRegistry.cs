// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Registries;

/// <summary>Entity collision/eye dimensions from the vanilla entity-type registration order (metadata, not assets).</summary>
public readonly record struct EntityDimensionsInfo(float Width, float Height, float EyeHeight);

/// <summary>
/// Entity-type dimensions table captured from the 26.3 jar registration order. The client needs eye height to aim
/// per-head rotations (e.g. the Wither's side heads) exactly like the reference.
/// </summary>
public static class EntityDimensionsRegistry
{
    private static readonly Lazy<EntityDimensionsInfo[]> Entries = new(Load);

    /// <summary>Dimensions for a numeric entity type id, or null when unknown.</summary>
    public static EntityDimensionsInfo? Get(int typeId) =>
        (uint)typeId < (uint)Entries.Value.Length ? Entries.Value[typeId] : null;

    /// <summary>
    /// Pose- and age-aware dimensions, matching <c>LivingEntity.getDimensions(Pose)</c>: sleeping uses the shared
    /// 0.2×0.2 box, babies scale by 0.5, and players use their pose table (crouch 1.27, swim/fall-fly 0.4 eye).
    /// The generic SCALE attribute is not synced, so a non-1 scale is not represented.
    /// </summary>
    public static EntityDimensionsInfo? Get(int typeId, int pose, bool isBaby)
    {
        if (Get(typeId) is not { } baseDimensions)
        {
            return null;
        }

        // Pose ordinals: 0 standing, 1 fall flying, 2 sleeping, 3 swimming, 4 spin attack, 5 crouching.
        if (pose == 2)
        {
            return new EntityDimensionsInfo(0.2f, 0.2f, 0.2f);
        }

        if (typeId == 159) // player
        {
            return pose switch
            {
                1 or 3 or 4 => new EntityDimensionsInfo(0.6f, 0.6f, 0.4f),
                5 => new EntityDimensionsInfo(0.6f, 1.5f, 1.27f),
                _ => baseDimensions
            };
        }

        return isBaby
            ? new EntityDimensionsInfo(baseDimensions.Width * 0.5f, baseDimensions.Height * 0.5f, baseDimensions.EyeHeight * 0.5f)
            : baseDimensions;
    }

    private static EntityDimensionsInfo[] Load()
    {
        using var stream = typeof(EntityDimensionsRegistry).Assembly
            .GetManifestResourceStream("Sharpcraft.Common.Registries.Data.vanilla-entity-dimensions.tsv")
            ?? throw new InvalidOperationException("entity dimensions snapshot is missing");
        using var reader = new StreamReader(stream);

        var entries = new List<EntityDimensionsInfo>();
        while (reader.ReadLine() is { } line)
        {
            var parts = line.Split('\t');
            if (parts.Length < 5 || !int.TryParse(parts[0], out var id))
            {
                continue;
            }

            var width = float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);
            var height = float.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture);
            var eye = float.Parse(parts[4], System.Globalization.CultureInfo.InvariantCulture);
            while (entries.Count < id)
            {
                entries.Add(default);
            }

            if (entries.Count == id)
            {
                entries.Add(new EntityDimensionsInfo(width, height, eye));
            }
            else
            {
                entries[id] = new EntityDimensionsInfo(width, height, eye);
            }
        }

        return entries.ToArray();
    }
}
