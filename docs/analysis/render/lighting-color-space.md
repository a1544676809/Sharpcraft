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

## 6. 逐项对照（原版 vs 本实现，2026-11-01 实读）

| # | 环节 | 原版证据 | 本实现 | 结论 |
| --- | --- | --- | --- | --- |
| 1 | 采样纹理 | `block.fsh`: `texture(Sampler0,uv)`；`Lightmap` 纹理 `RGBA8_UNORM` | `terrain.wgsl` `textureSampleGrad`，atlas `RGBA8Unorm` | 一致 |
| 2 | 顶点色 Color | `block.vsh`: `Color`（tint×面明暗×AO，0..1） | `ChunkMesher.CornerShade`: `red=tint*shadeFactor*occlusion` | 一致（明暗 1/0.5/0.8/0.6、AO 见实现） |
| 3 | 光照级别 | `lightmap.fsh`: `level=floor(texCoord*16)/15`（16 级量化） | 顶点 `sky/15`、`block/15`（连续）再 `lightmapColour` | 近似一致（≤1/15 量化差） |
| 4 | 亮度曲线 | `Lightmap.getBrightness`: `lerp(ambientLight, l/(4-3l), 1)` | `brightnessCurve(l)=l/(4-3l)` | 主世界 ambientLight=0 等价；其他维度有差 |
| 5 | 光照图合成 | `lightmap.fsh` 公式 | `terrain.wgsl::lightmapColour` | 一致（省 nightVision/boss/darkness，默认 0） |
| 6 | 光照图取值域 | `RGBA8_UNORM` 原始值 | 原始值 + 直接相乘 | 一致 |
| 7 | 最终合成 | `block.fsh`: `texture*vertexColor*ColorModulator`（**无 gamma**） | `litColour=clamp(texel*light)`（已改） | 一致 |
| 8 | blockFactor | `LightmapRenderStateExtractor`: `blockLightFlicker+1.4` | `DayOverworld.BlockFactor=1.4` | 近似（省略 ±flicker） |
| 9 | brightness(gamma) | `Options.gamma` 默认 **0.5** | `BrightnessFactor=0.5` | 一致 |
| 10 | BlockLightTint | `ARGB.vector3fFromRGB24(-10100)`=`v/255`（`from8BitChannel`，**无 gamma**） | `Srgb24ToLinear(-10100)`（gamma 解码） | **偏差**：块光色偏暗/偏色 |
| 11 | SkyLightColor / SkyColor / SkyLightFactor 时间曲线 | `data/minecraft/timeline/day.json`: 730/11270/13140/22860=0.24、`#7a7aff`、133/11867/13670/22330 | `SkyAttributeTracks` 同值 | 一致 |
| 12 | skyFactor 取值 | `LightmapRenderStateExtractor`: `attributeProbe().getValue(SKY_LIGHT_FACTOR, partialTicks)` | `OverworldAt(dayTime, partial)` 前后 tick 插值 | 一致 |
| 13 | 世界时钟推进 | 原版客户端每 tick 推进世界时钟（时间与 partial 前进） | `ClientWorld.AdvanceClocks()` **未被调用** | **偏差**：时间冻结/昼夜不推进（改时间仅在收到 set_time 时跳变） |
| 14 | set_time 解析 | `ClientboundSetTimePacket` LONG gameTime + map(WorldClock→ClockNetworkState{VAR_LONG,FLOAT,FLOAT}) | `SetTimePacketCodec` 同结构 | 一致 |

**结论**：核心光照合成已与原版等效；剩余实质偏差为 **#10 BlockLightTint（gamma 误用）** 与 **#13 世界时钟未推进**，前者影响块光色，后者影响昼夜/亮度随时间变化（"整体偏暗、改时间不变"很可能源于 #13 或服务器时间本身）。#3、#8 为可接受近似。
