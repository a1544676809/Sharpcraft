// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Buffers.Binary;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Vanilla 26.3 <c>DefaultPlayerSkin</c>: 18 built-in skins (index 0..8 slim, 9..17 wide) selected by
/// <c>Math.floorMod(uuid.hashCode(), 18)</c>. See docs/modules/entity-equipment.md §12. All 18 paths are
/// precomputed so the per-frame texture selector stays allocation-free.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.resources.DefaultPlayerSkin", "ref://mc-26.3-analysis/src/net/minecraft/client/resources/DefaultPlayerSkin.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "18 UUID-indexed built-in skins and slim/wide model selection")]
public static class DefaultPlayerSkin
{
    /// <summary>A built-in default skin: its atlas path and whether it uses the 3px-arm model.</summary>
    [Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.entity.player.PlayerSkin", "ref://mc-26.3-analysis/src/net/minecraft/world/entity/player/PlayerSkin.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Projection of a built-in skin: body texture path plus model type")]
    public readonly record struct Skin(string Path, bool Slim);

    private const int Count = 18;

    private static readonly string[] Names =
        ["alex", "ari", "efe", "kai", "makena", "noor", "steve", "sunny", "zuri"];

    private static readonly Skin[] Skins = BuildSkins();

    /// <summary>All 18 built-in skin paths, registered into the entity skin atlas by the model definitions.</summary>
    public static IReadOnlyList<string> AllPaths { get; } =
        Array.AsReadOnly(Array.ConvertAll(Skins, static skin => skin.Path));

    /// <summary>The reference <c>getDefaultSkin()</c> entry: fixed index 6 = <c>entity/player/slim/steve</c>.</summary>
    public static Skin Default => Skins[6];

    /// <summary>Resolves the UUID-indexed built-in skin.</summary>
    public static Skin Get(Guid uuid) => Skins[IndexOf(uuid)];

    /// <summary>Java <c>Math.floorMod(uuid.hashCode(), 18)</c>, exposed for golden-vector assertions.</summary>
    public static int IndexOf(Guid uuid)
    {
        var index = JavaUuidHashCode(uuid) % Count;
        return index < 0 ? index + Count : index;
    }

    /// <summary>
    /// <c>java.util.UUID.hashCode()</c>: <c>hilo = mostSigBits ^ leastSigBits</c>, then
    /// <c>(int)(hilo &gt;&gt; 32) ^ (int)hilo</c>. The two longs are the canonical UUID string's big-endian
    /// halves; .NET <c>Guid.GetHashCode()</c> is unrelated and must not be used here.
    /// </summary>
    public static int JavaUuidHashCode(Guid uuid)
    {
        Span<byte> bytes = stackalloc byte[16];
        uuid.TryWriteBytes(bytes, bigEndian: true, out _);
        var most = BinaryPrimitives.ReadInt64BigEndian(bytes[..8]);
        var least = BinaryPrimitives.ReadInt64BigEndian(bytes[8..]);
        unchecked
        {
            var hilo = most ^ least;
            return (int)(hilo >> 32) ^ (int)hilo;
        }
    }

    private static Skin[] BuildSkins()
    {
        var skins = new Skin[Count];
        for (var index = 0; index < Count; index++)
        {
            var slim = index < Names.Length;
            skins[index] = new Skin(
                "entity/player/" + (slim ? "slim" : "wide") + "/" + Names[index % Names.Length],
                slim);
        }

        return skins;
    }
}
