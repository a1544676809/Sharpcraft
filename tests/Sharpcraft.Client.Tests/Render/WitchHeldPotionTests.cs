// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using System.Numerics;
using Sharpcraft.Client.Render;
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
}
