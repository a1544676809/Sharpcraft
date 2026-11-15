// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Text.RegularExpressions;
using Xunit;

namespace Sharpcraft.Architecture.Tests;

/// <summary>
/// Documentation drift gate (AGENTS.md principle 12): the repository must stay
/// self-consistent without human review. Every check here fails the build.
/// </summary>
public sealed partial class DocumentationDriftTests
{
    [Fact]
    public void SourceFilesCarrySpdxHeader()
    {
        var offenders = new List<string>();
        foreach (var file in EnumerateFiles("src", "*.cs").Concat(EnumerateFiles("tests", "*.cs")))
        {
            var head = File.ReadLines(file).Take(3).ToArray();
            if (!head.Any(line => line.Contains("SPDX-License-Identifier: LGPL-3.0-or-later", StringComparison.Ordinal)))
            {
                offenders.Add(Relative(file));
            }
        }

        Assert.True(offenders.Count == 0, "source files without SPDX header:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void DocumentsCarrySpdxHeader()
    {
        var offenders = EnumerateFiles("docs", "*.md")
            .Where(file => !File.ReadLines(file).Take(5)
                .Any(line => line.Contains("SPDX-License-Identifier: LGPL-3.0-or-later", StringComparison.Ordinal)))
            .Select(Relative)
            .ToList();

        Assert.True(offenders.Count == 0, "documents without SPDX header:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void EveryDocumentIsReachableFromTheIndex()
    {
        // The root index links to each directory README, and each README lists its own documents,
        // so reachability is a walk over links, not a substring search in one file
        // (docs/modules/doc-indexer.md).
        var docsRoot = Path.Combine(RepositoryRoot(), "docs");
        var reachable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        queue.Enqueue(Path.Combine(docsRoot, "README.md"));

        while (queue.Count > 0)
        {
            var file = queue.Dequeue();
            if (!reachable.Add(Path.GetFullPath(file)))
            {
                continue;
            }

            foreach (Match match in MarkdownLink().Matches(File.ReadAllText(file)))
            {
                var target = match.Groups[1].Value;
                if (target.StartsWith("http", StringComparison.OrdinalIgnoreCase) || target.StartsWith('#'))
                {
                    continue;
                }

                var anchor = target.IndexOf('#');
                if (anchor >= 0)
                {
                    target = target[..anchor];
                }

                var resolved = Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(file)!,
                    target.Replace('/', Path.DirectorySeparatorChar)));

                if (!resolved.StartsWith(docsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                    !File.Exists(resolved) ||
                    !resolved.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                queue.Enqueue(resolved);
            }
        }

        var missing = EnumerateFiles("docs", "*.md")
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}_templates{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => !reachable.Contains(Path.GetFullPath(file)))
            .Select(Relative)
            .ToList();

        Assert.True(missing.Count == 0,
            "documents unreachable from docs/README.md (AGENTS.md 3.5):\n" + string.Join("\n", missing));
    }

    [Fact]
    public void IndexLinksPointToExistingFiles()
    {
        var root = RepositoryRoot();
        var broken = new List<string>();

        // Both entry points, not just the docs index: the root README is the first file a reader
        // opens, and it pointed at three files that no longer exist until this covered it.
        foreach (var (file, baseDir) in new[]
        {
            (Path.Combine(root, "README.md"), root),
            (Path.Combine(root, "docs", "README.md"), Path.Combine(root, "docs"))
        })
        {
            // A missing entry point is itself a defect: skipping it would let the check pass by
            // having nothing to check.
            Assert.True(File.Exists(file), $"entry document is missing: {Path.GetRelativePath(root, file)}");

            foreach (Match match in MarkdownLink().Matches(File.ReadAllText(file)))
            {
                var target = match.Groups[1].Value;
                if (target.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
                    target.StartsWith('#'))
                {
                    continue;
                }

                var path = Path.Combine(baseDir, target.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path) && !Directory.Exists(path))
                {
                    broken.Add($"{Path.GetRelativePath(root, file)}: {target}");
                }
            }
        }

        Assert.True(broken.Count == 0, "broken links:\n" + string.Join("\n", broken));
    }

    [Fact]
    public void DocumentsContainNoAbsoluteWorkspacePaths()
    {
        var offenders = new List<string>();
        foreach (var file in EnumerateFiles("docs", "*.md").Concat(EnumerateFiles(".", "*.md")))
        {
            var text = File.ReadAllText(file);
            if (DrivePath().IsMatch(text))
            {
                offenders.Add(Relative(file));
            }
        }

        Assert.True(offenders.Count == 0,
            "documents must use relative paths or ref:// pointers (AGENTS.md 3.6):\n" + string.Join("\n", offenders));
    }


    [Fact]
    public void ModuleTemplateRequiresVanillaSourceMappingAndGoldenVectors()
    {
        var template = Path.Combine(RepositoryRoot(), "docs", "_templates", "module-doc.md");
        Assert.True(File.Exists(template), "docs/_templates/module-doc.md is missing");

        var text = File.ReadAllText(template);
        Assert.Contains("原版源码映射表", text, StringComparison.Ordinal);
        Assert.Contains("真值向量（Golden Vectors）", text, StringComparison.Ordinal);
    }


    private static IEnumerable<string> EnumerateFiles(string relativeRoot, string pattern)
    {
        var root = Path.Combine(RepositoryRoot(), relativeRoot);
        return Directory.Exists(root)
            ? Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            : [];
    }

    private static string Relative(string file) => Path.GetRelativePath(RepositoryRoot(), file).Replace('\\', '/');

    private static string RepositoryRoot() => RepositoryLocator.Root;

    [GeneratedRegex(@"\]\(([^)\s]+)\)")]
    private static partial Regex MarkdownLink();

    [GeneratedRegex(@"(?<![\w/])[A-Za-z]:\\")]
    private static partial Regex DrivePath();
}
