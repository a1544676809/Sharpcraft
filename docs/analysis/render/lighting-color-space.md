<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 地形光照色彩空间（texel × vertexColor × lightmap）
summary: 26.3 block 着色器全程 sRGB 域直接相乘；据此修正我们误加的 gamma 解码/编码
status: 草稿
updated: 2026-11-01
-->

# 地形光照色彩空间

本文记录 26.3 原版地形（block）着色器的颜色合成方式，用于判定"逐群系 tint 正确但同群系颜色仍偏淡"的根因。结论全部来自对原版 shader 的实读。

## 1. 原版证据

- `ref://mc-26.3-analysis/src/assets/minecraft/shaders/core/block.vsh`
  - `vertexColor = Color * sample_lightmap(Sampler2, UV2);`
  - `Color` = 顶点 RGBA（群系/方块 tint × 面明暗 × AO，均为 0..1 sRGB 域）；`Sampler2` = 光照图纹理。
- `ref://mc-26.3-analysis/src/assets/minecraft/shaders/core/block.fsh`
  - `vec4 color = texture(Sampler0, texCoord0) * vertexColor * ColorModulator;`
  - 之后仅做雾混合；**全程没有任何 `pow` / sRGB 解码 / 编码**。
- `ref://mc-26.3-analysis/src/assets/minecraft/shaders/core/lightmap.fsh`
  - `get_brightness(level) = level / (4 - 3*level)`；
  - `color = ambientColor + SkyLightColor*skyBrightness + BlockLightColor*blockBrightness`（其中 `parabolicMixFactor`、`notGamma`、`BrightnessFactor` 混合同我们现有端口）。

**结论**：原版把「纹理 texel」「顶点色 Color（含 tint/shade/AO）」「光照图 colour」三者**在 sRGB（gamma）域直接逐通道相乘**，输出不做任何转换（最终呈现层处理）。`lightmap.fsh` 的数值我们已按 `terrain.wgsl::lightmapColour` 1:1 复刻。

## 2. 我们的错误

`terrain.wgsl` / `oit_reference.wgsl` 的 `litColour` 曾写成：

```
linear = pow(texel, 2.2);  out = pow(linear * light, 1/2.2)
```

它假定 `light` 是线性光。但 `light = vertexColor × lightmapColour` 里 `vertexColor`(tint/shade/AO) 是 sRGB 值，被一并当作线性，等价于把 tint 结果开了 `^(1/2.2)` 次方——**染色方块（草/叶/水）被显著提亮、去饱和**，正是"同群系草地偏淡"的表现。原版代码里没有这段 gamma。

## 3. 修正

`litColour` 改为 sRGB 域直接相乘：`out = clamp(texel * light)`，其中 `light = vertexColor * lightmapColour(...)`。与原版 `color = texture * vertexColor * ColorModulator` 等价（ColorModulator 默认 1）。影响范围：所有地形/染色方块亮度（对原版行为的对齐修正，属既有 `litColour` 偏差的纠正，非新偏差）。

## 4. 验证

- 代码级：与本文件 §1 原版 shader 逐句对应（无 pow）。
- 数值级：`MC_BIOME_PROBE` 证明 tint 与原版名色一致（beach 0.8/0.4→`0x91BD59`）。
- 实机：`window` 复看草地颜色与光照。

## 5. 待确认

- 光照图 framebuffer 是否 sRGB 存储（若存储即编码，则 `lightmapColour` 需再 `^(1/2.2)`）。当前按原版 `block.fsh` 直接相乘实现；如需精确，可在拿参考帧后用同一机位像素对照定标。
