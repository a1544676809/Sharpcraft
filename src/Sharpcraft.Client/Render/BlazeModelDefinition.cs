// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// BlazeModel: one head cube and twelve root rods arranged in three rings.
/// </summary>
public static class BlazeModelDefinition
{
    public const int BlazeEntityTypeId = 14;

    public static readonly EntityModelDefinition Definition = Create();

    private static EntityModelDefinition Create()
    {
        var parts = new List<EntityModelPart>
        {
            PlayerModelDefinition.FromVanilla("head", 0f, 0f, 0f, -4f, -4f, -4f, 8f, 8f, 8f, 0, 0)
        };

        var angle = 0f;
        for (var i = 0; i < 4; i++)
        {
            AddRod(parts, i, MathF.Cos(angle) * 9f, -2f + MathF.Cos(i * 2 * 0.25f), MathF.Sin(angle) * 9f);
            angle += MathF.PI / 2f;
        }

        angle = MathF.PI / 4f;
        for (var i = 4; i < 8; i++)
        {
            AddRod(parts, i, MathF.Cos(angle) * 7f, 2f + MathF.Cos(i * 2 * 0.25f), MathF.Sin(angle) * 7f);
            angle += MathF.PI / 2f;
        }

        angle = 0.47123894f;
        for (var i = 8; i < 12; i++)
        {
            AddRod(parts, i, MathF.Cos(angle) * 5f, 11f + MathF.Cos(i * 1.5f * 0.5f), MathF.Sin(angle) * 5f);
            angle += MathF.PI / 2f;
        }

        return new EntityModelDefinition("blaze", 64, 32, parts, PlayerModelDefinition.DefaultFaceTransforms,
            "entity/blaze/blaze");
    }

    private static void AddRod(List<EntityModelPart> parts, int index, float x, float y, float z) =>
        parts.Add(PlayerModelDefinition.FromVanilla($"part{index}", x, y, z, 0f, 0f, 0f, 2f, 8f, 2f, 0, 16));
}
