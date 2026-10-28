// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using System.Text;
using MinecraftCSharp.Client.Render;
using MinecraftCSharp.Client.World;
using MinecraftCSharp.Common.World;

namespace MinecraftCSharp.Client.Tests;

/// <summary>
/// Geometry goldens for the mesher. The fingerprint deliberately ignores vertex and triangle
/// order, so internal optimisations (pooled buffers, different iteration order) can be verified as
/// behaviour-preserving while a missing/extra/moved triangle still fails the test.
/// </summary>
public sealed class ChunkMesherGoldenTests
{
    // Pinned 2026-10-28 after switching to greedy meshing; a change here means the emitted
    // triangles changed, which is only allowed with an explicit behaviour review. The mesher
    // tests keep an area-based check (quad count and total surface area) as the semantic guard,
    // because a fingerprint alone cannot tell 'merged' from 'missing geometry'.
    private const ulong DemoWorldFingerprint = 0x2C76588C65C14E10ul;
    private const ulong SingleBlockFingerprint = 0xEF375DDE92762030ul;
    private const ulong TwoBlockFingerprint = 0x66AD5A566BBF67F0ul;

    [Fact]
    public void DumpFingerprints()
    {
        // Helper for re-pinning: prints the current fingerprints to a file when asked, because
        // failing assertions get redacted by tooling.
        var target = Environment.GetEnvironmentVariable("MC_DUMP_GOLDENS");
        if (string.IsNullOrEmpty(target))
        {
            return;
        }

        var demo = Fingerprint(ChunkMesher.Mesh(BuildDemoWorld()));
        var single = Fingerprint(ChunkMesher.Mesh(SingleBlockWorld(1)));

        var twoWorld = new ClientWorld(minSectionY: 0, sectionCount: 1);
        var twoChunk = twoWorld.AddChunk(0, 0);
        twoChunk.SetSection(new ChunkSection(singleState: 0, sectionY: 0));
        twoChunk.SetBlockState(8, 8, 8, state: 1);
        twoChunk.SetBlockState(9, 8, 8, state: 2);
        var two = Fingerprint(ChunkMesher.Mesh(twoWorld));

        File.WriteAllText(target, $"demo=0x{demo:X16}\nsingle=0x{single:X16}\ntwo=0x{two:X16}\n");
    }

    [Fact]
    public void DemoWorldGeometryMatchesThePinnedFingerprint()
    {
        var world = BuildDemoWorld();

        var fingerprint = Fingerprint(ChunkMesher.Mesh(world));

        Assert.Equal(DemoWorldFingerprint, fingerprint);
    }

    [Fact]
    public void SingleBlockGeometryMatchesThePinnedFingerprint()
    {
        var world = SingleBlockWorld(1);

        var fingerprint = Fingerprint(ChunkMesher.Mesh(world));

        Assert.Equal(SingleBlockFingerprint, fingerprint);
    }

    [Fact]
    public void TwoAdjacentBlocksMatchThePinnedFingerprint()
    {
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        var chunk = world.AddChunk(0, 0);
        chunk.SetSection(new ChunkSection(singleState: 0, sectionY: 0));
        chunk.SetBlockState(8, 8, 8, state: 1);
        chunk.SetBlockState(9, 8, 8, state: 2);

        var fingerprint = Fingerprint(ChunkMesher.Mesh(world));

        Assert.Equal(TwoBlockFingerprint, fingerprint);
    }

    /// <summary>
    /// Order-insensitive fingerprint of the emitted geometry: every triangle contributes a hash of
    /// its three corner positions (quantised to 1/16 block) and the contributions are summed.
    /// Colour is excluded on purpose, so lighting changes are verified separately while any change
    /// to which triangles exist still fails this test.
    /// </summary>
    internal static ulong Fingerprint(MeshData mesh)
    {
        var total = 0ul;

        for (var i = 0; i + 2 < mesh.Indices.Length; i += 3)
        {
            total += HashTriangle(mesh, mesh.Indices[i], mesh.Indices[i + 1], mesh.Indices[i + 2]);
        }

        return total;
    }

    private static ulong HashTriangle(MeshData mesh, uint a, uint b, uint c)
    {
        // Sorting the three corner hashes makes the fingerprint independent of winding order.
        Span<ulong> corners = [HashVertex(mesh, a), HashVertex(mesh, b), HashVertex(mesh, c)];
        corners.Sort();

        var hash = 1469598103934665603ul;
        foreach (var corner in corners)
        {
            hash = (hash ^ corner) * 1099511628211ul;
        }

        return hash;
    }

    private static ulong HashVertex(MeshData mesh, uint index)
    {
        var offset = (int)index * ChunkMesher.FloatsPerVertex;
        var hash = 1469598103934665603ul;

        for (var i = 0; i < 3; i++)
        {
            // Quantise so the hash survives a fixed-point rewrite of the same geometry.
            var value = (long)Math.Round(mesh.Vertices[offset + i] * 16.0, MidpointRounding.AwayFromZero);
            hash = (hash ^ (ulong)value) * 1099511628211ul;
        }

        return hash;
    }

    private static ClientWorld SingleBlockWorld(int state)
    {
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        var chunk = world.AddChunk(0, 0);
        chunk.SetSection(new ChunkSection(singleState: 0, sectionY: 0));
        chunk.SetBlockState(8, 8, 8, state);
        return world;
    }

    private static ClientWorld BuildDemoWorld()
    {
        var world = new ClientWorld(minSectionY: 1, sectionCount: 2);
        var chunk = world.AddChunk(0, 0);
        var lower = new ChunkSection(0, 1);
        var upper = new ChunkSection(0, 2);
        chunk.SetSection(lower);
        chunk.SetSection(upper);

        for (var x = 0; x < 16; x++)
        {
            for (var z = 0; z < 16; z++)
            {
                var height = 4 + ((x * 3 + z * 5) % 7);
                for (var y = 0; y < height; y++)
                {
                    lower.Set(x, y, z, 1 + (y % 3));
                }
            }
        }

        var neighbour = world.AddChunk(1, 0);
        var neighbourSection = new ChunkSection(0, 1);
        neighbour.SetSection(neighbourSection);
        neighbourSection.Set(0, 0, 0, 5);
        chunk.SetBlockState(15, 0, 0, 7);

        return world;
    }
}
