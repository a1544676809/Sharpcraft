// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering;

/// <summary>Which reference render type an entity layer corresponds to (docs/decisions/0021).</summary>
public enum EntityGeometryLayerKind
{
    /// <summary>Alpha tested, no blending, depth writes on (base model, clothing).</summary>
    Cutout,

    /// <summary>Equipment layer (armor/elytra): alpha tested, no blending, submitted as its own draw after the base model.</summary>
    Elytra,

    /// <summary>Blended, depth writes off (player overlays, drowned outer, slime outer).</summary>
    Translucent,

    /// <summary>Emissive eyes: additive, depth test greater-or-equal, no writes (not used yet).</summary>
    Emissive,

    /// <summary>Enchanted glint overlay: additive, depth test greater-or-equal, no writes.</summary>
    Glint
}

/// <summary>
/// One drawable entity layer: its own geometry, render kind and submit order.
/// </summary>
/// <remarks>
/// Framework cannot depend on the client mesh type, so this carries the reusable <see cref="DynamicGeometry"/> the
/// client fills and the renderer uploads. Layers are drawn in ascending <see cref="Order"/>.
/// </remarks>
public sealed class EntityGeometryLayer
{
    public EntityGeometryLayer(string name, EntityGeometryLayerKind kind, int order, DynamicGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        Name = name;
        Kind = kind;
        Order = order;
        Geometry = geometry;
    }

    /// <summary>Diagnostic name ("cutout", "translucent", "emissive").</summary>
    public string Name { get; }

    /// <summary>Reference render type this layer represents.</summary>
    public EntityGeometryLayerKind Kind { get; }

    /// <summary>Submit order, ascending.</summary>
    public int Order { get; }

    /// <summary>Geometry, owned by the producer and reused every frame.</summary>
    public DynamicGeometry Geometry { get; }
}
