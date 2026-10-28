// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using System.Globalization;

namespace MinecraftCSharp.Launcher.Accounts;

/// <summary>How an account was obtained. The two kinds are never interchangeable.</summary>
public enum AccountKind
{
    /// <summary>Signed in through the official chain; the only kind that unlocks offline play.</summary>
    Premium,

    /// <summary>Local only: no token, no online services (AGENTS.md principle 7.1).</summary>
    Offline
}

/// <summary>One entry in the launcher's account list.</summary>
/// <param name="Kind">Premium or offline.</param>
/// <param name="Name">Profile name.</param>
/// <param name="Uuid">Profile UUID; for offline accounts this is derived, never random.</param>
/// <param name="VerifiedAt">Last successful official verification; null for offline accounts.</param>
/// <param name="CreatedAt">When the entry was added.</param>
public sealed record AccountRecord(
    AccountKind Kind,
    string Name,
    string Uuid,
    DateTimeOffset? VerifiedAt,
    DateTimeOffset CreatedAt)
{
    /// <summary>True when this entry can unlock offline play.</summary>
    public bool IsVerifiedPremium => Kind == AccountKind.Premium && VerifiedAt is not null;

    /// <summary>Never prints tokens (there are none) and keeps the identity readable.</summary>
    public override string ToString() =>
        string.Format(CultureInfo.InvariantCulture, "{0} {1} ({2})", Kind, Name, Uuid);
}

/// <summary>
/// The offline gate from AGENTS.md principle 7.1. It is a pure function over the current list on
/// purpose: a cached flag is the only way deleting the last premium account could fail to
/// invalidate offline play immediately (ADR-0006).
/// </summary>
public static class OfflineLoginGate
{
    /// <summary>Evaluates whether offline play is currently allowed.</summary>
    /// <param name="accounts">The account list as it is right now.</param>
    public static GateResult Evaluate(IReadOnlyList<AccountRecord> accounts)
    {
        ArgumentNullException.ThrowIfNull(accounts);

        var premium = accounts.FirstOrDefault(account => account.IsVerifiedPremium);

        return premium is null
            ? new GateResult(
                Available: false,
                "no verified premium account: sign in with a Microsoft account first (AGENTS.md principle 7.1)")
            : new GateResult(Available: true, $"premium account present: {premium.Name}");
    }
}

/// <summary>Outcome of the gate.</summary>
/// <param name="Available">Whether offline play may proceed.</param>
/// <param name="Reason">Explanation for logs and user-facing errors.</param>
public sealed record GateResult(bool Available, string Reason);
