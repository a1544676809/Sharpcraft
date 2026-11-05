// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Text;
using System.Text.RegularExpressions;

namespace Sharpcraft.ClassMap;

/// <summary>
/// Generates and checks the "implementation ↔ reference class" map for AGENTS.md 3.5. Uses a lightweight line scan
/// (no Roslyn dependency) over src/**/*.cs for <c>[ReferenceClass("...","ref://...")]</c> attributes that annotate type
/// declarations. Only class names and ref:// pointers are recorded — no reference source is ever read or stored.
/// </summary>
public static class ClassMapTool
{
    private const string GeneratedPath = "docs/architecture/class-map.generated.md";
    private const string BaselinePath = "docs/src/.classmap-baseline.txt";
    private const string RefPrefix = "ref://mc-26.3-analysis/";

    private static readonly string[] ScanProjects = ["Sharpcraft.Client", "Sharpcraft.Common", "Sharpcraft.Framework", "Sharpcraft.Server", "Sharpcraft.Auth", "Sharpcraft.Modding", "Sharpcraft.Launcher"];

    private static readonly Regex NamespaceRx = new(@"^\s*namespace\s+([\w.]+)", RegexOptions.Compiled);
    private static readonly Regex AttrRx = new(@"\[(?:[\w.]+\.)?ReferenceClass\(\s*""(?<cls>[^""]+)""\s*,\s*""(?<path>[^""]+)""(?<rest>[^\]]*)\]", RegexOptions.Compiled);
    private static readonly Regex KindRx = new(@"Kind\s*=\s*(?:[\w.]+\.)?ReferenceKind\.(?<v>\w+)", RegexOptions.Compiled);
    private static readonly Regex StatusRx = new(@"Status\s*=\s*(?:[\w.]+\.)?ReferenceStatus\.(?<v>\w+)", RegexOptions.Compiled);
    private static readonly Regex NoteRx = new(@"Note\s*=\s*""(?<v>[^""]*)""", RegexOptions.Compiled);
    private static readonly Regex TypeRx = new(@"^\s*(?<mods>(?:(?:public|internal|protected|private|static|sealed|abstract|partial|readonly|ref|unsafe|new)\s+)*)(?:class|record|struct|interface|enum)\s+(?<name>\w+)", RegexOptions.Compiled);

    public sealed record Entry(string Project, string TypeName, string ReferenceClass, string ReferencePath, string Kind, string Status, string Note);

    /// <summary>Scans the repository and returns every annotated type plus the set of public types missing an attribute.</summary>
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
                if (file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) ||
                    file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
                {
                    continue;
                }

                var rel = Path.GetRelativePath(root, file).Replace('\\', '/');
                var ns = string.Empty;
                string? pendingCls = null, pendingPath = null, pendingKind = null, pendingStatus = null, pendingNote = null;

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
                        pendingCls = attr.Groups["cls"].Value;
                        pendingPath = attr.Groups["path"].Value;
                        var rest = attr.Groups["rest"].Value;
                        pendingKind = KindRx.Match(rest) is { Success: true } kr ? kr.Groups["v"].Value : "Mirror";
                        pendingStatus = StatusRx.Match(rest) is { Success: true } sr ? sr.Groups["v"].Value : "Full";
                        pendingNote = NoteRx.Match(rest) is { Success: true } nr ? nr.Groups["v"].Value : string.Empty;
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

                    if (pendingCls is not null)
                    {
                        entries.Add(new Entry(project, fullName, pendingCls, pendingPath!, pendingKind!, pendingStatus!, pendingNote ?? string.Empty));
                        pendingCls = null;
                        pendingPath = null;
                    }
                    else if (mods.Contains("public", StringComparison.Ordinal))
                    {
                        unannotated.Add(rel + " :: " + fullName);
                    }
                }
            }
        }

        entries.Sort((a, b) => string.CompareOrdinal(a.Project + a.TypeName, b.Project + b.TypeName));
        unannotated.Sort(StringComparer.Ordinal);
        return (entries, unannotated);
    }

    /// <summary>Renders the generated markdown page.</summary>
    public static string Render(List<Entry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->");
        sb.AppendLine("<!-- Copyright (C) 2026 Sharpcraft contributors -->");
        sb.AppendLine();
        sb.AppendLine("<!-- BEGIN GENERATED: class-map (run: dotnet run --project tools/Sharpcraft.ClassMap -- --write) -->");
        sb.AppendLine();
        sb.AppendLine("| Project | Sharpcraft type | Kind | Status | Reference class | Reference path | Note |");
        sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");
        foreach (var e in entries)
        {
            sb.Append("| ").Append(e.Project)
              .Append(" | ").Append(e.TypeName)
              .Append(" | ").Append(e.Kind)
              .Append(" | ").Append(e.Status)
              .Append(" | ").Append(e.ReferenceClass)
              .Append(" | ").Append(e.ReferencePath)
              .Append(" | ").Append(e.Note.Replace("|", "\\|"))
              .AppendLine(" |");
        }

        sb.AppendLine();
        sb.AppendLine("<!-- END GENERATED: class-map -->");
        return sb.ToString().Replace("\r\n", "\n");
    }

    private static string ReadBaseline(string root)
    {
        var path = Path.Combine(root, BaselinePath);
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }

    private static HashSet<string> BaselineSet(string root) =>
        new(ReadBaseline(root).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), StringComparer.Ordinal);

    /// <summary>Writes the generated page (and baseline when <paramref name="updateBaseline"/>).</summary>
    public static (int Annotated, int Unannotated) Write(string root, bool updateBaseline)
    {
        var (entries, unannotated) = Scan(root);
        var outPath = Path.Combine(root, GeneratedPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        File.WriteAllText(outPath, Render(entries));

        if (updateBaseline)
        {
            Directory.CreateDirectory(Path.Combine(root, "docs", "src"));
            File.WriteAllText(Path.Combine(root, BaselinePath), string.Join('\n', unannotated) + (unannotated.Count > 0 ? "\n" : string.Empty));
        }

        return (entries.Count, unannotated.Count);
    }

    /// <summary>Returns violations: stale generated page, new unannotated public types, or missing reference files.</summary>
    public static List<string> Check(string root)
    {
        var violations = new List<string>();
        var (entries, unannotated) = Scan(root);

        var outPath = Path.Combine(root, GeneratedPath);
        var expected = Render(entries);
        var actual = File.Exists(outPath) ? File.ReadAllText(outPath) : null;
        if (actual != expected)
        {
            violations.Add("class-map.generated.md is stale; run: dotnet run --project tools/Sharpcraft.ClassMap -- --write");
        }

        var baseline = BaselineSet(root);
        foreach (var u in unannotated)
        {
            if (!baseline.Contains(u))
            {
                violations.Add("public type without [ReferenceClass] (add it or baseline it): " + u);
            }
        }

        // Baseline is a ratchet: entries that are now annotated (or gone) make the baseline stale.
        var unannotatedSet = new HashSet<string>(unannotated, StringComparer.Ordinal);
        foreach (var b in baseline)
        {
            if (!unannotatedSet.Contains(b))
            {
                violations.Add("class-map baseline entry is stale (now annotated or removed); run --update-baseline: " + b);
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


