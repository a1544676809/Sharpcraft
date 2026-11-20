// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Sharpcraft.Assets;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Repro;

/// <summary>
/// A dropped item baked fourteen floats per vertex for its index base while the writer it calls
/// advances by <see cref="ChunkMesher.FloatsPerVertex"/>, so every quad after the first indexed an
/// earlier vertex. On screen the drop became a spray of triangles sampling random pieces of the
/// item atlas instead of the item.
/// </summary>
/// <remarks>
/// The invariant that pins this down needs no reference data: quad <c>q</c> writes vertices
/// <c>4q .. 4q+3</c>, so its first index is exactly <c>4q</c>. A stride that disagrees with the
/// writer makes that number too small and nothing else changes.
/// </remarks>
public sealed class Repro_267_DroppedItemMeshTests
{
    [Fact]
    public void EveryQuadIndexesItsOwnFourVertices()
    {
        var root = Path.Combine(Path.GetTempPath(), "sharpcraft-repro-267-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "assets", "minecraft", "models", "item"));
        var previous = EntityModelResolver.ItemModels;
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "assets", "minecraft", "items"));
            Directory.CreateDirectory(Path.Combine(root, "assets", "minecraft", "models", "item"));
            File.WriteAllText(
                Path.Combine(root, "assets", "minecraft", "items", "paper.json"),
                """{"model":{"type":"minecraft:model","model":"minecraft:item/paper_cube"}}""");
            File.WriteAllText(
                Path.Combine(root, "assets", "minecraft", "models", "item", "paper_cube.json"),
                """
                {"textures":{"particle":"item/paper","all":"item/paper"},"elements":[
                  {"from":[0,0,0],"to":[16,16,16],"faces":{
                    "down":{"texture":"#all"},"up":{"texture":"#all"},"north":{"texture":"#all"},
                    "south":{"texture":"#all"},"west":{"texture":"#all"},"east":{"texture":"#all"}}}]}
                """);
            using var stack = new AssetStack { Policy = MissingResourcePolicy.Warn, Warn = _ => { } }
                .Add(new DirectoryAssetSource(root, "repro-267"));
            using var resources = new ResourceManager(stack, 1);
            EntityModelResolver.ItemModels = new ItemModelResolver(resources);

            var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(System.StringComparer.Ordinal)
            {
                ["item/paper"] = new(16, 16, new byte[16 * 16 * 4]),
            });

            var paperId = EntityModelResolver.Items!.GetId(Identifier.Parse("minecraft:paper"));
            var state = new EntityRenderState(
                1, EntityTracker.ItemEntityTypeId, 0f, 0f, 0f, 0f, 0f, ItemEntityItemId: paperId, ItemEntityCount: 1);

            var layers = new ItemEntityMesher().Build([state], atlas);
            var layer = Assert.Single(layers, candidate => candidate.Kind == EntityGeometryLayerKind.ItemCutout);
            Assert.True(layer.UsesItemAtlas, "a dropped item samples the item atlas");

            var geometry = layer.Geometry;
            var vertexCount = geometry.VertexFloats / ChunkMesher.FloatsPerVertex;
            var quadCount = geometry.IndexCount / 6;
            Assert.True(quadCount >= 2, $"a generated sprite bakes at least two quads, got {quadCount}");
            Assert.Equal(4 * quadCount, vertexCount);

            for (var quad = 0; quad < quadCount; quad++)
            {
                var first = (uint)(4 * quad);
                Assert.Equal(first, geometry.Indices[quad * 6]);
                Assert.Equal(first + 1u, geometry.Indices[(quad * 6) + 1]);
                Assert.Equal(first + 2u, geometry.Indices[(quad * 6) + 2]);
                Assert.Equal(first, geometry.Indices[(quad * 6) + 3]);
                Assert.Equal(first + 2u, geometry.Indices[(quad * 6) + 4]);
                Assert.Equal(first + 3u, geometry.Indices[(quad * 6) + 5]);
            }

            for (var index = 0; index < geometry.IndexCount; index++)
            {
                Assert.True(geometry.Indices[index] < vertexCount, "an index points past the vertices it can reach");
            }

            // The quad corners differ, so the item is a real billboard rather than a degenerate point.
            Assert.NotEqual(Vector3.Zero, ReadPosition(geometry, 1) - ReadPosition(geometry, 0));
        }
        finally
        {
            EntityModelResolver.ItemModels = previous;
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
                // A leftover temp directory must not fail the test.
            }
        }
    }

    private static Vector3 ReadPosition(DynamicGeometry geometry, int vertex) =>
        new(
            geometry.Vertices[vertex * ChunkMesher.FloatsPerVertex],
            geometry.Vertices[(vertex * ChunkMesher.FloatsPerVertex) + 1],
            geometry.Vertices[(vertex * ChunkMesher.FloatsPerVertex) + 2]);
}
