// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Text.Json;

namespace Sharpcraft.Auth;

/// <summary>
/// Development token cache. Tokens are sensitive: this store keeps them on the local machine
/// only and refuses any location inside a repository working tree
/// (docs/modules/auth.md 5).
/// </summary>
public sealed class TokenStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General) { WriteIndented = true };

    private readonly string _path;

    /// <summary>Creates a store at an explicit path.</summary>
    public TokenStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = System.IO.Path.GetFullPath(path);
    }

    /// <summary>Location on disk. Named FilePath so it cannot shadow <see cref="System.IO.Path"/>.</summary>
    public string FilePath => _path;

    /// <summary>Default location: the user's local application data, outside any checkout.</summary>
    public static string DefaultPath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Sharpcraft",
        "tokens.json");

    /// <summary>Creates the default store after verifying the location is outside a repository.</summary>
    public static TokenStore CreateDefault() => new(DefaultPath);

    /// <summary>True when the store file exists.</summary>
    public bool Exists => File.Exists(_path);

    /// <summary>Verifies that a directory is not inside a git working tree.</summary>
    /// <exception cref="InvalidOperationException">The path is inside a repository.</exception>
    public static void EnsureOutsideRepository(string path)
    {
        var directory = Directory.Exists(path) ? path : System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path));
        while (!string.IsNullOrEmpty(directory))
        {
            if (Directory.Exists(System.IO.Path.Combine(directory, ".git")) ||
                File.Exists(System.IO.Path.Combine(directory, ".git")))
            {
                throw new InvalidOperationException(
                    $"refusing to store tokens inside a repository working tree: {directory}");
            }

            directory = System.IO.Path.GetDirectoryName(directory);
        }
    }

    /// <summary>Saves tokens, creating the directory when needed.</summary>
    /// <param name="token">Microsoft token to cache.</param>
    /// <param name="scope">Scope the refresh token was issued for.</param>
    /// <param name="clientId">Client id that owns the refresh token.</param>
    public void Save(MicrosoftToken token, string scope, string clientId)
    {
        ArgumentNullException.ThrowIfNull(token);
        EnsureOutsideRepository(_path);

        var directory = System.IO.Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
            RestrictToCurrentUser(directory);
        }

        var payload = new StoredTokens(clientId, scope, token.AccessToken, token.RefreshToken, token.ExpiresAt);
        File.WriteAllText(_path, JsonSerializer.Serialize(payload, JsonOptions));
        RestrictToCurrentUser(_path);
    }

    /// <summary>
    /// Stores the Minecraft session and profile alongside the existing Microsoft token, keeping one
    /// file so a partial cache can never hold a session without the grant that produced it.
    /// </summary>
    /// <param name="session">Minecraft session to cache.</param>
    /// <param name="profile">Profile the session belongs to.</param>
    public void SaveSession(MinecraftSession session, MinecraftProfile profile)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(profile);

        var existing = Load();
        if (existing is null)
        {
            // Without a Microsoft grant the session alone is not enough to renew anything, so it is
            // not written on its own.
            return;
        }

        var payload = existing with
        {
            MinecraftAccessToken = session.AccessToken,
            MinecraftExpiresAt = session.ExpiresAt,
            ProfileId = profile.Id,
            ProfileName = profile.Name
        };

        EnsureOutsideRepository(_path);
        File.WriteAllText(_path, JsonSerializer.Serialize(payload, JsonOptions));
        RestrictToCurrentUser(_path);
    }

    /// <summary>Loads tokens, or null when absent or unreadable.</summary>
    public StoredTokens? Load()
    {
        if (!Exists)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<StoredTokens>(File.ReadAllText(_path), JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>Deletes the cache; returns true when a file was removed.</summary>
    public bool Delete() => File.Exists(_path) && RemoveFile(_path);

    private static bool RemoveFile(string path)
    {
        File.Delete(path);
        return true;
    }

    private static void RestrictToCurrentUser(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            // The file inherits the user profile ACL, which is already user-only by default.
            return;
        }

        try
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | (Directory.Exists(path) ? UnixFileMode.UserExecute : 0));
        }
        catch (IOException)
        {
            // Best effort: the store stays functional even if the mode could not be tightened.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Cached token material as written to disk.</summary>
    /// <param name="ClientId">Client id that issued the refresh token.</param>
    /// <param name="Scope">Requested scope.</param>
    /// <param name="AccessToken">Cached access token.</param>
    /// <param name="RefreshToken">Refresh token; sensitive.</param>
    /// <param name="ExpiresAt">Access token expiry.</param>
    /// <param name="MinecraftAccessToken">Minecraft session token; the only credential that can be
    /// renewed without Xbox Live (docs/modules/auth.md 5.1).</param>
    /// <param name="MinecraftExpiresAt">Minecraft session expiry.</param>
    /// <param name="ProfileId">Profile id of the signed-in account, without dashes.</param>
    /// <param name="ProfileName">Profile name of the signed-in account.</param>
    public sealed record StoredTokens(
        string ClientId,
        string Scope,
        string AccessToken,
        string? RefreshToken,
        DateTimeOffset ExpiresAt,
        string? MinecraftAccessToken = null,
        DateTimeOffset? MinecraftExpiresAt = null,
        string? ProfileId = null,
        string? ProfileName = null)
    {
        /// <inheritdoc />
        public override string ToString() =>
            $"StoredTokens(expires={ExpiresAt:u}, minecraft={(MinecraftAccessToken is null ? "none" : "cached")})";
    }
}
