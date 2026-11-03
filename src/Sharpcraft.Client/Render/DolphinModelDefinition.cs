// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// DolphinModel: body with back fin and both side fins, a two-segment tail, and a head with nose. All pivots and the
/// bind rotations follow the reference; animations are not ported.
/// </summary>
public static class DolphinModelDefinition
{
    public const int DolphinEntityTypeId = 36;

    public static readonly EntityModelDefinition Definition = new(
        "dolphin",
        64,
        64,
        [
            PlayerModelDefinition.FromVanilla("body", 0f, 22f, -5f, -4f, -7f, 0f, 8f, 7f, 13f, 22, 0),
            PlayerModelDefinition.FromVanillaChild("back_fin", "body", 0f, 0f, 0f, -0.5f, 0f, 8f, 1f, 4f, 5f, 51, 0,
                rotationX: MathF.PI / 3f),
            PlayerModelDefinition.FromVanillaChild("left_fin", "body", 2f, -2f, 4f, -0.5f, -4f, 0f, 1f, 4f, 7f, 48, 20,
                mirror: true, rotationX: MathF.PI / 3f, rotationZ: MathF.PI * 2f / 3f),
            PlayerModelDefinition.FromVanillaChild("right_fin", "body", -2f, -2f, 4f, -0.5f, -4f, 0f, 1f, 4f, 7f, 48, 20,
                rotationX: MathF.PI / 3f, rotationZ: -MathF.PI * 2f / 3f),
            PlayerModelDefinition.FromVanillaChild("tail", "body", 0f, -2.5f, 11f, -2f, -2.5f, 0f, 4f, 5f, 11f, 0, 19,
                rotationX: -0.10471976f),
            PlayerModelDefinition.FromVanillaChild("tail_fin", "tail", 0f, 0f, 9f, -5f, -0.5f, 0f, 10f, 1f, 6f, 19, 20),
            PlayerModelDefinition.FromVanillaChild("head", "body", 0f, -4f, -3f, -4f, -3f, -3f, 8f, 7f, 6f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("nose", "head", 0f, 0f, 0f, -1f, 2f, -7f, 2f, 2f, 4f, 0, 13)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/dolphin/dolphin");
}
