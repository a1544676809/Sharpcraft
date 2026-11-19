// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using System.Numerics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Issue #234: pig / strider / camel-husk saddle layers. Each type has a raw source-frame sample and a living
/// world-frame sample; the production layer is compared per source key (layer|part|cube|polygon|vertex) with
/// position and UV at 1e-5, and the layer must only appear while the entity is saddled.
/// </summary>
public sealed class MissingSaddleRawReferenceSampleTests
{
    private const float Tolerance = 1e-5f;
    private const string Saddle = "minecraft:saddle";

    // type id, production layer, kind, raw sample, world sample, part mapping
    public static TheoryData<int, string, EquipmentAssetKind, string, string, string> Cases => new()
    {
        { 101, "equipment_PigSaddle_7", EquipmentAssetKind.PigSaddle, "pig-saddle-raw-vectors.reference.txt", "pig-saddle-world-frame.reference.txt", "default" },
        { 132, "equipment_StriderSaddle_7", EquipmentAssetKind.StriderSaddle, "strider-saddle-raw-vectors.reference.txt", "strider-saddle-world-frame.reference.txt", "default" },
        { 20, "equipment_CamelHuskSaddle_7", EquipmentAssetKind.CamelHuskSaddle, "camel-husk-saddle-raw-vectors.reference.txt", "camel-husk-saddle-world-frame.reference.txt", "camel-head" }
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void ProductionSaddleLayerMatchesTheSourceSamplePerKey(int typeId, string layerName, EquipmentAssetKind kind,
        string rawSample, string worldSample, string mapping)
    {
        var definition = EntityModelResolver.Resolve(typeId);
        var layer = definition.RenderLayers.Single(entry => entry.Name == layerName);
        var model = layer.Model!;
        Assert.True(model.RawSourceFrame, layerName);
        Assert.Equal(0f, model.BaselinePx);
        Assert.Equal(0f, model.SourceFrameBaselinePx);

        var saddleId = EntityModelResolver.Items!.GetId(Identifier.Parse(Saddle));
        Assert.True(saddleId >= 0);
        var equipment = new EntityEquipment(-1, -1, -1, -1, -1, -1, -1, saddleId);
        // The camel-husk saddle layer keeps the camel reins cube, which the jar probe baked unconditionally;
        // the source gates it on IsVehicle, so the production comparison also rides the camel.
        var state = new EntityRenderState(1, typeId, 0f, 0f, 0f, 180f, 0f, Equipment: equipment, IsVehicle: true);
        var texturePath = model.TextureSelector?.Invoke(state) ?? model.TexturePath;
        var expectedTexture = kind switch
        {
            EquipmentAssetKind.PigSaddle => "entity/equipment/pig_saddle/saddle",
            EquipmentAssetKind.StriderSaddle => "entity/equipment/strider_saddle/saddle",
            _ => "entity/equipment/camel_husk_saddle/saddle"
        };
        Assert.Equal(expectedTexture, texturePath);
        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            [texturePath!] = new(model.TextureWidth, model.TextureHeight, new byte[model.TextureWidth * model.TextureHeight * 4])
        });

        var rows = Load(rawSample).Where(row => row.Layer == LayerName(kind)).ToArray();
        Assert.NotEmpty(rows);
        var full = BuildLayer(state, layerName, model.TextureWidth, model.TextureHeight, onlyPart: null, atlas);
        Assert.True(rows.Length == VertexCount(full), $"{layerName}: full vertices {VertexCount(full)} != sample rows {rows.Length}");

        // The second sample is the source living world frame. Bind it to the raw sample per key and drive the
        // production BuildLayers world frame from it (position + UV), not just a position formula.
        var worldRows = Load(worldSample).Where(row => row.Layer == LayerName(kind)).ToArray();
        Assert.Equal(rows.Length, worldRows.Length);

