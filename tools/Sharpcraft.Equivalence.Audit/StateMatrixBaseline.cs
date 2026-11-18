// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using System.Text.RegularExpressions;
using Sharpcraft.Client.Render;
using Sharpcraft.Common.World;

namespace Sharpcraft.Equivalence.Audit;

/// <summary>
/// Machine-generated state-coverage baseline for production entity/block render types. This is a coverage matrix,
/// not proof of behavioural equivalence: SOURCE_SAMPLE rows point at a real repro sample file and key, SOURCE_CONSTRAINT
/// rows point at a formula/constant sample (energy/glint), and NO_SOURCE rows are explicitly uncovered.
/// </summary>
public static class StateMatrixBaseline
{
    public const string SourceSample = "SOURCE_SAMPLE";
    public const string SourceConstraint = "SOURCE_CONSTRAINT";
    public const string NoSource = "NO_SOURCE";
    /// <summary>Source behaviour exists but no keyed sample or formula can be confirmed; not a coverage gap.</summary>
    public const string Unknown = "UNKNOWN";

    // State classes are paired with the layers each can actually affect; no global cross product (#219).
    private static readonly (string State, string[] Layers)[] StateLayerAffinity =
    [
        ("base", ["base", "armor", "trim", "elytra", "special", "held"]),
        ("baby", ["base", "armor", "trim", "elytra", "special", "held"]),
        ("pose", ["base"]),
        ("held", ["held", "special", "special_glint"]),
        ("glint", ["glint", "armor", "trim", "special_glint"]),
        ("powered", ["base", "energy"]),
        ("energy", ["energy"])
    ];
    private static readonly HashSet<string> FileQualifiers =
    [
        "raw", "vectors", "reference", "baby", "armor", "helmet", "chestplate", "leggings", "boots",
        "small", "slim", "outer", "large", "medium", "double", "left", "right", "single", "patterns",
        "pattern", "flag", "base", "sides", "standing", "wall", "energy", "swirl", "uv", "held", "hand",
        "source", "double", "bone",
    ];

    public sealed record StateMatrixRow(
        string TypeKind,
        int TypeId,
        string TypeName,
        string StateClass,
        string LayerClass,
        string DefinitionOrLayer,
        string SampleFile,
        string SampleKey,
        string Status)
    {
        public bool Strict => Status is SourceSample or SourceConstraint;
        /// <summary>True when source has this type×state×layer combination (metadata-confirmed), else a cross-product row.</summary>
        public bool SourceRelevant { get; init; } = true;
    }

    private sealed record SampleFile(string RelativePath, string[] Tokens, string StateClass, string LayerClass, string[] Keys);

