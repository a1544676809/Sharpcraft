// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;

namespace Sharpcraft.Client.Render;

/// <summary>Read-only sprite lookup shared by entity and item atlases.</summary>
[Sharpcraft.Common.Reference.ReferenceClass("sharpcraft.ISpriteAtlas", "ref://mc-26.3-analysis/src", Kind = Sharpcraft.Common.Reference.ReferenceKind.New, Status = Sharpcraft.Common.Reference.ReferenceStatus.Full, Note = "Sharpcraft render abstraction; no reference counterpart")]
public interface ISpriteAtlas
{
    /// <summary>Sprite rect for a texture path, or null when the atlas does not contain it.</summary>
    SpriteRect? Get(string? texturePath);
}