        var nodes = Flatten(model.Compiled!.Root).GroupBy(node => node.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var usedCubes = new HashSet<string>(StringComparer.Ordinal);
        var consumed = 0;
        foreach (var group in rows.GroupBy(row => (row.Part, row.Cube)))
        {
            var nodeName = NodeFor(mapping, group.Key.Part, group.Key.Cube);
            var nodeCube = NodeCubeFor(mapping, group.Key.Part, group.Key.Cube);
            Assert.True(nodes.TryGetValue(nodeName, out var node), $"{layerName}: no compiled node '{nodeName}'");
            Assert.InRange(nodeCube, 0, node!.Cubes.Count - 1);
            var cubeName = node.Cubes[nodeCube].Name;
            Assert.True(usedCubes.Add(cubeName), $"{layerName}: duplicate production cube '{cubeName}'");

            var ordered = group.OrderBy(row => row.Polygon).ThenBy(row => row.Vertex).ToArray();
            var geometry = BuildLayer(state, layerName, model.TextureWidth, model.TextureHeight, cubeName, atlas);
            Assert.True(ordered.Length == VertexCount(geometry), $"{layerName}/{cubeName}: vertices {VertexCount(geometry)} != rows {ordered.Length}");
            for (var i = 0; i < ordered.Length; i++)
            {
                var row = ordered[i];
                var raw = VertexFieldReader.Position(geometry.Vertices, i);
                var actual = new Vector3(-raw.X / EntityPlaceholderMesher.ModelScale, -raw.Y / EntityPlaceholderMesher.ModelScale,
                    raw.Z / EntityPlaceholderMesher.ModelScale);
                Assert.True((actual - row.Position).Length() <= Tolerance,
                    $"{layerName}/{row.Part}#{row.Cube} {row.Key}: position expected {row.Position}, actual {actual}");
                var uv = VertexFieldReader.Uv(geometry.Vertices, i);
                Assert.True(MathF.Abs(uv.U - QuantizeHalf(row.U)) <= Tolerance && MathF.Abs(uv.V - QuantizeHalf(row.V)) <= Tolerance,
                    $"{layerName}/{row.Part}#{row.Cube} {row.Key}: raw production UV");
            }

            // Source world frame: world = S(-1,-1,1)*T(0,-1.501,0)*raw. Bind raw<->world per key (position and
            // UV) and compare the production BuildLayers world output against the world sample per key.
            var worldCube = worldRows.Where(world => world.Part == group.Key.Part && world.Cube == group.Key.Cube)
                .OrderBy(world => world.Polygon).ThenBy(world => world.Vertex).ToArray();
            Assert.Equal(ordered.Length, worldCube.Length);
            var worldGeometry = BuildLayer(state, layerName, model.TextureWidth, model.TextureHeight, cubeName, atlas);
            Assert.Equal(worldCube.Length, VertexCount(worldGeometry));
            for (var i = 0; i < worldCube.Length; i++)
            {
                var rawRow = ordered[i];
                var worldRow = worldCube[i];
                var bound = new Vector3(-rawRow.Position.X, 1.501f - rawRow.Position.Y, rawRow.Position.Z);
                Assert.True((worldRow.Position - bound).Length() <= Tolerance,
                    $"{layerName}/{worldRow.Part}#{worldRow.Cube} {worldRow.Key}: raw->world position binding");
                Assert.True(MathF.Abs(worldRow.U - rawRow.U) <= Tolerance && MathF.Abs(worldRow.V - rawRow.V) <= Tolerance,
                    $"{layerName}/{worldRow.Part}#{worldRow.Cube} {worldRow.Key}: raw/world UV binding");

                // BuildLayers emits the equipment layer in the living world frame; the mesh baseline is 0, so the
                // entity living translate is already folded out: expected = (world - (0,1.501,0)) * ModelScale.
                var worldRaw = VertexFieldReader.Position(worldGeometry.Vertices, i);
                var worldPosition = new Vector3(worldRaw.X, worldRaw.Y, worldRaw.Z);
                var expectedWorld = new Vector3(worldRow.Position.X * EntityPlaceholderMesher.ModelScale,
                    (worldRow.Position.Y - 1.501f) * EntityPlaceholderMesher.ModelScale,
                    worldRow.Position.Z * EntityPlaceholderMesher.ModelScale);
                Assert.True((worldPosition - expectedWorld).Length() <= Tolerance,
                    $"{layerName}/{worldRow.Part}#{worldRow.Cube} {worldRow.Key}: production world position {worldPosition} vs {expectedWorld}");
                var worldUv = VertexFieldReader.Uv(worldGeometry.Vertices, i);
                Assert.True(MathF.Abs(worldUv.U - QuantizeHalf(worldRow.U)) <= Tolerance
                        && MathF.Abs(worldUv.V - QuantizeHalf(worldRow.V)) <= Tolerance,
                    $"{layerName}/{worldRow.Part}#{worldRow.Cube} {worldRow.Key}: production world UV");
            }

            consumed += ordered.Length;
        }

        Assert.Equal(rows.Length, consumed);

    }

