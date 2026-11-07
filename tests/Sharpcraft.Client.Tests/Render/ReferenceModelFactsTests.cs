// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Text.Json;
using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// T-708: compares the production DSL output with model facts extracted from the local 26.3 analysis workspace by
/// tools/Sharpcraft.ReferenceVerifier. This is a source-derived oracle, independent of the legacy FromVanilla arrays.
/// Breeze is intentionally not compared yet: the reference source has multi-cube parts (head/eyes/wind shells) while the
/// current flattened EntityModelPart representation splits those cubes into pseudo-parts; it needs a geometry-level
/// golden vector, not a part-by-part comparison.
/// </summary>
public sealed class ReferenceModelFactsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void WardenMatchesReferenceFacts()
    {
        var model = Load("WardenModel");
        var expected = BuildExpectedParts(model);
        Assert.True(expected.Length <= WardenModelDefinition.Definition.Parts.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            ModelPartAssertions.AssertEqual(expected[i], WardenModelDefinition.Definition.Parts[i]);
        }
    }

    [Fact]
    public void AdultFoxMatchesReferenceFacts()
    {
        var model = Load("AdultFoxModel");
        AssertPartsMatch(BuildExpectedParts(model), FoxModelDefinition.Definition.Parts);
    }

    [Fact]
    public void AdultRabbitMatchesReferenceFacts()
    {
        var model = Load("AdultRabbitModel");
        AssertPartsMatch(BuildExpectedParts(model), RabbitModelDefinition.Definition.Parts);
    }

    [Fact]
    public void ElytraFactsMatchVanillaConstants()
    {
        var model = Load("ElytraModel");
        Assert.Equal(64, model.TextureWidth);
        Assert.Equal(32, model.TextureHeight);
        Assert.Equal(2, model.Parts.Count);

        var left = model.Parts[0];
        Assert.Equal("left_wing", left.Name);
        Assert.Null(left.Parent);
        Assert.Equal(5d, left.Pose.X);
        Assert.Equal(Math.PI / 12d, left.Pose.RotationX, 12);
        Assert.Equal(-Math.PI / 12d, left.Pose.RotationZ, 12);
        Assert.Single(left.Cubes);
        var cube = left.Cubes[0];
        Assert.Equal(22, cube.TextureX);
        Assert.Equal(0, cube.TextureY);
        Assert.Equal(-10d, cube.MinX);
        Assert.Equal(10d, cube.Width);
        Assert.Equal(20d, cube.Height);
        Assert.Equal(2d, cube.Depth);
        Assert.Equal(1d, cube.GrowX);
        Assert.Equal(1d, cube.GrowY);
        Assert.Equal(1d, cube.GrowZ);

        var right = model.Parts[1];
        Assert.Equal("right_wing", right.Name);
        Assert.True(right.Cubes[0].Mirror);
        Assert.Equal(-5d, right.Pose.X);
        Assert.Equal(Math.PI / 12d, right.Pose.RotationZ, 12);
    }

    private static ModelFact Load(string className)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Render", "ReferenceFacts", "vanilla-model-facts.json");
        var document = JsonSerializer.Deserialize<FactDocument>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException("reference facts could not be decoded");
        return document.Models.Single(model => model.Class == className);
    }

    private static EntityModelPart[] BuildExpectedParts(ModelFact model)
    {
        var parts = new List<EntityModelPart>();
        foreach (var part in model.Parts)
        {
            if (part.Cubes.Count > 1)
            {
                throw new InvalidOperationException($"part {part.Name} has multiple cubes; use geometry-level comparison");
            }

            if (part.Cubes.Count == 0)
            {
                parts.Add(BuildPart(part, null));
                continue;
            }

            parts.Add(BuildPart(part, part.Cubes[0]));
        }

        return [.. parts];
    }

    private static EntityModelPart BuildPart(FactPart part, FactCube? cube)
    {
        if (Math.Abs(part.Pose.ScaleX - 1d) > 1e-9
            || Math.Abs(part.Pose.ScaleY - 1d) > 1e-9
            || Math.Abs(part.Pose.ScaleZ - 1d) > 1e-9)
        {
            throw new InvalidOperationException($"part {part.Name} has non-unit pose scale; add DSL scale support first");
        }

        var minX = cube?.MinX ?? 0d;
        var minY = cube?.MinY ?? 0d;
        var minZ = cube?.MinZ ?? 0d;
        var width = cube?.Width ?? 0d;
        var height = cube?.Height ?? 0d;
        var depth = cube?.Depth ?? 0d;
        var grow = cube?.GrowX ?? 0d;
        if (cube is not null
            && (Math.Abs(cube.GrowY - cube.GrowX) > 1e-9 || Math.Abs(cube.GrowZ - cube.GrowX) > 1e-9))
        {
            throw new InvalidOperationException($"part {part.Name} uses non-uniform grow; add DSL GrowXyz support first");
        }

        var rootRotationX = part.Parent is null ? -part.Pose.RotationX : part.Pose.RotationX;
        var rootRotationY = part.Parent is null ? -part.Pose.RotationY : part.Pose.RotationY;
        // Current production model definitions store root X/Y rotations already negated relative to the reference.
        // Child values go through BakeChild, which performs the same conjugation. A future canonicalisation can move
        // this historical root compensation into BakeRoot and remove the negation here.
        var rotationX = (float)rootRotationX;
        var rotationY = (float)rootRotationY;
        var rotationZ = (float)part.Pose.RotationZ;

        if (part.Parent is null)
        {
            return LegacyModelPartFactory.FromVanilla(
                part.Name,
                (float)part.Pose.X,
                (float)part.Pose.Y,
                (float)part.Pose.Z,
                (float)minX,
                (float)minY,
                (float)minZ,
                (float)width,
                (float)height,
                (float)depth,
                cube?.TextureX ?? 0,
                cube?.TextureY ?? 0,
                cube?.Mirror ?? false,
                mirrorX: true,
                grow: (float)grow,
                rotationX: rotationX,
                rotationY: rotationY,
                rotationZ: rotationZ);
        }

        return LegacyModelPartFactory.FromVanillaChild(
            part.Name,
            part.Parent,
            (float)part.Pose.X,
            (float)part.Pose.Y,
            (float)part.Pose.Z,
            (float)minX,
            (float)minY,
            (float)minZ,
            (float)width,
            (float)height,
            (float)depth,
            cube?.TextureX ?? 0,
            cube?.TextureY ?? 0,
            cube?.Mirror ?? false,
            grow: (float)grow,
            rotationX: (float)part.Pose.RotationX,
            rotationY: (float)part.Pose.RotationY,
            rotationZ: (float)part.Pose.RotationZ);
    }

    private static void AssertPartsMatch(EntityModelPart[] expected, IReadOnlyList<EntityModelPart> actual)
    {
        Assert.Equal(expected.Length, actual.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            ModelPartAssertions.AssertEqual(expected[i], actual[i]);
        }
    }

    private sealed class FactDocument
    {
        public List<ModelFact> Models { get; set; } = [];
    }

    private sealed class ModelFact
    {
        public string Class { get; set; } = string.Empty;
        public int TextureWidth { get; set; }
        public int TextureHeight { get; set; }
        public List<FactPart> Parts { get; set; } = [];
    }

    private sealed class FactPart
    {
        public string Name { get; set; } = string.Empty;
        public string? Parent { get; set; }
        public FactPose Pose { get; set; } = new();
        public List<FactCube> Cubes { get; set; } = [];
    }

    private sealed class FactPose
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double RotationX { get; set; }
        public double RotationY { get; set; }
        public double RotationZ { get; set; }
        public double ScaleX { get; set; }
        public double ScaleY { get; set; }
        public double ScaleZ { get; set; }
    }

    private sealed class FactCube
    {
        public int TextureX { get; set; }
        public int TextureY { get; set; }
        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MinZ { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Depth { get; set; }
        public double GrowX { get; set; }
        public double GrowY { get; set; }
        public double GrowZ { get; set; }
        public bool Mirror { get; set; }
    }
}
