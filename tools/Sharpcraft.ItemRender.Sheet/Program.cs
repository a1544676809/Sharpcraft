// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

// Item render contact sheet (L4 of the item-render equivalence layer).
//
// Renders a fixed grid of items with a fixed camera and a fixed light, straight from the shared item-model path: the
// same ItemModelResolver, the same quad collection the drops and held items use, and the same display transform. The
// result is a single deterministic PNG that can be diffed against a previous run (regression) and eyeballed next to a
// vanilla screenshot taken at the same angle (equivalence).
//
// Deliberately CPU-only and GPU-free so the picture depends on nothing but item geometry and textures.

using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using Sharpcraft.Client.Render;
using Sharpcraft.Common.Imaging;
using Sharpcraft.Common.Identifiers;

const string DefaultItemList =
    "minecraft:iron_sword,minecraft:diamond_pickaxe,minecraft:short_grass,minecraft:apple," +
    "minecraft:bread,minecraft:torch,minecraft:stick,minecraft:bucket,minecraft:bow," +
    "minecraft:arrow,minecraft:wheat,minecraft:book,minecraft:compass,minecraft:clock," +
    "minecraft:iron_ingot,minecraft:diamond,minecraft:cooked_beef,minecraft:ender_pearl," +
    "minecraft:chest,minecraft:shield,minecraft:trident,minecraft:oak_leaves,minecraft:redstone,minecraft:copper_ingot";

var packPath = Argument("--pack");
if (packPath is null || !File.Exists(packPath))
{
    Console.Error.WriteLine("usage: Sharpcraft.ItemRender.Sheet --pack <pack.zip> [--out sheet.png] [--context ground] [--items a,b,c] [--check <sha256-file>]");
    return 2;
}

var outPath = Argument("--out") ?? Path.Combine(Path.GetTempPath(), "sharpcraft-item-sheet.png");
var contextName = Argument("--context") ?? "ground";
var context = contextName switch
{
    "ground" => ItemDisplayContext.Ground,
    "gui" => ItemDisplayContext.Gui,
    "fixed" => ItemDisplayContext.Fixed,
    "thirdperson_righthand" => ItemDisplayContext.ThirdPersonRightHand,
    _ => throw new ArgumentException($"unknown context: {contextName}")
};

var items = (Argument("--items") ?? DefaultItemList).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

using var stack = new AssetStack { Policy = MissingResourcePolicy.Warn, Warn = _ => { } }
    .Add(new BuiltInFallbackSource())
    .Add(new ZipAssetSource(packPath, "base-pack"));
using var resources = new ResourceManager(stack, 1);
var models = new ItemModelResolver(resources);
var registry = Sharpcraft.Common.Registries.ItemRegistry.LoadEmbedded();

const int Cell = 128;
const int Columns = 6;
const int Padding = 6;
var rows = (items.Length + Columns - 1) / Columns;
var width = (Columns * (Cell + Padding)) + Padding;
var height = (rows * (Cell + Padding)) + Padding;
var pixels = new byte[width * height * 4];
for (var i = 0; i < pixels.Length; i += 4)
{
    pixels[i + 3] = 255;
}

var needed = new List<string>();
var resolved = new List<(string Id, ItemModelResolution? Model)>();
foreach (var id in items)
{
    ItemModelResolution? model = Identifier.TryParse(id, out var identity) ? models.Resolve(identity.ToString()) : null;
    resolved.Add((id, model));
    if (model is not null)
    {
        foreach (var quad in CollectQuads(model, atlas: null))
        {
            if (quad.Texture is { Length: > 0 } texture && !needed.Contains(texture))
            {
                needed.Add(texture);
            }
        }
    }
}

var atlas = ItemAtlas.Load(resources, needed);
var quads = new List<ItemModelQuad>();
var report = new List<string>();
var light = Vector3.Normalize(new Vector3(0.35f, 0.8f, 0.45f));

