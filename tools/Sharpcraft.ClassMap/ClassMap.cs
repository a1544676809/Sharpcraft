// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Text;
using System.Text.RegularExpressions;

namespace Sharpcraft.ClassMap;

/// <summary>
/// Generates and checks the "implementation ↔ reference class" map (AGENTS.md 3.5). Lightweight line scan (no Roslyn)
/// over src/**/*.cs for <c>[ReferenceClass("...","ref://...")]</c> attributes. Only names and ref:// pointers are recorded —
/// no reference source is stored (principle 3). Phase 2: injects a per-module section into docs/src and enforces opt-in
/// reverse coverage lists.
/// </summary>
public static class ClassMapTool
{
    private const string AggregatePath = "docs/architecture/class-map.generated.md";
    private const string BaselinePath = "docs/src/.classmap-baseline.txt";
    private const string RefsDir = "docs/architecture/class-map";
    private const string RefPrefix = "ref://mc-26.3-analysis/";
    private const string BeginMarker = "<!-- BEGIN GENERATED: class-map -->";
    private const string EndMarker = "<!-- END GENERATED: class-map -->";

    private static readonly string[] ScanProjects =
        ["Sharpcraft.Client", "Sharpcraft.Common", "Sharpcraft.Framework", "Sharpcraft.Server", "Sharpcraft.Auth", "Sharpcraft.Modding", "Sharpcraft.Launcher"];

    private static readonly Regex NamespaceRx = new(@"^\s*namespace\s+([\w.]+)", RegexOptions.Compiled);
    private static readonly Regex AttrRx = new(@"\[(?:[\w.]+\.)?ReferenceClass\(\s*""(?<cls>[^""]+)""\s*,\s*""(?<path>[^""]+)""(?<rest>[^\]]*)\]", RegexOptions.Compiled);
    private static readonly Regex KindRx = new(@"Kind\s*=\s*(?:[\w.]+\.)?ReferenceKind\.(?<v>\w+)", RegexOptions.Compiled);
    private static readonly Regex StatusRx = new(@"Status\s*=\s*(?:[\w.]+\.)?ReferenceStatus\.(?<v>\w+)", RegexOptions.Compiled);
    private static readonly Regex NoteRx = new(@"Note\s*=\s*""(?<v>[^""]*)""", RegexOptions.Compiled);
    private static readonly Regex TypeRx = new(@"^\s*(?<mods>(?:(?:public|internal|protected|private|static|sealed|abstract|partial|readonly|ref|unsafe|new)\s+)*)(?:class|record(?:\s+(?:struct|class))?|struct|interface|enum)\s+(?<name>\w+)", RegexOptions.Compiled);

    public sealed record Entry(string Project, string ModuleDir, string TypeName, string ReferenceClass, string ReferencePath, string Kind, string Status, string Note);

    public static (List<Entry> Entries, List<string> Unannotated) Scan(string root)
    {
        var entries = new List<Entry>();
        var unannotated = new List<string>();

        foreach (var project in ScanProjects)
        {
            var dir = Path.Combine(root, "src", project);
            if (!Directory.Exists(dir))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                if (IsBuildOutput(file))
                {
                    continue;
                }

                var rel = Path.GetRelativePath(root, file).Replace('\\', '/');
                var subDir = Path.GetRelativePath(dir, Path.GetDirectoryName(file)!).Replace('\\', '/');
                if (subDir == ".")
                {
                    subDir = string.Empty;
                }

                var ns = string.Empty;
                string? pCls = null, pPath = null, pKind = null, pStatus = null, pNote = null;

                foreach (var raw in File.ReadLines(file))
                {
                    var nsMatch = NamespaceRx.Match(raw);
                    if (nsMatch.Success)
                    {
                        ns = nsMatch.Groups[1].Value;
                        continue;
                    }

                    var attr = AttrRx.Match(raw);
                    if (attr.Success)
                    {
                        pCls = attr.Groups["cls"].Value;
                        pPath = attr.Groups["path"].Value;
                        var rest = attr.Groups["rest"].Value;
                        pKind = KindRx.Match(rest) is { Success: true } kr ? kr.Groups["v"].Value : "Mirror";
                        pStatus = StatusRx.Match(rest) is { Success: true } sr ? sr.Groups["v"].Value : "Full";
                        pNote = NoteRx.Match(rest) is { Success: true } nr ? nr.Groups["v"].Value : string.Empty;
                        continue;
                    }

                    var type = TypeRx.Match(raw);
                    if (!type.Success)
                    {
                        continue;
                    }

                    var mods = type.Groups["mods"].Value;
                    var name = type.Groups["name"].Value;
                    var fullName = string.IsNullOrEmpty(ns) ? name : ns + "." + name;

                    if (pCls is not null)
                    {
                        entries.Add(new Entry(project, subDir, fullName, pCls, pPath!, pKind!, pStatus!, pNote ?? string.Empty));
                        pCls = null;
                        pPath = null;
                    }
                    else if (mods.Contains("public", StringComparison.Ordinal))
                    {
                        unannotated.Add(rel + " :: " + fullName);
                    }
                }
            }
        }

