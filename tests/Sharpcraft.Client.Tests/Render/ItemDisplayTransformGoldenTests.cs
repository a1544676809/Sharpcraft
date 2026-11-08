// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using System.Numerics;
using System.Text.Json;
using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Golden vectors captured from the reference implementation itself: the Java probe in
/// <c>tools/Sharpcraft.ItemRender.Audit/truth/DisplayTransformProbe.java</c> replays the reference
/// <c>ItemTransform.apply</c> sequence through the same JOML build the game ships and records where model points land, so
/// this test compares against the reference's own numbers instead of a hand-derived convention.
/// </summary>
public sealed class ItemDisplayTransformGoldenTests
{
    private sealed record Case(string Name, Vector3 Rotation, Vector3 Translation, Vector3 Scale, (Vector3 In, Vector3 Out)[] Points);

    [Fact]
    public void EveryGoldenCaseMatchesTheReferencePointMapping()
    {
        var cases = Load();
        Assert.NotEmpty(cases);

        var failures = new List<string>();
        foreach (var kase in cases)
        {
            var transform = new ItemDisplayTransform(kase.Rotation, kase.Translation, kase.Scale);
            var matrix = transform.ToMatrix();
            foreach (var (input, expected) in kase.Points)
            {
                var actual = Vector3.Transform(input, matrix);
                if ((actual - expected).Length() > 1e-4f)
                {
                    failures.Add(
                        string.Create(CultureInfo.InvariantCulture,
                            $"{kase.Name} ({input.X},{input.Y},{input.Z}): expected ({expected.X:F6},{expected.Y:F6},{expected.Z:F6}) actual ({actual.X:F6},{actual.Y:F6},{actual.Z:F6})"));
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static IReadOnlyList<Case> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "item-display-truth.json");
        if (!File.Exists(path))
        {
            // Fall back to the repository layout when the test host does not copy the data folder.
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current is not null && !File.Exists(Path.Combine(current.FullName, "Sharpcraft.slnx")))
            {
                current = current.Parent;
            }

            if (current is not null)
            {
                path = Path.Combine(current.FullName, "tests", "Sharpcraft.Client.Tests", "Data", "item-display-truth.json");
            }
        }

        Assert.True(File.Exists(path), $"truth fixture missing: {path}");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var result = new List<Case>();
        foreach (var entry in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            var name = entry.GetProperty("name").GetString()!;
            var points = new List<(Vector3, Vector3)>();
            foreach (var point in entry.GetProperty("points").EnumerateArray())
            {
                points.Add((Read(point.GetProperty("in")), Read(point.GetProperty("out"))));
            }

            // The reference stores each case's pose values in the source probe; reconstruct them from the fixture name.
            var (rotation, translation, scale) = PoseOf(name);
            result.Add(new Case(name, rotation, translation, scale, [.. points]));
        }

        return result;
    }

    private static Vector3 Read(JsonElement element) =>
        new(element[0].GetSingle(), element[1].GetSingle(), element[2].GetSingle());

    private static (Vector3 Rotation, Vector3 Translation, Vector3 Scale) PoseOf(string name) => name switch
    {
        "chest_gui" => (new Vector3(30f, 45f, 0f), Vector3.Zero, new Vector3(0.625f)),
        "chest_ground" => (Vector3.Zero, new Vector3(0f, 3f / 16f, 0f), new Vector3(0.25f)),
        "chest_fixed" => (new Vector3(0f, 180f, 0f), Vector3.Zero, new Vector3(0.5f)),
        "chest_thirdperson_righthand" => (new Vector3(75f, 315f, 0f), new Vector3(0f, 2.5f / 16f, 0f), new Vector3(0.375f)),
        "rotation_order_probe" => (new Vector3(30f, 45f, 60f), Vector3.Zero, Vector3.One),
        "nonuniform_scale_probe" => (new Vector3(10f, 20f, 30f), new Vector3(1f / 16f, -2f / 16f, 3f / 16f), new Vector3(0.5f, 0.75f, 1.25f)),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "unknown golden case")
    };
}
