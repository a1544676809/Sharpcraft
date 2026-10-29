// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Ui;

/// <summary>One element of an interface document.</summary>
/// <param name="Name">Element name, such as <c>Screen</c>.</param>
/// <param name="Attributes">Attribute values as written in the document.</param>
/// <param name="Children">Child elements, in document order.</param>
public sealed record UiNode(string Name, IReadOnlyDictionary<string, string> Attributes, IReadOnlyList<UiNode> Children)
{
    /// <summary>Reads an attribute, or a fallback when absent.</summary>
    /// <param name="name">Attribute name.</param>
    /// <param name="fallback">Value to use when absent.</param>
    public string Get(string name, string fallback = "") =>
        Attributes.TryGetValue(name, out var value) ? value : fallback;
}

/// <summary>
/// Reads the small XAML subset the interface layer understands (docs/modules/ui.md 7).
/// </summary>
/// <remarks>
/// Deliberately small and loud: unknown elements and unknown attributes are rejected with a message that names
/// the offending item, because a silently ignored property is far more expensive to debug than a refusal at load
/// time. Supported today are <c>Screen</c>, <c>StackPanel</c> and <c>TextBlock</c>, the three the kernel needs.
/// </remarks>
public static class UiMarkup
{
    private static readonly Dictionary<string, string[]> Allowed = new(StringComparer.Ordinal)
    {
        ["Screen"] = ["Background", "Padding"],
        ["StackPanel"] = ["Background", "Padding", "Spacing"],
        ["TextBlock"] = ["Text", "Colour", "Height"]
    };

    /// <summary>Parses a document into a node tree.</summary>
    /// <param name="text">Document text.</param>
    /// <exception cref="FormatException">The text is not well formed or uses an unsupported element or attribute.</exception>
    public static UiNode Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var position = 0;
        var root = ParseElement(text, ref position);

        SkipWhitespaceAndComments(text, ref position);

        if (position != text.Length)
        {
            throw new FormatException($"unexpected trailing content at offset {position}");
        }

        if (root.Name != "Screen")
        {
            throw new FormatException($"the root element must be Screen, found {root.Name}");
        }

        return root;
    }

    private static UiNode ParseElement(string text, ref int position)
    {
        SkipWhitespaceAndComments(text, ref position);

        if (position >= text.Length || text[position] != '<')
        {
            throw new FormatException($"expected an element at offset {position}");
        }

        position++;
        var name = ReadName(text, ref position);

        if (!Allowed.TryGetValue(name, out var allowedAttributes))
        {
            throw new FormatException($"unknown element {name}");
        }

        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
        var selfClosing = false;

        while (true)
        {
            SkipWhitespace(text, ref position);

            if (position >= text.Length)
            {
                throw new FormatException($"element {name} is not closed");
            }

            if (text[position] == '/')
            {
                position++;
                Expect(text, ref position, '>');
                selfClosing = true;
                break;
            }

            if (text[position] == '>')
            {
                position++;
                break;
            }

            var attributeName = ReadName(text, ref position);

            if (!allowedAttributes.Contains(attributeName))
            {
                throw new FormatException($"unknown attribute {attributeName} on {name}");
            }

            SkipWhitespace(text, ref position);
            Expect(text, ref position, '=');
            SkipWhitespace(text, ref position);
            attributes[attributeName] = ReadQuoted(text, ref position);
        }

        var children = new List<UiNode>();

        if (selfClosing)
        {
            return new UiNode(name, attributes, children);
        }

        while (true)
        {
            SkipWhitespaceAndComments(text, ref position);

            if (position >= text.Length)
            {
                throw new FormatException($"element {name} is not closed");
            }

            if (text[position] == '<' && position + 1 < text.Length && text[position + 1] == '/')
            {
                position += 2;
                var closing = ReadName(text, ref position);
                Expect(text, ref position, '>');

                if (closing != name)
                {
                    throw new FormatException($"element {name} closed by {closing}");
                }

                return new UiNode(name, attributes, children);
            }

            children.Add(ParseElement(text, ref position));
        }
    }

    private static void SkipWhitespace(string text, ref int position)
    {
        while (position < text.Length && char.IsWhiteSpace(text[position]))
        {
            position++;
        }
    }

    private static void SkipWhitespaceAndComments(string text, ref int position)
    {
        while (true)
        {
            SkipWhitespace(text, ref position);

            if (position + 3 < text.Length && text.AsSpan(position, 4).SequenceEqual("<!--"))
            {
                var end = text.IndexOf("-->", position + 4, StringComparison.Ordinal);

                if (end < 0)
                {
                    throw new FormatException("unterminated comment");
                }

                position = end + 3;
                continue;
            }

            return;
        }
    }

    private static string ReadName(string text, ref int position)
    {
        var start = position;

        while (position < text.Length && (char.IsLetterOrDigit(text[position]) || text[position] is '_' or ':' or '.'))
        {
            position++;
        }

        if (position == start)
        {
            throw new FormatException($"expected a name at offset {position}");
        }

        return text[start..position];
    }

    private static string ReadQuoted(string text, ref int position)
    {
        if (position >= text.Length || text[position] != '"')
        {
            throw new FormatException($"expected a quoted value at offset {position}");
        }

        position++;
        var end = text.IndexOf('"', position);

        if (end < 0)
        {
            throw new FormatException("unterminated attribute value");
        }

        var value = text[position..end];
        position = end + 1;
        return value;
    }

    private static void Expect(string text, ref int position, char expected)
    {
        if (position >= text.Length || text[position] != expected)
        {
            throw new FormatException($"expected '{expected}' at offset {position}");
        }

        position++;
    }
}
