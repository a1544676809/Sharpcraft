// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client;
using Sharpcraft.Common.Lifecycle;
using Xunit;

namespace Sharpcraft.Client.Tests;

/// <summary>
/// The client's vanilla-style command line (docs/decisions/0026-launcher-client-decoupling.md §4 and
/// docs/modules/client.md §3): it must accept a launcher handoff and a vanilla offline start, apply vanilla
/// defaults, derive the offline UUID, and ignore unknown arguments.
/// </summary>
public sealed class ClientArgumentTests
{
    [Fact]
    public void FullLauncherHandoffParses()
    {
        var arguments = ClientArguments.Parse(
        [
            "--version", "26.3",
            "--gameDir", @"C:\data\instances\26.3",
            "--assetsDir", @"E:\games\minecraft\.minecraft\assets",
            "--assetIndex", "34",
            "--pack", @"C:\data\packs\minecraft-26.3-a51bffa6a64c.pack.zip",
            "--sessionFile", @"C:\data\instances\26.3\session.json",
            "--username", "Steve",
            "--uuid", "069a79f4-44e9-4726-a5be-fca90e38aaf5"
        ]);

        Assert.True(arguments.IsUsable);
        Assert.Equal("26.3", arguments.VersionId);
        Assert.Equal("34", arguments.AssetIndexId);
        Assert.Equal("Steve", arguments.Username);
        Assert.EndsWith("session.json", arguments.SessionFilePath!, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingVersionIsReported()
    {
        var arguments = ClientArguments.Parse(["--username", "Steve"]);

        Assert.False(arguments.IsUsable);
        Assert.Contains(arguments.Errors, error => error.Contains("--version", StringComparison.Ordinal));
    }

    [Fact]
    public void VanillaDefaultsDeriveTheOfflineUuidAndName()
    {
        var arguments = ClientArguments.Parse(["--version", "26.3"]);

        Assert.True(arguments.IsUsable);
        Assert.False(string.IsNullOrWhiteSpace(arguments.GameDirectory));
        Assert.False(string.IsNullOrWhiteSpace(arguments.Username));
        Assert.StartsWith("Player", arguments.Username!, StringComparison.Ordinal);
        Assert.Equal(
            OfflineIdentity.FormatUuid(OfflineIdentity.DeriveUuid(arguments.Username!)),
            arguments.Uuid);
    }

    [Fact]
    public void InvalidUuidFallsBackToTheOfflineDerivation()
    {
        var arguments = ClientArguments.Parse(["--version", "26.3", "--username", "Steve", "--uuid", "not-a-uuid"]);

        Assert.True(arguments.IsUsable);
        Assert.Equal(OfflineIdentity.FormatUuid(OfflineIdentity.DeriveUuid("Steve")), arguments.Uuid);
    }

    [Fact]
    public void VanillaQuickPlayAndSizeParse()
    {
        var arguments = ClientArguments.Parse(
        [
            "--version", "26.3",
            "--accessToken", "0",
            "--username", "OfflineTester1",
            "--offlineDeveloperMode",
            "--quickPlayMultiplayer", "127.0.0.1:25565",
            "--width", "1280",
            "--height", "720",
            "--versionType", "release"
        ]);

        Assert.True(arguments.IsUsable);
        Assert.Equal("127.0.0.1:25565", arguments.QuickPlayMultiplayer);
        Assert.Equal(1280, arguments.Width);
        Assert.Equal(720, arguments.Height);
        Assert.Equal("release", arguments.VersionType);
        Assert.True(arguments.OfflineDeveloperMode);
    }

    [Fact]
    public void DanglingSwitchIsReported()
    {
        var arguments = ClientArguments.Parse(["--version", "26.3", "--gameDir"]);

        Assert.False(arguments.IsUsable);
        Assert.Contains(arguments.Errors, error => error.Contains("needs a value", StringComparison.Ordinal));
    }

    [Fact]
    public void UnknownSwitchesAndNonOptionsAreToleratedForThirdPartyLaunchers()
    {
        var arguments = ClientArguments.Parse(
        [
            "--version", "26.3",
            "--gameDir", ".",
            "--username", "Steve",
            "--width", "1280",
            "--demo",
            "stray-non-option"
        ]);

        Assert.True(arguments.IsUsable);
        Assert.Equal("26.3", arguments.VersionId);
    }
}