        entries.Sort((a, b) => string.CompareOrdinal(a.Project + a.ModuleDir + a.TypeName, b.Project + b.ModuleDir + b.TypeName));
        unannotated.Sort(StringComparer.Ordinal);
        return (entries, unannotated);
    }

    private static bool IsBuildOutput(string file) =>
        file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) ||
        file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar);

    private static string Table(IEnumerable<Entry> entries)
    {
        var sb = new StringBuilder();
        sb.Append("| Project | Sharpcraft type | Kind | Status | Reference class | Reference path | Note |\n");
        sb.Append("| --- | --- | --- | --- | --- | --- | --- |\n");
        foreach (var e in entries)
        {
            sb.Append("| ").Append(e.Project)
              .Append(" | ").Append(e.TypeName)
              .Append(" | ").Append(e.Kind)
              .Append(" | ").Append(e.Status)
              .Append(" | ").Append(e.ReferenceClass)
              .Append(" | ").Append(e.ReferencePath)
              .Append(" | ").Append(e.Note.Replace("|", "\\|"))
              .Append(" |\n");
        }

        return sb.ToString();
    }

    private static string Block(IEnumerable<Entry> entries) =>
        BeginMarker + "\n\n" + Table(entries) + "\n" + EndMarker + "\n";

    private static string Aggregate(IEnumerable<Entry> entries) =>
        "<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->\n<!-- Copyright (C) 2026 Sharpcraft contributors -->\n\n"
        + "<!-- generated by tools/Sharpcraft.ClassMap -- --write; do not edit -->\n\n"
        + BeginMarker + "\n\n" + Table(entries) + "\n" + EndMarker + "\n";

    /// <summary>Maps a source (project, moduleDir) to its docs/src module document path.</summary>
    private static string DocPath(string root, string project, string moduleDir)
    {
        var slug = project.ToLowerInvariant().Replace('.', '-');
        var relative = "docs/src/" + slug;
        if (!string.IsNullOrEmpty(moduleDir))
        {
            relative += "/" + moduleDir.ToLowerInvariant();
        }

        return Path.Combine(root, (relative + ".md").Replace('/', Path.DirectorySeparatorChar));
    }

    public static (int Annotated, int Injected, int Unannotated) Write(string root, bool updateBaseline)
    {
        var (entries, unannotated) = Scan(root);

        var aggPath = Path.Combine(root, AggregatePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(aggPath)!);
        File.WriteAllText(aggPath, Aggregate(entries));

        var injected = InjectDocs(root, entries);

        if (updateBaseline)
        {
            Directory.CreateDirectory(Path.Combine(root, "docs", "src"));
            File.WriteAllText(Path.Combine(root, BaselinePath.Replace('/', Path.DirectorySeparatorChar)),
                string.Join('\n', unannotated) + (unannotated.Count > 0 ? "\n" : string.Empty));
        }

        return (entries.Count, injected, unannotated.Count);
    }

    private static int InjectDocs(string root, List<Entry> entries)
    {
        var injected = 0;
        foreach (var group in entries.GroupBy(e => DocPath(root, e.Project, e.ModuleDir)))
        {
            if (!File.Exists(group.Key))
            {
                continue;
            }

            var content = File.ReadAllText(group.Key).Replace("\r\n", "\n");
            var block = Block(group);
            string updated;
            var start = content.IndexOf(BeginMarker, StringComparison.Ordinal);
            var end = content.IndexOf(EndMarker, StringComparison.Ordinal);
            if (start >= 0 && end > start)
            {
                updated = content[..start] + block + content[(end + EndMarker.Length)..].TrimStart('\n');
            }
            else
            {
                updated = content.TrimEnd('\n') + "\n\n" + block;
            }

            if (updated != content)
            {
                File.WriteAllText(group.Key, updated);
                injected++;
            }
        }

        return injected;
    }

    public static List<string> Check(string root)
    {
        var violations = new List<string>();
        var (entries, unannotated) = Scan(root);

        var aggPath = Path.Combine(root, AggregatePath.Replace('/', Path.DirectorySeparatorChar));
        var actualAgg = File.Exists(aggPath) ? File.ReadAllText(aggPath).Replace("\r\n", "\n") : null;
        if (actualAgg != Aggregate(entries))
        {
            violations.Add("class-map.generated.md is stale; run --write");
        }

        foreach (var group in entries.GroupBy(e => DocPath(root, e.Project, e.ModuleDir)))
        {
            if (!File.Exists(group.Key))
            {
                continue;
            }

            var content = File.ReadAllText(group.Key).Replace("\r\n", "\n");
            var start = content.IndexOf(BeginMarker, StringComparison.Ordinal);
            var end = content.IndexOf(EndMarker, StringComparison.Ordinal);
            var expected = Block(group).TrimEnd('\n');
            if (start < 0 || end <= start || content[start..(end + EndMarker.Length)] != expected)
            {
                violations.Add($"module doc class-map section is stale: {Path.GetRelativePath(root, group.Key)}; run --write");
            }
        }

        var baseline = new HashSet<string>(
            File.Exists(Path.Combine(root, BaselinePath.Replace('/', Path.DirectorySeparatorChar)))
                ? File.ReadAllText(Path.Combine(root, BaselinePath.Replace('/', Path.DirectorySeparatorChar))).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : Array.Empty<string>(),
            StringComparer.Ordinal);
        var unannotatedSet = new HashSet<string>(unannotated, StringComparer.Ordinal);
        foreach (var u in unannotated)
        {
            if (!baseline.Contains(u))
            {
                violations.Add("public type without [ReferenceClass]: " + u);
            }
        }

        foreach (var b in baseline)
        {
            if (!unannotatedSet.Contains(b))
            {
                violations.Add("class-map baseline entry is stale; run --update-baseline: " + b);
            }
        }

        // Reverse coverage (opt-in): each refs list line is a reference class that must be mapped by an annotation, or be
        // prefixed with '!' to mark it intentionally out of scope.
        var refsDir = Path.Combine(root, RefsDir.Replace('/', Path.DirectorySeparatorChar));
        if (Directory.Exists(refsDir))
        {
            var covered = new HashSet<string>(entries.Select(e => e.ReferenceClass), StringComparer.Ordinal);
            foreach (var refsFile in Directory.EnumerateFiles(refsDir, "*.refs.txt", SearchOption.AllDirectories))
            {
                foreach (var raw in File.ReadAllLines(refsFile))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith('#'))
                    {
                        continue;
                    }

                    var outOfScope = line.StartsWith('!');
                    var className = outOfScope ? line[1..].Trim() : line;
                    if (!outOfScope && !covered.Contains(className))
                    {
                        violations.Add($"reverse coverage: {className} (in {Path.GetFileName(refsFile)}) is not mapped by any [ReferenceClass] and not marked out-of-scope with '!'");
                    }
                }
            }
        }

        var analysisRoot = Environment.GetEnvironmentVariable("MC_ANALYSIS_ROOT");
        if (!string.IsNullOrEmpty(analysisRoot))
        {
            foreach (var e in entries)
            {
                if (!e.ReferencePath.StartsWith(RefPrefix, StringComparison.Ordinal))
                {
                    violations.Add($"reference path must start with {RefPrefix}: {e.TypeName} -> {e.ReferencePath}");
                    continue;
                }

                var rel = e.ReferencePath[RefPrefix.Length..].Replace('/', Path.DirectorySeparatorChar);
                if (!File.Exists(Path.Combine(analysisRoot, rel)))
                {
                    violations.Add($"reference file not found under MC_ANALYSIS_ROOT: {e.TypeName} -> {e.ReferencePath}");
                }
            }
        }

        return violations;
    }
}


