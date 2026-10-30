// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Text;
using Sharpcraft.Common.Localization;
using Sharpcraft.Common.Nbt;

namespace Sharpcraft.Common.Network;

/// <summary>A span of text that shares one appearance.</summary>
/// <param name="Text">Characters.</param>
/// <param name="Colour">Packed colour, white when the component carried none.</param>
/// <param name="Bold">True when the span is bold, which the reference draws by repeating the glyphs one pixel over.</param>
public readonly record struct TextRun(string Text, uint Colour, bool Bold);

/// <summary>
/// Flattens a chat component into styled runs (docs/modules/ui.md 8.4).
/// </summary>
/// <remarks>
/// A component is a tree where appearance is inherited: a node without a colour takes its parent's, and the default
/// is white. Flattening to a single string throws that away and makes every message the same colour, which is visibly
/// wrong for team names, warnings and errors. The sixteen named colours and the hex form are the reference's own,
/// taken from its colour table rather than from memory.
/// </remarks>
/// <summary>Runs together with the click and hover each one carries.</summary>
/// <param name="Runs">The text runs.</param>
/// <param name="Clicks">Click per run, parallel to <paramref name="Runs"/>.</param>
/// <param name="Hovers">Hover per run, parallel to <paramref name="Runs"/>.</param>
public readonly record struct StyledRuns(TextRun[] Runs, ChatClick?[] Clicks, TextRun[]?[] Hovers)
{
    /// <summary>An empty set.</summary>
    public static StyledRuns Empty => new([], [], []);
}

public static class ComponentRuns
{
    /// <summary>Default appearance: white, not bold.</summary>
    public const uint DefaultColour = 0xFFFFFFFFu;

    /// <summary>How deep the tree is followed.</summary>
    public const int MaxDepth = 16;

    private static readonly Dictionary<string, uint> NamedColours = new(StringComparer.Ordinal)
    {
        ["black"] = 0xFF000000u,
        ["dark_blue"] = 0xFF0000AAu,
        ["dark_green"] = 0xFF00AA00u,
        ["dark_aqua"] = 0xFF00AAAAu,
        ["dark_red"] = 0xFFAA0000u,
        ["dark_purple"] = 0xFFAA00AAu,
        ["gold"] = 0xFFFFAA00u,
        ["gray"] = 0xFFAAAAAAu,
        ["dark_gray"] = 0xFF555555u,
        ["blue"] = 0xFF5555FFu,
        ["green"] = 0xFF55FF55u,
        ["aqua"] = 0xFF55FFFFu,
        ["red"] = 0xFFFF5555u,
        ["light_purple"] = 0xFFFF55FFu,
        ["yellow"] = 0xFFFFFF55u,
        ["white"] = 0xFFFFFFFFu
    };

    /// <summary>Flattens a component into runs.</summary>
    /// <param name="tag">Component node.</param>
    /// <param name="language">Translation table, or null to leave keys unresolved.</param>
    public static TextRun[] Flatten(NbtTag? tag, LanguageTable? language = null)
    {
        var runs = new List<TextRun>(4);
        Append(runs, tag, DefaultColour, inheritedBold: false, depth: 0, language: language);
        return [.. runs];
    }

    /// <summary>
    /// Flattens a component and keeps the click and hover each run carries.
    /// </summary>
    /// <param name="tag">Component to flatten.</param>
    /// <param name="language">Translation table, or null.</param>
    /// <returns>The runs and, parallel to them, the interactions.</returns>
    /// <remarks>
    /// The interaction cannot ride on the run itself without changing a type used everywhere, so it is returned beside
    /// the runs and indexed the same way. A run with no interaction has null in both arrays at its position.
    /// </remarks>
    public static (TextRun[] Runs, ChatClick?[] Clicks, TextRun[]?[] Hovers) FlattenStyled(
        NbtTag? tag,
        LanguageTable? language = null)
    {
        var runs = new List<TextRun>(4);
        var clicks = new List<ChatClick?>(4);
        var hovers = new List<TextRun[]?>(4);
        Append(runs, tag, DefaultColour, inheritedBold: false, depth: 0, language: language, clicks: clicks, hovers: hovers);
        return ([.. runs], [.. clicks], [.. hovers]);
    }

