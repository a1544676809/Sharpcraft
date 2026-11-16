// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>Cave spider reuses the spider geometry at scale 0.7 with its own texture.</summary>
public static class CaveSpiderModelDefinition
{
    public const int CaveSpiderEntityTypeId = 22;

    /// <summary>
    /// Cave spider reuses the raw source-frame spider geometry, so it must carry the same raw flags
    /// (face transforms disabled, no baseline, living flip) or the mesher would treat y-down parts as
    /// legacy y-up and invert geometry/UVs. Only the scale and textures differ from the spider.
    /// </summary>
    public static readonly EntityModelDefinition Definition = new(
        "cave_spider",
        64,
        32,
        SpiderModelDefinition.Definition.Parts,
        faceTransforms: null,
        texturePath: "entity/spider/cave_spider",
        scale: 0.7f,
        emissiveTexturePath: "entity/spider/spider_eyes",
        compiled: SpiderModelDefinition.Definition.Compiled,
        baselinePx: 0f,
        rawSourceFrame: true);
}
