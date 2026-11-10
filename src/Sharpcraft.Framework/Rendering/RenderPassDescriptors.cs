// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Framework.Rendering;

/// <summary>What a pass does with an attachment at its start.</summary>
public enum LoadOp
{
    /// <summary>Keep what the attachment already holds.</summary>
    Load,

    /// <summary>Clear it to the given value.</summary>
    Clear
}

/// <summary>What a pass does with an attachment at its end.</summary>
public enum StoreOp
{
    /// <summary>Write the result back.</summary>
    Store,

    /// <summary>Leave it undefined; the contents are not read afterwards.</summary>
    Discard
}

/// <summary>One colour attachment of a pass.</summary>
/// <param name="View">View rendered into.</param>
/// <param name="Load">Whether to keep or clear the existing contents.</param>
/// <param name="Store">Whether to keep the result.</param>
/// <param name="ClearColour">Value written when <paramref name="Load"/> is <see cref="LoadOp.Clear"/>.</param>
public readonly record struct ColorAttachmentDesc(TextureViewHandle View, LoadOp Load, StoreOp Store, Vector4 ClearColour)
{
    /// <summary>A colour attachment that is cleared first and kept afterwards.</summary>
    /// <param name="view">View rendered into.</param>
    /// <param name="clear">Colour written before the pass.</param>
    public static ColorAttachmentDesc Clear(TextureViewHandle view, Vector4 clear) =>
        new(view, LoadOp.Clear, StoreOp.Store, clear);

    /// <summary>A colour attachment that keeps its existing contents.</summary>
    /// <param name="view">View rendered into.</param>
    public static ColorAttachmentDesc Keep(TextureViewHandle view) =>
        new(view, LoadOp.Load, StoreOp.Store, default);
}

/// <summary>The depth attachment of a pass.</summary>
/// <param name="View">Depth view rendered into.</param>
/// <param name="Load">Whether to keep or clear the existing contents.</param>
/// <param name="Store">Whether to keep the result.</param>
/// <param name="ClearDepth">Value written when <paramref name="Load"/> is <see cref="LoadOp.Clear"/>.</param>
public readonly record struct DepthAttachmentDesc(TextureViewHandle View, LoadOp Load, StoreOp Store, float ClearDepth)
{
    /// <summary>A depth attachment that is cleared first and kept afterwards.</summary>
    /// <param name="view">Depth view rendered into.</param>
    /// <param name="clear">Depth written before the pass.</param>
    public static DepthAttachmentDesc Clear(TextureViewHandle view, float clear) =>
        new(view, LoadOp.Clear, StoreOp.Store, clear);

    /// <summary>A depth attachment that keeps its existing contents.</summary>
    /// <param name="view">Depth view rendered into.</param>
    public static DepthAttachmentDesc Keep(TextureViewHandle view) =>
        new(view, LoadOp.Load, StoreOp.Store, 0f);
}

/// <summary>Format of an index buffer.</summary>
public enum IndexFormat
{
    /// <summary>Sixteen bits per index.</summary>
    Uint16,

    /// <summary>Thirty-two bits per index.</summary>
    Uint32
}