    /// <summary>
    /// Applies a chat type decoration while keeping the click and hover each argument carries.
    /// </summary>
    /// <param name="language">Translation table, or null.</param>
    /// <param name="translationKey">Key such as <c>chat.type.text</c>.</param>
    /// <param name="arguments">One styled run set per placeholder, in order.</param>
    /// <returns>The decorated runs with their interactions.</returns>
    /// <remarks>
    /// A decorated message is where a clickable name or a clickable body most often appears, so rebuilding the runs
    /// without their interactions silently made every decorated line unclickable. The interaction lives beside the run,
    /// so the substitution has to carry both.
    /// </remarks>
    public static StyledRuns DecorateStyled(
        LanguageTable? language,
        string translationKey,
        ReadOnlySpan<StyledRuns> arguments)
    {
        ArgumentNullException.ThrowIfNull(translationKey);

        var format = language is not null && language.TryGet(translationKey, out var resolved)
            ? resolved
            : translationKey;

        var runs = new List<TextRun>(8);
        var clicks = new List<ChatClick?>(8);
        var hovers = new List<TextRun[]?>(8);
        var pending = new System.Text.StringBuilder();
        var next = 0;

        void Flush()
        {
            if (pending.Length == 0)
            {
                return;
            }

            runs.Add(new TextRun(pending.ToString(), DefaultColour, Bold: false));
            clicks.Add(null);
            hovers.Add(null);
            pending.Clear();
        }

        for (var index = 0; index < format.Length; index++)
        {
            if (format[index] != '%' || index + 1 >= format.Length)
            {
                pending.Append(format[index]);
                continue;
            }

            var marker = format[index + 1];

            if (marker == '%')
            {
                pending.Append('%');
                index++;
                continue;
            }

            if (marker == 's')
            {
                index++;

                if (next < arguments.Length)
                {
                    Flush();
                    var argument = arguments[next];
                    runs.AddRange(argument.Runs);
                    clicks.AddRange(argument.Clicks);
                    hovers.AddRange(argument.Hovers);
                    next++;
                }

                continue;
            }

            pending.Append(format[index]);
        }

        Flush();

        return new StyledRuns([.. runs], [.. clicks], [.. hovers]);
    }

    /// <summary>Looks up a colour name or hex value.</summary>
    /// <param name="value">A name such as <c>gold</c> or a value such as <c>#FFAA00</c>.</param>
    /// <param name="colour">Packed colour when recognised.</param>
    /// <summary>
    /// Applies a chat type decoration: a translation key whose <c>%s</c> placeholders are replaced by runs.
    /// </summary>
    /// <param name="language">Translation table, or null to leave the key itself visible.</param>
    /// <param name="translationKey">Key such as <c>chat.type.text</c>.</param>
    /// <param name="arguments">One run sequence per placeholder, in order.</param>
    /// <returns>The decorated runs.</returns>
    /// <remarks>
    /// Substitution happens on runs rather than on text, otherwise a coloured sender name would lose its colour the
    /// moment it is placed inside the decoration. Placeholders are matched in order, <c>%%</c> is a literal per cent
    /// sign, and a placeholder with no argument is dropped rather than shown as <c>%s</c>.
    /// </remarks>
    public static TextRun[] Decorate(
        LanguageTable? language,
        string translationKey,
        ReadOnlySpan<TextRun[]> arguments)
    {
        ArgumentNullException.ThrowIfNull(translationKey);

        var format = language is not null && language.TryGet(translationKey, out var resolved)
            ? resolved
            : translationKey;

        var runs = new List<TextRun>(8);
        var pending = new System.Text.StringBuilder();
        var next = 0;

        void Flush()
        {
            if (pending.Length > 0)
            {
                runs.Add(new TextRun(pending.ToString(), DefaultColour, Bold: false));
                pending.Clear();
            }
        }

        for (var index = 0; index < format.Length; index++)
        {
            if (format[index] != '%' || index + 1 >= format.Length)
            {
                pending.Append(format[index]);
                continue;
            }

            var marker = format[index + 1];

            if (marker == '%')
            {
                pending.Append('%');
                index++;
                continue;
            }

            if (marker == 's')
            {
                index++;

                if (next < arguments.Length)
                {
                    Flush();
                    runs.AddRange(arguments[next]);
                    next++;
                }

                continue;
            }

            pending.Append(format[index]);
        }

        Flush();

        return [.. runs];
    }

