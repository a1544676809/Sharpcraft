// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using System.Numerics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

public sealed class ZzSpecialDiag
{
    [Fact]
    public void Show()
    {
        var wt = @"C:\source\repo\Sharpcraft\.agents\worktrees\raw-block-entities";
        var cases = new (string Sample, string Layer, EntityModelDefinition Def, int W, int H, string Tex)[]
        {
            ("ghast-raw-vectors.reference.txt", "GHAST", GhastModelDefinition.Definition, 64, 32, "entity/ghast/ghast"),
            ("giant-raw-vectors.reference.txt", "GIANT", GiantModelDefinition.Definition, 64, 64, "entity/giant/zombie"),
            ("breeze-raw-vectors.reference.txt", "BREEZE", BreezeModelDefinition.Definition, 64, 64, "entity/breeze/breeze"),
            ("creaking-raw-vectors.reference.txt", "CREAKING", CreakingModelDefinition.Definition, 64, 64, "entity/creaking/creaking"),
            ("guardian-raw-vectors.reference.txt", "GUARDIAN", GuardianModelDefinition.Definition, 64, 64, "entity/guardian/guardian"),
            ("iron-golem-raw-vectors.reference.txt", "IRON_GOLEM", IronGolemModelDefinition.Definition, 64, 64, "entity/iron_golem/iron_golem"),
        };
        foreach (var c in cases)
        {
            var sf = Path.Combine(wt, "tests", "Sharpcraft.Client.Tests", "Repro", "Data", c.Sample);
            if (!File.Exists(sf)) { System.Console.WriteLine($"SD|{c.Layer}|NO_SAMPLE"); continue; }
            var rows = File.ReadLines(sf).Where(l => l.StartsWith(c.Layer + "\t")).Select(l => l.Split('\t')).ToArray();
            if (rows.Length == 0) { System.Console.WriteLine($"SD|{c.Layer}|NO_ROWS"); continue; }
            // build raw twin from def parts? Def may be legacy. Try BuildModel directly with def as-is.
            var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal) { [c.Tex] = new(c.W, c.H, new byte[c.W*c.H*4]) });
            var g = new EntityModelMesher().BuildModel(c.Def, new EntityRenderState(1,0,0,0,0,180f,0), atlas.Get(c.Tex)!, c.W, c.H, modelScale: 1f);
            var n = g.VertexFloats / ChunkMesher.FloatsPerVertex;
            System.Console.WriteLine($"SD|{c.Layer}|rows={rows.Length} ourVerts={n} rawFlag={c.Def.RawSourceFrame} scale={c.Def.Scale}");
            // compare first sample pos to nearest our pos
            var s0 = new Vector3(float.Parse(rows[0][5], CultureInfo.InvariantCulture), float.Parse(rows[0][6], CultureInfo.InvariantCulture), float.Parse(rows[0][7], CultureInfo.InvariantCulture));
            var best = float.MaxValue; Vector3 bp = default;
            for (var i = 0; i < n; i++) { var p = VertexFieldReader.Position(g.Vertices, i); var d = (new Vector3(p.X,p.Y,p.Z) - s0).Length(); if (d < best) { best = d; bp = new Vector3(p.X,p.Y,p.Z); } }
            System.Console.WriteLine($"SD|{c.Layer}|s0=({s0.X:F4},{s0.Y:F4},{s0.Z:F4}) nearest=({bp.X:F4},{bp.Y:F4},{bp.Z:F4}) d={best:F4}");
        }
        Assert.True(true);
    }
}
