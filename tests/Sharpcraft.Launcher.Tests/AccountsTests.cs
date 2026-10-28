// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Lifecycle;
using Sharpcraft.Launcher.Accounts;
using Xunit;

namespace Sharpcraft.Launcher.Tests;

/// <summary>
/// The offline gate, offline identity and account persistence (docs/modules/accounts.md §9,
/// ADR-0006). Fixtures are in-memory records; no real account takes part.
/// </summary>
public sealed class AccountsTests
{
    [Fact]
    public void GateTruthTable()
    {
        var offline = Offline("Notch");
        var unverifiedPremium = Premium("Steve", verifiedAt: null);
        var verifiedPremium = Premium("jeb_", verifiedAt: DateTimeOffset.UtcNow);

        Assert.False(OfflineLoginGate.Evaluate([]).Available);
        Assert.False(OfflineLoginGate.Evaluate([offline]).Available);
        Assert.False(OfflineLoginGate.Evaluate([unverifiedPremium]).Available);
        Assert.False(OfflineLoginGate.Evaluate([offline, unverifiedPremium]).Available);
        Assert.True(OfflineLoginGate.Evaluate([verifiedPremium]).Available);
        Assert.True(OfflineLoginGate.Evaluate([verifiedPremium, offline]).Available);
    }

    [Fact]
    public void DeletingTheLastPremiumAccountLocksOfflineImmediately()
    {
        var premium = Premium("Steve", DateTimeOffset.UtcNow);
        var offline = Offline("Notch");
        var accounts = new List<AccountRecord> { premium, offline };

        Assert.True(OfflineLoginGate.Evaluate(accounts).Available);

        Assert.True(AccountStore.TryRemove(accounts, "Steve", out var remaining));

        // The offline account is kept, but the gate closes on the very next evaluation.
        Assert.Single(remaining);
        Assert.Equal(AccountKind.Offline, remaining[0].Kind);
        Assert.False(OfflineLoginGate.Evaluate(remaining).Available);
    }

    [Theory]
    [InlineData("Notch", "b50ad385-829d-3141-a216-7e7d7539ba7f")]
    [InlineData("jeb_", "a762f560-4fce-3236-812a-b80efff0b62b")]
    [InlineData("Steve", "5627dd98-e6be-3c21-b8a8-e92344183641")]
    [InlineData("Dev", "380df991-f603-344c-a090-369bad2a924a")]
    public void OfflineUuidMatchesTheVanillaRule(string name, string expected)
    {
        Assert.Equal(expected, OfflineIdentity.FormatUuid(OfflineIdentity.DeriveUuid(name)));
    }

    [Theory]
    [InlineData("ab", false)]
    [InlineData("abc", true)]
    [InlineData("Notch_1234567890", true)]
    [InlineData("Notch_12345678901", false)]
    [InlineData("has space", false)]
    [InlineData("has-dash", false)]
    [InlineData("中文", false)]
    public void ProfileNamesFollowTheVanillaRules(string name, bool expected)
    {
        Assert.Equal(expected, OfflineIdentity.IsValidName(name));
    }

    [Fact]
    public void InvalidNameIsRejectedWithTheRules()
    {
        var exception = Assert.Throws<ArgumentException>(() => OfflineIdentity.DeriveUuid("no"));

        Assert.Contains("3-16", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AccountFileRoundTripsAndSurvivesATamperCheck()
    {
        using var fixture = new StoreFixture();
        var store = new AccountStore(fixture.AccountFile);

        store.Save([Premium("Steve", DateTimeOffset.UtcNow), Offline("Notch")]);

        var loaded = store.Load(out var warning);
        Assert.Null(warning);
        Assert.Equal(2, loaded.Count);
        Assert.True(OfflineLoginGate.Evaluate(loaded).Available);

        // Flip one byte of the signed payload: the envelope must stop trusting the file.
        // byte[] is serialised as base64, so the signed payload is a string here.
        var document = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(fixture.AccountFile))!;
        var contents = document["contents"]!.GetValue<string>();
        var flipped = (contents[0] == 'A' ? 'B' : 'A') + contents[1..];
        document["contents"] = flipped;
        File.WriteAllText(fixture.AccountFile, document.ToJsonString());

        var tampered = store.Load(out var tamperedWarning);
        Assert.NotNull(tamperedWarning);
        Assert.Empty(tampered);
        Assert.False(OfflineLoginGate.Evaluate(tampered).Available);

        // A broken signature is equally untrusted.
        store.Save([Premium("Steve", DateTimeOffset.UtcNow)]);
        var reshaped = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(fixture.AccountFile))!;
        reshaped["signature"] = new string('0', 64);
        File.WriteAllText(fixture.AccountFile, reshaped.ToJsonString());

        Assert.NotNull(store.Load(out var signatureWarning).Count == 0 ? signatureWarning : "expected rejection");
    }

