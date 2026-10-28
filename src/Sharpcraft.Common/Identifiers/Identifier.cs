// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Identifiers;

/// <summary>
/// Namespaced identifier in the form <c>namespace:path</c>, matching Minecraft 26.3
/// semantics (see docs/analysis/common/identifier.md).
/// </summary>
/// <remarks>
/// Behavioural notes that are easy to get wrong:
/// <list type="bullet">
///   <item>Ordering compares <see cref="Path"/> first, then <see cref="Namespace"/>.</item>
///   <item>Empty path and empty namespace are both accepted; there is no length limit.</item>
///   <item>The namespace may not be the literal <c>..</c> (path-traversal guard).</item>
///   <item><c>default(Identifier)</c> is not a valid identifier; its members read as empty strings.</item>
/// </list>
/// </remarks>
public readonly struct Identifier : IEquatable<Identifier>, IComparable<Identifier>
{
    /// <summary>Separator between namespace and path.</summary>
    public const char NamespaceSeparator = ':';

    /// <summary>Namespace used when the input has none.</summary>
    public const string DefaultNamespace = "minecraft";

    /// <summary>Namespace reserved for Realms content.</summary>
    public const string RealmsNamespace = "realms";

    private readonly string? _namespace;
    private readonly string? _path;

    /// <summary>Creates an identifier from an already validated pair.</summary>
    /// <exception cref="IdentifierException">Either part contains a forbidden character.</exception>
    public Identifier(string @namespace, string path)
    {
        ArgumentNullException.ThrowIfNull(@namespace);
        ArgumentNullException.ThrowIfNull(path);

        _namespace = IsValidNamespace(@namespace.AsSpan())
            ? @namespace
            : throw new IdentifierException($"Non [a-z0-9_.-] character in namespace of identifier: {@namespace}:{path}");

        _path = IsValidPath(path.AsSpan())
            ? path
            : throw new IdentifierException($"Non [a-z0-9/._-] character in path of location: {@namespace}:{path}");
    }

    /// <summary>Namespace part; empty for <c>default(Identifier)</c>.</summary>
    public string Namespace => _namespace ?? string.Empty;

    /// <summary>Path part; empty for <c>default(Identifier)</c>.</summary>
    public string Path => _path ?? string.Empty;

    /// <summary>True when this value is <c>default(Identifier)</c> and therefore not a real identifier.</summary>
    public bool IsDefault => _namespace is null && _path is null;

    /// <summary>Parses <c>namespace:path</c>, <c>path</c> or <c>:path</c>.</summary>
    /// <exception cref="IdentifierException">The input is not a valid identifier.</exception>
    public static Identifier Parse(string identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        return Parse(identifier.AsSpan());
    }

    /// <summary>Parses an identifier without allocating for the namespace/path split.</summary>
    /// <exception cref="IdentifierException">The input is not a valid identifier.</exception>
    public static Identifier Parse(ReadOnlySpan<char> identifier)
    {
        if (!TryParse(identifier, out var result))
        {
            throw new IdentifierException($"Not a valid resource location: {identifier}");
        }

        return result;
    }

    /// <summary>Attempts to parse an identifier without throwing.</summary>
    public static bool TryParse(string? identifier, out Identifier result)
    {
        if (identifier is null)
        {
            result = default;
            return false;
        }

        return TryParse(identifier.AsSpan(), out result);
    }

    /// <summary>Attempts to parse an identifier without throwing.</summary>
    public static bool TryParse(ReadOnlySpan<char> identifier, out Identifier result)
    {
        var separator = identifier.IndexOf(NamespaceSeparator);
        if (separator < 0)
        {
            return TryWithDefaultNamespace(identifier, out result);
        }

        var path = identifier[(separator + 1)..];
        if (separator == 0)
        {
            return TryWithDefaultNamespace(path, out result);
        }

        var @namespace = identifier[..separator];
        if (!IsValidNamespace(@namespace) || !IsValidPath(path))
        {
            result = default;
            return false;
        }

        result = new Identifier(@namespace.ToString(), path.ToString());
        return true;
    }

    /// <summary>Builds an identifier in the default namespace.</summary>
    /// <exception cref="IdentifierException">The path contains a forbidden character.</exception>
    public static Identifier WithDefaultNamespace(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return IsValidPath(path.AsSpan())
            ? new Identifier(DefaultNamespace, path)
            : throw new IdentifierException($"Non [a-z0-9/._-] character in path of location: {DefaultNamespace}:{path}");
    }

    /// <summary>Builds an identifier from an explicit namespace and path.</summary>
    /// <exception cref="IdentifierException">Either part contains a forbidden character.</exception>
    public static Identifier FromNamespaceAndPath(string @namespace, string path) => new(@namespace, path);

    /// <summary>Replaces the path, keeping the namespace.</summary>
    /// <exception cref="IdentifierException">The new path contains a forbidden character.</exception>
    public Identifier WithPath(string newPath) => new(Namespace, newPath);

    /// <summary>Prefixes the path.</summary>
    /// <exception cref="IdentifierException">The resulting path contains a forbidden character.</exception>
    public Identifier WithPrefix(string prefix) => new(Namespace, prefix + Path);

    /// <summary>Suffixes the path.</summary>
    /// <exception cref="IdentifierException">The resulting path contains a forbidden character.</exception>
    public Identifier WithSuffix(string suffix) => new(Namespace, Path + suffix);

    /// <summary>True when the value is a legal namespace (empty allowed, literal <c>..</c> rejected).</summary>
    public static bool IsValidNamespace(string? value) => value is not null && IsValidNamespace(value.AsSpan());

    /// <summary>True when the value is a legal path (empty allowed, no length limit).</summary>
    public static bool IsValidPath(string? value) => value is not null && IsValidPath(value.AsSpan());

    /// <summary>Ordinal comparison: path first, then namespace (vanilla ordering).</summary>
    public int CompareTo(Identifier other)
    {
        var byPath = string.CompareOrdinal(Path, other.Path);
        return byPath != 0 ? byPath : string.CompareOrdinal(Namespace, other.Namespace);
    }

    /// <inheritdoc />
    public bool Equals(Identifier other) =>
        string.Equals(Namespace, other.Namespace, StringComparison.Ordinal) &&
        string.Equals(Path, other.Path, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Identifier other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(_namespace, _path);

    /// <inheritdoc />
    public override string ToString() => string.Concat(Namespace, ":", Path);

    /// <summary>Form used for debug file names: separators replaced with underscores.</summary>
    public string ToDebugFileName() => ToString().Replace('/', '_').Replace(':', '_');

    /// <summary>Translation key form: <c>namespace.path</c>.</summary>
    public string ToLanguageKey() => string.Concat(Namespace, ".", Path);

    /// <summary>Translation key with the default namespace omitted.</summary>
    public string ToShortLanguageKey() =>
        string.Equals(Namespace, DefaultNamespace, StringComparison.Ordinal) ? Path : ToLanguageKey();

    /// <summary>String form with the default namespace omitted.</summary>
    public string ToShortString() =>
        string.Equals(Namespace, DefaultNamespace, StringComparison.Ordinal) ? Path : ToString();

    /// <summary>
    /// Resolves this identifier to a file path under <paramref name="rootDirectory"/>,
    /// rejecting any result that escapes the root.
    /// </summary>
    /// <exception cref="InvalidOperationException">The resolved path escapes the root.</exception>
    public string ResolveAgainst(string rootDirectory)
    {
        ArgumentNullException.ThrowIfNull(rootDirectory);

        var root = System.IO.Path.GetFullPath(rootDirectory);
        var combined = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, Namespace, Path));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var rootPrefix = root.EndsWith(System.IO.Path.DirectorySeparatorChar)
            ? root
            : root + System.IO.Path.DirectorySeparatorChar;

        if (!combined.StartsWith(rootPrefix, comparison))
        {
            throw new InvalidOperationException(
                $"Identifier \"{this}\" tried to access path \"{combined}\" from root \"{root}\"");
        }

        return combined;
    }

    /// <summary>Equality operator.</summary>
    public static bool operator ==(Identifier left, Identifier right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(Identifier left, Identifier right) => !left.Equals(right);

    /// <summary>Less-than operator, using vanilla ordering.</summary>
    public static bool operator <(Identifier left, Identifier right) => left.CompareTo(right) < 0;

    /// <summary>Greater-than operator, using vanilla ordering.</summary>
    public static bool operator >(Identifier left, Identifier right) => left.CompareTo(right) > 0;

    /// <summary>Less-than-or-equal operator, using vanilla ordering.</summary>
    public static bool operator <=(Identifier left, Identifier right) => left.CompareTo(right) <= 0;

    /// <summary>Greater-than-or-equal operator, using vanilla ordering.</summary>
    public static bool operator >=(Identifier left, Identifier right) => left.CompareTo(right) >= 0;

    private static bool TryWithDefaultNamespace(ReadOnlySpan<char> path, out Identifier result)
    {
        if (!IsValidPath(path))
        {
            result = default;
            return false;
        }

        result = new Identifier(DefaultNamespace, path.ToString());
        return true;
    }

    private static bool IsValidNamespace(ReadOnlySpan<char> value)
    {
        if (value.SequenceEqual(".."))
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!IsValidNamespaceCharacter(character))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidPath(ReadOnlySpan<char> value)
    {
        foreach (var character in value)
        {
            if (!IsValidPathCharacter(character))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidNamespaceCharacter(char character) =>
        character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-' or '.';

    private static bool IsValidPathCharacter(char character) =>
        character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-' or '.' or '/';
}
