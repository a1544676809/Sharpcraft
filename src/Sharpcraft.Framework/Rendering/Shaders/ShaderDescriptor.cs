// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Text.RegularExpressions;

namespace Sharpcraft.Framework.Rendering.Shaders;

/// <summary>
/// What a shader file is expected to declare, checked before any pipeline is created.
/// </summary>
/// <param name="Name">Pipeline name, used in messages.</param>
/// <param name="File">Shader file the declaration comes from.</param>
/// <param name="EntryPoints">Entry points the descriptor will ask for.</param>
/// <param name="Bindings">Binding numbers the layout will declare.</param>
public sealed partial record ShaderDescriptor(
    string Name,
    string File,
    IReadOnlyList<string> EntryPoints,
    IReadOnlyList<int> Bindings)
{
    [GeneratedRegex(@"@binding\((\d+)\)")]
    private static partial Regex BindingPattern();

    [GeneratedRegex(@"@(vertex|fragment|compute)\s+fn\s+(\w+)")]
    private static partial Regex EntryPattern();

    /// <summary>Binding numbers declared in a shader source.</summary>
    /// <param name="source">Composed shader source.</param>
    public static SortedSet<int> DeclaredBindings(string source)
    {
        var found = new SortedSet<int>();

        foreach (Match match in BindingPattern().Matches(source))
        {
            found.Add(int.Parse(match.Groups[1].Value));
        }

        return found;
    }

    /// <summary>Entry point names declared in a shader source.</summary>
    /// <param name="source">Composed shader source.</param>
    public static SortedSet<string> DeclaredEntries(string source)
    {
        var found = new SortedSet<string>(StringComparer.Ordinal);

        foreach (Match match in EntryPattern().Matches(source))
        {
            found.Add(match.Groups[2].Value);
        }

        return found;
    }

    /// <summary>
    /// Compares what the file declares with what the descriptor expects.
    /// </summary>
    /// <param name="source">Composed shader source.</param>
    /// <returns>Human readable violations; empty when the two agree.</returns>
    public IReadOnlyList<string> Validate(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var violations = new List<string>();
        var declaredBindings = DeclaredBindings(source);
        var expectedBindings = new SortedSet<int>(Bindings);

        foreach (var missing in expectedBindings.Where(binding => !declaredBindings.Contains(binding)))
        {
            violations.Add($"{Name} ({File}): layout declares binding {missing} but the shader does not");
        }

        foreach (var extra in declaredBindings.Where(binding => !expectedBindings.Contains(binding)))
        {
            violations.Add($"{Name} ({File}): shader declares binding {extra} but the layout does not");
        }

        var declaredEntries = DeclaredEntries(source);

        foreach (var entry in EntryPoints.Where(entry => !declaredEntries.Contains(entry)))
        {
            violations.Add($"{Name} ({File}): entry point '{entry}' is missing");
        }

        return violations;
    }
}
