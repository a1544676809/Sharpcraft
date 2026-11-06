// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Reflection;
using System.Text.RegularExpressions;
using Sharpcraft.Common.Network;

namespace Sharpcraft.Architecture.Tests;

/// <summary>
/// Play-clientbound dispatch coverage gate (T-666). Guards the "parsed but never consumed" bug class where a packet
/// constant is referenced in <c>PlaySession</c> yet silently falls through the default branch of the live consumer
/// (<c>InteractiveSession</c> / <c>LivePlaySession</c>).
/// </summary>
/// <remarks>
/// Packet names come from reflection over <see cref="PacketIds.PlayClientbound"/> so a newly added constant fails the
/// gate until it is wired up or explicitly allowlisted. Source references are counted with a single regex scan per file.
/// </remarks>
public sealed class ProtocolDispatchGateTests
{
    private static readonly Lazy<SourceSnapshot> Sources = new(LoadSources, isThreadSafe: true);
    private static readonly Lazy<HashSet<string>> Constants = new(LoadConstants, isThreadSafe: true);

    /// <summary>
    /// Play clientbound packets that intentionally have no client-side consumer. Every entry needs a reason.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> IntentionallyUnhandled = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Transfer"] = "id 132; the transfer packet redirects the client to another server and no client-side handler consumes it."
    };

    /// <summary>
    /// Packets decoded/handled by <see cref="PlaySession"/> that belong to preload or transport ownership, so they are
    /// not expected to reach <c>InteractiveSession</c> or <c>LivePlaySession</c>.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> PlaySessionOwned = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["KeepAlive"] = "transport liveness; answered in PlaySession before any live dispatch.",
        ["Ping"] = "transport/keep-alive response owned by PlaySession.",
        ["ChunkBatchFinished"] = "chunk-batch flow control owned by the preload path.",
        ["Login"] = "join preload trigger; hands off to the live session afterwards.",
        ["StartConfiguration"] = "configuration transition handled by PlaySession."
    };

    [Fact]
    public void EveryPlayClientboundConstantIsAccountedFor()
    {
        var constants = Constants.Value;
        var sources = Sources.Value;
        Assert.NotEmpty(constants);

        var referenced = new HashSet<string>(sources.PlaySessionRefs, StringComparer.Ordinal);
        referenced.UnionWith(sources.InteractiveSessionRefs);
        referenced.UnionWith(sources.LivePlaySessionRefs);

        var missing = constants
            .Where(name => !referenced.Contains(name) && !IntentionallyUnhandled.ContainsKey(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            missing.Length == 0,
            "Play clientbound constants are neither referenced nor allowlisted (missing from PlaySession.cs, "
            + "InteractiveSession.cs and LivePlaySession.cs):\n  " + string.Join("\n  ", missing));
    }

    [Fact]
    public void ParsedPlayPacketsReachALiveConsumer()
    {
        var sources = Sources.Value;

        var liveConsumerRefs = new HashSet<string>(sources.InteractiveSessionRefs, StringComparer.Ordinal);
        liveConsumerRefs.UnionWith(sources.LivePlaySessionRefs);

        var offenders = sources.PlaySessionRefs
            .Where(name => !liveConsumerRefs.Contains(name) && !PlaySessionOwned.ContainsKey(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "Packets referenced in PlaySession.cs but not consumed in InteractiveSession.cs or LivePlaySession.cs, and "
            + "not in the PlaySessionOwned allowlist:\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void AllowlistsOnlyReferenceKnownConstants()
    {
        var constants = Constants.Value;

        var unknown = IntentionallyUnhandled.Keys
            .Concat(PlaySessionOwned.Keys)
            .Where(name => !constants.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            unknown.Length == 0,
            "Allowlisted names that are not PacketIds.PlayClientbound constants:\n  " + string.Join("\n  ", unknown));
    }

    private static HashSet<string> LoadConstants()
    {
        var fields = typeof(PacketIds.PlayClientbound)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(int));
        return new HashSet<string>(fields.Select(field => field.Name), StringComparer.Ordinal);
    }

    private static SourceSnapshot LoadSources()
    {
        var root = RepositoryLocator.Root;
        var playSession = File.ReadAllText(Path.Combine(root, "src", "Sharpcraft.Common", "Network", "PlaySession.cs"));
        var interactive = File.ReadAllText(Path.Combine(root, "src", "Sharpcraft.Client", "Game", "InteractiveSession.cs"));
        var live = File.ReadAllText(Path.Combine(root, "src", "Sharpcraft.Client", "Game", "LivePlaySession.cs"));

        return new SourceSnapshot(
            CollectReferences(playSession),
            CollectReferences(interactive),
            CollectReferences(live));
    }

    /// <summary>Collects every <c>PlayClientbound.NAME</c> reference in one source file (comments included).</summary>
    private static HashSet<string> CollectReferences(string source)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(source, @"\bPlayClientbound\.([A-Za-z_][A-Za-z0-9_]*)"))
        {
            names.Add(match.Groups[1].Value);
        }

        return names;
    }

    private sealed record SourceSnapshot(
        HashSet<string> PlaySessionRefs,
        HashSet<string> InteractiveSessionRefs,
        HashSet<string> LivePlaySessionRefs);
}
