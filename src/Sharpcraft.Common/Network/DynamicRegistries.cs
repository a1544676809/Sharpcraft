// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Nbt;

namespace Sharpcraft.Common.Network;

/// <summary>
/// Registries synchronised during the configuration phase
/// (docs/specs/network/protocol-phases.md 5.1).
/// </summary>
/// <remarks>
/// Entry ids are positional: a reference on the wire is an index into the order the server
/// sent, exactly like block state ids depend on registration order. Nothing else may reorder
/// the lists.
/// </remarks>
public sealed class DynamicRegistries
{
    private readonly Dictionary<string, List<Entry>> _registries = new(StringComparer.Ordinal);

    /// <summary>One registry entry with its optional payload.</summary>
    /// <param name="Id">Entry identifier.</param>
    /// <param name="Data">Payload NBT, when the registry carries data.</param>
    public readonly record struct Entry(Identifier Id, NbtCompound? Data);

    /// <summary>Names of the registries received so far.</summary>
    public IReadOnlyCollection<string> RegistryKeys => _registries.Keys;

    /// <summary>Total number of entries across all registries.</summary>
    public int TotalEntries => _registries.Values.Sum(entries => entries.Count);

    /// <summary>Stores (or replaces) a registry payload.</summary>
    public void Apply(Identifier registryId, IReadOnlyList<Entry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _registries[registryId.ToString()] = [.. entries];
    }

    /// <summary>Entries of a registry, or null when it was never sent.</summary>
    public IReadOnlyList<Entry>? Get(Identifier registryId) =>
        _registries.TryGetValue(registryId.ToString(), out var entries) ? entries : null;

    /// <summary>Position of an entry inside its registry, or null.</summary>
    public int? IdOf(Identifier registryId, Identifier entryId)
    {
        var entries = Get(registryId);
        if (entries is null)
        {
            return null;
        }

        for (var i = 0; i < entries.Count; i++)
        {
            if (entries[i].Id.Equals(entryId))
            {
                return i;
            }
        }

        return null;
    }

    /// <summary>Entry at a positional id, or null.</summary>
    public Entry? ById(Identifier registryId, int id)
    {
        var entries = Get(registryId);
        return entries is null || id < 0 || id >= entries.Count ? null : entries[id];
    }
}

/// <summary>Decoded dimension type facts needed to lay out chunks.</summary>
/// <param name="MinY">Lowest block Y.</param>
/// <param name="Height">Total height in blocks.</param>
/// <param name="LogicalHeight">Logical height used by the client for some checks.</param>
public sealed record DimensionTypeInfo(int MinY, int Height, int LogicalHeight)
{
    /// <summary>Number of 16-block sections in this dimension.</summary>
    public int SectionCount => Height / 16;

    /// <summary>Lowest section index.</summary>
    public int MinSectionY => MinY / 16;

    /// <summary>Reads the fields from a dimension type payload.</summary>
    public static DimensionTypeInfo? TryRead(NbtCompound? data)
    {
        if (data is null)
        {
            return null;
        }

        var minY = data.GetInt("min_y", int.MinValue);
        var height = data.GetInt("height", int.MinValue);
        if (minY == int.MinValue || height == int.MinValue || height <= 0 || height % 16 != 0)
        {
            return null;
        }

        return new DimensionTypeInfo(minY, height, data.GetInt("logical_height", height));
    }
}

/// <summary>
/// Parses the configuration-phase <c>registry_data</c> packet.
/// </summary>
public static class RegistryDataPackets
{
    /// <summary>Reads a registry payload.</summary>
    public static DecodeStatus TryRead(
        ReadOnlySpan<byte> body,
        out Identifier registryId,
        out IReadOnlyList<DynamicRegistries.Entry> entries)
    {
        registryId = default;
        entries = [];

        var reader = new PayloadReader(body);
        var status = reader.TryReadVarInt(out var packetId);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        if (packetId != PacketIds.ConfigurationClientbound.RegistryData)
        {
            return DecodeStatus.Invalid;
        }

        status = reader.TryReadString(out var registryKey, 32767);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        status = reader.TryReadVarInt(out var count);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        if (count < 0 || count > 4096)
        {
            return DecodeStatus.Invalid;
        }

        var parsed = new List<DynamicRegistries.Entry>(count);
        for (var i = 0; i < count; i++)
        {
            status = reader.TryReadString(out var entryKey, 32767);
            if (status != DecodeStatus.Success)
            {
                return status;
            }

            status = reader.TryReadBool(out var hasData);
            if (status != DecodeStatus.Success)
            {
                return status;
            }

            NbtCompound? data = null;
            if (hasData)
            {
                var remaining = body[reader.Position..];
                status = NbtReader.TryReadNetworkRoot(remaining, out data, out var consumed);
                if (status != DecodeStatus.Success)
                {
                    return status;
                }

                reader.Skip(consumed);
            }

            parsed.Add(new DynamicRegistries.Entry(Identifier.Parse(entryKey), data));
        }

        registryId = Identifier.Parse(registryKey);
        entries = parsed;
        return DecodeStatus.Success;
    }
}