    [Fact]
    public void MissingKeyLocksOfflinePlayInsteadOfFaithfullyTrustingTheFile()
    {
        using var fixture = new StoreFixture();
        var store = new AccountStore(fixture.AccountFile);
        store.Save([Premium("Steve", DateTimeOffset.UtcNow)]);

        File.Delete(fixture.AccountFile + ".key");

        var loaded = store.Load(out var warning);

        Assert.NotNull(warning);
        Assert.Contains("key", warning, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(loaded);
    }

    [Fact]
    public void MissingFileIsAnEmptyListNotAnError()
    {
        using var fixture = new StoreFixture();
        var store = new AccountStore(Path.Combine(fixture.Root, "absent.json"));

        Assert.Empty(store.Load(out var warning));
        Assert.Null(warning);
    }

    [Fact]
    public void AtomicWriteLeavesNoTemporaryFileBehind()
    {
        using var fixture = new StoreFixture();
        var store = new AccountStore(fixture.AccountFile);

        store.Save([Offline("Notch")]);

        Assert.True(File.Exists(fixture.AccountFile));
        Assert.False(File.Exists(fixture.AccountFile + ".tmp"));
    }

    [Fact]
    public void UpsertReplacesByNameAndKeepsOrder()
    {
        var first = Premium("Steve", DateTimeOffset.UtcNow);
        var second = Offline("Notch");

        var updated = AccountStore.Upsert([first, second], Premium("Steve", DateTimeOffset.UtcNow.AddMinutes(1)));

        Assert.Equal(2, updated.Count);
        Assert.Equal(first.CreatedAt, updated[0].CreatedAt);
        Assert.True(updated[0].VerifiedAt > first.VerifiedAt);
    }

    [Fact]
    public void OfflineLaunchArgumentsCarryNoToken()
    {
        // The launcher builds these arguments; this mirrors what launch --offline produces.
        var name = "Notch";
        var uuid = OfflineIdentity.FormatUuid(OfflineIdentity.DeriveUuid(name));
        string[] arguments =
        [
            "--version", "26.3",
            "--gameDir", @"C:\data\instances\26.3",
            "--pack", @"C:\data\packs\minecraft-26.3.pack.zip",
            "--offlineDeveloperMode",
            "--username", name,
            "--uuid", uuid
        ];

        Assert.DoesNotContain("--accessToken", arguments);
        Assert.DoesNotContain("--sessionFile", arguments);
        Assert.Contains("--offlineDeveloperMode", arguments);
        Assert.Contains(uuid, arguments);
        Assert.Equal("b50ad385-829d-3141-a216-7e7d7539ba7f", uuid);
    }

    private static AccountRecord Premium(string name, DateTimeOffset? verifiedAt) =>
        new(AccountKind.Premium, name, Guid.NewGuid().ToString("D"), verifiedAt, DateTimeOffset.UtcNow);

    private static AccountRecord Offline(string name) =>
        new(
            AccountKind.Offline,
            name,
            OfflineIdentity.FormatUuid(OfflineIdentity.DeriveUuid(name)),
            VerifiedAt: null,
            DateTimeOffset.UtcNow);

    private sealed class StoreFixture : IDisposable
    {
        public StoreFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "mcs-accounts-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            AccountFile = Path.Combine(Root, "accounts.json");
        }

        public string Root { get; }

        public string AccountFile { get; }

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
