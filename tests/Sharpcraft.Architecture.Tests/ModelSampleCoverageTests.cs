// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Reflection;
using System.Text.RegularExpressions;
using Sharpcraft.Client.Render;
using Sharpcraft.Equivalence.Audit;
using Xunit;

namespace Sharpcraft.Architecture.Tests;

/// <summary>
/// Issue #165: a model-family x truth-sample coverage ratchet. A public model definition must either
/// be named by a truth assertion in the tests or be listed in the shrinking baseline; every recorded
/// reference sample must likewise be named by a test literal or be baselined. The baseline may only
/// shrink: an entry that becomes covered is a gate failure until it is removed.
/// </summary>
/// <remarks>
/// The two domains share one baseline file. A line ending in <c>.golden.txt</c> or
/// <c>.reference.txt</c> is an unreferenced sample; any other line is a <c>Type.Member</c> definition
/// identifier. Both are discovered, never hand-maintained as counts.
/// </remarks>
public sealed class ModelSampleCoverageTests
{
    /// <summary>Repository-relative baseline path; resolved through the shared repository locator.</summary>
    public const string BaselineRelativePath =
        "tests/Sharpcraft.Architecture.Tests/.model-sample-coverage-baseline.txt";

    private const string SampleDirectoryRelativePath =
        "tests/Sharpcraft.Client.Tests/Repro/Data";

    private const string TestsDirectoryRelativePath = "tests";

    /// <summary>String literals whose last path segment is a recorded reference sample.</summary>
    private static readonly Regex SampleLiteral = new(
        @"""([^""\r\n]*\.(?:reference|golden)\.txt)""",
        RegexOptions.Compiled);

    /// <summary>Assertion A: every recorded sample is named by a test literal or baselined.</summary>
    [Fact]
    public void EveryReferenceSampleIsReferencedOrBaselined()
    {
        var root = StateMatrixBaseline.FindRepositoryRoot();
        var samples = SampleFiles(root);
        var referenced = ReferencedSamples(root);
        var baseline = Baseline(root);

        var orphans = samples
            .Where(sample => !referenced.Contains(sample) && !baseline.Contains(sample))
            .OrderBy(sample => sample, StringComparer.Ordinal)
            .ToArray();

        Assert.True(orphans.Length == 0,
            "recorded reference samples with no test literal (reference them or add to "
            + BaselineRelativePath + "):\n" + string.Join("\n", orphans));
    }

    /// <summary>Assertion B: every public model definition is asserted by a test or baselined.</summary>
    [Fact]
    public void EveryModelDefinitionIsAssertedOrBaselined()
    {
        var root = StateMatrixBaseline.FindRepositoryRoot();
        var definitions = ModelDefinitions();
        var used = AssertedDefinitions(root, definitions);
        var baseline = Baseline(root);

        var uncovered = definitions
            .Where(definition => !used.Contains(definition) && !baseline.Contains(definition))
            .OrderBy(definition => definition, StringComparer.Ordinal)
            .ToArray();

        Assert.True(uncovered.Length == 0,
            "model definitions with no truth assertion (assert them or add to "
            + BaselineRelativePath + "):\n" + string.Join("\n", uncovered));
    }

    /// <summary>
    /// Assertion C: the baseline only shrinks. An entry that is now covered, or that no longer names a
    /// real sample or definition, must be deleted. A covered definition must be asserted by a real test.
    /// </summary>
    [Fact]
    public void BaselineOnlyShrinks()
    {
        var root = StateMatrixBaseline.FindRepositoryRoot();
        var samples = SampleFiles(root);
        var definitions = ModelDefinitions();
        var referenced = ReferencedSamples(root);
        var used = AssertedDefinitions(root, definitions);
        var baseline = Baseline(root);

        var nowCovered = baseline
            .Where(entry => IsSample(entry) ? referenced.Contains(entry) : used.Contains(entry))
            .OrderBy(entry => entry, StringComparer.Ordinal)
            .ToArray();
        Assert.True(nowCovered.Length == 0,
            "baseline entries are now covered; remove them from " + BaselineRelativePath
            + " so the ratchet can shrink:\n" + string.Join("\n", nowCovered));

        var stale = baseline
            .Where(entry => IsSample(entry) ? !samples.Contains(entry) : !definitions.Contains(entry))
            .OrderBy(entry => entry, StringComparer.Ordinal)
            .ToArray();
        Assert.True(stale.Length == 0,
            "baseline entries that no longer exist (delete them from "
            + BaselineRelativePath + "):\n" + string.Join("\n", stale));
    }

    private static bool IsSample(string entry) =>
        entry.EndsWith(".reference.txt", StringComparison.Ordinal)
        || entry.EndsWith(".golden.txt", StringComparison.Ordinal);

    private static HashSet<string> SampleFiles(string root)
    {
        var directory = Path.Combine(root, SampleDirectoryRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(directory))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        return Directory.EnumerateFiles(directory, "*.txt")
            .Select(Path.GetFileName)
            .Where(name => name is not null)
            .Select(name => name!)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static HashSet<string> ReferencedSamples(string root) =>
        TestSources(root)
            .SelectMany(source => SampleLiteral.Matches(source).Select(match => match.Groups[1].Value))
            .Select(name => name.Replace('\\', '/').Split('/')[^1])
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Definitions asserted by test source. Each reflected identifier is looked up as a literal
    /// substring in the concatenated test text rather than extracted with a naming-convention regex,
    /// so a definition whose declaring type is plural (BabyModelDefinitions) or otherwise outside the
    /// convention is still discovered. Comments count as mentions; the ratchet guards against a new
    /// model shipping with no test mention at all, and being lenient cannot create a false gap.
    /// </summary>
    private static HashSet<string> AssertedDefinitions(string root, IEnumerable<string> definitions)
    {
        var text = string.Join('\n', TestSources(root));
        return definitions.Where(text.Contains).ToHashSet(StringComparer.Ordinal);
    }

    private static IEnumerable<string> TestSources(string root)
    {
        var directory = Path.Combine(root, TestsDirectoryRelativePath);
        if (!Directory.Exists(directory))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (relative.Contains("/bin/", StringComparison.Ordinal) || relative.Contains("/obj/", StringComparison.Ordinal))
            {
                continue;
            }

            yield return File.ReadAllText(file);
        }
    }

    /// <summary>
    /// Every public static field or property in the EntityModelDefinition assembly whose type is a
    /// definition or a container of definitions, named as DeclaringType.Member.
    /// </summary>
    private static HashSet<string> ModelDefinitions()
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        var assembly = typeof(EntityModelDefinition).Assembly;
        foreach (var type in assembly.GetTypes())
        {
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (IsDefinitionBearing(field.FieldType))
                {
                    result.Add(type.Name + "." + field.Name);
                }
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

    private static bool IsDefinitionBearing(Type type)
    {
        if (type == typeof(EntityModelDefinition))
        {
            return true;
        }

        if (type.HasElementType)
        {
            return IsDefinitionBearing(type.GetElementType()!);
        }

        return type.IsGenericType && type.GetGenericArguments().Any(IsDefinitionBearing);
    }

    private static HashSet<string> Baseline(string root)
    {
        var path = Path.Combine(root, BaselineRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        return File.ReadLines(path)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToHashSet(StringComparer.Ordinal);
    }
}
