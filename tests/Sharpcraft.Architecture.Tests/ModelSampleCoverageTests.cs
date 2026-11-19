// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Sharpcraft.Client.Render;
using Sharpcraft.Equivalence.Audit;
using Xunit;

namespace Sharpcraft.Architecture.Tests;

/// <summary>
/// Issue #165: a model-family x truth-sample coverage ratchet over exactly two domains - scalar public
/// model definitions and recorded reference samples. A definition must be named by a truth assertion or
/// be listed in the shrinking baseline; a sample must be named by a test literal or be baselined. The
/// baseline may only shrink.
/// </summary>
/// <remarks>
/// 容器元素是运行时构造值，无法通过反射命名；其覆盖机制见 Issue #256。本棘轮只覆盖标量定义成员与样本文件。
///
/// Definition coverage reads comment-free, literal-free test code and matches complete dotted tokens,
/// including every prefix of a longer chain (A.B.C also covers A.B), so member access such as
/// BookModelDefinition.Definition.TextureWidth still asserts the definition. A declaring type that has
/// a collection accessor in test code (ForSlot, Models, Variants, ByPose, All) asserts the whole family.
/// Sample coverage reads comment-free code with literals preserved, so a comment naming a sample does
/// not count as an assertion but a literal passed through a helper does.
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
    /// Fallback floor for the sample directory, not coverage evidence: at the time of writing it holds
    /// 286 files (after the evidence-cleanup merge removed two). This rounds down to 280 to catch a
    /// wholesale deletion only; real per-sample coverage is asserted by ReferencedSamples and the
    /// orphan check below.
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

    /// <summary>Whole dotted identifiers in comment-free, literal-free test code.</summary>
    private static readonly Regex DottedIdentifier = new(
        @"[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)+",
        RegexOptions.Compiled);

    /// <summary>
    /// Type-level accessors whose presence means every definition of the type is asserted. ForSlot,
    /// Variants and Models were confirmed in test source; ByPose and All are defined but not yet used.
    /// </summary>
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

    /// <summary>Assertion C: the baseline only shrinks - a covered or stale entry must be deleted.</summary>
    [Fact]
    public void BaselineOnlyShrinks()
    {
        var root = StateMatrixBaseline.FindRepositoryRoot();
        var samples = SampleFiles(root);
        var definitions = ScalarDefinitions();
        var referenced = ReferencedSamples(root);
        var covered = CoveredDefinitions(root, definitions);
        var baseline = Baseline(root);

        var nowCovered = new List<string>();
        var stale = new List<string>();
        foreach (var entry in baseline)
        {
            if (IsSample(entry))
            {
                if (referenced.Contains(entry)) nowCovered.Add(entry);
                if (!samples.Contains(entry)) stale.Add(entry);
            }
            else
            {
                if (covered.Contains(entry)) nowCovered.Add(entry);
                if (!definitions.Contains(entry)) stale.Add(entry);
            }
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

    /// <summary>
    /// Sample literals are taken from comment-free code with string literals preserved: a comment that
    /// merely names a sample is not an assertion, while a literal passed through a helper is.
    /// </summary>
    private static HashSet<string> ReferencedSamples(string root)
    {
        var code = string.Join('\n', TestFiles(root).Select(file => StripCode(File.ReadAllText(file), keepLiterals: true)));
        return SampleLiteral.Matches(code)
            .Select(match => match.Groups[1].Value.Replace('\\', '/').Split('/')[^1])
            .Where(name => SampleName.IsMatch(name))
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// A definition counts as asserted when its complete Type.Member token (or a longer chain starting
    /// with it) appears in code, or when the declaring type has a collection accessor in code.
    /// </summary>
    private static HashSet<string> CoveredDefinitions(string root, IEnumerable<string> definitions)
    {
        var code = CodeText(root);
        var tokens = DottedIdentifier.Matches(code)
            .SelectMany(match => AllPrefixes(match.Value))
            .ToHashSet(StringComparer.Ordinal);
        var accessorTypes = tokens
            .Where(token => AccessorMembers.Any(member => token.EndsWith("." + member, StringComparison.Ordinal)))
            .Select(token => token[..token.LastIndexOf('.')])
            .ToHashSet(StringComparer.Ordinal);

        return definitions
            .Where(definition => tokens.Contains(definition)
                || accessorTypes.Contains(definition[..definition.IndexOf('.')]))
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>A.B.C also asserts A.B and A, so a definition reached through a member chain still counts.</summary>
    private static IEnumerable<string> AllPrefixes(string dotted)
    {
        var parts = dotted.Split('.');
        for (var count = 1; count <= parts.Length; count++)
        {
            yield return string.Join('.', parts, 0, count);
        }
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

    /// <summary>Test code with comments and literals blanked, used for definition identifier matching.</summary>
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
    /// Minimal C# lexer that replaces comments and (unless keepLiterals) literals with spaces, keeping
    /// newlines so later code stays visible. Handles line/block comments, regular, verbatim,
    /// interpolated, raw strings (3+ quotes) and char literals. A // inside a string is never a comment.
    /// </summary>
    internal static string StripCode(string source, bool keepLiterals = false)
    {
        var output = new StringBuilder(source.Length);
        var index = 0;
        while (index < source.Length)
        {
            var start = index;
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
                EmitBlank(output, source, start, index + 2);
                index += 2;
                while (index < source.Length && !(source[index] == '*' && index + 1 < source.Length && source[index + 1] == '/'))
                {
                    index++;
                }

                if (index < source.Length)
                {
                    EmitBlank(output, source, index, index + 2);
                    index += 2;
                }

                continue;
            }

            if (c == '@' && next == '"')
            {
                index += 2;
                while (index < source.Length)
                {
                    if (source[index] == '"' && index + 1 < source.Length && source[index + 1] == '"')
                    {
                        index += 2;
                        continue;
                    }

                    if (source[index] == '"')
                    {
                        index++;
                        break;
                    }

                    index++;
                }

                Emit(output, source, start, index, keepLiterals);
                continue;
            }

            if (c == '"')
            {
                var quotes = 0;
                while (start + quotes < source.Length && source[start + quotes] == '"')
                {
                    quotes++;
                }

                if (quotes >= 3)
                {
                    index = start + quotes;
                    while (index < source.Length)
                    {
                        var run = 0;
                        while (index + run < source.Length && source[index + run] == '"')
                        {
                            run++;
                        }

                        if (run >= quotes)
                        {
                            index += quotes;
                            break;
                        }

                        index += run == 0 ? 1 : run;
                    }

                    Emit(output, source, start, index, keepLiterals);
                    continue;
                }

                index++;
                while (index < source.Length)
                {
                    if (source[index] == '\\')
                    {
                        index += 2;
                        continue;
                    }

                    if (source[index] == '"')
                    {
                        index++;
                        break;
                    }

                    index++;
                }

                Emit(output, source, start, index, keepLiterals);
                continue;
            }

            if (c == '\'')
            {
                index++;
                while (index < source.Length)
                {
                    if (source[index] == '\\')
                    {
                        index += 2;
                        continue;
                    }

                    if (source[index] == '\'')
                    {
                        index++;
                        break;
                    }

                    index++;
                }

                Emit(output, source, start, index, keepLiterals);
                continue;
            }

            output.Append(c);
            index++;
        }

        return output.ToString();
    }

    private static void Emit(StringBuilder output, string source, int start, int end, bool keepLiterals)
    {
        if (keepLiterals)
        {
            output.Append(source, start, end - start);
        }
        else
        {
            EmitBlank(output, source, start, end);
        }
    }

    private static void EmitBlank(StringBuilder output, string source, int start, int end)
    {
        for (var index = start; index < end; index++)
        {
            output.Append(source[index] == '\n' ? '\n' : ' ');
        }
    }

    /// <summary>Scalar definitions: exactly one EntityModelDefinition per public static member.</summary>
    private static HashSet<string> ScalarDefinitions()
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in typeof(EntityModelDefinition).Assembly.GetTypes())
        {
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.FieldType == typeof(EntityModelDefinition)) result.Add(type.Name + "." + field.Name);
            }

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Static))
            {
                if (property.GetIndexParameters().Length == 0 && property.PropertyType == typeof(EntityModelDefinition))
                {
                    result.Add(type.Name + "." + property.Name);
                }
            }
        }

        return result;
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

