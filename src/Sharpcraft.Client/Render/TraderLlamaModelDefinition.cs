// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Trader llama reuses the llama geometry and body texture; the trader carpet is a decor layer built from
/// equipment/llama_body/trader_llama, which is not ported yet.
/// </summary>
public static class TraderLlamaModelDefinition
{
    public const int TraderLlamaEntityTypeId = 138;

    public static readonly EntityModelDefinition Definition = new("trader_llama", 128, 64, faceTransforms: null, texturePath: "entity/llama/llama_brown", compiled: LlamaModelDefinition.Definition.Compiled, baselinePx: 0f, rawSourceFrame: true);
}
