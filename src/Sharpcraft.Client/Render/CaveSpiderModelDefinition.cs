// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>Cave spider reuses the spider geometry at scale 0.7 with its own texture.</summary>
public static class CaveSpiderModelDefinition
{
    public const int CaveSpiderEntityTypeId = 22;

    public static readonly EntityModelDefinition Definition = new(
        "cave_spider",
        64,
        32,
        SpiderModelDefinition.Definition.Parts,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/spider/cave_spider",
        0.7f,
        "entity/spider/spider_eyes");
}
