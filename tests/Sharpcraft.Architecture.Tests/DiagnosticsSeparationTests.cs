// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Docs.Indexer;
using Xunit;

namespace Sharpcraft.Architecture.Tests;

/// <summary>
/// Diagnostics separation (AGENTS.md principle 13): the game assemblies must not read the environment, so removing the
/// host's diagnostics folder takes every debug switch with it and leaves the game unchanged.
/// </summary>
public sealed class DiagnosticsSeparationTests
{
    private static readonly string[] GameAssemblies = ["Sharpcraft.Framework", "Sharpcraft.Client", "Sharpcraft.Common"];

    [Fact]
    public void TheGameAssembliesReadNoEnvironmentVariables()
    {
        var offenders = new List<string>();

        foreach (var assembly in GameAssemblies)
        {
            var directory = Path.Combine(RepositoryLocator.Root, "src", assembly);

            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            {
                var lines = File.ReadAllLines(file);

                for (var line = 0; line < lines.Length; line++)
                {
                    if (lines[line].Contains("GetEnvironmentVariable", StringComparison.Ordinal))
                    {
                        offenders.Add($"{Path.GetRelativePath(RepositoryLocator.Root, file)}:{line + 1}: {lines[line].Trim()}");
                    }
                }
            }
        }

        Assert.True(offenders.Count == 0,
            "the game must not read the environment; move the switch into the host's diagnostics folder\n"
            + string.Join("\n", offenders));
    }
}
