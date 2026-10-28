// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using MinecraftCSharp.Auth;
using MinecraftCSharp.Common.Lifecycle;
using MinecraftCSharp.Launcher.Launch;
using Xunit;

namespace MinecraftCSharp.Launcher.Tests;

/// <summary>
/// The launcher-to-client handoff (docs/modules/launcher.md §5.1): argument assembly, token
/// redaction, session files and dry-run behaviour.
/// </summary>
public sealed class LaunchPlanTests
{
    private const string Token = "eyJhbGciOiJIUzI1NiJ9.thisIsTheAccessTokenValueThatMustNeverBePrinted.nope";

    [Fact]
    public void PlanCarriesEveryArgumentTheClientNeeds()
    {
        using var fixture = new LaunchFixture();
        var plan = LaunchPlanBuilder.Build(fixture.Context());

        Assert.Equal("26.3", plan.VersionId);
        Assert.Contains("--version", plan.Arguments);
        Assert.Contains("26.3", plan.Arguments);
        Assert.Contains("--gameDir", plan.Arguments);
        Assert.Contains("--assetsDir", plan.Arguments);
        Assert.Contains("--assetIndex", plan.Arguments);
        Assert.Contains("34", plan.Arguments);
        Assert.Contains("--pack", plan.Arguments);
        Assert.Contains(plan.PackPath, plan.Arguments);
        Assert.Contains("--sessionFile", plan.Arguments);
        Assert.Contains("--username", plan.Arguments);
        Assert.Contains("Steve", plan.Arguments);
        Assert.Contains("--uuid", plan.Arguments);
    }

    [Fact]
    public void PlanNeverPutsTheTokenOnTheCommandLine()
    {
        using var fixture = new LaunchFixture();
        var plan = LaunchPlanBuilder.Build(fixture.Context());

        Assert.DoesNotContain(Token, plan.Arguments);
        Assert.DoesNotContain(plan.Arguments, argument => argument.Contains(Token, StringComparison.Ordinal));
        Assert.Contains("token in file, not on the command line", plan.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void DescribeRedactsLongOpaqueValues()
    {
        Assert.Equal("abcdefgh…(redacted)", LaunchPlan.Redact("abcdefgh" + new string('X', 70)));
        Assert.Equal("--username", LaunchPlan.Redact("--username"));
        Assert.Equal("C:/games/minecraft/.minecraft/assets", LaunchPlan.Redact("C:/games/minecraft/.minecraft/assets"));
    }

    [Fact]
    public void SessionFileRoundTripsWithoutLeakingThroughToString()
    {
        using var fixture = new LaunchFixture();
        var path = Path.Combine(fixture.Root, "instance", SessionFile.FileName);

        var session = new SessionFile("26.3", "Steve", "069a79f4-44e9-4726-a5be-fca90e38aaf5", Token, DateTimeOffset.UtcNow);
        session.WriteTo(path);

        var read = SessionFile.ReadFrom(path);

        Assert.Equal("Steve", read.Username);
        Assert.Equal(Token, read.AccessToken);
        Assert.DoesNotContain(Token, read.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void MissingSessionFileIsReportedWithThePath()
    {
        using var fixture = new LaunchFixture();
        var path = Path.Combine(fixture.Root, "absent", SessionFile.FileName);

        var exception = Assert.Throws<InvalidDataException>(() => SessionFile.ReadFrom(path));

        Assert.Contains("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(SessionFile.FileName, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MalformedSessionFileIsRejected()
    {
        using var fixture = new LaunchFixture();
        var path = Path.Combine(fixture.Root, "broken.json");
        File.WriteAllText(path, "{ not json");

        Assert.Throws<InvalidDataException>(() => SessionFile.ReadFrom(path));
    }

    [Fact]
    public void ClientPathPrefersTheConfiguredOverride()
    {
        var previous = Environment.GetEnvironmentVariable(LaunchPlanBuilder.ClientPathVariable);
        try
        {
            Environment.SetEnvironmentVariable(LaunchPlanBuilder.ClientPathVariable, @"C:\custom\client.dll");

            Assert.Equal(@"C:\custom\client.dll", LaunchPlanBuilder.ResolveClientPath());
            Assert.True(LaunchPlanBuilder.NeedsDotnetHost(@"C:\custom\client.dll"));
            Assert.False(LaunchPlanBuilder.NeedsDotnetHost(@"C:\custom\client.exe"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(LaunchPlanBuilder.ClientPathVariable, previous);
        }
    }

    [Fact]
    public void AssetIndexBecomesOptionalWhenTheManifestHasNone()
    {
        using var fixture = new LaunchFixture();
        var plan = LaunchPlanBuilder.Build(fixture.Context(assetIndexId: null));

        Assert.DoesNotContain("--assetIndex", plan.Arguments);
        Assert.Null(plan.AssetIndexId);
    }

    private sealed class LaunchFixture : IDisposable
    {
        public LaunchFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "mcs-launch-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public LaunchContext Context(string? assetIndexId = "34") => new(
            "26.3",
            Path.Combine(Root, "26.3.jar"),
            Path.Combine(Root, "minecraft-26.3-a51bffa6a64c.pack.zip"),
            Path.Combine(Root, "assets"),
            assetIndexId,
            Path.Combine(Root, "instance"),
            new MinecraftProfile("069a79f4-44e9-4726-a5be-fca90e38aaf5", "Steve", Skins: false),
            Token);

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, recursive: true);
                }
            }
            catch (IOException)
            {
                // Cleanup must never fail a test run.
            }
        }
    }
}
