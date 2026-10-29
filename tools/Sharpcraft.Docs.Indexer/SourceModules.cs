// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Text;

namespace Sharpcraft.Docs.Indexer;

/// <summary>
/// Maps source directories to module documents and enforces the coverage ratchet.
///
/// The unit is a source directory, not a file: <c>src/Sharpcraft.Client/Render/</c> maps to
/// <c>docs/src/sharpcraft-client/render.md</c>. The mapping is a pure transformation of the
/// source tree, so there is no hand-maintained list to drift (docs/modules/doc-indexer.md).
/// </summary>
public static class SourceModules
{
    /// <summary>Checked-in list of source directories that still have no module document.</summary>
    public const string BaselineRelativePath = "docs/src/.coverage-baseline.txt";

    /// <summary>Returns every source directory that contains C# files, relative to the repository.</summary>
    public static IReadOnlyList<string> Enumerate(string repositoryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        var srcRoot = Path.Combine(repositoryRoot, "src");
        if (!Directory.Exists(srcRoot))
        {
            return [];
        }

        var modules = new List<string>();
        foreach (var project in Directory.EnumerateDirectories(srcRoot))
        {
            foreach (var directory in Directory.EnumerateDirectories(project, "*", SearchOption.AllDirectories).Prepend(project))
            {
                if (IsBuildOutput(directory))
                {
                    continue;
                }

                if (!Directory.EnumerateFiles(directory, "*.cs", SearchOption.TopDirectoryOnly).Any())
                {
                    continue;
                }

                modules.Add(Relative(repositoryRoot, directory));
            }
        }

        modules.Sort(StringComparer.Ordinal);
        return modules;
    }

    /// <summary>Module document path for a source directory, e.g. src/A/B -> docs/src/a/b.md.</summary>
    public static string ExpectedDocPath(string sourceDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);

        var relative = sourceDirectory.Replace('\\', '/').TrimEnd('/');
        if (!relative.StartsWith("src/", StringComparison.Ordinal))
        {
            throw new ArgumentException($"not a source directory: {sourceDirectory}", nameof(sourceDirectory));
        }

        var parts = relative["src/".Length..].Split('/');
        var project = Kebab(parts[0]);
        if (parts.Length == 1)
        {
            return $"docs/src/{project}.md";
        }

        var builder = new StringBuilder("docs/src/").Append(project).Append('/');
        for (var index = 1; index < parts.Length; index++)
        {
            if (index > 1)
            {
                builder.Append('/');
            }

            builder.Append(Kebab(parts[index]));
        }

        return builder.Append(".md").ToString();
    }

    /// <summary>Returns every coverage violation; an empty list means the ratchet is satisfied.</summary>
    public static IReadOnlyList<string> Check(string repositoryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        var modules = Enumerate(repositoryRoot);
        var moduleSet = modules.ToHashSet(StringComparer.Ordinal);
        var baseline = ReadBaseline(repositoryRoot);
        var expectedDocs = modules.ToDictionary(module => module, ExpectedDocPath, StringComparer.Ordinal);
        var violations = new List<string>();

        foreach (var module in modules)
        {
            var doc = expectedDocs[module];
            var hasDoc = File.Exists(Path.Combine(repositoryRoot, doc.Replace('/', Path.DirectorySeparatorChar)));
            if (hasDoc)
            {
                if (baseline.Contains(module))
                {
                    violations.Add($"{module}: documented; drop it from {BaselineRelativePath} (run --update-baseline)");
                }

                continue;
            }

            if (!baseline.Contains(module))
            {
                violations.Add($"{module}: no module doc at {doc}");
            }
        }

        foreach (var entry in baseline)
        {
            if (!moduleSet.Contains(entry))
            {
                violations.Add($"{BaselineRelativePath}: '{entry}' is not a source module (run --update-baseline)");
            }
        }

        var docsRoot = Path.Combine(repositoryRoot, "docs", "src");
        if (Directory.Exists(docsRoot))
        {
            foreach (var file in Directory.EnumerateFiles(docsRoot, "*.md", SearchOption.AllDirectories))
            {
                var relative = Relative(repositoryRoot, file);

                // README.md files are the generated directory indexes, not module documents.
                if (Path.GetFileName(file).Equals("README.md", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!expectedDocs.ContainsValue(relative))
                {
                    violations.Add($"{relative}: does not correspond to a source module");
                }
            }
        }

        return violations;
    }

    /// <summary>Rewrites the baseline with the modules that still lack a document.</summary>
    public static IReadOnlyList<string> UpdateBaseline(string repositoryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        var undocumented = Enumerate(repositoryRoot)
            .Where(module => !File.Exists(Path.Combine(repositoryRoot, ExpectedDocPath(module).Replace('/', Path.DirectorySeparatorChar))))
            .ToList();

        var path = Path.Combine(repositoryRoot, BaselineRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var content = "# Source directories that still lack a module doc (docs/src/<project>/<module>.md).\n"
            + "# Generated by tools/Sharpcraft.Docs.Indexer --update-baseline; shrink it by writing docs.\n"
            + string.Join('\n', undocumented) + "\n";
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return undocumented;
    }

    private static HashSet<string> ReadBaseline(string repositoryRoot)
    {
        var path = Path.Combine(repositoryRoot, BaselineRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        return File.ReadAllLines(path)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Lower-case kebab form of a source segment (Sharpcraft.Client -> sharpcraft-client).</summary>
    private static string Kebab(string value)
    {
        var builder = new StringBuilder(value.Length + 4);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (char.IsUpper(character))
            {
                var previous = index > 0 ? value[index - 1] : '\0';
                var next = index + 1 < value.Length ? value[index + 1] : '\0';
                if (index > 0 && (char.IsLower(previous) || char.IsDigit(previous) || (char.IsUpper(previous) && char.IsLower(next))))
                {
                    builder.Append('-');
                }

                builder.Append(char.ToLowerInvariant(character));
            }
            else if (character == '.')
            {
                builder.Append('-');
            }
            else
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private static bool IsBuildOutput(string directory)
    {
        var relative = "/" + directory.Replace('\\', '/').TrimEnd('/') + "/";
        return relative.Contains("/bin/", StringComparison.Ordinal) || relative.Contains("/obj/", StringComparison.Ordinal);
    }

    private static string Relative(string repositoryRoot, string path) =>
        Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/');
}
