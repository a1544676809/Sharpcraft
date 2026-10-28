// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Text;

namespace Sharpcraft.Docs.Indexer;

/// <summary>One documentation file as the index generator sees it.</summary>
/// <param name="RelativePath">Repository-relative path with forward slashes, e.g. docs/modules/render.md.</param>
/// <param name="Title">Document title (metadata, first heading, or file name).</param>
/// <param name="Summary">One-line summary shown in the generated index.</param>
/// <param name="Status">Document status, without the surrounding brackets.</param>
/// <param name="Updated">Last-updated value, or "-" when the document does not state one.</param>
/// <param name="Side">Optional side (Client / Server / Common / host) for module documents.</param>
/// <param name="Source">Optional source file a per-file document describes.</param>
public sealed record DocEntry(
    string RelativePath,
    string Title,
    string Summary,
    string Status,
    string Updated,
    string? Side,
    string? Source);

/// <summary>
/// Generates the directory indexes from the documents themselves. Hand-written text stays
/// outside the BEGIN/END markers, so a human can keep an introduction while the table is
/// machine-maintained (docs/modules/doc-indexer.md).
/// </summary>
public static class DocIndexer
{
    /// <summary>Opening marker prefix; the directory key follows it.</summary>
    public const string BeginPrefix = "<!-- BEGIN GENERATED: ";

    /// <summary>Closing marker prefix; the directory key follows it.</summary>
    public const string EndPrefix = "<!-- END GENERATED: ";

    private static readonly string[] MetadataLinePrefixes =
    [
        "状态：", "状态:", "基线：", "基线:", "证据：", "证据:", "最后更新：", "最后更新:"
    ];

