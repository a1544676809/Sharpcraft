// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

// Item rendering audit (L1 of the item-render equivalence layer).
//
// Resolves every item in the vanilla registry against a real asset pack and reports, per item and per display context,
// what the shared item-model path actually produces: model kind, quad count, texture slots, display transform and the
// resulting display-space extents. This produces facts only; it does not change rendering behaviour. The output is a
// JSONL file plus a summary, so a build with a regression (an item that stops resolving, a lost display transform, a
// bounding box that leaves the unit cube) shows up as a number instead of a screenshot.

using System.Globalization;
using System.Text;
using System.Text.Json;
using Sharpcraft.Client.Render;
using Sharpcraft.Common.Registries;

var packPath = Argument("--pack");
if (packPath is null)
{
    Console.Error.WriteLine("usage: Sharpcraft.ItemRender.Audit --pack <pack.zip> [--out <file.jsonl>] [--contexts gui,ground,fixed,thirdperson_righthand]");
    return 2;
}

if (!File.Exists(packPath))
{
    Console.Error.WriteLine($"pack not found: {packPath}");
    return 2;
}

var outPath = Argument("--out") ?? Path.Combine(Path.GetTempPath(), "sharpcraft-item-audit.jsonl");
var contexts = Contexts(Argument("--contexts"));

using var stack = new AssetStack { Policy = MissingResourcePolicy.Warn, Warn = _ => { } }
    .Add(new BuiltInFallbackSource())
    .Add(new ZipAssetSource(packPath, "base-pack"));
using var resources = new ResourceManager(stack, 1);

var models = new ItemModelResolver(resources);
var registry = ItemRegistry.LoadEmbedded();

var counts = new Counters();
using var writer = new StreamWriter(outPath, append: false, Encoding.UTF8);

foreach (var item in registry.Items)
{
    var resolution = models.Resolve(item.Id.ToString());
    var kind = Classify(resolution);
    counts.ByKind[kind] = counts.ByKind.GetValueOrDefault(kind) + 1;

    if (resolution is null)
    {
        counts.Unresolved.Add(item.Id.ToString());
    }

    var contextsJson = new Dictionary<string, object?>(StringComparer.Ordinal);
    foreach (var context in contexts)
    {
        var transform = resolution is not null && resolution.Display.TryGetValue(context, out var found)
            ? found
            : (ItemDisplayTransform?)null;
        if (transform is null)
        {
            counts.MissingDisplay[context] = counts.MissingDisplay.GetValueOrDefault(context) + 1;
            contextsJson[ContextName(context)] = null;
            continue;
        }

        ItemModelExtents? extents = resolution is null ? null : ItemModelExtents.ForDisplay(resolution, context);
        if (extents is { } box && !FitsUnitCube(box))
        {
            counts.ExtentOutliers++;
        }

        contextsJson[ContextName(context)] = new
        {
            rotation = new[] { transform.Value.RotationDegrees.X, transform.Value.RotationDegrees.Y, transform.Value.RotationDegrees.Z },
            translation = new[] { transform.Value.Translation.X, transform.Value.Translation.Y, transform.Value.Translation.Z },
            scale = new[] { transform.Value.Scale.X, transform.Value.Scale.Y, transform.Value.Scale.Z },
            min = extents is { } b2 ? new[] { b2.Min.X, b2.Min.Y, b2.Min.Z } : null,
            max = extents is { } b3 ? new[] { b3.Max.X, b3.Max.Y, b3.Max.Z } : null
        };
    }

    var line = JsonSerializer.Serialize(new
    {
        item = item.Id.ToString(),
        kind,
        model = resolution?.ModelId,
        special = resolution?.SpecialRendererType,
        textures = resolution?.Textures.Count ?? 0,
        quads = resolution?.Cuboids.Count ?? 0,
        tints = resolution?.TintSources.Count ?? 0,
        contexts = contextsJson
    });
    writer.WriteLine(line);
}

writer.Flush();

Console.WriteLine($"items            : {registry.Count}");
foreach (var (kind, count) in counts.ByKind.OrderBy(pair => pair.Key, StringComparer.Ordinal))
{
    Console.WriteLine($"  {kind,-14} : {count}");
}

Console.WriteLine($"unresolved       : {counts.Unresolved.Count}");
foreach (var id in counts.Unresolved.Take(12))
{
    Console.WriteLine($"  - {id}");
}

foreach (var (context, missing) in counts.MissingDisplay.OrderBy(pair => pair.Key))
{
    Console.WriteLine($"missing display  : {ContextName(context),-22} {missing}");
}

