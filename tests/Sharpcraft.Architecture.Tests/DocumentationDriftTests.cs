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
    private static readonly string[] KnownTraceabilityStatuses =
    [
        "已完成", "进行中", "规格已就绪", "阻塞于评审", "规格不足", "已废弃"
    ];

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
        var docsRoot = Path.Combine(RepositoryRoot(), "docs");
        var index = File.ReadAllText(Path.Combine(docsRoot, "README.md"));

        var broken = new List<string>();
        foreach (Match match in MarkdownLink().Matches(index))
        {
            var target = match.Groups[1].Value;
            if (target.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
                target.StartsWith('#'))
            {
                continue;
            }

            var path = Path.Combine(docsRoot, target.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                broken.Add(target);
            }
        }

        Assert.True(broken.Count == 0, "broken links in docs/README.md:\n" + string.Join("\n", broken));
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
    public void StateDocumentKeepsItsRequiredSections()
    {
        var state = Path.Combine(RepositoryRoot(), "docs", "STATE.md");
        Assert.True(File.Exists(state), "docs/STATE.md is missing (AGENTS.md 4.2)");

        var text = File.ReadAllText(state);
        foreach (var section in new[] { "## 当前里程碑", "## 已完成", "## 进行中", "## 阻塞项", "## 下一步具体动作" })
        {
            Assert.Contains(section, text, StringComparison.Ordinal);
        }

        Assert.Contains("最后更新：", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TraceabilityRowsUseKnownStatuses()
    {
        var path = Path.Combine(RepositoryRoot(), "docs", "traceability.md");
        Assert.True(File.Exists(path), "docs/traceability.md is missing (AGENTS.md principle 12)");

        var rows = File.ReadAllLines(path)
            .Where(line => line.StartsWith("| T-", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(rows);

        foreach (var row in rows)
        {
            var cells = row.Split('|', StringSplitOptions.TrimEntries);
            // leading/trailing empty cells plus 7 content cells: id, task, spec, test, code, status
            Assert.True(cells.Length >= 8, $"traceability row has too few columns: {row}");

            var status = cells[^2];
            Assert.Contains(status, KnownTraceabilityStatuses);
        }
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
