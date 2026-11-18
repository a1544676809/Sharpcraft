// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using System.Text;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Common.Entities;
using Sharpcraft.Common.Registries;
using Sharpcraft.Common.World;
using Sharpcraft.Equivalence.Audit;

var suite = "all";
string? outPath = null;
for (var i = 0; i < args.Length; i++)
{
    if (args[i] == "--suite" && i + 1 < args.Length) { suite = args[++i]; }
    else if (args[i] == "--out" && i + 1 < args.Length) { outPath = args[++i]; }
    else if (outPath is null) { outPath = args[i]; }
}

if (string.IsNullOrEmpty(outPath))
{
    Console.Error.WriteLine("usage: Sharpcraft.Equivalence.Audit [--suite all|state|entity|vis|physics|statematrix] --out <output.tsv>");
    return 2;
}

using var writer = new StreamWriter(outPath, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
writer.NewLine = "\n";
writer.WriteLine("# SPDX-License-Identifier: LGPL-3.0-or-later");
writer.WriteLine($"# Canonical SUT output (suite={suite})");

var written = 0;
if (suite is "all" or "state") { written += EmitStateCases(writer); }
if (suite is "all" or "entity") { written += EmitEntityCases(writer); }
if (suite is "all" or "vis") { written += EmitVisibilityCases(writer); }
if (suite is "all" or "physics") { written += EmitPhysicsCases(writer); }
if (suite is "all" or "statematrix") { written += EmitStateMatrix(writer); }

Console.WriteLine($"wrote {written} equivalence cases (suite={suite})");
return 0;

static int EmitStateCases(StreamWriter writer)
{
    var layout = VanillaBlockRegistry.Layout;
    var defs = BlockStateDefinitions.Baseline;
    var count = 0;
    // Sample every 64th state plus boundary states so full-project runs stay fast and deterministic.
    for (var stateId = 0; stateId < layout.TotalStates; stateId++)
    {
        if (stateId % 64 != 0 && stateId != layout.TotalStates - 1) { continue; }
        if (layout.BlockAt(stateId) is not { } block) { continue; }
        var sb = new StringBuilder();
        sb.Append($"state\tid_{stateId}\tblock={block}");
        if (defs.TryGet(block, out var def) && layout.TryGetBaseId(block, out var baseId))
        {
            var vals = def.ValuesAt(stateId - baseId);
            for (var i = 0; i < def.Properties.Count; i++)
            {
                sb.Append('\t').Append(def.Properties[i].Name).Append('=').Append(vals[i]);
            }
        }
        writer.WriteLine(sb.ToString());
        count++;
    }
    return count;
}

static int EmitEntityCases(StreamWriter writer)
{
    var count = 0;
    var chestMesher = new ChestMesher();
    var fullSprite = new SpriteRect(0, 0, 16, 16, 0);
    byte[] facings = [4, 1, 5, 0];
    string[] facingNames = ["north", "east", "south", "west"];
    float[] opens = [0f, 0.5f, 1f];
    byte[] chestTypes = [0, 1, 2];
    string[] typeNames = ["single", "left", "right"];

    for (var ti = 0; ti < chestTypes.Length; ti++)
    {
        for (var fi = 0; fi < facings.Length; fi++)
        {
            foreach (var open in opens)
            {
                var state = new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.Chest, Facing: facings[fi], Open: open, ChestType: chestTypes[ti]);
                var geom = chestMesher.Build([state], fullSprite);
                var (minX, minY, minZ, maxX, maxY, maxZ) = ComputeBounds(geom.Vertices, (geom.VertexFloats / ChunkMesher.FloatsPerVertex));
                var caseId = $"{typeNames[ti]}_{facingNames[fi]}_open{open.ToString("0.0", CultureInfo.InvariantCulture)}";
                writer.WriteLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"chest_geom\t{caseId}\tverts={(geom.VertexFloats / ChunkMesher.FloatsPerVertex)}\ttris={geom.IndexCount / 3}\tangle={ChestMesher.OpenAngle(open):F5}\tmin=({minX:F5},{minY:F5},{minZ:F5})\tmax=({maxX:F5},{maxY:F5},{maxZ:F5})"));
                count++;
            }
        }
    }
    return count;
}

static int EmitVisibilityCases(StreamWriter writer)
{
    // Not implemented: SectionVisibilityCompute.Compute takes an opaque mask, a visited bitmap and a
    // queue, and SectionVisibility exposes no mask property, so this probe cannot drive it as written.
    // Registered as a known simplification in docs/analysis/render/equivalence-harness-v2.md.
    _ = writer;
    return 0;
}

static int EmitPhysicsCases(StreamWriter writer)
{
    // Not implemented: the player entity needs an IEntityWorld and its Tick signature differs from what
    // this probe assumed, so the physics suite is a stub rather than a guess. Registered as a known
    // simplification in docs/analysis/render/equivalence-harness-v2.md.
    _ = writer;
    return 0;
}
static int EmitStateMatrix(StreamWriter writer)
{
    var rows = StateMatrixBaseline.Build(StateMatrixBaseline.FindRepositoryRoot());
    var count = StateMatrixBaseline.Write(writer, rows);
    foreach (var group in rows.GroupBy(row => row.Status).OrderBy(group => group.Key, StringComparer.Ordinal))
    {
        Console.WriteLine($"statematrix {group.Key}={group.Count()}");
    }

    foreach (var group in rows.GroupBy(row => row.LayerClass).OrderBy(group => group.Key, StringComparer.Ordinal))
    {
        Console.WriteLine($"statematrix layer_{group.Key}={group.Count()}");
    }

    return count;
}

static (float MinX, float MinY, float MinZ, float MaxX, float MaxY, float MaxZ) ComputeBounds(float[] vertices, int vertexCount)
{
    var minX = float.PositiveInfinity; var minY = float.PositiveInfinity; var minZ = float.PositiveInfinity;
    var maxX = float.NegativeInfinity; var maxY = float.NegativeInfinity; var maxZ = float.NegativeInfinity;
    for (var v = 0; v < vertexCount; v++)
    {
        var o = v * ChunkMesher.FloatsPerVertex;
        minX = MathF.Min(minX, vertices[o]); minY = MathF.Min(minY, vertices[o + 1]); minZ = MathF.Min(minZ, vertices[o + 2]);
        maxX = MathF.Max(maxX, vertices[o]); maxY = MathF.Max(maxY, vertices[o + 1]); maxZ = MathF.Max(maxZ, vertices[o + 2]);
    }
    return (minX, minY, minZ, maxX, maxY, maxZ);
}
