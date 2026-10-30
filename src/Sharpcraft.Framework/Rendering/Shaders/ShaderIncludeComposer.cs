// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering.Shaders;

/// <summary>
/// Expands <c>//#include "path"</c> directives, since WGSL has no preprocessor.
/// </summary>
/// <remarks>
/// Rules from docs/specs/render/shader-organisation.md: each include is expanded once, nesting is limited, and a missing or
/// circular include fails with the pipeline name, the file and the offending line, because a silently missing declaration
/// turns into a struct mismatch at device creation with no clue where it came from.
/// </remarks>
public static class ShaderIncludeComposer
{
    /// <summary>Longest include chain allowed.</summary>
    public const int MaxDepth = 4;

    private const string Directive = "//#include ";

    /// <summary>Expands the directives of one shader.</summary>
    /// <param name="name">Shader name, used in messages.</param>
    /// <param name="source">Source text.</param>
    /// <param name="resolve">Maps an include path to its text, or null when absent.</param>
    public static string Compose(string name, string source, Func<string, string?> resolve)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(resolve);

        var expanded = new HashSet<string>(StringComparer.Ordinal);
        return ComposeCore(name, source, resolve, expanded, 0);
    }

    private static string ComposeCore(string name, string source, Func<string, string?> resolve, HashSet<string> expanded, int depth)
    {
        if (depth > MaxDepth)
        {
            throw new InvalidOperationException($"shader '{name}': include chain deeper than {MaxDepth}");
        }

        var lines = source.Replace("\r\n", "\n").Split('\n');
        var output = new System.Text.StringBuilder(source.Length * 2);

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var trimmed = line.TrimStart();

            if (!trimmed.StartsWith(Directive, StringComparison.Ordinal))
            {
                output.Append(line).Append('\n');
                continue;
            }

            var path = trimmed[Directive.Length..].Trim().Trim('"');

            if (path.Length == 0)
            {
                throw new InvalidOperationException($"shader '{name}' line {index + 1}: empty include");
            }

            if (!expanded.Add(path))
            {
                // Already inlined once; including it again would redeclare symbols.
                continue;
            }

            var included = resolve(path) ?? throw new InvalidOperationException(
                $"shader '{name}' line {index + 1}: include '{path}' not found");

            output.Append(ComposeCore(name, included, resolve, expanded, depth + 1)).Append('\n');
        }

        return output.ToString();
    }
}
