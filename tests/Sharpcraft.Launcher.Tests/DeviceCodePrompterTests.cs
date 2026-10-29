// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Auth;
using Sharpcraft.Launcher;
using Xunit;

namespace Sharpcraft.Launcher.Tests;

/// <summary>
/// Device code presentation: the verification URL carries the code so the browser lands on a usable
/// page, and the opener command is chosen per platform (docs/modules/auth.md 3).
/// </summary>
public sealed class DeviceCodePrompterTests
{
    private static DeviceCodeChallenge Challenge(string verificationUri = "https://www.microsoft.com/link") =>
        new("device", "ABC123", verificationUri, TimeSpan.FromMinutes(15), TimeSpan.FromSeconds(5), "sign in");

    [Fact]
    public void TheVerificationUrlCarriesTheCode()
    {
        Assert.Equal(
            "https://www.microsoft.com/link?otc=ABC123",
            DeviceCodePrompter.VerificationUrl(Challenge()));
    }

    [Fact]
    public void AnExistingQueryStringIsExtended()
    {
        Assert.Equal(
            "https://www.microsoft.com/link?x=1&otc=ABC123",
            DeviceCodePrompter.VerificationUrl(Challenge("https://www.microsoft.com/link?x=1")));
    }

    [Fact]
    public void WindowsPrefersTheConfiguredBrowser()
    {
        var (fileName, arguments) = DeviceCodePrompter.OpenerFor(
            "https://example.invalid", DeviceCodePrompter.OSPlatformKind.Windows, "msedge");

        Assert.Equal("msedge", fileName);
        Assert.Contains("https://example.invalid", arguments);
    }

    [Fact]
    public void WindowsWithoutABrowserUsesTheShell()
    {
        var (fileName, arguments) = DeviceCodePrompter.OpenerFor(
            "https://example.invalid", DeviceCodePrompter.OSPlatformKind.Windows, null);

        Assert.Equal("cmd.exe", fileName);
        Assert.Contains("https://example.invalid", arguments);
    }

    [Fact]
    public void MacAndLinuxUseTheirOwnOpeners()
    {
        Assert.Equal("open", DeviceCodePrompter.OpenerFor("u", DeviceCodePrompter.OSPlatformKind.MacOS, null).FileName);
        Assert.Equal("xdg-open", DeviceCodePrompter.OpenerFor("u", DeviceCodePrompter.OSPlatformKind.Linux, null).FileName);
    }
}