/// <summary>
/// Unit tests for the lexer the coverage gate trusts. Each case asserts that code after a comment or
/// literal remains visible: swallowing later code is the failure mode being guarded against.
/// </summary>
public sealed class ModelSampleStripCodeTests
{
    [Fact]
    public void LineCommentDoesNotSwallowNextLine()
    {
        var stripped = ModelSampleCoverageTests.StripCode("// note Keep.Me\nAfter.Definition");
        Assert.DoesNotContain("note Keep.Me", stripped, StringComparison.Ordinal);
        Assert.Contains("After.Definition", stripped, StringComparison.Ordinal);
        Assert.Contains("\n", stripped, StringComparison.Ordinal);
    }

    [Fact]
    public void BlockCommentDoesNotSwallowNextCode()
    {
        var stripped = ModelSampleCoverageTests.StripCode("/* note Keep.Me */ After.Definition");
        Assert.DoesNotContain("note", stripped, StringComparison.Ordinal);
        Assert.Contains("After.Definition", stripped, StringComparison.Ordinal);
    }

    [Fact]
    public void RegularStringDoesNotSwallowNextCode()
    {
        var stripped = ModelSampleCoverageTests.StripCode("\"Keep.Me\" After.Definition");
        Assert.DoesNotContain("Keep.Me", stripped, StringComparison.Ordinal);
        Assert.Contains("After.Definition", stripped, StringComparison.Ordinal);
    }

