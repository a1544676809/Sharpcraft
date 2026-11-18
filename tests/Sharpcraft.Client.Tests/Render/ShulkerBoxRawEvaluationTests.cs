// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>task-211 shulker box raw evaluation evidence: static bind sample and state-dependent blocker.</summary>
public sealed class ShulkerBoxRawEvaluationTests
{
    [Fact]
    public void SourceBindSampleHasTwoFullBoxes()
    {
        var rows = Load();
        Assert.Equal(48, rows.Count);
        Assert.Equal(24, rows.Count(r => r.Part == "/lid"));
        Assert.Equal(24, rows.Count(r => r.Part == "/base"));
        Assert.All(rows, row => Assert.Equal("SHULKER_BOX", row.Layer));
    }

    [Fact]
    public void SourceBindFirstVertexMatchesModelProbe()
    {
        var first = Load()[0];
        Assert.Equal("/lid", first.Part);
        Assert.Equal(0.5f, first.X);
        Assert.Equal(0.5f, first.Y);
        Assert.Equal(0.5f, first.Z);
        Assert.Equal(0.5f, first.U);
        Assert.Equal(0f, first.V);
    }

    [Fact]
    public void StaticEntityModelDefinitionCannotExpressOpenProgress()
    {
        // The source lid position/rotation is a function of progress; EntityModelDefinition only carries
        // bind-pose parts (no openness/progress parameter), so a raw definition would drop the state.
        var members = typeof(EntityModelDefinition).GetMembers()
            .Select(member => member.Name)
            .ToArray();
        Assert.DoesNotContain(members, name => name.Contains("Open", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(members, name => name.Contains("Progress", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CustomMesherKeepsTheStateDrivenPath()
    {
        Assert.Contains(typeof(ShulkerBoxMesher).GetMethods(), method => method.Name == "Build");
    }

    private static IReadOnlyList<Row> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Repro", "Data", "shulker-box-raw-vectors.reference.txt");
        Assert.True(File.Exists(path), $"missing reference data: {path}");
        var rows = new List<Row>();
        foreach (var line in File.ReadAllLines(path))
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var fields = line.Split('\t');
            rows.Add(new Row(
                fields[0],
                fields[1],
                float.Parse(fields[5], System.Globalization.CultureInfo.InvariantCulture),
                float.Parse(fields[6], System.Globalization.CultureInfo.InvariantCulture),
                float.Parse(fields[7], System.Globalization.CultureInfo.InvariantCulture),
                float.Parse(fields[8], System.Globalization.CultureInfo.InvariantCulture),
                float.Parse(fields[9], System.Globalization.CultureInfo.InvariantCulture)));
        }

        return rows;
    }

    private readonly record struct Row(string Layer, string Part, float X, float Y, float Z, float U, float V);
}
