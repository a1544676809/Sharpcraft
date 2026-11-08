// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Text.RegularExpressions;
using Xunit;

namespace Sharpcraft.Architecture.Tests;

/// <summary>
/// Simplification registration gate (AGENTS.md principle 15). Active [Simplification] markers in src must have a
/// machine-readable entry in docs/modules; resolved markers may be removed while their documentation entry remains.
/// </summary>
public sealed class SimplificationRegistrationTests
{
    private static readonly Regex SourceMarker = new(
        """\[(?:[A-Za-z0-9_.]+\.)?Simplification\s*\(\s*"([^"]+)"\s*,\s*"([^"]+)" """.TrimEnd(),
        RegexOptions.CultureInvariant);

    private static readonly Regex DocMarker = new(
        """<!--\s*simplification:([A-Za-z0-9_.-]+)\s+task:([A-Za-z0-9_.-]+)\s*-->""",
        RegexOptions.CultureInvariant);

    [Fact]
    public void EveryActiveSimplificationHasDocumentation()
    {
        var documented = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(
                     Path.Combine(RepositoryLocator.Root, "docs", "modules"), "*.md", SearchOption.AllDirectories))
        {
            foreach (var line in File.ReadLines(file))
            {
                var match = DocMarker.Match(line);
                if (match.Success)
                {
                    documented[match.Groups[1].Value] = match.Groups[2].Value;
                }
            }
        }

        var errors = new List<string>();
        foreach (var file in Directory.EnumerateFiles(
                     Path.Combine(RepositoryLocator.Root, "src"), "*.cs", SearchOption.AllDirectories))
        {
            var lineNumber = 0;
            foreach (var line in File.ReadLines(file))
            {
                lineNumber++;
                foreach (Match match in SourceMarker.Matches(line))
                {
                    var id = match.Groups[1].Value;
                    var task = match.Groups[2].Value;
                    if (!documented.TryGetValue(id, out var documentedTask))
                    {
                        errors.Add($"{Path.GetRelativePath(RepositoryLocator.Root, file)}:{lineNumber}: {id} has no docs/modules marker");
                    }
                    else if (!string.Equals(task, documentedTask, StringComparison.Ordinal))
                    {
                        errors.Add($"{Path.GetRelativePath(RepositoryLocator.Root, file)}:{lineNumber}: {id} task={task}, docs task={documentedTask}");
                    }
                }
            }
        }

        Assert.True(errors.Count == 0, "simplification registration violations:\n" + string.Join("\n", errors));
    }
}
