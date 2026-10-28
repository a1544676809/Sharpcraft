// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Microsoft.VisualBasic;
using Xunit;

namespace Sharpcraft.Common.Tests;

/// <summary>
/// Manual integration test for the login handshake. It deliberately stops before any
/// authentication, so it proves the crypto layer without needing credentials.
/// </summary>
/// <remarks>
/// Set <c>MC_TEST_SERVER</c> to run it. A vanilla server answers the encryption response with
/// an <b>encrypted</b> disconnect ("Failed to verify username"), which only decodes if the
/// AES/CFB8 stream is correct, so a passing run is real evidence.
/// </remarks>
public sealed class LoginProbeIntegrationTests
{
    private const string ServerVariable = "MC_TEST_SERVER";

    [Fact]
    public void ServerRepliesWithEncryptedTrafficAfterKeyExchange()
    {
        var target = Environment.GetEnvironmentVariable(ServerVariable);
        if (string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        var result = LoginProbe.Run(target, "MCSProbe", TimeSpan.FromSeconds(20));

        Assert.True(
            result.EncryptionEnabled,
            $"expected an encrypted reply after the key exchange, got: {result.Reason}");

        Console.WriteLine(
            $"[login] address={target} encrypted=true loggedIn={result.LoggedIn} reason={result.Reason}");
    }
}
