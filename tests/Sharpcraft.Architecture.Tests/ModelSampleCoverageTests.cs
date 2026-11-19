// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Collections;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Sharpcraft.Client.Render;
using Sharpcraft.Equivalence.Audit;
using Xunit;

namespace Sharpcraft.Architecture.Tests;

/// <summary>
/// Issue #165: a model-family x truth-sample coverage ratchet. A public model definition must either
/// be named by a truth assertion in the tests or be listed in the shrinking baseline; every recorded
/// reference sample must likewise be named by a test literal or be baselined. Container members carry
/// an exact element-count anchor, so adding an element without a truth assertion turns the gate red.
/// The baseline may only shrink.
/// </summary>
/// <remarks>
/// One shared baseline file has three entry kinds: a sample name ending in <c>.golden.txt</c> or
/// <c>.reference.txt</c>; a scalar definition <c>Type.Member</c>; and a container anchor
/// <c>Type.Member = N</c>. All are discovered by reflection and source scanning, never hardcoded counts.
/// </remarks>
public sealed class ModelSampleCoverageTests
{
    /// <summary>Repository-relative baseline path; resolved through the shared repository locator.</summary>
    public const string BaselineRelativePath =
        "tests/Sharpcraft.Architecture.Tests/.model-sample-coverage-baseline.txt";

    private const string SampleDirectoryRelativePath =
        "tests/Sharpcraft.Client.Tests/Repro/Data";

    private const string TestsDirectoryRelativePath = "tests";

    /// <summary>
    /// Floor for the sample directory. At the time of writing it holds 286 files (after the
    /// evidence-cleanup merge removed two); 280 guards against wholesale deletion while tolerating
    /// housekeeping. A breach means samples disappeared from the repository, not that coverage grew.
    /// </summary>
    private const int MinimumSampleFiles = 280;

    /// <summary>String literals whose last path segment is a recorded reference sample.</summary>
    private static readonly Regex SampleLiteral = new(
        @"""([^""\r\n]*\.(?:reference|golden)\.txt)""",
        RegexOptions.Compiled);

    /// <summary>Sample names must have a stem before the extension; empty stems come from the gate's own regex.</summary>
    private static readonly Regex SampleName = new(
        @"^[^/\\]+\.(?:reference|golden)\.txt$",
        RegexOptions.Compiled);

    /// <summary>A baseline container anchor, e.g. BabyModelDefinitionsAquatic.Models = 5.</summary>
    private static readonly Regex AnchorLine = new(
        @"^(\S+?) = (\d+)$",
        RegexOptions.Compiled);

    /// <summary>Whole dotted identifiers in comment-free, literal-free test code.</summary>
    private static readonly Regex DottedIdentifier = new(
        @"[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)+",
        RegexOptions.Compiled);

    /// <summary>Type-level accessors whose presence means every definition of the type is asserted.</summary>
    /// <remarks>
    /// Confirmed in test source: ForSlot (armor families), Variants and Models (baby and variant
    /// families). ByPose and All are listed because the source defines them; a grep found no test use
    /// yet, so they currently cover nothing.
    /// </remarks>
    private static readonly string[] AccessorMembers = ["ForSlot", "Models", "Variants", "ByPose", "All"];

    /// <summary>The baseline file must exist and contain at least one entry; a missing file must never pass.</summary>
    [Fact]
    public void BaselineExistsAndIsNonEmpty()
    {
        var root = StateMatrixBaseline.FindRepositoryRoot();
        _ = Baseline(root);
    }

