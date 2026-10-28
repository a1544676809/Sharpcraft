// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using MinecraftCSharp.Client;
using Xunit;

namespace MinecraftCSharp.Client.Tests;

/// <summary>
/// The client side of the launcher handoff (docs/modules/launcher.md §5.1): it must accept the
/// launcher's arguments, reject incomplete ones, and never degrade silently.
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
    public void MissingVersionAndGameDirectoryAreBothReported()
    {
        var arguments = ClientArguments.Parse(["--username", "Steve"]);

        Assert.False(arguments.IsUsable);
        Assert.Contains(arguments.Errors, error => error.Contains("--version", StringComparison.Ordinal));
        Assert.Contains(arguments.Errors, error => error.Contains("--gameDir", StringComparison.Ordinal));
    }

    [Fact]
    public void IdentityIsRequiredSomehow()
    {
        var arguments = ClientArguments.Parse(["--version", "26.3", "--gameDir", "."]);

        Assert.False(arguments.IsUsable);
        Assert.Contains(arguments.Errors, error => error.Contains("--sessionFile", StringComparison.Ordinal));
    }

    [Fact]
    public void DanglingSwitchIsReported()
    {
        var arguments = ClientArguments.Parse(["--version", "26.3", "--gameDir"]);

        Assert.False(arguments.IsUsable);
        Assert.Contains(arguments.Errors, error => error.Contains("needs a value", StringComparison.Ordinal));
    }

    [Fact]
    public void UnknownSwitchesAreToleratedForThirdPartyLaunchers()
    {
        var arguments = ClientArguments.Parse(
        [
            "--version", "26.3",
            "--gameDir", ".",
            "--username", "Steve",
            "--width", "1280",
            "--demo"
        ]);

        Assert.True(arguments.IsUsable);
        Assert.Equal("26.3", arguments.VersionId);
    }
}
