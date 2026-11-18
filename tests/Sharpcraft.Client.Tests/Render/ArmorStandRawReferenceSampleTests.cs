// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using System.Numerics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Network;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>task-259: ArmorStand body/small raw, synced poses, small routing and reused armor.</summary>
public sealed class ArmorStandRawReferenceSampleTests
{
    [Theory]
    [InlineData("armor-stand-body-raw-vectors.reference.txt", false)]
    [InlineData("armor-stand-small-body-raw-vectors.reference.txt", true)]
    public void ArmorStandBodyRawGeometryMatchesSource(string sampleFile, bool small)
    {
        var rows = Load(sampleFile);
        Assert.Equal(264, rows.Length);
        var definition = small ? ArmorStandModelDefinition.SmallBody : ArmorStandModelDefinition.Body;
        Assert.True(definition.RawSourceFrame);
        Assert.Equal(0f, definition.BaselinePx);

        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            ["entity/armorstand/armorstand"] = new(64, 64, new byte[64 * 64 * 4])
        });
        var state = new EntityRenderState(1, ArmorStandModelDefinition.ArmorStandEntityTypeId, 0f, 0f, 0f, 180f, 0f)
        {
            ShowHat = true,
            ShowArms = true,
            ShowBasePlate = true
        };
        var geometry = new EntityModelMesher().BuildModel(
            definition, state, atlas.Get("entity/armorstand/armorstand")!, 64, 64, modelScale: 1f);
        Assert.Equal(rows.Length, geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
        for (var i = 0; i < rows.Length; i++)
        {
            var (x, y, z) = VertexFieldReader.Position(geometry.Vertices, i);
            var (u, v) = VertexFieldReader.Uv(geometry.Vertices, i);
            var row = rows[i];
            var expectedU = QuantizeHalf(row.U);
            var expectedV = QuantizeHalf(row.V);
            Assert.True(MathF.Abs(x - row.X) <= 1e-5f, $"{sampleFile} {row.Part} v{i} x {x} vs {row.X}");
            Assert.True(MathF.Abs(y - row.Y) <= 1e-5f, $"{sampleFile} {row.Part} v{i} y {y} vs {row.Y}");
            Assert.True(MathF.Abs(z - row.Z) <= 1e-5f, $"{sampleFile} {row.Part} v{i} z {z} vs {row.Z}");
            Assert.True(MathF.Abs(u - expectedU) <= 1e-5f, $"{sampleFile} {row.Part} v{i} u {u} vs {expectedU}");
            Assert.True(MathF.Abs(v - expectedV) <= 1e-5f, $"{sampleFile} {row.Part} v{i} v {v} vs {expectedV}");
        }
    }

    [Fact]
    public void SyncedPosesRoundTripAndApplyToParts()
    {
        var tracker = new EntityTracker();
        tracker.Add(new AddedEntity(5, Guid.Empty, ArmorStandModelDefinition.ArmorStandEntityTypeId, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        EntityDataItem[] items =
        [
            new(15, 0, 4), // show arms (showBasePlate is bit 8 cleared)
            new(16, 9, 0, RotationX: 10f, RotationY: 20f, RotationZ: 30f),
            new(17, 9, 0, RotationX: -5f, RotationY: -6f, RotationZ: -7f),
            new(18, 9, 0, RotationX: 1f, RotationY: 2f, RotationZ: 3f),
            new(19, 9, 0, RotationX: 4f, RotationY: 5f, RotationZ: 6f),
            new(20, 9, 0, RotationX: 7f, RotationY: 8f, RotationZ: 9f),
            new(21, 9, 0, RotationX: -1f, RotationY: -2f, RotationZ: -3f)
        ];
        Assert.True(tracker.ApplyEntityData(5, items));
        var state = tracker.Interpolate(0f)[0];
        Assert.True(state.ShowArms);
        Assert.True(state.ShowBasePlate);
        Assert.Equal(new Vector3(10f, 20f, 30f), state.HeadPose);
        Assert.Equal(new Vector3(-5f, -6f, -7f), state.BodyPose);
        Assert.Equal(new Vector3(1f, 2f, 3f), state.LeftArmPose);
        Assert.Equal(new Vector3(4f, 5f, 6f), state.RightArmPose);
        Assert.Equal(new Vector3(7f, 8f, 9f), state.LeftLegPose);
        Assert.Equal(new Vector3(-1f, -2f, -3f), state.RightLegPose);

        var frame = EntityAnimationFrame.From(state);
        var pose = new EntityPose();
        new ArmorStandAnimationProvider().Apply(new EmptyState(), frame, pose);
        var radians = MathF.PI / 180f;
        Assert.Equal(new Vector3(10f, 20f, 30f) * radians, pose.Get("head").Rotation);
        Assert.Equal(new Vector3(-5f, -6f, -7f) * radians, pose.Get("body").Rotation);
        Assert.Equal(new Vector3(1f, 2f, 3f) * radians, pose.Get("left_arm").Rotation);
        Assert.Equal(new Vector3(4f, 5f, 6f) * radians, pose.Get("right_arm").Rotation);
        Assert.Equal(new Vector3(7f, 8f, 9f) * radians, pose.Get("left_leg").Rotation);
        Assert.Equal(new Vector3(-1f, -2f, -3f) * radians, pose.Get("right_leg").Rotation);
    }

    [Fact]
    public void SmallStateRoutesToSmallBodyAndSmallArmor()
    {
        var state = new EntityRenderState(1, ArmorStandModelDefinition.ArmorStandEntityTypeId, 0f, 0f, 0f, 0f, 0f)
        {
            IsSmall = true
        };
        var small = EntityModelMesher.ResolveEntityModel(state, variants: null);
        Assert.Equal("armor_stand_small", small.Name);
        foreach (var (name, vertices) in new (string Name, int Vertices)[]
                 {
                     ("equipment_HumanoidArmor_5", 48),
                     ("equipment_HumanoidArmor_4", 72),
                     ("equipment_HumanoidLeggings_3", 72),
                     ("equipment_HumanoidArmor_2", 48)
                 })
        {
            var layer = small.RenderLayers!.Single(layer => layer.Name == name).Model!;
            Assert.True(layer.RawSourceFrame, name);
            Assert.Equal(vertices, layer.Compiled!.Cubes.Sum(cube => 24));
        }

        var adult = EntityModelResolver.Resolve(ArmorStandModelDefinition.ArmorStandEntityTypeId);
        Assert.Equal("armor_stand", adult.Name);
        Assert.Equal(264, ArmorStandModelDefinition.Body.Compiled!.Cubes.Sum(cube => 24));
    }

    private sealed class EmptyState : IEntityAnimationState;

    private static float QuantizeHalf(float value) =>
        (float)BitConverter.UInt16BitsToHalf((ushort)BitConverter.HalfToUInt16Bits((Half)value));

    private static Row[] Load(string sampleFile)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Repro", "Data", sampleFile);
        Assert.True(File.Exists(path), $"missing reference data: {path}");
        var rows = new List<Row>();
        foreach (var line in File.ReadAllLines(path))
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var f = line.Split('\t');
            rows.Add(new Row(f[1], f[2], f[3], f[4],
                float.Parse(f[5], CultureInfo.InvariantCulture),
                float.Parse(f[6], CultureInfo.InvariantCulture),
                float.Parse(f[7], CultureInfo.InvariantCulture),
                float.Parse(f[8], CultureInfo.InvariantCulture),
                float.Parse(f[9], CultureInfo.InvariantCulture)));
        }

        return [.. rows];
    }

    private readonly record struct Row(string Part, string Cube, string Polygon, string Vertex,
        float X, float Y, float Z, float U, float V);
}
