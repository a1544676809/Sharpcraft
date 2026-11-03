// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Giant zombie: the humanoid body mesh at scale 6 (matching the reference's 6x layer transform) with the zombie texture.
/// </summary>
/// <remarks>Bind pose only; the zombie arm animation and equipment layers are not ported.</remarks>
public static class GiantModelDefinition
{
    public const int GiantEntityTypeId = 60;

    public static readonly EntityModelDefinition Definition = new(
        "giant",
        64,
        64,
        HumanoidMobModelDefinition.Zombie.Parts,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/zombie/zombie",
        6f);
}
