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

    // All state classes known to the coverage baseline. A type only emits the subset it can actually
    // express (see CapabilitiesFor) and only on layers that carry the matching metadata (AllowedCombinations).
    private static readonly string[] AllStates = ["base", "baby", "pose", "held", "glint", "energy", "powered"];

    /// <summary>The full state-class domain used by tests to build per-type candidate pairs (#219 round 4).</summary>
    public static IReadOnlyList<string> AllStateClasses => AllStates;

    /// <summary>States a concrete entity type can express, derived from resolver/layer metadata (#219).</summary>
    public sealed record TypeStateCapabilities(
        bool Baby, bool Pose, bool Held, bool Glint, bool Energy, bool Powered)
    {
        public IEnumerable<string> States()
        {
            if (Baby) yield return "baby";
            if (Pose) yield return "pose";
            if (Held) yield return "held";
            if (Glint) yield return "glint";
            if (Energy) yield return "energy";
            if (Powered) yield return "powered";
            yield return "base";
        }
    }

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
        /// <summary>
        /// Computed: true only when the (state,layer) pair belongs to the metadata-derived allowed set for
        /// this row's type. Disallowed cross-product combinations are never emitted into the matrix (#219).
        /// </summary>
        public bool SourceRelevant { get; init; }
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

    /// <summary>All entity type ids the matrix is built from, from the machine resolver enumeration (#219 round 3).</summary>
    public static IReadOnlyList<int> AllEntityTypeIds() => EntityModelResolver.Definitions
        .Select(definition => definition.Name)
        .Where(name => !string.IsNullOrWhiteSpace(name))
        .Distinct(StringComparer.Ordinal)
        .Select(EntityModelResolver.FindTypeId)
        .Where(id => id.HasValue)
        .Select(id => id!.Value)
        .Distinct()
        .OrderBy(id => id)
        .ToArray();

    /// <summary>All block entity type ids with a registry path, from the machine registry enumeration (#219 round 3).</summary>
    public static IReadOnlyList<int> AllBlockTypeIds() => Enumerable.Range(0, VanillaBlockEntityTypes.Count)
        .Where(typeId => VanillaBlockEntityTypes.PathOf(typeId) is not null)
        .ToArray();

    /// <summary>The real render layer classes declared for a block entity type.</summary>
    public static IReadOnlyList<string> BlockLayersFor(int typeId) => BlockLayerClasses(typeId).ToArray();

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
        foreach (var typeId in AllEntityTypeIds())
        {
            var definition = EntityModelResolver.Resolve(typeId);
            var name = definition.Name;
            var layers = LayersFor(definition);
            var capabilities = CapabilitiesFor(typeId);
            var allowed = AllowedCombinations(typeId);
            // Iterate the full state×layer domain and compute relevance per pair; disallowed pairs are
            // filtered out. This keeps SourceRelevant a computed value (false cases exist) and emits
            // only metadata-allowed combinations (#219).
            foreach (var state in capabilities.States())
            {
                foreach (var layer in layers)
                {
                    var relevant = allowed.Contains((state, layer));
                    if (!relevant)
                    {
                        continue;
                    }

                    var row = Row("entity", typeId, name, state, layer, definition.Name, samples);
                    rows.Add(row with { SourceRelevant = relevant });
                }
            }
        }

        foreach (var typeId in AllBlockTypeIds())
        {
            var path = VanillaBlockEntityTypes.PathOf(typeId)!;
            foreach (var layer in BlockLayerClasses(typeId))
            {
                // Block entities have no baby/powered/pose/held/energy states; only `base` is meaningful.
                // Without a keyed sample the source behaviour is unconfirmed -> UNKNOWN, never NO_SOURCE by default (#219).
                var row = Row("block", typeId, path, "base", layer, path, samples);
                // Block `base` on its declared layers is always metadata-allowed; unconfirmed -> UNKNOWN.
                row = row with { SourceRelevant = true };
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

    /// <summary>Real render layer classes of a resolved definition (metadata: Model bound by Decorate/equipment).</summary>
    public static IReadOnlyList<string> LayersFor(int typeId) => LayersFor(EntityModelResolver.Resolve(typeId));

    /// <summary>
    /// States a type can express, derived entirely from resolver/layer metadata (#219):
    /// baby from a resolving baby definition; pose from real body geometry; held/glint/energy/powered from
    /// the matching layer markers (held/special layer, GlintScale, UvOffset energy-swirl layer).
    /// </summary>
    public static TypeStateCapabilities CapabilitiesFor(int typeId)
    {
        var definition = EntityModelResolver.Resolve(typeId);
        var layers = definition.RenderLayers
            .Where(layer => layer.Model is not null)
            .ToArray();
        var baby = EntityModelResolver.TryResolve(typeId, baby: true) is not null;
        // The base body layer exists for every model with real geometry; pose animates it. Non-equipment
        // definitions keep the shared DefaultRenderLayers placeholders (Model == null) yet still have body parts.
        var pose = definition.Parts.Count > 0;
        var held = layers.Any(layer => LayerClassFor(layer) is "held" or "special" or "special_glint");
        var glint = layers.Any(layer => layer.GlintScale is not null || LayerClassFor(layer) is "glint" or "special_glint");
        // EnergySwirlLayer presence (UvOffset layer) gates both the powered and energy states on its owner.
        var energy = layers.Any(layer => layer.UvOffset is not null || LayerClassFor(layer) == "energy");
        return new TypeStateCapabilities(baby, pose, held, glint, energy, energy);
    }

    /// <summary>
    /// The metadata-derived set of (state, layer) pairs source can actually express for a type (#219).
    /// Each layer contributes the states carried by its own markers; no cross-product table is involved.
    /// </summary>
    public static HashSet<(string State, string Layer)> AllowedCombinations(int typeId)
    {
        var capabilities = CapabilitiesFor(typeId);
        var allowed = new HashSet<(string, string)>();
        var definition = EntityModelResolver.Resolve(typeId);

        // Every entity with body geometry has a base layer: base/pose (+baby) are metadata-allowed there.
        // This is derived from Parts, not from the equipment-layer Model markers used below.
        if (definition.Parts.Count > 0)
        {
            allowed.Add(("base", "base"));
            if (capabilities.Pose) allowed.Add(("pose", "base"));
            if (capabilities.Baby) allowed.Add(("baby", "base"));
        }

        foreach (var layer in definition.RenderLayers.Where(layer => layer.Model is not null))
        {
            var layerClass = LayerClassFor(layer);
            if (layer.UvOffset is not null || layerClass == "energy")
            {
                allowed.Add(("energy", layerClass));
                allowed.Add(("powered", layerClass));
            }

            if (layer.GlintScale is not null || layerClass is "glint" or "special_glint")
            {
                allowed.Add(("glint", layerClass));
            }

            if (layerClass is "held" or "special" or "special_glint")
            {
                allowed.Add(("held", layerClass));
                if (capabilities.Baby) allowed.Add(("baby", layerClass));
            }

            if (layerClass is "armor" or "trim" or "elytra")
            {
                allowed.Add(("base", layerClass));
                if (capabilities.Baby) allowed.Add(("baby", layerClass));
            }
        }

        return allowed;
    }

    /// <summary>Computed relevance of a (state,layer) pair for a type, from metadata (#219).</summary>
    public static bool IsSourceRelevant(int typeId, string state, string layer) =>
        AllowedCombinations(typeId).Contains((state, layer));

    private static string LayerClassFor(EntityRenderLayerDefinition layer)
    {
        var name = layer.Name;
        if (name.Contains("held_special", StringComparison.OrdinalIgnoreCase))
        {
            return name.Contains("glint", StringComparison.OrdinalIgnoreCase) ? "special_glint" : "special";
        }

        if (name.Contains("glint", StringComparison.OrdinalIgnoreCase) || layer.GlintScale is not null) return "glint";
        if (name.Contains("energy", StringComparison.OrdinalIgnoreCase) || name.Contains("creeper_power", StringComparison.OrdinalIgnoreCase)
            || layer.UvOffset is not null) return "energy";
        if (name.Contains("armor", StringComparison.OrdinalIgnoreCase)) return "armor";
        if (name.Contains("trim", StringComparison.OrdinalIgnoreCase)) return "trim";
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
