// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Identifiers;

/// <summary>
/// Thrown when an <see cref="Identifier"/> violates the naming rules.
/// Mirrors the vanilla split between namespace and path diagnostics.
/// </summary>
public sealed class IdentifierException : Exception
{
    /// <summary>Creates the exception with a message.</summary>
    public IdentifierException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and inner exception.</summary>
    public IdentifierException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
