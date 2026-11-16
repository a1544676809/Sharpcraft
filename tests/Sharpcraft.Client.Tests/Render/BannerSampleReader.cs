// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>Reads the keyed banner reference sample (group|part|cube|polygon|vertex|x|y|z|u|v).</summary>
internal static class BannerSampleReader
{
    internal sealed record Row(string Group, string Part, int Cube, int Polygon, int Vertex,
        float X, float Y, float Z, float U, float V);

    internal static IEnumerable<Row> AllRows()
    {
        foreach (var line in File.ReadLines(BannerGoldenTests.SamplePath))
        {
            if (line.StartsWith('#') || line.Length == 0)
            {
                continue;
            }

            var p = line.Split('|');
            yield return new Row(
                p[0], p[1],
                int.Parse(p[2], CultureInfo.InvariantCulture),
                int.Parse(p[3], CultureInfo.InvariantCulture),
                int.Parse(p[4], CultureInfo.InvariantCulture),
                Parse(p[5]), Parse(p[6]), Parse(p[7]), Parse(p[8]), Parse(p[9]));
        }
    }

    internal static List<Row> Rows(string group) => AllRows().Where(r => r.Group == group).ToList();

    internal static IEnumerable<string> AllGroups() => AllRows().Select(r => r.Group).Distinct();

    private static float Parse(string value) => float.Parse(value, CultureInfo.InvariantCulture);
}
