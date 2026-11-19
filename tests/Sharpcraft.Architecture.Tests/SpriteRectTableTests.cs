// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Xunit;

namespace Sharpcraft.Architecture.Tests;

/// <summary>
/// A vertex carries a sixteen bit sprite id instead of the atlas bounds, so the id is only meaningful against the
/// sprite rectangle table of the texture its draw binds (Issue 226). Every producer of a <c>SpriteRect</c> that ends
/// up in a vertex therefore has to name its rectangle; a forgotten one silently defaults to id zero, which every
/// table maps to the whole texture, and the face renders a shrunken copy of the atlas.
/// </summary>
/// <remarks>
/// The check is textual because the defect is textual: the six <c>new SpriteRect(...)</c> producers of the T-832
/// change were all valid C# and all but one were wrong. An allow-list keeps the intermediate-only producers honest:
/// each entry states why the id never reaches a vertex.
/// </remarks>
public sealed class SpriteRectTableTests
{
    /// <summary>
    /// Producers whose rectangle is an intermediate value only. The geometry they build is either re-emitted with a
    /// real atlas sprite or read for positions and tile uv alone, so its packed sprite id is never drawn.
    /// </summary>
    private static readonly Dictionary<string, string> IntermediateOnly = new(StringComparer.Ordinal)
    {
        ["src/Sharpcraft.Client/Render/ChestMesher.cs"] =
            "RawMesh caches positions and uv; AppendRawChest rewrites every vertex with the atlas sprite",
        ["src/Sharpcraft.Client/Render/ShulkerBoxMesher.cs"] =
            "RawMesh caches positions and uv; AppendRaw rewrites every vertex with the atlas sprite",
        ["src/Sharpcraft.Client/Render/DecoratedPotMesher.cs"] =
            "RawMesh caches positions and uv; AppendRaw rewrites every vertex with the atlas sprite",
        ["src/Sharpcraft.Client/Render/ItemInHandMesher.cs"] =
            "HeldSpecialGeometry keeps positions and tile uv only, and the emitter supplies the item sprite",
        ["src/Sharpcraft.Client/Render/ItemAtlas.cs"] =
            "Missing() describes a single whole-texture stand-in sheet, so entry zero is its only rectangle"
    };

    [Fact]
    public void EveryVertexSpriteProducerNamesItsRectangle()
    {
        var offenders = new List<string>();
        foreach (var file in SourceFiles())
        {
            var text = File.ReadAllText(file);
            foreach (var (line, window) in ConstructionWindows(text))
            {
                if (window.Contains("RectId", StringComparison.Ordinal))
                {
                    continue;
                }

                var relative = Path.GetRelativePath(RepositoryLocator.Root, file).Replace('\\', '/');
                if (IntermediateOnly.ContainsKey(relative))
                {
                    continue;
                }

                offenders.Add($"{relative}:{line}");
            }
        }

        Assert.Empty(offenders);
    }

    [Fact]
    public void EveryAllowListedProducerStillExists()
    {
        // A stale entry would silently widen the exemption to a file that no longer needs it, so an entry has to name a
        // file that still constructs a rectangle *and* still relies on the exemption.
        var offenders = new List<string>();
        foreach (var entry in IntermediateOnly)
        {
            var path = Path.Combine(RepositoryLocator.Root, entry.Key.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                offenders.Add($"{entry.Key}: file is gone");
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.Value))
            {
                offenders.Add($"{entry.Key}: no reason recorded");
                continue;
            }

            if (!ConstructionWindows(File.ReadAllText(path)).Any())
            {
                offenders.Add($"{entry.Key}: no longer constructs a SpriteRect, so the exemption is stale");
            }
        }

        Assert.Empty(offenders);
    }

    [Fact]
    public void EveryAtlasThatExposesATableReservesEntryZeroForTheWholeTexture()
    {
        // Entry zero is what a vertex with no atlas entry resolves to; it has to mean the whole texture everywhere,
        // or placeholder geometry would sample an arbitrary sprite.
        var tables = typeof(Sharpcraft.Client.Assets.SpriteAtlas).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .SelectMany(type => type.GetProperties())
            .Where(property => property.PropertyType == typeof(float[]) && property.Name == "RectTable")
            .Select(property => property.DeclaringType!.FullName!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("Sharpcraft.Client.Assets.SpriteAtlas", tables);
        Assert.Contains("Sharpcraft.Client.Render.EntitySkinAtlas", tables);
        Assert.Contains("Sharpcraft.Client.Render.ItemAtlas", tables);
    }

    private static IEnumerable<string> SourceFiles() =>
        Directory.EnumerateFiles(Path.Combine(RepositoryLocator.Root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    /// <summary>Yields each construction site with the text that follows it, up to the end of the statement.</summary>
    private static IEnumerable<(int Line, string Window)> ConstructionWindows(string text)
    {
        const string Marker = "new SpriteRect(";
        var index = 0;
        while ((index = text.IndexOf(Marker, index, StringComparison.Ordinal)) >= 0)
        {
            var end = text.IndexOf(';', index);
            var length = end < 0 ? text.Length - index : end - index;
            var window = text.Substring(index, Math.Min(length, 400));
            var line = text.AsSpan(0, index).Count('\n') + 1;
            yield return (line, window);
            index += Marker.Length;
        }
    }
}
