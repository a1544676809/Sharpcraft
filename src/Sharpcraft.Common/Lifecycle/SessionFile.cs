// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sharpcraft.Common.Lifecycle;

/// <summary>
/// The session handoff between launcher and client. Tokens live here rather than on the command
/// line, which is visible to other users on the machine (AGENTS.md principle 8).
/// </summary>
/// <param name="VersionId">Version id the session belongs to.</param>
/// <param name="Username">Profile name.</param>
/// <param name="Uuid">Profile UUID.</param>
/// <param name="AccessToken">Minecraft access token.</param>
/// <param name="CreatedUtc">Creation timestamp.</param>
public sealed record SessionFile(
    string VersionId,
    string Username,
    string Uuid,
    string AccessToken,
    DateTimeOffset CreatedUtc)
{
    /// <summary>Default file name inside the instance directory.</summary>
    public const string FileName = "session.json";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    /// <summary>Writes the session file atomically, replacing any previous one.</summary>
    public void WriteTo(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, SerializerOptions));
        File.Move(temporary, path, overwrite: true);
    }

    /// <summary>Reads a session file.</summary>
    /// <exception cref="InvalidDataException">The file is missing or not a usable session.</exception>
    public static SessionFile ReadFrom(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new InvalidDataException($"session file not found: {path}");
        }

        SessionFile? session;
        try
        {
            session = JsonSerializer.Deserialize<SessionFile>(File.ReadAllText(path), SerializerOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"session file is not valid JSON: {exception.Message}", exception);
        }

        if (session is null || string.IsNullOrWhiteSpace(session.Username))
        {
            throw new InvalidDataException($"session file has no profile: {path}");
        }

        return session;
    }

    /// <summary>Never prints the token.</summary>
    public override string ToString() => $"session {Username} ({Uuid}) for {VersionId}";
}
