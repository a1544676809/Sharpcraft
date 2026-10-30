// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Assets;

/// <summary>
/// Alpha content of a texture region, mirroring the reference's <c>Transparency</c> record.
/// </summary>
/// <param name="HasTransparent">A fully transparent pixel exists, so the texture is cut out.</param>
/// <param name="HasTranslucent">A partially transparent pixel exists, so the texture must be blended.</param>
/// <remarks>
/// The reference decides a block's chunk layer from this: a translucent texture goes to the translucent layer, a texture
/// with holes to the cutout layer, anything else to the solid layer
/// (ref://mc-26.3-analysis/src/net/minecraft/client/renderer/chunk/ChunkSectionLayer.java).
/// </remarks>
public readonly record struct Transparency(bool HasTransparent, bool HasTranslucent)
{
    /// <summary>Opaque texture: no transparent and no translucent pixels.</summary>
    public static Transparency None => default;

    /// <summary>Builds the record from the two flags, as the reference does.</summary>
    /// <param name="hasTransparent">Whether a fully transparent pixel exists.</param>
    /// <param name="hasTranslucent">Whether a partially transparent pixel exists.</param>
    public static Transparency Of(bool hasTransparent, bool hasTranslucent) =>
        new(hasTransparent, hasTranslucent);

    /// <summary>Combines two regions, as a model combines the textures of its faces.</summary>
    /// <param name="other">The other region.</param>
    public Transparency Or(Transparency other) =>
        new(HasTransparent || other.HasTransparent, HasTranslucent || other.HasTranslucent);

    /// <summary>True when the texture is fully opaque.</summary>
    public bool IsOpaque => !HasTransparent && !HasTranslucent;
}
