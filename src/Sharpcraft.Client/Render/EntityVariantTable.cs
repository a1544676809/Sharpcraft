// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.World;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Network;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Turns an entity's raw variant value into a model definition.
/// </summary>
/// <remarks>
/// The value on the wire is positional: for registry-backed types it indexes the registry the server sent during
/// configuration, so the name has to come from that same connection (docs/analysis/render/entity-variants.md 3).
/// When no registry data is available (sandbox, offscreen, a server that skipped it) the vanilla bootstrap order is used
/// instead, which matches vanilla defaults but cannot survive a datapack that adds or reorders variants.
/// <para>
/// Unknown variants never crash the render path: they fall back to the type's default variant and are recorded so the
/// host can report them. <see cref="ResolveStrict"/> is the loud variant used by tools and tests
/// (docs/analysis/render/entity-variants.md 6).
/// </para>
/// </remarks>
public sealed class EntityVariantTable
{
    private readonly Dictionary<(int TypeId, int Value), string> _names = new();
    private readonly HashSet<string> _unknown = new(StringComparer.Ordinal);

    /// <summary>Table built from the vanilla bootstrap order, used before/without registry data.</summary>
    public static EntityVariantTable Builtin { get; } = new(null);

    /// <summary>Builds a table from the configuration-phase registries, falling back to the bootstrap order per type.</summary>
    public static EntityVariantTable FromRegistries(DynamicRegistries? registries) =>
        registries is null ? Builtin : new EntityVariantTable(registries);

    private EntityVariantTable(DynamicRegistries? registries)
    {
        foreach (var pair in EntityVariantSpecs.All)
        {
            var typeId = pair.Key;
            var spec = pair.Value;

            if (spec.RegistryPath is { Length: > 0 } path &&
                registries?.Get(new Identifier(Identifier.DefaultNamespace, path)) is { Count: > 0 } entries)
            {
                for (var i = 0; i < entries.Count; i++)
                {
                    _names[(typeId, i)] = entries[i].Id.Path;
                }

                continue;
            }

            for (var i = 0; i < spec.Keys.Count; i++)
            {
                _names[(typeId, i)] = spec.Keys[i];
            }
        }
    }

    /// <summary>Distinct unknown variant keys seen so far (for example "101:custom_pig").</summary>
    public IReadOnlyCollection<string> UnknownVariants => _unknown;

    /// <summary>Variant name for a raw value, or the type's default name when the value is unknown.</summary>
    public string KeyFor(int typeId, int variantId)
    {
        if (!EntityVariantSpecs.TryGet(typeId, out var spec))
        {
            return string.Empty;
        }

        var value = variantId & spec.ValueMask;
        if (spec.KeySelector is { } selector)
        {
            return selector(variantId);
        }

        return _names.TryGetValue((typeId, value), out var name) ? name : spec.DefaultKey;
    }

    /// <summary>Resolves a raw variant value, falling back to the default variant and recording a miss.</summary>
    public EntityModelDefinition Resolve(int typeId, int variantId, bool baby = false)
    {
        if (!EntityVariantSpecs.TryGet(typeId, out var spec))
        {
            return EntityModelResolver.Resolve(typeId, baby);
        }

        var value = variantId & spec.ValueMask;
        var known = spec.KeySelector is { } selector
            ? (name: selector(variantId), found: true)
            : _names.TryGetValue((typeId, value), out var mapped)
                ? (name: mapped, found: true)
                : (name: string.Empty, found: false);
        if (known.found && EntityModelResolver.TryResolveVariant(typeId, known.name, baby) is { } definition)
        {
            return definition;
        }

        _unknown.Add($"{typeId}:{(known.found ? known.name : value.ToString(System.Globalization.CultureInfo.InvariantCulture))}");
        return EntityModelResolver.TryResolveVariant(typeId, spec.DefaultKey, baby)
            ?? EntityModelResolver.Resolve(typeId, baby);
    }

    /// <summary>Resolves a raw variant value, throwing when no built-in definition exists.</summary>
    /// <exception cref="KeyNotFoundException">The value has no registered variant definition.</exception>
    public EntityModelDefinition ResolveStrict(int typeId, int variantId, bool baby = false)
    {
        if (!EntityVariantSpecs.TryGet(typeId, out var spec))
        {
            throw new KeyNotFoundException($"entity type {typeId} has no variant field");
        }

        var value = variantId & spec.ValueMask;
        if (spec.KeySelector is { } selector)
        {
            return EntityModelResolver.ResolveVariantStrict(typeId, selector(variantId), baby);
        }

        if (!_names.TryGetValue((typeId, value), out var name))
        {
            throw new KeyNotFoundException($"variant value {value} is not in the registry for entity type {typeId}");
        }

        return EntityModelResolver.ResolveVariantStrict(typeId, name, baby);
    }
}