    public static bool TryParseColour(string value, out uint colour)
    {
        ArgumentNullException.ThrowIfNull(value);
        colour = 0;

        if (NamedColours.TryGetValue(value, out var named))
        {
            colour = named;
            return true;
        }

        if (value.Length == 7 && value[0] == '#')
        {
            var red = Hex(value.AsSpan(1, 2));
            var green = Hex(value.AsSpan(3, 2));
            var blue = Hex(value.AsSpan(5, 2));

            if (red < 0 || green < 0 || blue < 0)
            {
                return false;
            }

            colour = 0xFF000000u | ((uint)red << 16) | ((uint)green << 8) | (uint)blue;
            return true;
        }

        return false;
    }

    private static int Hex(ReadOnlySpan<char> digits)
    {
        var value = 0;

        for (var index = 0; index < digits.Length; index++)
        {
            var digit = digits[index] switch
            {
                >= '0' and <= '9' => digits[index] - '0',
                >= 'a' and <= 'f' => digits[index] - 'a' + 10,
                >= 'A' and <= 'F' => digits[index] - 'A' + 10,
                _ => -1
            };

            if (digit < 0)
            {
                return -1;
            }

            value = (value * 16) + digit;
        }

        return value;
    }

    private static void Append(
        List<TextRun> runs,
        NbtTag? tag,
        uint inheritedColour,
        bool inheritedBold,
        int depth,
        LanguageTable? language,
        List<ChatClick?>? clicks = null,
        List<TextRun[]?>? hovers = null)
    {
        if (tag is null || depth > MaxDepth)
        {
            return;
        }

        if (tag is NbtString literal)
        {
            runs.Add(new TextRun(literal.Value, inheritedColour, inheritedBold));
            clicks?.Add(null);
            hovers?.Add(null);
            return;
        }

        if (tag is not NbtCompound compound)
        {
            return;
        }

        var colour = inheritedColour;

        if (compound.GetString("color") is { } declared && TryParseColour(declared, out var parsed))
        {
            colour = parsed;
        }

        var bold = inheritedBold;

        if (compound.Get("bold") is NbtByte boldTag)
        {
            bold = boldTag.Value != 0;
        }

        var click = ChatInteractions.ReadClick(compound);
        var hover = ChatInteractions.ReadHover(compound, language);

        void Emit(string value)
        {
            runs.Add(new TextRun(value, colour, bold));
            clicks?.Add(click);
            hovers?.Add(hover);
        }

        var text = compound.GetString("text");

        if (text is not null)
        {
            Emit(text);
        }
        else if (compound.GetString("translate") is { } translate)
        {
            var arguments = FlattenArguments(compound, colour, bold, depth, language);
            var resolved = language is null ? translate : language.Format(translate, arguments);

            if (resolved.Length > 0)
            {
                Emit(resolved);
            }
        }
        else if (compound.GetString("fallback") is { } fallback)
        {
            Emit(fallback);
        }

        if (compound.Get("extra") is NbtList extra)
        {
            for (var index = 0; index < extra.Items.Count; index++)
            {
                Append(runs, extra.Items[index], colour, bold, depth + 1, language, clicks, hovers);
            }
        }
    }

    /// <summary>
    /// Flattens the <c>with</c> arguments of a translated component, one string per argument.
    /// </summary>
    /// <remarks>
    /// Each argument is flattened on its own so the count still matches the placeholders in the translation. Styling
    /// inside an argument is dropped here because the translation substitutes text, not runs; that is a recorded gap
    /// rather than an oversight.
    /// </remarks>
    private static string[] FlattenArguments(
        NbtCompound compound,
        uint colour,
        bool bold,
        int depth,
        LanguageTable? language)
    {
        if (compound.Get("with") is not NbtList arguments || arguments.Items.Count == 0)
        {
            return [];
        }

        var text = new string[arguments.Items.Count];

        for (var index = 0; index < text.Length; index++)
        {
            var runs = new List<TextRun>(2);
            Append(runs, arguments.Items[index], colour, bold, depth + 1, language);
            var builder = new StringBuilder();

            for (var run = 0; run < runs.Count; run++)
            {
                builder.Append(runs[run].Text);
            }

            text[index] = builder.ToString();
        }

        return text;
    }
}
