// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Nbt;
using Sharpcraft.Common.World;

namespace Sharpcraft.Client.World;


/// <summary>One face of a sign.</summary>
/// <param name="Messages">Four lines, top to bottom.</param>
/// <param name="Colour">Dye colour name, black when the tag omits it.</param>
/// <param name="Glowing">Whether the text glows.</param>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.level.block.entity.SignText", "ref://mc-26.3-analysis/src/net/minecraft/world/level/block/entity/SignText.java")]
public readonly record struct SignTextData(IReadOnlyList<string> Messages, string Colour, bool Glowing);

/// <summary>Everything a sign renderer needs from the block entity.</summary>
/// <param name="Front">Front face.</param>
/// <param name="Back">Back face.</param>
/// <param name="Waxed">Whether the sign has been waxed.</param>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.level.block.entity.SignBlockEntity", "ref://mc-26.3-analysis/src/net/minecraft/world/level/block/entity/SignBlockEntity.java")]
public readonly record struct SignData(SignTextData Front, SignTextData Back, bool Waxed);

/// <summary>
/// Typed reads of block entity NBT, one family at a time. The transport already parses the tag into
/// <see cref="BlockEntityRecord.Tag"/>; this is the part that knows which fields each family has
/// (docs/specs/world/block-entity-data.md).
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.level.block.entity.SignBlockEntity", "ref://mc-26.3-analysis/src/net/minecraft/world/level/block/entity/SignBlockEntity.java")]
public static class BlockEntityData
{
    /// <summary>Lines whose component was richer than plain text and could not be read.</summary>
    public static int UnsupportedComponentCount { get; private set; }

    /// <summary>Reads a sign's front and back text.</summary>
    /// <param name="record">Block entity as it arrived with the chunk.</param>
    /// <param name="data">The sign data when the record carries a front face.</param>
    /// <returns>Whether the record looks like a sign.</returns>
    public static bool TryReadSign(BlockEntityRecord record, out SignData data)
    {
        data = default;

        if (record.Tag is not { } tag || !TryReadSignText(tag.GetCompound("front_text"), out var front))
        {
            return false;
        }

        // A sign always carries both faces; a missing back face is read as blank rather than failing.
        TryReadSignText(tag.GetCompound("back_text"), out var back);
        data = new SignData(front, back, tag.GetBool("is_waxed"));
        return true;
    }

    private static bool TryReadSignText(NbtCompound? text, out SignTextData data)
    {
        data = default;

        if (text is null)
        {
            return false;
        }

        var messages = new string[4];
        Array.Fill(messages, string.Empty);
        if (text.GetList("messages") is { } list)
        {
            for (var i = 0; i < messages.Length && i < list.Items.Count; i++)
            {
                messages[i] = list.Items[i] switch
                {
                    NbtString plain => plain.Value,
                    _ => Unsupported()
                };
            }
        }

        data = new SignTextData(messages, text.GetString("color") ?? "black", text.GetBool("has_glowing_text"));
        return true;
    }

    private static string Unsupported()
    {
        UnsupportedComponentCount++;
        return string.Empty;
    }
}
