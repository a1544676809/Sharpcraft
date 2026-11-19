// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using System.Numerics;
using Sharpcraft.Assets;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// B-4 T4b: witch held-potion real item-model render verified against the jar golden vectors:
/// potion-colors.reference.txt (effect-derived tint table) and witch-potion-pose.reference.txt (source pose).
/// </summary>
public sealed class WitchHeldPotionTests
{
    private static string SamplePath(string name) => Path.Combine(
        AppContext.BaseDirectory, "Repro", "Data", name);

    [Fact]
    public void PotionColorTableLoadsJarVectors()
    {
        var lines = File.ReadLines(SamplePath("potion-colors.reference.txt"));
        var table = PotionColorTable.Parse(lines);

        Assert.True(table.PotionCount > 20);
        Assert.True(table.EffectCount > 20);
        // water is the BASE_POTION_COLOR; night_vision is effect-derived (distinct from the default).
        Assert.Equal(-13083194, table.PlainColor("water"));
        Assert.Equal(-13083194, table.PlainColor("awkward"));
        Assert.NotNull(table.PlainColor("night_vision"));
        Assert.NotEqual(-13083194, table.PlainColor("night_vision"));
        Assert.Equal(-3997850, table.PlainColor("night_vision"));
        Assert.Equal(3402751, table.EffectColor("speed"));
    }

    [Fact]
    public void PotionSourcePoseMatchesJarSampleMatrix()
    {
        // The POTION branch pose must reproduce the jar probe matrix keyed element-for-element (1e-5). This is the
        // equivalence anchor for the source WitchItemLayer.applyTranslation chain; no self-derived expectation.
        var numeric = new float[16];
        foreach (var line in File.ReadLines(SamplePath("witch-potion-pose.reference.txt")))
        {
            if (!line.StartsWith("#MATRIX POTION_POSE ")) continue;
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < 16; i++) numeric[i] = float.Parse(parts[i + 2], CultureInfo.InvariantCulture);
            break;
        }

        Assert.NotEqual(0f, numeric[0]); // sample parsed

        var root = WitchModelDefinition.Definition.Compiled!.Root;
        var pose = WitchHeldItemFrame.PotionSource(root);
        // Same mapping as HumanoidHeldHandProductionTests.ParseMatrices: the probe's JOML m0..m15 are written in a
        // System.Numerics-compatible layout (values fill rows directly; translation lives in M41..M43).
        var expected = new Matrix4x4(
            numeric[0], numeric[1], numeric[2], numeric[3],
            numeric[4], numeric[5], numeric[6], numeric[7],
            numeric[8], numeric[9], numeric[10], numeric[11],
            numeric[12], numeric[13], numeric[14], numeric[15]);

