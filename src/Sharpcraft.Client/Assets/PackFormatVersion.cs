// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Assets;

/// <summary>
/// A pack format number, major plus the minor that was introduced with 25w31a
/// (docs/analysis/assets/resource-pack-format.md).
/// </summary>
/// <param name="Major">Major number.</param>
/// <param name="Minor">Minor number; zero for formats that predate minors.</param>
public readonly record struct PackFormatVersion(int Major, int Minor) : IComparable<PackFormatVersion>
{
    /// <summary>Parses <c>97</c>, <c>97.1</c> or <c>[97, 97]</c>-style pairs already split by the caller.</summary>
    /// <param name="text">Text such as <c>96.0</c>.</param>
    public static bool TryParse(string? text, out PackFormatVersion version)
    {
        version = default;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = text.Split('.');
        var majorText = parts[0].Trim();

        if (!int.TryParse(majorText, out var major))
        {
            return false;
        }

        var minor = 0;

        if (parts.Length > 1 && !int.TryParse(parts[1].Trim(), out minor))
        {
            return false;
        }

        version = new PackFormatVersion(major, minor);
        return true;
    }

    /// <inheritdoc />
    public int CompareTo(PackFormatVersion other) =>
        Major != other.Major ? Major.CompareTo(other.Major) : Minor.CompareTo(other.Minor);

    /// <inheritdoc />
    public override string ToString() => $"{Major}.{Minor}";
}
