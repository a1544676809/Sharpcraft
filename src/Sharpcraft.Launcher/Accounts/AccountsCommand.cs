// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using Sharpcraft.Common.Lifecycle;

namespace Sharpcraft.Launcher.Accounts;

/// <summary>
/// The <c>accounts</c> command family: list, add an offline account, remove one. The gate is
/// evaluated on every call from the current list, never from a stored flag
/// (docs/modules/accounts.md §4, ADR-0006).
/// </summary>
public static class AccountsCommand
{
    /// <summary>Exit code when the offline gate refuses.</summary>
    public const int GateRefused = 4;

    /// <summary>Exit code for usage problems.</summary>
    public const int BadUsage = 2;

    /// <summary>Runs the command family.</summary>
    /// <param name="target">Subcommand plus its argument, as produced by the command line parser.</param>
    public static int Run(string? target)
    {
        var store = AccountStore.CreateDefault();
        var accounts = store.Load(out var warning);

        if (warning is not null)
        {
            Console.Error.WriteLine($"[accounts] {warning}");
        }

        var parts = (target ?? string.Empty).Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
        {
            return List(accounts);
        }

        return parts[0] switch
        {
            "add-offline" => AddOffline(store, accounts, parts.ElementAtOrDefault(1)),
            "remove" => Remove(store, accounts, parts.ElementAtOrDefault(1)),
            _ => Unknown(parts[0])
        };
    }

    /// <summary>Registers a premium account after a successful official sign-in.</summary>
    /// <param name="name">Profile name.</param>
    /// <param name="uuid">Profile UUID.</param>
    public static void RecordPremiumSignIn(string name, string uuid)
    {
        var store = AccountStore.CreateDefault();
        var accounts = store.Load(out _);

        var record = new AccountRecord(
            AccountKind.Premium,
            name,
            uuid,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        store.Save(AccountStore.Upsert(accounts, record));
        Console.WriteLine($"[accounts] recorded premium account {name}");
    }

    /// <summary>Builds the offline launch arguments for a name, enforcing the gate.</summary>
    /// <exception cref="InvalidOperationException">The gate refuses or the name is invalid.</exception>
    public static (string Name, string Uuid) ResolveOfflineAccount(string name)
    {
        if (!OfflineIdentity.IsValidName(name))
        {
            throw new InvalidOperationException(
                $"invalid profile name '{name}': {OfflineIdentity.MinNameLength}-{OfflineIdentity.MaxNameLength} " +
                "characters, letters, digits and underscores only");
        }

        var store = AccountStore.CreateDefault();
        var accounts = store.Load(out _);
        var gate = OfflineLoginGate.Evaluate(accounts);

        if (!gate.Available)
        {
            throw new InvalidOperationException(
                $"{gate.Reason}. Offline accounts are kept, but starting the game requires signing in " +
                "with a premium account first.");
        }

        var existing = accounts.FirstOrDefault(account =>
            account.Kind == AccountKind.Offline &&
            string.Equals(account.Name, name, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            return (existing.Name, existing.Uuid);
        }

        var uuid = OfflineIdentity.FormatUuid(OfflineIdentity.DeriveUuid(name));
        var record = new AccountRecord(AccountKind.Offline, name, uuid, null, DateTimeOffset.UtcNow);
        store.Save(AccountStore.Upsert(accounts, record));
        return (name, uuid);
    }

    private static int List(IReadOnlyList<AccountRecord> accounts)
    {
        var gate = OfflineLoginGate.Evaluate(accounts);

        if (accounts.Count == 0)
        {
            Console.WriteLine("no accounts recorded yet");
        }
        else
        {
            foreach (var account in accounts)
            {
                Console.WriteLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0,-8} {1,-18} {2}  verified {3}",
                    account.Kind,
                    account.Name,
                    account.Uuid,
                    account.VerifiedAt?.ToString("u", CultureInfo.InvariantCulture) ?? "-"));
            }
        }

        Console.WriteLine($"offline login: {(gate.Available ? "available" : "locked")} — {gate.Reason}");
        return 0;
    }

    private static int AddOffline(AccountStore store, IReadOnlyList<AccountRecord> accounts, string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            Console.Error.WriteLine("usage: accounts add-offline <name>");
            return BadUsage;
        }

        if (!OfflineIdentity.IsValidName(name))
        {
            Console.Error.WriteLine(
                $"invalid profile name '{name}': {OfflineIdentity.MinNameLength}-{OfflineIdentity.MaxNameLength} " +
                "characters, letters, digits and underscores only");
            return BadUsage;
        }

        var gate = OfflineLoginGate.Evaluate(accounts);
        if (!gate.Available)
        {
            Console.Error.WriteLine($"[accounts] refused: {gate.Reason}");
            return GateRefused;
        }

        var uuid = OfflineIdentity.FormatUuid(OfflineIdentity.DeriveUuid(name));
        var record = new AccountRecord(AccountKind.Offline, name, uuid, null, DateTimeOffset.UtcNow);
        store.Save(AccountStore.Upsert(accounts, record));

        Console.WriteLine($"[accounts] added offline account {name} ({uuid})");
        return 0;
    }

    private static int Remove(AccountStore store, IReadOnlyList<AccountRecord> accounts, string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            Console.Error.WriteLine("usage: accounts remove <name>");
            return BadUsage;
        }

        if (!AccountStore.TryRemove(accounts, name, out var remaining))
        {
            Console.Error.WriteLine($"[accounts] no account named '{name}'");
            return BadUsage;
        }

        store.Save(remaining);

        // Re-evaluate immediately: the gate must not survive a deletion.
        var gate = OfflineLoginGate.Evaluate(remaining);
        Console.WriteLine($"[accounts] removed {name}; offline login: {(gate.Available ? "available" : "locked")}");

        if (!gate.Available)
        {
            Console.WriteLine("[accounts] offline accounts are kept but cannot start the game until a " +
                              "premium account signs in again");
        }

        return 0;
    }

    private static int Unknown(string subcommand)
    {
        Console.Error.WriteLine($"unknown accounts subcommand '{subcommand}'");
        Console.Error.WriteLine("usage: accounts [add-offline <name> | remove <name>]");
        return BadUsage;
    }
}