    /// <summary>Returns every document under <c>docs/</c>, excluding indexes and templates.</summary>
    public static IReadOnlyList<DocEntry> Scan(string repositoryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        var docsRoot = Path.Combine(repositoryRoot, "docs");
        if (!Directory.Exists(docsRoot))
        {
            return [];
        }

        var entries = new List<DocEntry>();
        foreach (var file in Directory.EnumerateFiles(docsRoot, "*.md", SearchOption.AllDirectories))
        {
            var relative = Relative(repositoryRoot, file);
            if (relative.Contains("/_templates/", StringComparison.Ordinal))
            {
                continue;
            }

            if (Path.GetFileName(file).Equals("README.md", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            entries.Add(Parse(file, relative));
        }

        entries.Sort(static (left, right) => string.CompareOrdinal(left.RelativePath, right.RelativePath));
        return entries;
    }

    /// <summary>Rewrites every generated index and returns the files that changed.</summary>
    public static IReadOnlyList<string> Write(string repositoryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        var changed = new List<string>();
        foreach (var (readme, expected) in ExpectedReadmes(repositoryRoot))
        {
            var actual = File.Exists(readme) ? Normalize(File.ReadAllText(readme)) : null;
            if (string.Equals(actual, expected, StringComparison.Ordinal))
            {
                continue;
            }

            var directory = Path.GetDirectoryName(readme)!;
            Directory.CreateDirectory(directory);
            File.WriteAllText(readme, expected, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            changed.Add(Relative(repositoryRoot, readme));
        }

        return changed;
    }

    /// <summary>Returns the indexes that are missing or differ from what the generator would write.</summary>
    public static IReadOnlyList<string> Check(string repositoryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        var stale = new List<string>();
        foreach (var (readme, expected) in ExpectedReadmes(repositoryRoot))
        {
            var relative = Relative(repositoryRoot, readme);
            if (!File.Exists(readme))
            {
                stale.Add(relative + " (missing)");
                continue;
            }

            if (!string.Equals(Normalize(File.ReadAllText(readme)), expected, StringComparison.Ordinal))
            {
                stale.Add(relative + " (stale)");
            }
        }

        return stale;
    }

    private static IEnumerable<(string Readme, string Expected)> ExpectedReadmes(string repositoryRoot)
    {
        var docsRoot = Path.Combine(repositoryRoot, "docs");
        var entries = Scan(repositoryRoot);
        var directories = CollectDirectories(entries, docsRoot);

        foreach (var (relativeDirectory, _) in directories)
        {
            if (relativeDirectory.Length > 0 &&
                directories[relativeDirectory].Count == 0 &&
                !HasChildren(relativeDirectory, directories))
            {
                // A prose-only directory (for example docs/repro) keeps its hand-written README;
                // it is still listed by its parent so that every document stays reachable.
                continue;
            }

            var directory = relativeDirectory.Length == 0
                ? docsRoot
                : Path.Combine(docsRoot, relativeDirectory.Replace('/', Path.DirectorySeparatorChar));
            var readme = Path.Combine(directory, "README.md");
            var key = relativeDirectory.Length == 0 ? "docs" : "docs/" + relativeDirectory;
            var block = RenderBlock(relativeDirectory, entries, directories);
            yield return (readme, Compose(readme, key, relativeDirectory, block));
        }
    }

    /// <summary>Directories that contain documents or a hand-written README, plus every ancestor.</summary>
    private static SortedDictionary<string, List<DocEntry>> CollectDirectories(
        IReadOnlyList<DocEntry> entries,
        string docsRoot)
    {
        var result = new SortedDictionary<string, List<DocEntry>>(StringComparer.Ordinal)
        {
            [string.Empty] = []
        };

        foreach (var entry in entries)
        {
            var directory = DirectoryOf(entry.RelativePath);
            EnsureDirectory(result, directory);
            result[directory].Add(entry);
        }

        foreach (var readme in Directory.EnumerateFiles(docsRoot, "README.md", SearchOption.AllDirectories))
        {
            var directory = Path.GetRelativePath(docsRoot, Path.GetDirectoryName(readme)!);
            if (directory == ".")
            {
                continue;
            }

            var normalized = directory.Replace('\\', '/');
            if (normalized.StartsWith("_templates", StringComparison.Ordinal))
            {
                continue;
            }

            EnsureDirectory(result, normalized);
        }

        return result;
    }

    private static void EnsureDirectory(SortedDictionary<string, List<DocEntry>> directories, string directory)
    {
        var current = directory;
        while (true)
        {
            if (!directories.ContainsKey(current))
            {
                directories[current] = [];
            }

            if (current.Length == 0)
            {
                return;
            }

            current = ParentOf(current);
        }
    }

    private static bool HasChildren(string directory, SortedDictionary<string, List<DocEntry>> directories) =>
        directories.Keys.Any(key => key.Length > 0 && string.Equals(ParentOf(key), directory, StringComparison.Ordinal));

    private static string RenderBlock(
        string relativeDirectory,
        IReadOnlyList<DocEntry> entries,
        SortedDictionary<string, List<DocEntry>> directories)
    {
        var builder = new StringBuilder();
        var files = entries
            .Where(entry => string.Equals(DirectoryOf(entry.RelativePath), relativeDirectory, StringComparison.Ordinal))
            .OrderBy(entry => Path.GetFileName(entry.RelativePath), StringComparer.Ordinal)
            .ToList();

        if (files.Count == 0)
        {
            builder.Append("（本目录暂无文档。）\n");
        }
        else
        {
            builder.Append("| 文档 | 摘要 | 状态 | 最后更新 |\n");
            builder.Append("| --- | --- | --- | --- |\n");
            foreach (var entry in files)
            {
                var name = Path.GetFileName(entry.RelativePath);
                builder.Append("| [`").Append(name).Append("`](").Append(name).Append(") | ")
                    .Append(Escape(entry.Summary)).Append(" | ")
                    .Append(Escape(entry.Status)).Append(" | ")
                    .Append(Escape(entry.Updated)).Append(" |\n");
            }
        }

        var children = directories.Keys
            .Where(key => key.Length > 0 && string.Equals(ParentOf(key), relativeDirectory, StringComparison.Ordinal))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

        if (children.Count > 0)
        {
            builder.Append("\n子目录：\n\n| 目录 | 文档数 |\n| --- | --- |\n");
            foreach (var child in children)
            {
                var name = relativeDirectory.Length == 0 ? child : child[(relativeDirectory.Length + 1)..];
                builder.Append("| [`").Append(name).Append("/`](").Append(name).Append("/README.md) | ")
                    .Append(CountDocuments(child, entries)).Append(" |\n");
            }
        }

        return builder.ToString();
    }

    private static int CountDocuments(string directory, IReadOnlyList<DocEntry> entries)
    {
        var prefix = directory + "/";
        return entries.Count(entry => entry.RelativePath.StartsWith("docs/" + prefix, StringComparison.Ordinal));
    }

    private static string Compose(string readme, string key, string relativeDirectory, string block)
    {
        var begin = BeginPrefix + key + " -->";
        var end = EndPrefix + key + " -->";
        var blockBody = block.TrimEnd('\n');

        if (File.Exists(readme))
        {
            var text = Normalize(File.ReadAllText(readme));
            var beginIndex = text.IndexOf(begin, StringComparison.Ordinal);
            var endIndex = text.IndexOf(end, StringComparison.Ordinal);
            if (beginIndex >= 0 && endIndex > beginIndex)
            {
                var afterBegin = text.IndexOf('\n', beginIndex);
                var beforeEnd = text.LastIndexOf('\n', endIndex);
                if (afterBegin < 0 || beforeEnd < 0)
                {
                    throw new InvalidOperationException($"malformed generated markers in {readme}");
                }

                var prefix = text[..(afterBegin + 1)].TrimEnd('\n');
                var suffix = text[(beforeEnd + 1)..];
                return prefix + "\n\n" + blockBody + "\n\n" + suffix;
            }

            var trimmed = text.TrimEnd('\n');
            return trimmed + "\n\n" + begin + "\n\n" + blockBody + "\n\n" + end + "\n";
        }

        var title = relativeDirectory.Length == 0
            ? "文档体系"
            : relativeDirectory.Split('/')[^1];
        var intro = "本目录的文档索引由 `tools/Sharpcraft.Docs.Indexer` 生成；手写内容放在标记之外。";

        return "<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->\n"
            + "<!-- Copyright (C) 2026 Sharpcraft contributors -->\n\n"
            + "# " + title + "\n\n"
            + intro + "\n\n"
            + begin + "\n\n" + blockBody + "\n\n" + end + "\n";
    }

    private static DocEntry Parse(string file, string relativePath)
    {
        var lines = Normalize(File.ReadAllText(file)).Split('\n');
        var metadata = ReadMetadata(lines);

        var title = Value(metadata, "title") ?? FindHeading(lines) ?? Path.GetFileNameWithoutExtension(file);
        var status = Value(metadata, "status") ?? FindPrefixed(lines, "状态") ?? "未标注";
        var updated = Value(metadata, "updated") ?? FindPrefixed(lines, "最后更新") ?? "-";
        var summary = Value(metadata, "summary") ?? ExtractSummary(lines);

        return new DocEntry(
            relativePath,
            Clean(title),
            Clean(summary),
            CleanStatus(status),
            Clean(updated),
            Value(metadata, "side"),
            Value(metadata, "source"));
    }

    private static Dictionary<string, string> ReadMetadata(string[] lines)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < lines.Length; index++)
        {
            if (!lines[index].TrimStart().StartsWith("<!-- doc", StringComparison.Ordinal))
            {
                continue;
            }

            for (var inner = index + 1; inner < lines.Length; inner++)
            {
                var line = lines[inner].Trim();
                if (line.StartsWith("-->", StringComparison.Ordinal))
                {
                    return result;
                }

                var separator = line.IndexOf(':', StringComparison.Ordinal);
                if (separator <= 0)
                {
                    continue;
                }

                var key = line[..separator].Trim();
                var value = line[(separator + 1)..].Trim();
                if (key.Length > 0 && value.Length > 0)
                {
                    result[key] = value;
                }
            }
        }

        return result;
    }

    private static string? Value(Dictionary<string, string> metadata, string key) =>
        metadata.TryGetValue(key, out var value) ? value : null;

    private static string? FindHeading(string[] lines)
    {
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("# ", StringComparison.Ordinal))
            {
                return trimmed[2..].Trim();
            }
        }