    /// <summary>
    /// Assertion A: every recorded sample is named by a test literal or baselined, EVERY referenced
    /// sample name resolves to a real file, and the sample directory still holds a plausible count.
    /// </summary>
    [Fact]
    public void EveryReferenceSampleExistsAndIsReferencedOrBaselined()
    {
        var root = StateMatrixBaseline.FindRepositoryRoot();
        var samples = SampleFiles(root);
        var referenced = ReferencedSamples(root);
        var baseline = Baseline(root);

        var missing = referenced
            .Where(name => !samples.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.True(missing.Length == 0,
            "test literals name reference samples that are not in the sample directory:\n"
            + string.Join("\n", missing));

        var orphans = samples
            .Where(sample => !referenced.Contains(sample) && !baseline.Contains(sample))
            .OrderBy(sample => sample, StringComparer.Ordinal)
            .ToArray();
        Assert.True(orphans.Length == 0,
            "recorded reference samples with no test literal (reference them or add to "
            + BaselineRelativePath + "):\n" + string.Join("\n", orphans));
    }

    /// <summary>Assertion B: every scalar public model definition is asserted by a test or baselined.</summary>
    [Fact]
    public void EveryModelDefinitionIsAssertedOrBaselined()
    {
        var root = StateMatrixBaseline.FindRepositoryRoot();
        var definitions = ScalarDefinitions();
        var covered = CoveredDefinitions(root, definitions);
        var baseline = Baseline(root);

        var uncovered = definitions
            .Where(definition => !covered.Contains(definition) && !baseline.Contains(definition))
            .OrderBy(definition => definition, StringComparer.Ordinal)
            .ToArray();

        Assert.True(uncovered.Length == 0,
            "model definitions with no truth assertion (assert them or add to "
            + BaselineRelativePath + "):\n" + string.Join("\n", uncovered));
    }

    /// <summary>
    /// Assertion B2: every container member has an exact count anchor, the anchor matches the live
    /// element count, and no stale anchors remain. Adding or removing an element without updating the
    /// anchor (and providing a truth assertion) turns this red.
    /// </summary>
    [Fact]
    public void ContainerAnchorCountsAreExact()
    {
        var root = StateMatrixBaseline.FindRepositoryRoot();
        var containers = ContainerCounts();
        var anchors = Baseline(root)
            .Select(entry => AnchorLine.Match(entry))
            .Where(match => match.Success)
            .ToDictionary(match => match.Groups[1].Value, match => int.Parse(match.Groups[2].Value), StringComparer.Ordinal);

        var unanchored = containers.Keys
            .Where(name => !anchors.ContainsKey(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.True(unanchored.Length == 0,
            "definition containers with no count anchor in " + BaselineRelativePath + ":\n"
            + string.Join("\n", unanchored));

        var stale = anchors.Keys
            .Where(name => !containers.ContainsKey(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.True(stale.Length == 0,
            "count anchors for containers that no longer exist; delete them from "
            + BaselineRelativePath + ":\n" + string.Join("\n", stale));

        var drift = containers
            .Where(pair => anchors[pair.Key] != pair.Value)
            .Select(pair => $"{pair.Key}: anchor {anchors[pair.Key]}, actual {pair.Value}")
            .OrderBy(line => line, StringComparer.Ordinal)
            .ToArray();
        Assert.True(drift.Length == 0,
            "definition container counts drifted from their anchors (a new element needs a truth "
            + "assertion and an updated anchor):\n" + string.Join("\n", drift));
    }

    /// <summary>
    /// Assertion C: the baseline only shrinks. A covered definition, a referenced sample, a stale
    /// entry, or a stale anchor must be deleted.
    /// </summary>
    [Fact]
    public void BaselineOnlyShrinks()
    {
        var root = StateMatrixBaseline.FindRepositoryRoot();
        var samples = SampleFiles(root);
        var scalarDefinitions = ScalarDefinitions();
        var allDefinitions = reflectedDefinitions();
        var containerNames = ContainerCounts().Keys;
        var referenced = ReferencedSamples(root);
        var covered = CoveredDefinitions(root, scalarDefinitions);
        var baseline = Baseline(root);

        var nowCovered = new List<string>();
        var stale = new List<string>();
        foreach (var entry in baseline)
        {
            var anchor = AnchorLine.Match(entry);
            if (anchor.Success)
            {
                if (!containerNames.Contains(anchor.Groups[1].Value))
                {
                    stale.Add(entry);
                }

                continue;
            }

            if (IsSample(entry))
            {
                if (referenced.Contains(entry)) nowCovered.Add(entry);
                if (!samples.Contains(entry)) stale.Add(entry);
                continue;
            }

            if (covered.Contains(entry)) nowCovered.Add(entry);
            if (!allDefinitions.Contains(entry)) stale.Add(entry);
        }

        Assert.True(nowCovered.Count == 0,
            "baseline entries are now covered; remove them from " + BaselineRelativePath
            + " so the ratchet can shrink:\n" + string.Join("\n", nowCovered.OrderBy(e => e, StringComparer.Ordinal)));
        Assert.True(stale.Count == 0,
            "baseline entries that no longer exist (delete them from "
            + BaselineRelativePath + "):\n" + string.Join("\n", stale.OrderBy(e => e, StringComparer.Ordinal)));
    }

    private static bool IsSample(string entry) =>
        entry.EndsWith(".reference.txt", StringComparison.Ordinal)
        || entry.EndsWith(".golden.txt", StringComparison.Ordinal);

    private static HashSet<string> SampleFiles(string root)
    {
        var directory = Path.Combine(root, SampleDirectoryRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(directory))
        {
            throw new InvalidOperationException($"sample directory is missing: {SampleDirectoryRelativePath}");
        }

        var files = Directory.EnumerateFiles(directory, "*.txt")
            .Select(Path.GetFileName)
            .Where(name => name is not null)
            .Select(name => name!)
            .ToHashSet(StringComparer.Ordinal);
        if (files.Count < MinimumSampleFiles)
        {
            throw new InvalidOperationException(
                $"sample directory holds {files.Count} files, below the {MinimumSampleFiles} floor; "
                + "samples were removed without a baseline decision");
        }

        return files;
    }

    private static HashSet<string> ReferencedSamples(string root) =>
        RawTestSources(root)
            .SelectMany(source => SampleLiteral.Matches(source).Select(match => match.Groups[1].Value))
            .Select(name => name.Replace('\\', '/').Split('/')[^1])
            .Where(name => SampleName.IsMatch(name))
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// A definition counts as asserted when its complete Type.Member token appears in code (comments
    /// and literals stripped, word boundaries enforced), or when the declaring type has a collection
    /// accessor in code, which asserts the whole family.
    /// </summary>
    private static HashSet<string> CoveredDefinitions(string root, IEnumerable<string> definitions)
    {
        var code = CodeText(root);
        var tokens = DottedIdentifier.Matches(code).Select(match => match.Value).ToHashSet(StringComparer.Ordinal);
        var accessorTypes = tokens
            .Where(token => AccessorMembers.Any(member => token.EndsWith("." + member, StringComparison.Ordinal)))
            .Select(token => token[..token.LastIndexOf('.')])
            .ToHashSet(StringComparer.Ordinal);

        return definitions
            .Where(definition => tokens.Contains(definition)
                || accessorTypes.Contains(definition[..definition.IndexOf('.')]))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IEnumerable<string> TestFiles(string root)
    {
        var directory = Path.Combine(root, TestsDirectoryRelativePath);
        if (!Directory.Exists(directory))
        {
            throw new InvalidOperationException("tests directory is missing");
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (relative.Contains("/bin/", StringComparison.Ordinal) || relative.Contains("/obj/", StringComparison.Ordinal))
            {
                continue;
            }

            yield return file;
        }
    }

    private static IEnumerable<string> RawTestSources(string root) =>
        TestFiles(root).Select(File.ReadAllText);

    /// <summary>Test code with comments, string literals, verbatim/raw strings and char literals blanked.</summary>
    private static string CodeText(string root)
    {
        var builder = new StringBuilder();
        foreach (var file in TestFiles(root))
        {
            builder.AppendLine(StripCode(File.ReadAllText(file)));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Minimal C# lexer that replaces comments and literals with spaces (newlines preserved) so only
    /// real code remains for identifier matching. Handles line/block comments, regular, verbatim,
    /// interpolated, raw and char literals.
    /// </summary>
    internal static string StripCode(string source)
    {
        var output = new StringBuilder(source.Length);
        var index = 0;
        while (index < source.Length)
        {
            var c = source[index];
            var next = index + 1 < source.Length ? source[index + 1] : '\0';

            if (c == '/' && next == '/')
            {
                while (index < source.Length && source[index] != '\n')
                {
                    output.Append(' ');
                    index++;
                }

                continue;
            }

            if (c == '/' && next == '*')
            {
                output.Append("  ");
                index += 2;
                while (index < source.Length && !(source[index] == '*' && index + 1 < source.Length && source[index + 1] == '/'))
                {
                    output.Append(source[index] == '\n' ? '\n' : ' ');
                    index++;
                }

                if (index < source.Length)
                {
                    output.Append("  ");
                    index += 2;
                }

                continue;
            }

            if (c == '@' && next == '"')
            {
                output.Append("  ");
                index += 2;
                while (index < source.Length)
                {
                    if (source[index] == '"' && index + 1 < source.Length && source[index + 1] == '"')
                    {
                        output.Append("  ");
                        index += 2;
                        continue;
                    }

                    if (source[index] == '"')
                    {
                        output.Append(' ');
                        index++;
                        break;
                    }

                    output.Append(source[index] == '\n' ? '\n' : ' ');
                    index++;
                }

                continue;
            }

            if (c == '"')
            {
                if (index + 2 < source.Length && source[index + 1] == '"' && source[index + 2] == '"')
                {
                    output.Append("   ");
                    index += 3;
                    while (index < source.Length)
                    {
                        if (index + 2 < source.Length && source[index] == '"' && source[index + 1] == '"' && source[index + 2] == '"')
                        {
                            output.Append("   ");
                            index += 3;
                            break;
                        }

                        output.Append(source[index] == '\n' ? '\n' : ' ');
                        index++;
                    }

                    continue;
                }

                output.Append(' ');
                index++;
                while (index < source.Length)
                {
                    if (source[index] == '\\')
                    {
                        output.Append("  ");
                        index += 2;
                        continue;
                    }

                    if (source[index] == '"')
                    {
                        output.Append(' ');
                        index++;
                        break;
                    }

                    output.Append(source[index] == '\n' ? '\n' : ' ');
                    index++;
                }

                continue;
            }

            if (c == '\'')
            {
                output.Append(' ');
                index++;
                while (index < source.Length)
                {
                    if (source[index] == '\\')
                    {
                        output.Append("  ");
                        index += 2;
                        continue;
                    }

                    if (source[index] == '\'')
                    {
                        output.Append(' ');
                        index++;
                        break;
                    }

                    output.Append(source[index] == '\n' ? '\n' : ' ');
                    index++;
                }

                continue;
            }

            output.Append(c);
            index++;
        }

        return output.ToString();
    }

    private static HashSet<string> reflectedDefinitions()
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in typeof(EntityModelDefinition).Assembly.GetTypes())
        {
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (IsDefinitionBearing(field.FieldType)) result.Add(type.Name + "." + field.Name);
            }

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Static))
            {
                if (property.GetIndexParameters().Length == 0 && IsDefinitionBearing(property.PropertyType))
                {
                    result.Add(type.Name + "." + property.Name);
                }
            }
        }

        return result;
    }

    /// <summary>Scalar definitions: one definition per member, asserted individually.</summary>
    private static HashSet<string> ScalarDefinitions() =>
        reflectedDefinitions().Where(id => IsScalar(id)).ToHashSet(StringComparer.Ordinal);

    /// <summary>Container members: count-anchored rather than individually asserted.</summary>
    private static Dictionary<string, int> ContainerCounts()
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var type in typeof(EntityModelDefinition).Assembly.GetTypes())
        {
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (IsDefinitionBearing(field.FieldType) && field.FieldType != typeof(EntityModelDefinition))
                {
                    result[type.Name + "." + field.Name] = ElementCount(field.GetValue(null));
                }
            }

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Static))
            {
                if (property.GetIndexParameters().Length == 0
                    && IsDefinitionBearing(property.PropertyType)
                    && property.PropertyType != typeof(EntityModelDefinition))
                {
                    result[type.Name + "." + property.Name] = ElementCount(property.GetValue(null));
                }
            }
        }

        return result;
    }

    private static int ElementCount(object? value) =>
        value is IEnumerable enumerable ? enumerable.Cast<object>().Count() : throw new InvalidOperationException("definition container is not enumerable");

    private static bool IsScalar(string identifier) => !ContainerCounts().ContainsKey(identifier);

    private static bool IsDefinitionBearing(Type type)
    {
        if (type == typeof(EntityModelDefinition)) return true;
        if (type.HasElementType) return IsDefinitionBearing(type.GetElementType()!);
        return type.IsGenericType && type.GetGenericArguments().Any(IsDefinitionBearing);
    }

    private static HashSet<string> Baseline(string root)
    {
        var path = Path.Combine(root, BaselineRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"coverage baseline is missing: {BaselineRelativePath}");
        }

        var entries = File.ReadLines(path)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToHashSet(StringComparer.Ordinal);
        if (entries.Count == 0)
        {
            throw new InvalidOperationException($"coverage baseline is empty: {BaselineRelativePath}");
        }

        return entries;
    }

}