        Assert.Equal(expected.M11, pose.M11, 5);
        Assert.Equal(expected.M12, pose.M12, 5);
        Assert.Equal(expected.M13, pose.M13, 5);
        Assert.Equal(expected.M21, pose.M21, 5);
        Assert.Equal(expected.M22, pose.M22, 5);
        Assert.Equal(expected.M23, pose.M23, 5);
        Assert.Equal(expected.M31, pose.M31, 5);
        Assert.Equal(expected.M32, pose.M32, 5);
        Assert.Equal(expected.M33, pose.M33, 5);
        Assert.Equal(expected.M41, pose.M41, 5);
        Assert.Equal(expected.M42, pose.M42, 5);
        Assert.Equal(expected.M43, pose.M43, 5);
    }

    [Fact]
    public void GeneratedPotionQuadsMatchJarWorldSampleKeyed()
    {
        // Full render chain geometry: for a bind witch with no yaw/scale, world = flip * pose * GROUND * (v_px/16).
        // Verify against the jar probe witch-potion-quad.reference.txt keyed by layer|polygon|vertex (position + UV + tint).
        var root = WitchModelDefinition.Definition.Compiled!.Root;
        var pose = WitchHeldItemFrame.PotionSource(root);
        Assert.Equal(-0.99129, pose.M11, 5); // source frame reproduced

        // Use the jar probe's POTION_FULL matrix (pose * GROUND display) directly as the equivalence anchor. The probe
        // applies S(-1,-1,1)*T(0,-1.501,0) as flip.mul(full) in JOML column space = fullCSharp * flipCSharp in
        // System.Numerics row-vector space (the probe's m0..m15 parse directly into the CSharp matrix layout).
        var fullValues = new float[16];
        foreach (var line in File.ReadLines(SamplePath("witch-potion-pose.reference.txt")))
        {
            if (!line.StartsWith("#MATRIX POTION_FULL ")) continue;
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < 16; i++) fullValues[i] = float.Parse(parts[i + 2], CultureInfo.InvariantCulture);
            break;
        }

        Assert.NotEqual(0f, fullValues[0]);
        var fullNoFlip = new Matrix4x4(
            fullValues[0], fullValues[1], fullValues[2], fullValues[3],
            fullValues[4], fullValues[5], fullValues[6], fullValues[7],
            fullValues[8], fullValues[9], fullValues[10], fullValues[11],
            fullValues[12], fullValues[13], fullValues[14], fullValues[15]);
        // JOML flip.mul(full) column-space = fullCSharp * flipCSharp in row-vector space. The row-vector equivalent of
        // JOML S(-1,-1,1)*T(0,-1.501,0) is T(0,-1.501,0)*S(-1,-1,1) (translate then scale, reversed order).
        var flip = Matrix4x4.CreateTranslation(0f, -1.501f, 0f) * Matrix4x4.CreateScale(-1f, -1f, 1f);
        var full = fullNoFlip * flip;

        // Generated layer corners: south face z=8.5/16, north face z=7.5/16 (ItemModelGenerator MIN/MAX_Z).
        var layerCorners = new (float z, int tint, string layer)[]
        {
            (8.5f / 16f, 0, "potion_overlay"),
            (7.5f / 16f, 0, "potion_overlay"),
            (8.5f / 16f, 1, "potion"),
            (7.5f / 16f, 1, "potion")
        };

        var sample = File.ReadLines(SamplePath("witch-potion-quad.reference.txt"))
            .Where(l => !l.StartsWith('#'))
            .Select(l => l.Split('\t'))
            .ToDictionary(p => $"{p[0]}|{p[2]}|{p[3]}");

        var south = new[] { (0f, 1f), (0f, 0f), (1f, 0f), (1f, 1f) };
        var north = new[] { (1f, 1f), (1f, 0f), (0f, 0f), (0f, 1f) };
        foreach (var (z, tint, layer) in layerCorners)
        {
            var corners = z > 8f / 16f ? south : north;
            var polygon = z > 8f / 16f ? 0 : 1;
            for (var i = 0; i < 4; i++)
            {
                var v = Vector3.Transform(new Vector3(corners[i].Item1, corners[i].Item2, z), full);
                var key = $"{layer}|{polygon}|{i}";
                Assert.True(sample.TryGetValue(key, out var row), $"missing sample key {key}");
                Assert.Equal(float.Parse(row[4], CultureInfo.InvariantCulture), v.X, 5);
                Assert.Equal(float.Parse(row[5], CultureInfo.InvariantCulture), v.Y, 5);
                Assert.Equal(float.Parse(row[6], CultureInfo.InvariantCulture), v.Z, 5);
                Assert.Equal(tint, int.Parse(row[9], CultureInfo.InvariantCulture));
            }
        }
    }

    [Fact]
    public void UnknownPotionOrEffectHasNoColour()
    {
        var table = PotionColorTable.Parse(File.ReadLines(SamplePath("potion-colors.reference.txt")));
        Assert.Null(table.PlainColor("not_a_potion"));
        Assert.Null(table.EffectColor("not_an_effect"));
    }

    [Fact]
    public void PotionTintSourceDefaultIsTheJarBaseColour()
    {
        var source = ItemTintSource.Parse(System.Text.Json.JsonDocument.Parse(
            """{"type":"minecraft:potion","default":-13083194}""").RootElement);
        Assert.NotNull(source);
        Assert.Equal(ItemTintKind.Potion, source!.Value.Kind);
        Assert.Equal(ItemTintSource.PotionDefaultColor, source.Value.Argb);
    }

    [Fact]
    public void ProductionMesherMatchesJarQuadWorldSampleKeyedAndConsumesCustomTint()
    {
        // Production keyed verification of WitchHeldPotionMesher.Build (not a formula re-computation): the 16 sample
        // vertices (south/north faces of both layers) must each appear in the mesher's emitted geometry at 1e-5 with
        // half-quantized UVs. Sprite alpha side faces are additional (blocker R-046) and are simply not in the sample.
        using var fixture = new PotionFixture();
        var potionModel = EntityModelResolver.ItemModels!.Resolve("minecraft:potion");
        Assert.NotNull(potionModel);

        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            ["item/potion_overlay"] = new(16, 16, new byte[16 * 16 * 4]),
            ["item/potion"] = new(16, 16, new byte[16 * 16 * 4])
        });
        const int customColor = unchecked((int)0xFF2A7F1E);
        // EntityModelTransform.From applies YawDegrees = 180 - state.Yaw and Scale = state.Scale. Use Yaw=180, Scale=1
        // so the entity matrix is identity (the jar quad sample excludes entity yaw/scale), leaving pose*display only.
        var state = new EntityRenderState(1, WitchModelDefinition.WitchEntityTypeId, 0f, 0f, 0f, 180f, 0f,
            IsHoldingPotion: true, HeldPotionColor: customColor, Scale: 1f);
        var layer = new WitchHeldPotionMesher().Build([state], [new EntityPose()], atlas, potionModel);
        Assert.NotNull(layer);
        Assert.Equal(WitchHeldPotionMesher.LayerName, layer!.Name);

        var sample = File.ReadLines(SamplePath("witch-potion-quad.reference.txt"))
            .Where(l => !l.StartsWith('#'))
            .Select(l => l.Split('\t'))
            .ToArray();
        Assert.Equal(16, sample.Length);

        var produced = ReadAllVertices(layer.Geometry);
        // The mesher emits item geometry in the source frame without the living flip; map jar world sample back:
        // (-x, 1.501 - y, z), same as HeldSpecialWorldReferenceSampleTests (level renderer applies the flip later).
        foreach (var row in sample)
        {
            var world = new Vector3(
                float.Parse(row[4], CultureInfo.InvariantCulture),
                float.Parse(row[5], CultureInfo.InvariantCulture),
                float.Parse(row[6], CultureInfo.InvariantCulture));
            var expected = new Vector3(-world.X, 1.501f - world.Y, world.Z);
            var expectedU = QuantizeHalf(float.Parse(row[7], CultureInfo.InvariantCulture));
            var expectedV = QuantizeHalf(float.Parse(row[8], CultureInfo.InvariantCulture));
            Assert.Contains(produced, v =>
                (v.Position - expected).Length() <= 1e-5f &&
                MathF.Abs(v.U - expectedU) <= 1e-5f && MathF.Abs(v.V - expectedV) <= 1e-5f);
        }

        // Tint consumption: the potion tint source is declared only on layer0 (potion_overlay, tint index 0), so the
        // 8 overlay vertices carry the stack custom colour while the 8 bottle vertices (tint index 1, no source) stay
        // white — the reference behaviour (ItemStackRenderState.tintLayers only on the overlay quad's tint index).
        var expectedRed = ((customColor >> 16) & 0xFF) / 255f;
        var expectedGreen = ((customColor >> 8) & 0xFF) / 255f;
        var expectedBlue = (customColor & 0xFF) / 255f;
        var overlayRows = sample.Where(r => r[9] == "0").ToArray();
        var bottleRows = sample.Where(r => r[9] == "1").ToArray();
        Assert.Equal(8, overlayRows.Length);
        Assert.Equal(8, bottleRows.Length);
        foreach (var row in overlayRows)
        {
            var world = new Vector3(
                float.Parse(row[4], CultureInfo.InvariantCulture),
                float.Parse(row[5], CultureInfo.InvariantCulture),
                float.Parse(row[6], CultureInfo.InvariantCulture));
            var expected = new Vector3(-world.X, 1.501f - world.Y, world.Z);
            var v = Assert.Single(produced, p =>
                (p.Position - expected).Length() <= 1e-5f &&
                MathF.Abs(p.Red - expectedRed) <= 1e-3f &&
                MathF.Abs(p.Green - expectedGreen) <= 1e-3f &&
                MathF.Abs(p.Blue - expectedBlue) <= 1e-3f);
        }
        foreach (var row in bottleRows)
        {
            var world = new Vector3(
                float.Parse(row[4], CultureInfo.InvariantCulture),
                float.Parse(row[5], CultureInfo.InvariantCulture),
                float.Parse(row[6], CultureInfo.InvariantCulture));
            var expected = new Vector3(-world.X, 1.501f - world.Y, world.Z);
            Assert.Single(produced, p =>
                (p.Position - expected).Length() <= 1e-5f &&
                MathF.Abs(p.Red - 1f) <= 1e-3f &&
                MathF.Abs(p.Green - 1f) <= 1e-3f &&
                MathF.Abs(p.Blue - 1f) <= 1e-3f);
        }
    }

    private static (Vector3 Position, float U, float V, float Red, float Green, float Blue)[] ReadAllVertices(DynamicGeometry geometry)
    {
        var count = geometry.VertexFloats / ChunkMesher.FloatsPerVertex;
        var result = new (Vector3, float, float, float, float, float)[count];
        for (var i = 0; i < count; i++)
        {
            var o = i * ChunkMesher.FloatsPerVertex;
            var packedColor = BitConverter.SingleToUInt32Bits(geometry.Vertices[o + 3]);
            var packedUv = BitConverter.SingleToUInt32Bits(geometry.Vertices[o + 4]);
            result[i] = (
                new Vector3(geometry.Vertices[o], geometry.Vertices[o + 1], geometry.Vertices[o + 2]),
                (float)BitConverter.UInt16BitsToHalf((ushort)(packedUv & 0xFFFF)),
                (float)BitConverter.UInt16BitsToHalf((ushort)(packedUv >> 16)),
                (packedColor & 0xFF) / 255f,
                ((packedColor >> 8) & 0xFF) / 255f,
                ((packedColor >> 16) & 0xFF) / 255f);
        }
        return result;
    }

    private static float QuantizeHalf(float value) => (float)BitConverter.UInt16BitsToHalf(BitConverter.HalfToUInt16Bits((Half)value));

    /// <summary>Temp resource stack with the real two-layer potion item model (data-only), like HeldSpecialWorldReferenceSampleTests.ItemFixture.</summary>
    private sealed class PotionFixture : IDisposable
    {
        private readonly string _root;
        private readonly Sharpcraft.Client.Render.ItemModelResolver _previous;
        private readonly ResourceManager _resources;

        public PotionFixture()
        {
            _root = Path.Combine(Path.GetTempPath(), "sharpcraft-potion-test-" + Guid.NewGuid().ToString("N"));
            Write("assets/minecraft/items/potion.json",
                """{"model":{"type":"minecraft:model","model":"minecraft:item/potion","tints":[{"type":"minecraft:potion","default":-13083194}]}}""");
            Write("assets/minecraft/models/item/potion.json",
                """{"parent":"minecraft:item/generated","textures":{"layer0":"minecraft:item/potion_overlay","layer1":"minecraft:item/potion"}}""");
            Write("assets/minecraft/models/item/generated.json",
                """{"display":{"ground":{"rotation":[0,0,0],"translation":[0,2,0],"scale":[0.5,0.5,0.5]}}}""");
            var stack = new AssetStack { Policy = MissingResourcePolicy.Warn, Warn = _ => { } }
                .Add(new DirectoryAssetSource(_root, "potion-test"));
            _resources = new ResourceManager(stack, 1);
            _previous = EntityModelResolver.ItemModels!;
            EntityModelResolver.ItemModels = new ItemModelResolver(_resources);
        }

        public void Dispose()
        {
            EntityModelResolver.ItemModels = _previous;
            _resources.Dispose();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private void Write(string relative, string json)
        {
            var path = Path.Combine(_root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, json);
        }
    }
}