    [Fact]
    public void VerbatimStringDoesNotSwallowNextCode()
    {
        var stripped = ModelSampleCoverageTests.StripCode("@\"Keep.Me \"\"quoted\"\"\" After.Definition");
        Assert.DoesNotContain("Keep.Me", stripped, StringComparison.Ordinal);
        Assert.Contains("After.Definition", stripped, StringComparison.Ordinal);
    }

    [Fact]
    public void RawStringDoesNotSwallowNextCode()
    {
        var stripped = ModelSampleCoverageTests.StripCode("\"\"\"Keep.Me\"\"\" After.Definition");
        Assert.DoesNotContain("Keep.Me", stripped, StringComparison.Ordinal);
        Assert.Contains("After.Definition", stripped, StringComparison.Ordinal);
    }

    [Fact]
    public void InterpolatedStringDoesNotSwallowNextCode()
    {
        var stripped = ModelSampleCoverageTests.StripCode("$\"Keep.Me {value}\" After.Definition");
        Assert.DoesNotContain("Keep.Me", stripped, StringComparison.Ordinal);
        Assert.Contains("After.Definition", stripped, StringComparison.Ordinal);
    }

    [Fact]
    public void InterpolatedVerbatimStringDoesNotSwallowNextCode()
    {
        var stripped = ModelSampleCoverageTests.StripCode("@$\"Keep.Me {value}\" After.Definition");
        Assert.DoesNotContain("Keep.Me", stripped, StringComparison.Ordinal);
        Assert.Contains("After.Definition", stripped, StringComparison.Ordinal);
    }

    [Fact]
    public void HttpInsideStringIsNotAComment()
    {
        var stripped = ModelSampleCoverageTests.StripCode("\"http://Keep.Me\" After.Definition");
        Assert.DoesNotContain("Keep.Me", stripped, StringComparison.Ordinal);
        Assert.Contains("After.Definition", stripped, StringComparison.Ordinal);
    }

    [Fact]
    public void FourQuoteRawStringDoesNotSwallowNextCode()
    {
        var stripped = ModelSampleCoverageTests.StripCode("\"\"\"\"Keep.Me \"\"\"\" After.Definition");
        Assert.DoesNotContain("Keep.Me", stripped, StringComparison.Ordinal);
        Assert.Contains("After.Definition", stripped, StringComparison.Ordinal);
    }

    [Fact]
    public void KeepLiteralsPreservesStringsButStillDropsComments()
    {
        var stripped = ModelSampleCoverageTests.StripCode("// note\nLoad(\"Keep.Me\");", keepLiterals: true);
        Assert.DoesNotContain("note", stripped, StringComparison.Ordinal);
        Assert.Contains("\"Keep.Me\"", stripped, StringComparison.Ordinal);
        Assert.Contains("Load(", stripped, StringComparison.Ordinal);
    }
}