    [Theory]
    [InlineData(101, "equipment_PigSaddle_7")]
    [InlineData(132, "equipment_StriderSaddle_7")]
    [InlineData(20, "equipment_CamelHuskSaddle_7")]
    public void SaddledEntitiesExposeOnlyTheirSaddleLayer(int typeId, string layerName)
    {
        var definition = EntityModelResolver.Resolve(typeId);
        var equipmentLayers = definition.RenderLayers
            .Where(layer => layer.Name.StartsWith("equipment_", StringComparison.Ordinal))
            .Select(layer => layer.Name)
            .ToArray();
        Assert.Equal(new[] { layerName }, equipmentLayers);

        var saddleId = EntityModelResolver.Items!.GetId(Identifier.Parse(Saddle));
        var state = new EntityRenderState(1, typeId, 0f, 0f, 0f, 180f, 0f,
            Equipment: new EntityEquipment(-1, -1, -1, -1, -1, -1, -1, saddleId));
        var atlas = TextureAtlas(64, 128, layerName);
        var layers = new EntityModelMesher().BuildLayers([state], new SpriteRect(0, 0, 64, 128, 1), 64, 128,
            skinAtlas: atlas, variants: EntityVariantTable.Builtin, pose: new EntityPose());
        Assert.Contains(layers, layer => layer.Name == layerName && layer.Geometry.IndexCount > 0);

        // The saddle glint material layer must follow the raw saddle geometry (baseline 0, raw frame).
        var glintName = "glint_" + layerName["equipment_".Length..];
        var glint = definition.RenderLayers.SingleOrDefault(layer => layer.Name == glintName);
        Assert.NotNull(glint);
        Assert.True(glint!.Model!.RawSourceFrame, glintName);
        Assert.Equal(0f, glint.Model.BaselinePx);
        Assert.Equal(0f, glint.Model.SourceFrameBaselinePx);
    }

    [Theory]
    [InlineData(101, "equipment_PigSaddle_7")]
    [InlineData(132, "equipment_StriderSaddle_7")]
    [InlineData(20, "equipment_CamelHuskSaddle_7")]
    public void UnsaddledEntitiesEmitNoEquipmentLayer(int typeId, string layerName)
    {
        var state = new EntityRenderState(1, typeId, 0f, 0f, 0f, 180f, 0f);
        var atlas = TextureAtlas(64, 128, null);
        var layers = new EntityModelMesher().BuildLayers([state], new SpriteRect(0, 0, 64, 128, 1), 64, 128,
            skinAtlas: atlas, variants: EntityVariantTable.Builtin, pose: new EntityPose());
        Assert.DoesNotContain(layers, layer => layer.Name == layerName);
    }

    [Fact]
    public void CamelHuskSaddleReusesTheCamelSaddleTree()
    {
        var camel = EntityModelResolver.Resolve(CamelModelDefinition.CamelEntityTypeId)
            .RenderLayers!.Single(layer => layer.Name == "equipment_CamelSaddle_7").Model!;
        var camelHusk = EntityModelResolver.Resolve(CamelHuskModelDefinition.CamelHuskEntityTypeId)
            .RenderLayers!.Single(layer => layer.Name == "equipment_CamelHuskSaddle_7").Model!;
        Assert.True(camel.RawSourceFrame);
        Assert.True(camelHusk.RawSourceFrame);
        Assert.Equal(552, camelHusk.Compiled!.Cubes.Count * 24);
        Assert.Equal(
            camel.Compiled!.Cubes.Select(cube => (cube.Name, cube.CentreX, cube.Bottom, cube.CentreZ, cube.Width, cube.Height, cube.Depth)),
            camelHusk.Compiled.Cubes.Select(cube => (cube.Name, cube.CentreX, cube.Bottom, cube.CentreZ, cube.Width, cube.Height, cube.Depth)));
    }

