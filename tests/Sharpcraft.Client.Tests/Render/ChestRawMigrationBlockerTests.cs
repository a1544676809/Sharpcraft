// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Common.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// task-210: records why ChestMesher cannot be expressed as a static raw EntityModelDefinition.
/// Static bind-pose jar samples (RawRenderUnitProbe, CHEST / DOUBLE_CHEST_LEFT / DOUBLE_CHEST_RIGHT)
/// prove the blocker; the existing Repro_T850ChestGeometryTests prove the current custom mesher already
/// matches the animated jar truth (3 openness x 4 facings x 3 halves) strictly.
/// Source: ref://mc-26.3-analysis/src/net/minecraft/client/model/object/chest/ChestModel.java:28-69.
/// </summary>
public sealed class ChestRawMigrationBlockerTests
{
    private static int VertexRows(string file)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Repro", "Data", file);
        var count = 0;
        foreach (var line in File.ReadLines(path))
            if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith('#')) count++;
        return count;
    }

    [Fact]
    public void SingleChestIsFullyExpressibleButDoubleHalvesCullAFace()
    {
        // Single chest: 3 parts x 24 verts (6 faces). Double halves: 3 parts x 20 verts (5 faces,
        // EAST/WEST face culled by per-cube visibleFaces) -- not representable without a VisibleSides API.
        Assert.Equal(72, VertexRows("chest-single-bone-raw-vectors.reference.txt"));
        Assert.Equal(60, VertexRows("chest-double-left-bone-raw-vectors.reference.txt"));
        Assert.Equal(60, VertexRows("chest-double-right-bone-raw-vectors.reference.txt"));
    }

    [Fact]
    public void ChestRawMigrationIsBlocked()
    {
        // (1) double-chest per-cube visibleFaces needs a Cube VisibleSides field the DSL lacks (R-037/task-207);
        // (2) lid/lock rotation is a runtime animation setupAnim(open)=-(eased(open)*PI/2), not static bind-pose
        //     geometry, so a raw EntityModelDefinition cannot hold it without changing render-state semantics.
        // The custom ChestMesher already reproduces the animated jar truth strictly (Repro_T850ChestGeometryTests),
        // so it intentionally stays a custom mesher -- assert it still emits the expected three animated boxes.
        var mesher = new ChestMesher();
        var state = new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.Chest,
            Open: 1f, Facing: 5, ChestType: 0);
        var sprite = new SpriteRect(0, 0, 64, 64, 1)
        {
            RectId = 1, U0 = 0f, V0 = 0f, U1 = 1f, V1 = 1f
        };
        var geometry = mesher.Build([state], sprite);
        // 3 boxes x 24 verts, with the lid/lock rotated by the open animation (not static bind pose).
        Assert.Equal(72, geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
    }
}