    /// <summary>Finds the checkout root from the current directory or the audit tool's build directory.</summary>
    public static string FindRepositoryRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Sharpcraft.slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Sharpcraft.slnx not found above the current or build directory");
    }

    /// <summary>Builds and self-validates the matrix against the repository's Repro/Data directory.</summary>
    public static IReadOnlyList<StateMatrixRow> Build(string repoRoot)
    {
        var sampleRoot = Path.Combine(repoRoot, "tests", "Sharpcraft.Client.Tests", "Repro", "Data");
        if (!Directory.Exists(sampleRoot))
        {
            throw new DirectoryNotFoundException($"state matrix sample root missing: {sampleRoot}");
        }

        var samples = Directory.GetFiles(sampleRoot, "*.reference.txt", SearchOption.TopDirectoryOnly)
            .Select(path => ParseSample(repoRoot, path))
            .OrderBy(sample => sample.RelativePath, StringComparer.Ordinal)
            .ToArray();

        var rows = new List<StateMatrixRow>();
        var names = EntityModelResolver.Definitions
            .Select(definition => definition.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal);
        foreach (var name in names)
        {
            var typeId = EntityModelResolver.FindTypeId(name);
            if (typeId is null)
            {
                continue;
            }

            var definition = EntityModelResolver.Resolve(typeId.Value);
            // Only layers with real geometry (Model bound by Decorate/equipment) exist in source; the
            // DefaultRenderLayers placeholders (elytra/cutout/...) have Model == null and are not render layers.
            var layers = LayersFor(definition);
            foreach (var state in StatesFor(typeId.Value))
            {
                foreach (var layer in layers)
                {
                    if (!AffinityAllows(state, layer))
                    {
                        continue; // source cannot express this state on this layer -> no cross-product row (#219)
                    }

                    rows.Add(Row("entity", typeId.Value, name, state, layer, definition.Name, samples));
                }
            }
        }

        for (var typeId = 0; typeId < VanillaBlockEntityTypes.Count; typeId++)
        {
            var path = VanillaBlockEntityTypes.PathOf(typeId);
            if (path is null)
            {
                continue;
            }

            foreach (var layer in BlockLayerClasses(typeId))
            {
                // Block entities have no baby/powered/pose/held/energy states; only `base` is meaningful.
                // Without a keyed sample the source behaviour is unconfirmed -> UNKNOWN, never NO_SOURCE by default (#219).
                var row = Row("block", typeId, path, "base", layer, path, samples);
                rows.Add(row.Status == NoSource ? row with { Status = Unknown } : row);
            }
        }

        // Self-check: every claimed source row must resolve to a real file and key. NO_SOURCE remains non-strict.
        foreach (var row in rows)
        {
            if (!row.Strict)
            {
                continue;
            }

            var sample = samples.Single(sample => sample.RelativePath == row.SampleFile);
            if (!sample.Keys.Contains(row.SampleKey, StringComparer.Ordinal))
            {
                throw new InvalidDataException($"state matrix references missing sample key: {row.SampleFile}#{row.SampleKey}");
            }

            if (!File.Exists(Path.Combine(repoRoot, row.SampleFile)))
            {
                throw new FileNotFoundException($"state matrix references missing sample file: {row.SampleFile}");
            }
        }

        return rows;
    }

    /// <summary>Writes the matrix TSV and its three-state/layer summaries.</summary>
    public static int Write(TextWriter writer, IReadOnlyList<StateMatrixRow> rows)
    {
        writer.WriteLine("type_kind\ttype_id\ttype_name\tstate_class\tlayer_class\tdefinition_or_layer\tsample_file\tsample_key\tstatus\tstrict");
        foreach (var row in rows)
        {
            writer.WriteLine(string.Join('\t',
                row.TypeKind,
                row.TypeId.ToString(CultureInfo.InvariantCulture),
                row.TypeName,
                row.StateClass,
                row.LayerClass,
                row.DefinitionOrLayer,
                row.SampleFile,
                row.SampleKey,
                row.Status,
                row.Strict ? "yes" : "no"));
        }

        writer.WriteLine();
        foreach (var group in rows.GroupBy(row => row.Status).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            writer.WriteLine($"# summary status={group.Key} count={group.Count()}");
        }

        foreach (var group in rows.GroupBy(row => row.LayerClass).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            writer.WriteLine($"# summary layer={group.Key} count={group.Count()}");
        }

        return rows.Count;
    }

    private static StateMatrixRow Row(
        string kind,
        int typeId,
        string typeName,
        string state,
        string layer,
        string definitionOrLayer,
        IReadOnlyList<SampleFile> samples)
    {
        var typeTokens = NormalizeTokens(typeName);
        if (state == "glint" && layer is "glint" or "armor" or "special_glint")
        {
            var glint = samples.FirstOrDefault(sample => sample.RelativePath.EndsWith("glint-texture-transform.reference.txt", StringComparison.Ordinal));
            if (glint is null)
            {
                return new StateMatrixRow(kind, typeId, typeName, state, layer, definitionOrLayer, string.Empty, string.Empty, NoSource);
            }

            var scale = layer == "special_glint" ? "0.5" : "0.16";
            var key = glint.Keys.FirstOrDefault(value => value.EndsWith("|" + scale, StringComparison.Ordinal)) ?? glint.Keys[0];
            return new StateMatrixRow(kind, typeId, typeName, state, layer, definitionOrLayer, glint.RelativePath, key, SourceConstraint);
        }

        if (state is "energy" or "powered" && layer == "energy" && typeTokens.Contains("creeper", StringComparer.Ordinal))
        {
            var energy = samples.FirstOrDefault(sample => sample.RelativePath.EndsWith("creeper-energy-swirl-uv.reference.txt", StringComparison.Ordinal));
            return energy is null
                ? new StateMatrixRow(kind, typeId, typeName, state, layer, definitionOrLayer, string.Empty, string.Empty, NoSource)
                : new StateMatrixRow(kind, typeId, typeName, state, layer, definitionOrLayer, energy.RelativePath, energy.Keys[0], SourceConstraint);
        }

        if (state is "glint" or "energy")
        {
            return new StateMatrixRow(kind, typeId, typeName, state, layer, definitionOrLayer, string.Empty, string.Empty, NoSource);
        }

        var candidate = FindSample(samples, typeTokens, state, layer, kind);
        return candidate is { } match
            ? new StateMatrixRow(kind, typeId, typeName, state, layer, definitionOrLayer, match.File.RelativePath, match.Key, SourceSample)
            : new StateMatrixRow(kind, typeId, typeName, state, layer, definitionOrLayer, string.Empty, string.Empty, NoSource);
    }

    private static (SampleFile File, string Key)? FindSample(
        IReadOnlyList<SampleFile> samples,
        string[] typeTokens,
        string state,
        string layer,
        string kind)
    {
        foreach (var sample in samples)
        {
            if (!MatchesType(sample.Tokens, typeTokens))
            {
                continue;
            }

            if (state == "baby" && sample.StateClass != "baby")
            {
                continue;
            }

            if (state != "baby" && sample.StateClass == "baby")
            {
                continue;
            }

            var stateCompatible = sample.StateClass == state
                || (state is "held" or "pose" && sample.StateClass == "held");
            if (!stateCompatible)
            {
                continue;
            }

            if (layer == "base" && sample.LayerClass != "base" && kind != "block")
            {
                continue;
            }

            if (layer != "base" && sample.LayerClass != layer && !(layer is "held" or "special" && sample.StateClass == "held"))
            {
                continue;
            }

            if (layer == "sides" && !sample.Keys.Any(key => key.Contains("SIDES", StringComparison.Ordinal)))
            {
                continue;
            }

            if (layer == "chest" && !sample.Keys.Any(key => key.Contains("CHEST", StringComparison.Ordinal)))
            {
                continue;
            }

            var key = layer switch
            {
                "armor" => sample.Keys.FirstOrDefault(value => value.Contains("ARMOR", StringComparison.Ordinal)) ?? sample.Keys[0],
                "sides" => sample.Keys.First(value => value.Contains("SIDES", StringComparison.Ordinal)),
                "chest" => sample.Keys.First(value => value.Contains("CHEST", StringComparison.Ordinal)),
                _ => sample.Keys[0]
            };
            return (sample, key);
        }

        return null;
    }

    private static bool MatchesType(string[] sampleTokens, string[] typeTokens)
    {
        if (typeTokens.Length == 0)
        {
            return false;
        }

        if (typeTokens.All(token => sampleTokens.Contains(token, StringComparer.Ordinal)))
        {
            return true;
        }

        // Held humanoid samples are named after the source layer, not each concrete humanoid type.
        return typeTokens.Contains("player", StringComparer.Ordinal)
            && sampleTokens.Contains("humanoid", StringComparer.Ordinal);
    }

    private static SampleFile ParseSample(string repoRoot, string path)
    {
        var relative = Path.GetRelativePath(repoRoot, path).Replace('\\', '/');
        var fileName = Path.GetFileName(path);
        var stem = fileName.Replace(".reference.txt", string.Empty, StringComparison.Ordinal)
            .Replace("-raw-vectors", string.Empty, StringComparison.Ordinal);
        var rawTokens = stem.Split('-', StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.ToLowerInvariant())
            .ToArray();
        var tokens = rawTokens.Where(token => !FileQualifiers.Contains(token)).ToArray();
        var state = fileName.Contains("energy", StringComparison.OrdinalIgnoreCase)
            ? "energy"
            : fileName.Contains("glint", StringComparison.OrdinalIgnoreCase)
                ? "glint"
                : fileName.Contains("held", StringComparison.OrdinalIgnoreCase) || fileName.Contains("hand", StringComparison.OrdinalIgnoreCase)
                    ? "held"
                    : fileName.Contains("baby", StringComparison.OrdinalIgnoreCase) ? "baby" : "base";
        var layer = rawTokens.Any(token => token is "armor" or "helmet" or "chestplate" or "leggings" or "boots")
            ? "armor"
            : rawTokens.Contains("elytra", StringComparer.Ordinal) ? "elytra"
            : fileName.Contains("decorated-pot-sides", StringComparison.OrdinalIgnoreCase) ? "sides"
                : fileName.Contains("chest", StringComparison.OrdinalIgnoreCase) ? "chest"
                    : "base";

        var keys = new List<string>();
        foreach (var line in File.ReadLines(path))
        {
            if (line.Length == 0 || line[0] == '#' || line.StartsWith("#MATRIX", StringComparison.Ordinal))
            {
                continue;
            }

            var columns = line.Split('\t');
            var key = state == "glint" && columns.Length > 1
                ? columns[0] + "|" + columns[1]
                : columns[0];
            if (!keys.Contains(key, StringComparer.Ordinal))
            {
                keys.Add(key);
            }
        }

        if (keys.Count == 0)
        {
            throw new InvalidDataException($"sample has no data keys: {fileName}");
        }

        return new SampleFile(relative, tokens, state, layer, keys.ToArray());
    }

    /// <summary>
    /// Real render layers of a definition: `base` plus every layer with bound geometry. The shared
    /// DefaultRenderLayers placeholders have Model == null and are not actual source render layers (#219).
    /// </summary>
    private static string[] LayersFor(EntityModelDefinition definition)
    {
        var layers = new[] { "base" }
            .Concat(definition.RenderLayers
                .Where(layer => layer.Model is not null)
                .Select(LayerClassFor))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(layer => layer, StringComparer.Ordinal)
            .ToArray();
        return layers;
    }

    /// <summary>
    /// States the given entity type can actually express, derived from resolver metadata:
    /// baby only when a baby definition resolves; powered/energy only for the source EnergySwirlLayer
    /// owners (creeper, wither); all other states are universally expressible by living entities.
    /// </summary>
    private static string[] StatesFor(int typeId)
    {
        var states = new List<string> { "base", "pose", "held", "glint" };
        if (EntityModelResolver.TryResolve(typeId, baby: true) is not null)
        {
            states.Add("baby");
        }

        if (typeId is CreeperModelDefinition.CreeperEntityTypeId or WitherBossModelDefinition.WitherEntityTypeId)
        {
            states.Add("powered");
            states.Add("energy");
        }

        return states.ToArray();
    }

    private static bool AffinityAllows(string state, string layer) =>
        StateLayerAffinity.Any(entry => entry.State == state && entry.Layers.Contains(layer, StringComparer.Ordinal));

    private static string LayerClassFor(EntityRenderLayerDefinition layer)
    {
        var name = layer.Name;
        if (name.Contains("held_special", StringComparison.OrdinalIgnoreCase))
        {
            return name.Contains("glint", StringComparison.OrdinalIgnoreCase) ? "special_glint" : "special";
        }

        if (name.Contains("glint", StringComparison.OrdinalIgnoreCase)) return "glint";
        if (name.Contains("energy", StringComparison.OrdinalIgnoreCase) || name.Contains("creeper_power", StringComparison.OrdinalIgnoreCase)) return "energy";
        if (name.Contains("armor", StringComparison.OrdinalIgnoreCase)) return "armor";
        if (name.Contains("trim", StringComparison.OrdinalIgnoreCase)) return "trim";
        if (name.Contains("held_special", StringComparison.OrdinalIgnoreCase)) return "special";
        if (name.Contains("held", StringComparison.OrdinalIgnoreCase)) return "held";
        if (layer.Kind == Sharpcraft.Framework.Rendering.EntityGeometryLayerKind.Elytra) return "elytra";
        return "base";
    }

    private static IEnumerable<string> BlockLayerClasses(int typeId)
    {
        if (typeId is VanillaBlockEntityTypes.Chest or VanillaBlockEntityTypes.TrappedChest or VanillaBlockEntityTypes.EnderChest)
        {
            return ["base", "chest"];
        }

        if (typeId == VanillaBlockEntityTypes.DecoratedPot)
        {
            return ["base", "sides"];
        }

        return ["base"];
    }

    private static string[] NormalizeTokens(string name) =>
        Regex.Split(name.ToLowerInvariant(), "[^a-z0-9]+")
            .Where(token => token.Length > 0)
            .ToArray();
}
