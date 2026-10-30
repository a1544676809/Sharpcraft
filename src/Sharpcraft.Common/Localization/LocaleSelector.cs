// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Localization;

/// <summary>
/// Chooses the language file to load from the machine's locale (docs/modules/ui.md 8.4).
/// </summary>
/// <remarks>
/// The reference shows the interface in the player's own language, so a client that only ever loads English is
/// visibly different from the original on a Chinese machine: the same message appears as a sentence on one screen and
/// as an English sentence on the other. The language files use a name such as <c>zh_cn</c>, while the platform reports
/// something like <c>zh-Hans-CN</c>, so candidates are produced from the most specific form down to the language
/// alone, with English last.
/// </remarks>
public static class LocaleSelector
{
    /// <summary>Language used when nothing else is available.</summary>
    public const string Fallback = "en_us";

    /// <summary>
    /// Candidate language names for a platform culture name, most specific first.
    /// </summary>
    /// <param name="cultureName">A name such as <c>zh-Hans-CN</c>, <c>en-US</c> or <c>pt-BR</c>.</param>
    public static string[] Candidates(string? cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
        {
            return [Fallback];
        }

        var normalised = cultureName.Trim().Replace('-', '_').ToLowerInvariant();
        var parts = normalised.Split('_', StringSplitOptions.RemoveEmptyEntries);
        var candidates = new List<string>(4);

        Add(candidates, normalised);

        if (parts.Length >= 3)
        {
            // zh_hans_cn has no file of its own; the language and region pair does.
            Add(candidates, $"{parts[0]}_{parts[^1]}");
        }

        if (parts.Length >= 2)
        {
            Add(candidates, $"{parts[0]}_{parts[1]}");
        }

        Add(candidates, parts[0]);
        Add(candidates, Fallback);

        return [.. candidates];
    }

    private static void Add(List<string> candidates, string value)
    {
        if (value.Length > 0 && !candidates.Contains(value, StringComparer.Ordinal))
        {
            candidates.Add(value);
        }
    }
}
