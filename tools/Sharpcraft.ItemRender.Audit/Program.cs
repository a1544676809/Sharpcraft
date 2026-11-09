// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

// Item rendering audit (L1 of the item-render equivalence layer).
//
// Resolves every item in the vanilla registry against a real asset pack and reports, per item and per display context,
// what the shared item-model path actually produces: model kind, quad count, texture slots, display transform and the
// resulting display-space extents. This produces facts only; it does not change rendering behaviour. The output is a
// JSONL file plus a summary, so a build with a regression (an item that stops resolving, a lost display transform, a
// bounding box that leaves the unit cube) shows up as a number instead of a screenshot.

using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Sharpcraft.Client.Render;
using Sharpcraft.Common.Imaging;
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

if (Argument("--textures") is not null)
{
    return TextureAudit(stack, outPath);
}
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

/// <summary>
/// Audits every texture in the pack: the colour-type / bit-depth distribution the decoder has to support, and a
/// decode round trip per file. A decoder that mis-indexes one layout (grey+alpha, T-773) shows up here as a per-file
/// failure instead of as a rendering bug days later.
/// </summary>
static int TextureAudit(AssetStack stack, string outPath)
{
    var byFormat = new Dictionary<(int ColourType, int BitDepth), int>();
    var failures = new List<string>();
    var total = 0;

    using var writer = new StreamWriter(outPath, append: false, Encoding.UTF8);
    foreach (var location in stack.ListAll("assets/minecraft/textures/").OrderBy(id => id.ToString(), StringComparer.Ordinal))
    {
        var bytes = stack.Read(ResourceKind.Texture, location);
        if (bytes is null)
        {
            continue;
        }

        total++;
        if (bytes.Length < 26)
        {
            failures.Add($"{location}: truncated ({bytes.Length} bytes)");
            continue;
        }

        var bitDepth = (int)bytes[24];
        var colourType = (int)bytes[25];
        var key = (colourType, bitDepth);
        byFormat[key] = byFormat.GetValueOrDefault(key) + 1;

        try
        {
            var image = PngDecoder.Decode(bytes);
            var width = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4));
            var height = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4));
            if (image.Width != width || image.Height != height || image.Pixels.Length != width * height * 4)
            {
                failures.Add($"{location}: decoded {image.Width}x{image.Height} ({image.Pixels.Length} bytes) for a header of {width}x{height}");
                continue;
            }

            writer.WriteLine(JsonSerializer.Serialize(new
            {
                texture = location.ToString(),
                colourType,
                bitDepth,
                width,
                height
            }));
        }
        catch (Exception error)
        {
            failures.Add($"{location}: {error.GetType().Name}: {error.Message}");
        }
    }

    Console.WriteLine($"textures: {total} PNGs, {byFormat.Count} distinct colour-type/bit-depth combinations");
    foreach (var pair in byFormat.OrderByDescending(entry => entry.Value))
    {
        Console.WriteLine($"  colourType {pair.Key.ColourType} bitDepth {pair.Key.BitDepth}: {pair.Value}");
    }

    if (failures.Count > 0)
    {
        Console.WriteLine($"FAILED: {failures.Count} textures did not decode cleanly:");
        foreach (var failure in failures.Take(20))
        {
            Console.WriteLine($"  {failure}");
        }

        return 1;
    }

    Console.WriteLine("ok: every texture decoded with matching dimensions");
    return 0;
}

internal sealed class Counters
{
    public Dictionary<string, int> ByKind { get; } = new(StringComparer.Ordinal);
    public List<string> Unresolved { get; } = [];
    public Dictionary<ItemDisplayContext, int> MissingDisplay { get; } = [];
    public int ExtentOutliers { get; set; }
}