    private static string LayerName(EquipmentAssetKind kind) => kind switch
    {
        EquipmentAssetKind.PigSaddle => "PIG_SADDLE",
        EquipmentAssetKind.StriderSaddle => "STRIDER_SADDLE",
        _ => "CAMEL_HUSK_SADDLE"
    };

    private static string NodeFor(string mapping, string partPath, int cube) => mapping switch
    {
        "camel-head" when partPath == "/body/head" => new[] { "head_lower", "head_upper", "head_snout" }[cube],
        _ => partPath.Split('/').Last()
    };

    private static int NodeCubeFor(string mapping, string partPath, int cube) =>
        mapping == "camel-head" && partPath == "/body/head" ? 0 : cube;

    private static DynamicGeometry BuildLayer(EntityRenderState state, string layerName, int width, int height, string? onlyPart, ItemAtlas atlas)
    {
        var layers = new EntityModelMesher().BuildLayers([state], new SpriteRect(0, 0, width, height, 1), width, height,
            onlyPart: onlyPart, skinAtlas: atlas, variants: EntityVariantTable.Builtin, pose: new EntityPose());
        return layers.Single(layer => layer.Name == layerName).Geometry;
    }

    private static ItemAtlas TextureAtlas(int width, int height, string? layerName)
    {
        var textures = new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            ["probe/none"] = new(width, height, new byte[width * height * 4]),
            ["entity/equipment/pig_saddle/saddle"] = new(64, 64, new byte[64 * 64 * 4]),
            ["entity/equipment/strider_saddle/saddle"] = new(64, 128, new byte[64 * 128 * 4]),
            ["entity/equipment/camel_husk_saddle/saddle"] = new(128, 128, new byte[128 * 128 * 4])
        };
        return ItemAtlas.FromTextures(textures);
    }

    private static IEnumerable<EntityModelPartNode> Flatten(EntityModelPartNode node)
    {
        yield return node;
        foreach (var child in node.ChildArray)
        {
            foreach (var nested in Flatten(child))
            {
                yield return nested;
            }
        }
    }

    private static int VertexCount(DynamicGeometry geometry) => geometry.VertexFloats / ChunkMesher.FloatsPerVertex;

    private static float QuantizeHalf(float value) => (float)BitConverter.UInt16BitsToHalf(BitConverter.HalfToUInt16Bits((Half)value));

    private static List<SampleRow> Load(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Repro", "Data", fileName);
        var rows = new List<SampleRow>();
        foreach (var line in File.ReadLines(path))
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var fields = line.Split('\t');
            rows.Add(new SampleRow(
                fields[0],
                fields[1],
                int.Parse(fields[2], CultureInfo.InvariantCulture),
                int.Parse(fields[3], CultureInfo.InvariantCulture),
                int.Parse(fields[4], CultureInfo.InvariantCulture),
                new Vector3(
                    float.Parse(fields[5], CultureInfo.InvariantCulture),
                    float.Parse(fields[6], CultureInfo.InvariantCulture),
                    float.Parse(fields[7], CultureInfo.InvariantCulture)),
                float.Parse(fields[8], CultureInfo.InvariantCulture),
                float.Parse(fields[9], CultureInfo.InvariantCulture)));
        }

        return rows;
    }

    private readonly record struct SampleRow(
        string Layer,
        string Part,
        int Cube,
        int Polygon,
        int Vertex,
        Vector3 Position,
        float U,
        float V)
    {
        public (int Polygon, int Vertex) Key => (Polygon, Vertex);
    }
}
