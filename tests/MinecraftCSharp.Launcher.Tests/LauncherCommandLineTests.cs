// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using Xunit;

namespace MinecraftCSharp.Launcher.Tests;

/// <summary>
/// Command line parsing contracts (docs/modules/launcher.md 3).
/// </summary>
public sealed class LauncherCommandLineTests
{
    [Fact]
    public void NoArgumentsShowsHelp()
    {
        var line = LauncherCommandLine.Parse([]);

        Assert.Equal(LauncherCommand.Help, line.Command);
        Assert.Null(line.Error);
    }

    [Theory]
    [InlineData("help")]
    [InlineData("--help")]
    [InlineData("-h")]
    public void HelpAliasesAreAccepted(string argument)
    {
        var line = LauncherCommandLine.Parse([argument]);

        Assert.Equal(LauncherCommand.Help, line.Command);
        Assert.Null(line.Error);
    }

    [Fact]
    public void LoginTakesNoArguments()
    {
        Assert.Equal(LauncherCommand.Login, LauncherCommandLine.Parse(["login"]).Command);
        Assert.NotNull(LauncherCommandLine.Parse(["login", "extra"]).Error);
    }

    [Fact]
    public void ProbeCarriesItsTarget()
    {
        var line = LauncherCommandLine.Parse(["probe", "mcf.ddger.top"]);

        Assert.Equal(LauncherCommand.Probe, line.Command);
        Assert.Equal("mcf.ddger.top", line.Target);
        Assert.Null(line.Error);
    }

    [Theory]
    [InlineData(new[] { "probe" }, "probe needs a server address")]
    [InlineData(new[] { "probe", "a", "b" }, "probe takes exactly one address")]
    public void ProbeRejectsWrongArity(string[] arguments, string expectedError)
    {
        var line = LauncherCommandLine.Parse(arguments);

        Assert.Equal(expectedError, line.Error);
    }

    [Fact]
    public void UnknownCommandsFailWithUsage()
    {
        var line = LauncherCommandLine.Parse(["frobnicate"]);

        Assert.Equal(LauncherCommand.Help, line.Command);
        Assert.Contains("unknown command", line.Error);
    }

    [Fact]
    public void UsageMentionsEveryCommand()
    {
        Assert.Contains("login", LauncherCommandLine.Usage);
        Assert.Contains("probe", LauncherCommandLine.Usage);
        Assert.Contains("help", LauncherCommandLine.Usage);
    }
}
