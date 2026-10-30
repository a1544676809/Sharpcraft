// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.IO.Compression;
using System.Text;
using Sharpcraft.Common.Localization;
using Xunit;

namespace Sharpcraft.Common.Tests.Localization;

/// <summary>
/// The language file as it really is inside the asset pack (docs/modules/ui.md 8.4).
/// </summary>
/// <remarks>
/// The unit tests use a hand written sample, which cannot catch a wrong resource path or a file that is not where it
/// is expected. This test reads the pack the launcher produced, when one is present, and checks that a key the server
/// actually sends resolves to text. It returns quietly when no pack exists, because a checkout without the game's
/// assets is a legitimate state and must not fail the suite.
/// </remarks>
public sealed class PackLanguageTests
{
    [Fact]
    public void TheLanguageFileInTheAssetPackResolvesAKeyTheServerSends()
    {
        var packPath = FindPack();

        if (packPath is null)
        {
            // No pack on this machine: nothing to verify, and inventing an asset would violate the asset rules.
            return;
        }

        using var archive = ZipFile.OpenRead(packPath);
        var entry = archive.GetEntry("assets/minecraft/lang/en_us.json");

        Assert.NotNull(entry);

        using var reader = new StreamReader(entry!.Open(), Encoding.UTF8);
        var table = LanguageTable.Parse(reader.ReadToEnd());

        Assert.True(table.Count > 1000, $"a language file has thousands of keys, found {table.Count}");

        // The two keys the server sent to this client during an earlier session, which used to be shown raw.
        Assert.True(table.TryGet("command.unknown.command", out var unknown));
        Assert.Contains("Unknown", unknown, StringComparison.OrdinalIgnoreCase);

        Assert.True(table.TryGet("chat.type.text", out var chat));
        Assert.Equal("<%s> %s", chat);
    }

    private static string? FindPack()
    {
        var configured = Environment.GetEnvironmentVariable("MC_ASSETS_PACK");

        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            return configured;
        }

        var directory = Environment.GetEnvironmentVariable("MC_ASSETS_OUT") is { Length: > 0 } output
            ? output
            : Path.Combine(Path.GetTempPath(), "mcs-assets-pack");

        if (!Directory.Exists(directory))
        {
            return null;
        }

        return Directory.EnumerateFiles(directory, "*.pack.zip").FirstOrDefault();
    }
}