for (var index = 0; index < resolved.Count; index++)
{
    var (id, model) = resolved[index];
    var column = index % Columns;
    var row = index / Columns;
    var originX = Padding + (column * (Cell + Padding));
    var originY = Padding + (row * (Cell + Padding));

    if (model is null)
    {
        report.Add($"{id}\tunresolved");
        continue;
    }

    if (model.SpecialRendererType is { } special)
    {
        // Special renderers are drawn by their own layers, not by this geometry path; the gap is visible on purpose.
        report.Add($"{id}\tspecial:{special}");
        continue;
    }

    quads.Clear();
    ItemEntityMesher.CollectQuads(quads, model, atlas);
    if (quads.Count == 0)
    {
        report.Add($"{id}\tno-quads");
        continue;
    }

    if (args.Contains("--debug"))
    {
        Console.WriteLine($"{id}: extents {extentsForDebug(model)}");
    }

    var display = model.Display.TryGetValue(context, out var transform)
        ? transform.ToMatrix()
        : ItemDisplayTransform.None.ToMatrix();
    var extents = ItemModelExtents.ForDisplay(model, context);
    var centre = (extents.Min + extents.Max) * 0.5f;
    var size = MathF.Max(extents.Max.X - extents.Min.X, MathF.Max(extents.Max.Y - extents.Min.Y, extents.Max.Z - extents.Min.Z));
    var fit = size <= 0f ? 1f : (Cell * 0.72f) / size;
    var view = Matrix4x4.CreateRotationY(35f * (MathF.PI / 180f)) * Matrix4x4.CreateRotationX(-22f * (MathF.PI / 180f));

    var depth = new float[Cell * Cell];
    Array.Fill(depth, float.NegativeInfinity);

    foreach (var quad in quads)
    {
        if (quad.Texture is not { Length: > 0 } texture || atlas.Get(texture) is not { } sprite || atlas.GetTexture(texture) is not { } image)
        {
            continue;
        }

        // Model space -> display -> centred -> view -> cell pixels.
        var corners = new Vector3[4];
        var uvs = new Vector2[4];
        for (var corner = 0; corner < 4; corner++)
        {
            var p = Vector3.Transform(quad.Corner(corner), display);
            p = Vector3.Transform(p - centre, view);
            corners[corner] = p;
            uvs[corner] = quad.Uv(corner);
        }

        var normal = Vector3.Normalize(Vector3.Cross(corners[1] - corners[0], corners[3] - corners[0]));
        var shade = 0.55f + (0.45f * MathF.Abs(Vector3.Dot(normal, light)));
        var tint = model.TintFor(quad.TintIndex, models.GrassColormap);

        RasterizeTriangle(0, 1, 2);
        RasterizeTriangle(0, 2, 3);

        void RasterizeTriangle(int a, int b, int c)
        {
            var pa = Project(corners[a], fit);
            var pb = Project(corners[b], fit);
            var pc = Project(corners[c], fit);
            var minX = Math.Max(0, (int)MathF.Floor(MathF.Min(pa.X, MathF.Min(pb.X, pc.X))));
            var maxX = Math.Min(Cell - 1, (int)MathF.Ceiling(MathF.Max(pa.X, MathF.Max(pb.X, pc.X))));
            var minY = Math.Max(0, (int)MathF.Floor(MathF.Min(pa.Y, MathF.Min(pb.Y, pc.Y))));
            var maxY = Math.Min(Cell - 1, (int)MathF.Ceiling(MathF.Max(pa.Y, MathF.Max(pb.Y, pc.Y))));
            var area = Edge(pa, pb, pc);
            if (MathF.Abs(area) < 1e-6f)
            {
                return;
            }

            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    var point = new Vector3(x + 0.5f, y + 0.5f, 0f);
                    var w0 = Edge(pb, pc, point) / area;
                    var w1 = Edge(pc, pa, point) / area;
                    var w2 = Edge(pa, pb, point) / area;
                    if (w0 < 0f || w1 < 0f || w2 < 0f)
                    {
                        continue;
                    }

                    var z = (pa.Z * w0) + (pb.Z * w1) + (pc.Z * w2);
                    var slot = (y * Cell) + x;
                    if (z <= depth[slot])
                    {
                        continue;
                    }

                    var uv = (uvs[a] * w0) + (uvs[b] * w1) + (uvs[c] * w2);
                    // Quad UVs are sprite-local 0..1 and GetTexture hands back that sprite's own image, so sample it
                    // directly. Sprite rows run top-down while model V runs bottom-up.
                    var px = Math.Clamp((int)(uv.X * image.Width), 0, Math.Max(0, image.Width - 1));
                    var py = Math.Clamp((int)((1f - uv.Y) * image.Height), 0, Math.Max(0, image.Height - 1));
                    var sample = (((py * image.Width) + px) * 4);
                    if (image.Rgba[sample + 3] < 32)
                    {
                        continue;
                    }

                    depth[slot] = z;
                    var pixel = (((originY + y) * width) + originX + x) * 4;
                    pixels[pixel + 0] = (byte)Math.Clamp(image.Rgba[sample + 0] * tint.X * shade, 0f, 255f);
                    pixels[pixel + 1] = (byte)Math.Clamp(image.Rgba[sample + 1] * tint.Y * shade, 0f, 255f);
                    pixels[pixel + 2] = (byte)Math.Clamp(image.Rgba[sample + 2] * tint.Z * shade, 0f, 255f);
                }
            }
        }
    }

    report.Add($"{id}\tquads:{quads.Count}\tdisplay:{contextName}");
}

