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
    // Pinned from the pre-optimisation implementation on 2026-10-28; a change here means the
    // emitted geometry changed, which is only allowed with an explicit behaviour review.
    private const ulong DemoWorldFingerprint = 10888870183846123962ul;
    private const ulong SingleBlockFingerprint = 4826761792491864256ul;
    private const ulong TwoBlockFingerprint = 13619110637006531008ul;

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
    /// Order-insensitive fingerprint: every triangle contributes a hash of its three vertices
    /// (position and colour, rounded to 1/16 block), and the contributions are summed.
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

        for (var i = 0; i < ChunkMesher.FloatsPerVertex; i++)
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
