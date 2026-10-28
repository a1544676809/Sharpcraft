// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MinecraftCSharp.Launcher.Assets;

namespace MinecraftCSharp.Launcher.Accounts;

/// <summary>
/// Persists the account list outside the repository with an HMAC envelope. A failed check means the
/// list is untrusted, so offline play stays unavailable instead of being repaired (ADR-0006 §6).
/// </summary>
public sealed class AccountStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _keyPath;

    /// <summary>Creates a store for a specific file and key file.</summary>
    /// <param name="path">Account list path.</param>
    /// <param name="keyPath">HMAC key path; defaults to a sibling <c>.key</c> file.</param>
    public AccountStore(string path, string? keyPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        Path = path;
        _keyPath = keyPath ?? path + ".key";
    }

    /// <summary>Path of the account list.</summary>
    public string Path { get; }

    /// <summary>Default location under the user's local application data.</summary>
    public static string DefaultPath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MinecraftCSharp",
        "accounts.json");

    /// <summary>Creates a store at the default location.</summary>
    public static AccountStore CreateDefault() => new(DefaultPath);

    /// <summary>
    /// Loads the list. A missing file is an empty list; a tampered or unreadable file is also an
    /// empty list plus a warning, which keeps offline play locked.
    /// </summary>
    public IReadOnlyList<AccountRecord> Load(out string? warning)
    {
        warning = null;

        if (!File.Exists(Path))
        {
            return [];
        }

        Envelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<Envelope>(File.ReadAllText(Path), SerializerOptions);
        }
        catch (JsonException)
        {
            warning = $"account file is not valid JSON, ignoring it: {Path}";
            return [];
        }

        if (envelope?.Contents is null || envelope.Signature is null)
        {
            warning = $"account file is incomplete, ignoring it: {Path}";
            return [];
        }

        var key = ReadKey();
        if (key is null)
        {
            warning = $"account file integrity key is missing, ignoring it: {Path}";
            return [];
        }

        var expected = Sign(key, envelope.Contents);
        if (!CryptographicOperations.FixedTimeEquals(expected, HexToBytes(envelope.Signature)))
        {
            warning = $"account file failed its integrity check, ignoring it: {Path}";
            return [];
        }

        // The list is decoded from the signed bytes only, so there is no unsigned copy to edit.
        try
        {
            return JsonSerializer.Deserialize<List<AccountRecord>>(envelope.Contents, SerializerOptions) ?? [];
        }
        catch (JsonException)
        {
            warning = $"account file payload is not valid JSON, ignoring it: {Path}";
            return [];
        }
    }

    /// <summary>Writes the list atomically with a fresh envelope.</summary>
    public void Save(IReadOnlyList<AccountRecord> accounts)
    {
        ArgumentNullException.ThrowIfNull(accounts);

        if (HostPaths.IsInsideRepository(Path))
        {
            throw new InvalidOperationException(
                $"refusing to write accounts inside the repository: {Path} (AGENTS.md principle 3)");
        }

        var directory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var key = ReadKey() ?? CreateKey();
        var contents = JsonSerializer.SerializeToUtf8Bytes(accounts, SerializerOptions);
        var envelope = new Envelope(contents, Convert.ToHexStringLower(Sign(key, contents)));

        var temporary = Path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(envelope, SerializerOptions));
        File.Move(temporary, Path, overwrite: true);
    }

    /// <summary>Adds or updates an entry, keeping the list ordered by first appearance.</summary>
    public static IReadOnlyList<AccountRecord> Upsert(
        IReadOnlyList<AccountRecord> accounts,
        AccountRecord record)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(record);

        var result = accounts.ToList();
        var index = result.FindIndex(existing =>
            string.Equals(existing.Name, record.Name, StringComparison.OrdinalIgnoreCase));

        if (index >= 0)
        {
            // Replacing keeps the original creation time: it is the same account, re-verified.
            result[index] = record with { CreatedAt = result[index].CreatedAt };
        }
        else
        {
            result.Add(record);
        }

        return result;
    }

    /// <summary>Removes an entry by name; returns false when it was not present.</summary>
    public static bool TryRemove(IReadOnlyList<AccountRecord> accounts, string name, out IReadOnlyList<AccountRecord> result)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var remaining = accounts
            .Where(account => !string.Equals(account.Name, name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        result = remaining;
        return remaining.Count != accounts.Count;
    }

    private byte[]? ReadKey()
    {
        if (!File.Exists(_keyPath))
        {
            return null;
        }

        try
        {
            var key = File.ReadAllBytes(_keyPath);
            return key.Length >= 16 ? key : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private byte[] CreateKey()
    {
        var directory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(_keyPath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var key = RandomNumberGenerator.GetBytes(32);
        File.WriteAllBytes(_keyPath, key);
        RestrictPermissions(_keyPath);
        return key;
    }

    private static byte[] Sign(byte[] key, byte[] contents) => HMACSHA256.HashData(key, contents);

    private static byte[] HexToBytes(string hex)
    {
        try
        {
            return Convert.FromHexString(hex);
        }
        catch (FormatException)
        {
            return [];
        }
    }

    private static void RestrictPermissions(string path)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }
        catch (IOException)
        {
            // Best effort: the ADR records this as local policy, not a security boundary.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed record Envelope(byte[]? Contents, string? Signature);
}
