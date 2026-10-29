// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Modding;

/// <summary>
/// What a mod may do through the host. Deliberately narrow: a mod that could reach game internals would
/// make the contract unable to evolve, and would also break unloadability by pinning host state
/// (docs/modules/modding.md 4).
/// </summary>
public interface IModContext
{
    /// <summary>Identifier of the mod currently being loaded.</summary>
    string ModId { get; }

    /// <summary>Game version the host belongs to, for compatibility checks.</summary>
    string GameVersion { get; }

    /// <summary>Contract version the host implements.</summary>
    int ContractVersion { get; }

    /// <summary>Writes a diagnostic line; the host owns the destination.</summary>
    void Log(string message);
}
