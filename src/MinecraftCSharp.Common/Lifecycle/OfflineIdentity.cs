// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace MinecraftCSharp.Common.Lifecycle;

/// <summary>
/// Offline identity derivation, shared by the launcher (which decides) and the client (which may
/// receive a name without a UUID). The rule is vanilla's: a name-based v3 UUID over
/// <c>"OfflinePlayer:" + name</c>, so servers treat the same offline name as the same player.
/// Evidence: <c>ref://mc-26.3-analysis/src/net/minecraft/core/UUIDUtil.java</c>.
/// </summary>
public static class OfflineIdentity
{
    /// <summary>Prefix vanilla uses when deriving an offline UUID.</summary>
    public const string UuidPrefix = "OfflinePlayer:";

    /// <summary>Shortest accepted profile name.</summary>
    public const int MinNameLength = 3;

    /// <summary>Longest accepted profile name.</summary>
    public const int MaxNameLength = 16;

    /// <summary>Validates a profile name the way the official client does.</summary>
    public static bool IsValidName(string? name) =>
        !string.IsNullOrEmpty(name) &&
        name.Length is >= MinNameLength and <= MaxNameLength &&
        name.All(character => char.IsAsciiLetterOrDigit(character) || character == '_');

    /// <summary>Derives the offline UUID for a name.</summary>
    /// <exception cref="ArgumentException">The name is not a valid profile name.</exception>
    public static Guid DeriveUuid(string name)
    {
        if (!IsValidName(name))
        {
            throw new ArgumentException(
                $"invalid profile name '{name}': {MinNameLength}-{MaxNameLength} characters, " +
                "letters, digits and underscores only",
                nameof(name));
        }

        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(UuidPrefix + name));

        // RFC 4122: version 3 (name based, MD5) and the IETF variant.
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x30);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        return new Guid(bytes, bigEndian: true);
    }

    /// <summary>Formats a UUID the way the launcher passes it on the command line.</summary>
    public static string FormatUuid(Guid uuid) => uuid.ToString("D", CultureInfo.InvariantCulture);
}