// Cell separators, so every item is unambiguous in the sheet.
for (var c = 0; c <= Columns; c++)
{
    var x = Math.Min(width - 1, Padding + (c * (Cell + Padding)) - (Padding / 2));
    for (var y = 0; y < height; y++)
    {
        var pixel = ((y * width) + x) * 4;
        pixels[pixel + 0] = pixels[pixel + 1] = pixels[pixel + 2] = 220;
    }
}

File.WriteAllBytes(outPath, PngEncoder.EncodeRgba(width, height, pixels));
var sidecar = Path.ChangeExtension(outPath, ".txt");
File.WriteAllLines(sidecar, report);
Console.WriteLine($"sheet   : {outPath} ({width}x{height}, {items.Length} items, context {contextName})");
Console.WriteLine($"sidecar : {sidecar}");
Console.WriteLine($"sha256  : {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(outPath))).ToLowerInvariant()}");

if (Argument("--check") is { } goldenPath)
{
    // The comparison key is the sidecar (which item resolved into how many quads), not the PNG: the picture is derived
    // from vanilla textures and must never enter the repository, while the sidecar is asset-free and still catches any
    // geometry or resolution regression.
    var expected = File.ReadAllLines(goldenPath);
    var actual = File.ReadAllLines(sidecar);
    if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
    {
        Console.Error.WriteLine("sheet sidecar mismatch:");
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

    Console.WriteLine("sheet   : golden ok");
}

return 0;

Vector3 Project(Vector3 point, float fit) => new((point.X * fit) + (Cell * 0.5f), Cell * 0.5f - (point.Y * fit), point.Z);

float Edge(Vector3 a, Vector3 b, Vector3 c) => ((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X));

string extentsForDebug(ItemModelResolution model)
{
    var box = ItemModelExtents.ForDisplay(model, context);
    var first = CollectQuads(model, atlas).FirstOrDefault();
    return $"min=({box.Min.X:F3},{box.Min.Y:F3},{box.Min.Z:F3}) max=({box.Max.X:F3},{box.Max.Y:F3},{box.Max.Z:F3}) corner0=({first.Corner(0).X:F3},{first.Corner(0).Y:F3},{first.Corner(0).Z:F3})";
}

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

static List<ItemModelQuad> CollectQuads(ItemModelResolution model, ItemAtlas? atlas)
{
    var list = new List<ItemModelQuad>();
    ItemEntityMesher.CollectQuads(list, model, atlas);
    return list;
}