        return null;
    }

    private static string? FindPrefixed(string[] lines, string prefix)
    {
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal))
            {
                trimmed = trimmed[2..].Trim();
            }

            foreach (var separator in new[] { "：", ":" })
            {
                var full = prefix + separator;
                if (trimmed.StartsWith(full, StringComparison.Ordinal))
                {
                    return trimmed[full.Length..].Trim();
                }
            }
        }

        return null;
    }

    /// <summary>First real paragraph after the title, used when a document has no metadata block.</summary>
    private static string ExtractSummary(string[] lines)
    {
        var heading = Array.FindIndex(lines, line => line.TrimStart().StartsWith("# ", StringComparison.Ordinal));
        var builder = new StringBuilder();

        for (var index = heading + 1; index < lines.Length; index++)
        {
            var trimmed = lines[index].Trim();
            if (trimmed.Length == 0)
            {
                if (builder.Length > 0)
                {
                    break;
                }

                continue;
            }

            if (IsStructural(trimmed))
            {
                if (builder.Length > 0)
                {
                    break;
                }

                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(trimmed);
        }

        if (builder.Length == 0)
        {
            return string.Empty;
        }

        var text = builder.ToString();
        var sentence = text.IndexOf('。', StringComparison.Ordinal);
        return sentence > 0 ? text[..(sentence + 1)] : text;
    }

    private static bool IsStructural(string trimmed)
    {
        if (trimmed.StartsWith('#') || trimmed.StartsWith('>') || trimmed.StartsWith('|')
            || trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("<!--", StringComparison.Ordinal)
            || trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal))
        {
            return true;
        }

        foreach (var prefix in MetadataLinePrefixes)
        {
            if (trimmed.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return char.IsDigit(trimmed[0]) && trimmed.Contains(". ", StringComparison.Ordinal);
    }

    private static string Clean(string text)
    {
        var value = text
            .Replace("**", string.Empty, StringComparison.Ordinal)
            .Replace("`", string.Empty, StringComparison.Ordinal)
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);

        value = StripLinks(value);
        value = CollapseSpaces(value);

        if (value.Length > 120)
        {
            value = value[..117].TrimEnd() + "…";
        }

        return value;
    }

    private static string CleanStatus(string status)
    {
        var value = status.Trim();
        if (value.Length >= 2 && value[0] == '[' && value[^1] == ']')
        {
            value = value[1..^1].Trim();
        }

        return value.Length == 0 ? "未标注" : value;
    }

    private static string StripLinks(string text)
    {
        var builder = new StringBuilder(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '[')
            {
                builder.Append(text[index]);
                continue;
            }

            var close = text.IndexOf(']', index + 1);
            if (close < 0 || close + 1 >= text.Length || text[close + 1] != '(')
            {
                builder.Append(text[index]);
                continue;
            }

            var end = text.IndexOf(')', close + 2);
            if (end < 0)
            {
                builder.Append(text[index]);
                continue;
            }

            builder.Append(text, index + 1, close - index - 1);
            index = end;
        }

        return builder.ToString();
    }

    private static string CollapseSpaces(string text)
    {
        var builder = new StringBuilder(text.Length);
        var previousSpace = false;
        foreach (var character in text)
        {
            var isSpace = char.IsWhiteSpace(character);
            if (isSpace)
            {
                if (!previousSpace)
                {
                    builder.Append(' ');
                }
            }
            else
            {
                builder.Append(character);
            }

            previousSpace = isSpace;
        }

        return builder.ToString().Trim();
    }

    private static string Escape(string text) => text.Replace("|", "\\|", StringComparison.Ordinal);

    private static string DirectoryOf(string relativePath)
    {
        var withoutDocs = relativePath["docs/".Length..];
        var separator = withoutDocs.LastIndexOf('/');
        return separator < 0 ? string.Empty : withoutDocs[..separator];
    }

    private static string ParentOf(string directory)
    {
        if (directory.Length == 0)
        {
            return string.Empty;
        }

        var separator = directory.LastIndexOf('/');
        return separator < 0 ? string.Empty : directory[..separator];
    }

    private static string Relative(string repositoryRoot, string path) =>
        Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/');

    private static string Normalize(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
}
