// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Xunit;

namespace Sharpcraft.Client.Tests.Assets;

/// <summary>
/// Pack format compatibility (docs/analysis/assets/resource-pack-format.md): a pack declares a range, and one that
/// excludes the client is skipped rather than loaded into a shape its author never tested.
/// </summary>
public sealed class ResourcePackFormatTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sharpcraft-fmt-" + Guid.NewGuid().ToString("N"));

    public ResourcePackFormatTests() => Directory.CreateDirectory(Path.Combine(_root, "packs"));

    [Fact]
    public void TheModernRangeFieldsAreRead()
    {
        var range = ResourcePackFormats.TryParseSupportedFormats(
            """{ "pack": { "description": "t", "min_format": 96, "max_format": "97.1" } }""");

        Assert.NotNull(range);
        Assert.Equal(new PackFormatVersion(96, 0), range!.Value.Min);
        Assert.Equal(new PackFormatVersion(97, 1), range.Value.Max);
    }

    [Fact]
    public void TheLegacySingleFormatCoversEveryMinorOfThatMajor()
    {
        var range = ResourcePackFormats.TryParseSupportedFormats(
            """{ "pack": { "description": "t", "pack_format": 64 } }""");

        Assert.NotNull(range);
        Assert.Equal(new PackFormatVersion(64, 0), range!.Value.Min);
        Assert.Equal(new PackFormatVersion(64, int.MaxValue), range.Value.Max);
    }

    [Fact]
    public void ARealPackDeclaringOnlyMajorVersionsIsAccepted()
    {
        // The shipped Faithful 32x pack for 26.3 declares exactly this, and 97.1 must fit inside "up to 97".
        var range = ResourcePackFormats.TryParseSupportedFormats(
            """{ "pack": { "description": "The go-to 32x resource pack.", "min_format": 84, "max_format": 97 } }""");

        Assert.NotNull(range);
        Assert.Equal(new PackFormatVersion(84, 0), range!.Value.Min);
        Assert.Equal(new PackFormatVersion(97, int.MaxValue), range.Value.Max);

        var client = new PackFormatVersion(97, 1);
        Assert.True(client.CompareTo(range.Value.Min) >= 0 && client.CompareTo(range.Value.Max) <= 0);
    }

    [Fact]
    public void APackOutsideTheClientFormatIsSkipped()
    {
        MakePack("old", """{ "pack": { "description": "t", "min_format": 60, "max_format": 70 } }""");
        MakePack("current", """{ "pack": { "description": "t", "min_format": 95, "max_format": 97 } }""");

        var packs = ResourcePackDiscovery.Discover(Path.Combine(_root, "packs"), new PackFormatVersion(96, 0));

        Assert.Single(packs);
        Assert.Equal("current", packs[0].Id);
        packs[0].Dispose();
    }

    [Fact]
    public void APackWithNoDeclaredFormatIsAccepted()
    {
        MakePack("bare", """{ "pack": { "description": "t" } }""");

        var packs = ResourcePackDiscovery.Discover(Path.Combine(_root, "packs"), new PackFormatVersion(96, 0));

        Assert.Single(packs);
        packs[0].Dispose();
    }

    [Fact]
    public void WithoutAClientFormatNothingIsFiltered()
    {
        MakePack("old", """{ "pack": { "description": "t", "min_format": 1, "max_format": 2 } }""");

        var packs = ResourcePackDiscovery.Discover(Path.Combine(_root, "packs"));

        Assert.Single(packs);
        packs[0].Dispose();
    }

    private void MakePack(string name, string metadata)
    {
        var root = Path.Combine(_root, "packs", name);
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "pack.mcmeta"), metadata);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
