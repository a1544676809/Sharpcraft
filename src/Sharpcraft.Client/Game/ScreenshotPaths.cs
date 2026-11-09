// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;

namespace Sharpcraft.Client.Game;

/// <summary>
/// Builds screenshot file names the way the reference does.
/// </summary>
/// <remarks>
/// The reference keeps screenshots under <c>&lt;gameDir&gt;/screenshots</c>, names them from
/// <c>Util.getFilenameFormattedDateTime()</c> and appends <c>_2</c>, <c>_3</c>… when that name is taken
/// (<c>Screenshot.getFile</c>). The timestamp is local time with second precision.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.Screenshot", "ref://mc-26.3-analysis/src/net/minecraft/client/Screenshot.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "screenshots directory, timestamped file name and the _N collision suffix")]
public static class ScreenshotPaths
{
    /// <summary>Reference directory name under the game directory.</summary>
    public const string DirectoryName = "screenshots";

    /// <summary>Returns the first free path for a screenshot taken now.</summary>
    /// <param name="gameDirectory">The game directory the screenshots folder belongs to.</param>
    /// <param name="now">Local time to name the file with; defaults to <see cref="DateTime.Now"/>.</param>
    public static string Next(string gameDirectory, DateTime? now = null)
    {
        var directory = Path.Combine(gameDirectory, DirectoryName);
        var stamp = (now ?? DateTime.Now).ToString("yyyy-MM-dd_HH.mm.ss", CultureInfo.InvariantCulture);

        for (var attempt = 1; ; attempt++)
        {
            var suffix = attempt == 1 ? string.Empty : "_" + attempt.ToString(CultureInfo.InvariantCulture);
            var candidate = Path.Combine(directory, stamp + suffix + ".png");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }
}
