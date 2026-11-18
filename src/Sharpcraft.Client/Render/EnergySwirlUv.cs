// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// EnergySwirlLayer texture-offset values. CreeperPowerLayer uses xOffset(t) = t * 0.01,
/// and the base layer passes u = xOffset(t) % 1 and v = t * 0.01 % 1 to RenderTypes.energySwirl.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.entity.layers.EnergySwirlLayer", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/entity/layers/EnergySwirlLayer.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "UV offset only; the energy-swirl shader itself is not ported")]
public static class EnergySwirlUv
{
    /// <summary>Reference CreeperPowerLayer.xOffset and EnergySwirlLayer v formula, including Java % sign behavior.</summary>
    public static (float U, float V) Creeper(float ageInTicks)
    {
        var u = ageInTicks * 0.01f % 1f;
        var v = ageInTicks * 0.01f % 1f;
        return (u, v);
    }
}
