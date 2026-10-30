// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Text;
using System.Text.Json;

namespace Sharpcraft.Common.Localization;

/// <summary>
/// The translation table the server's messages are written against (docs/modules/ui.md 8.4).
/// </summary>
/// <remarks>
/// Chat arrives as translation keys rather than sentences: a server says "command.unknown.command", and the client is
/// expected to look it up and fill in its arguments. Without this table the interface shows keys, which is at least
/// honest but is not what the original shows. The language file is an asset, extracted from the game by the launcher
/// like every other asset, so nothing of it enters the repository.
/// </remarks>
public sealed class LanguageTable
{
    private readonly Dictionary<string, string> _entries;

    private LanguageTable(Dictionary<string, string> entries) => _entries = entries;

    /// <summary>A table with no entries, which renders every key as itself.</summary>
    public static LanguageTable Empty { get; } = new([]);

    /// <summary>How many keys the table holds.</summary>
    public int Count => _entries.Count;

    /// <summary>Parses a language file.</summary>
    /// <param name="json">Contents of a language file, which is a flat object of key to text.</param>
    /// <exception cref="JsonException">The document is not a JSON object.</exception>
    public static LanguageTable Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        using var document = JsonDocument.Parse(json);

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("a language file is a JSON object of key to text");
        }

        var entries = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var property in document.RootElement.EnumerateObject())
        {
            // Values that are not text are skipped rather than rejected: a language file is data from the game and
            // one unusual entry must not cost the whole table.
            if (property.Value.ValueKind == JsonValueKind.String)
            {
                entries[property.Name] = property.Value.GetString() ?? string.Empty;
            }
        }

        return new LanguageTable(entries);
    }

    /// <summary>Looks a key up.</summary>
    /// <param name="key">Translation key.</param>
    /// <param name="value">Text when the key is known.</param>
    public bool TryGet(string key, out string value)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _entries.TryGetValue(key, out value!);
    }

    /// <summary>Text for a key, or the key itself when it is unknown.</summary>
    /// <param name="key">Translation key.</param>
    public string Resolve(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _entries.TryGetValue(key, out var value) ? value : key;
    }

    /// <summary>
    /// Text for a key with its arguments substituted.
    /// </summary>
    /// <param name="key">Translation key.</param>
    /// <param name="arguments">Arguments, already flattened to text.</param>
    /// <remarks>
    /// The reference's files use <c>%s</c> for the next argument and <c>%1$s</c> for a numbered one, and
    /// <c>%%</c> for a literal percent sign. Those three are what chat actually needs; other specifiers are left
    /// alone rather than guessed at.
    /// </remarks>
    public string Format(string key, ReadOnlySpan<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (!_entries.TryGetValue(key, out var pattern))
        {
            return key;
        }

        if (pattern.IndexOf('%') < 0)
        {
            return pattern;
        }

        var builder = new StringBuilder(pattern.Length + 16);
        var next = 0;

        for (var index = 0; index < pattern.Length; index++)
        {
            var character = pattern[index];

            if (character != '%' || index + 1 >= pattern.Length)
            {
                builder.Append(character);
                continue;
            }

            var cursor = index + 1;

            if (pattern[cursor] == '%')
            {
                builder.Append('%');
                index = cursor;
                continue;
            }

            var numbered = -1;
            var digitsStart = cursor;

            while (cursor < pattern.Length && char.IsAsciiDigit(pattern[cursor]))
            {
                cursor++;
            }

            if (cursor > digitsStart && cursor < pattern.Length && pattern[cursor] == '$')
            {
                numbered = int.Parse(pattern.AsSpan(digitsStart, cursor - digitsStart)) - 1;
                cursor++;
            }
            else
            {
                cursor = index + 1;
            }

            if (cursor < pattern.Length && pattern[cursor] == 's')
            {
                var argument = numbered >= 0 ? numbered : next++;

                if ((uint)argument < (uint)arguments.Length)
                {
                    builder.Append(arguments[argument]);
                }

                index = cursor;
                continue;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }
}
