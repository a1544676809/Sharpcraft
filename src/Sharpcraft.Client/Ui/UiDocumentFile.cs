// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Ui;

/// <summary>
/// Resolves the one place interface documents are allowed to come from (docs/modules/ui.md 8).
/// </summary>
/// <remarks>
/// There is deliberately no way to point the client at an arbitrary file. Loading a document from anywhere on
/// disk would let any process that can write that path replace the interface of a running game, which is a
/// phishing surface: a convincing "sign in again" panel is worth more to an attacker than any memory corruption.
/// Customisation therefore lives at a fixed location under the user data directory, and resolution refuses
/// anything that escapes it.
/// </remarks>
public static class UiDocumentFile
{
    /// <summary>Name of the document loaded at startup.</summary>
    public const string FileName = "main.xaml";

    /// <summary>Directory holding interface documents, relative to the user data root.</summary>
    public const string DirectoryName = "ui";

    /// <summary>Resolves the document path inside the given data root.</summary>
    /// <param name="dataRoot">User data directory.</param>
    public static string PathFor(string dataRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        return Path.Combine(dataRoot, DirectoryName, FileName);
    }

    /// <summary>
    /// Checks that a candidate path really lives inside the managed directory.
    /// </summary>
    /// <param name="dataRoot">User data directory.</param>
    /// <param name="candidate">Path to check.</param>
    /// <remarks>Used by tests and by any future caller that accepts a path from elsewhere; resolution never
    /// builds one outside the directory, so this is a guard rather than the only line of defence.</remarks>
    public static bool IsManaged(string dataRoot, string candidate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidate);

        var directory = Path.GetFullPath(Path.Combine(dataRoot, DirectoryName));
        var resolved = Path.GetFullPath(candidate);
        var prefix = directory + Path.DirectorySeparatorChar;

        return resolved.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Makes sure the document exists, writing the supplied starting point when it does not, and returns its path.
    /// </summary>
    /// <param name="dataRoot">User data directory.</param>
    /// <param name="starterDocument">Content used when no document exists yet.</param>
    public static string EnsureExists(string dataRoot, string starterDocument)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        ArgumentNullException.ThrowIfNull(starterDocument);

        var path = PathFor(dataRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        if (!File.Exists(path))
        {
            File.WriteAllText(path, starterDocument);
        }

        return path;
    }
}