Console.WriteLine($"extent outliers  : {counts.ExtentOutliers}");
Console.WriteLine($"report           : {outPath}");

var summaryPath = Argument("--check");
if (Argument("--write-baseline") is { } baseline) { WriteBaseline(baseline); Console.WriteLine($"baseline         : {baseline}"); }
if (summaryPath is not null)
{
    var expected = File.ReadAllLines(summaryPath);
    var actual = SummaryLines().ToArray();
    if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
    {
        Console.Error.WriteLine("audit baseline mismatch:");
        foreach (var line in actual.Except(expected, StringComparer.Ordinal))
        {
            Console.Error.WriteLine($"  + {line}");
        }

        foreach (var line in expected.Except(actual, StringComparer.Ordinal))
        {
            Console.Error.WriteLine($"  - {line}");
        }

        return 1;
    }

    Console.WriteLine("audit baseline   : ok");
}

return 0;

IEnumerable<string> SummaryLines()
{
    yield return $"items {registry.Count}";
    foreach (var (kind, count) in counts.ByKind.OrderBy(pair => pair.Key, StringComparer.Ordinal))
    {
        yield return $"kind {kind} {count}";
    }

    yield return $"unresolved {counts.Unresolved.Count}";
    foreach (var id in counts.Unresolved.OrderBy(id => id, StringComparer.Ordinal))
    {
        yield return $"unresolved-id {id}";
    }

    foreach (var (context, missing) in counts.MissingDisplay.OrderBy(pair => pair.Key))
    {
        yield return $"missing-display {ContextName(context)} {missing}";
    }
}

void WriteBaseline(string path) => File.WriteAllLines(path, SummaryLines());

string? Argument(string name)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], name, StringComparison.Ordinal))
        {
            return args[i + 1];
        }
    }

    return null;
}

static ItemDisplayContext[] Contexts(string? spec)
{
    if (spec is null)
    {
        return
        [
            ItemDisplayContext.Gui, ItemDisplayContext.Ground, ItemDisplayContext.Fixed,
            ItemDisplayContext.ThirdPersonRightHand, ItemDisplayContext.ThirdPersonLeftHand,
            ItemDisplayContext.FirstPersonRightHand, ItemDisplayContext.Head
        ];
    }

    var parsed = new List<ItemDisplayContext>();
    foreach (var part in spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        parsed.Add(part switch
        {
            "gui" => ItemDisplayContext.Gui,
            "ground" => ItemDisplayContext.Ground,
            "fixed" => ItemDisplayContext.Fixed,
            "head" => ItemDisplayContext.Head,
            "thirdperson_righthand" => ItemDisplayContext.ThirdPersonRightHand,
            "thirdperson_lefthand" => ItemDisplayContext.ThirdPersonLeftHand,
            "firstperson_righthand" => ItemDisplayContext.FirstPersonRightHand,
            "firstperson_lefthand" => ItemDisplayContext.FirstPersonLeftHand,
            _ => throw new ArgumentException($"unknown display context: {part}")
        });
    }

    return [.. parsed];
}

static string ContextName(ItemDisplayContext context) => context switch
{
    ItemDisplayContext.Gui => "gui",
    ItemDisplayContext.Ground => "ground",
    ItemDisplayContext.Fixed => "fixed",
    ItemDisplayContext.Head => "head",
    ItemDisplayContext.ThirdPersonRightHand => "thirdperson_righthand",
    ItemDisplayContext.ThirdPersonLeftHand => "thirdperson_lefthand",
    ItemDisplayContext.FirstPersonRightHand => "firstperson_righthand",
    ItemDisplayContext.FirstPersonLeftHand => "firstperson_lefthand",
    _ => context.ToString().ToLowerInvariant()
};

static string Classify(ItemModelResolution? resolution) => resolution switch
{
    null => "empty",
    { SpecialRendererType: not null } => "special:" + resolution.SpecialRendererType,
    { Cuboids.Count: > 0 } => "elements",
    _ => "generated"
};

static bool FitsUnitCube(ItemModelExtents box) =>
    box.Min.X >= -0.51f && box.Min.Y >= -0.51f && box.Min.Z >= -0.51f &&
    box.Max.X <= 1.51f && box.Max.Y <= 1.51f && box.Max.Z <= 1.51f;

internal sealed class Counters
{
    public Dictionary<string, int> ByKind { get; } = new(StringComparer.Ordinal);
    public List<string> Unresolved { get; } = [];
    public Dictionary<ItemDisplayContext, int> MissingDisplay { get; } = [];
    public int ExtentOutliers { get; set; }
}
