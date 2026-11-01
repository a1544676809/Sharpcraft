<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 生物群系数据（区块内群系 / 注册表 / 颜色派生）
summary: 26.3 区块生物群系容器的位序、注册表字段、位置采样与草/叶/水/天空颜色来源
status: 草稿
updated: 2026-11-01
-->

# 生物群系数据（biome data）

本文是 P3（`docs/modules/weather-and-sky-color.md`）的数据格式调查：客户端要拿到"每个位置属于哪个生物群系、该群系的温度/降水/颜色"，才能做逐群系方块调色与天空色。结论均为对原版代码的实读。

## 1. 区块内的生物群系容器

- 区块数据每个 section 的缓冲顺序：**方块状态 paletted container，然后生物群系 paletted container**。
- 生物群系容器：**4×4×4 = 64** 项（一 "quart" 为 4×4×4 方块体积一个群系）；局部调色板上限 `BiomeMaxLocalBits = 3`；全局调色板元素为 **VarInt 生物群系注册表 id**（direct 模式无局部调色板）。
- 线性索引（`Strategy.SECTION_BIOMES.getIndex`，bitsPerAxis=2）：`index = (y<<4) | (z<<2) | x`，x/y/z ∈ 0..3（section 内 quart 局部坐标）。
- **本仓库现状**：`Common/Network/ChunkPacketParser.cs` 已按此读入并**丢弃**（`BiomeCount=64`、`BiomeMaxLocalBits=3`、`TryRead(..., out _)`）；P3 只需改为按 section 存 `int[64]`（群系 id），随 `ChunkSection`/`LevelChunk` 保存。

## 2. 生物群系注册表条目（网络码 `Biome.NETWORK_CODEC`）

每个群系条目字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `has_precipitation` | bool | 是否有降水 |
| `temperature` | float | 气候温度（草/叶 colormap 用，clamp 0..1） |
| `temperature_modifier` | enum | 默认 `NONE`；`FROZEN` 影响 `getTemperature(pos)` 的冻结点（草/叶 colormap **不用**它） |
| `downfall` | float | 湿度（colormap y 轴，`rain=downfall*temperature`） |
| `attributes` | EnvironmentAttributeMap（可选） | 群系属性映射；26.3 的 `SKY_COLOR`=`visual/sky_color` 在此 |
| `effects` | 结构 | 见下 |

`effects`（`BiomeSpecialEffects.CODEC`）：`water_color`（必填，`STRING_RGB_COLOR` 十六进制 `"RRGGBB"`）、`foliage_color`?、`dry_foliage_color`?、`grass_color`?、`grass_color_modifier`?（`NONE`/`DARK_FOREST`/`SWAMP`，默认 NONE）。

## 3. 颜色来源与派生

- **草色** `Biome.getGrassColor(x,z)`：`grassColorOverride（effects.grass_color） ?? ColorMapColorUtil(temp, downfall, grassColormap, default)`，再经 `grassColorModifier.modifyColor(x,z,base)`。
- **叶/干叶**：`foliageColorOverride ?? FoliageColor.get(temp,downfall)`、`dryFoliageColorOverride ?? DryFoliageColor.get(...)`。
- **水色** `effects.water_color`（每群系常量）。
- **天空色** `SKY_COLOR`：26.3 由群系**属性** `visual/sky_color` 提供，`OverworldBiomes` 用 `ARGB.vector3fFromRGB24(calculateSkyColor(temperature))` 设入；即对原版主世界群系，`SKY_COLOR == calculateSkyColor(temperature)`。
- **方块 tint 空间平均**：`ClientLevel.calculateBlockTint(pos,resolver)`，`biomeBlendRadius` 方形（客户端默认 2 → 5×5）逐通道整数平均。

## 4. 位置 → 群系 → 空间混合

- 方块 `(x,y,z)` 落到 section 内 quart `(x>>2, y>>2, z>>2)`（局部取 0..3），按 §1 索引取群系 id。
- 环境属性（如天空色、草色）在相机附近做**空间混合**：`GaussianSampler.sample(pos.scale(0.25), getNoiseBiomeAtQuart, ...)`，7 抽头核 `{0,1,4,6,4,1,0}`（三轴可分离、乘积归一，总权重 4096）在生物群系属性间加权。
- `getNoiseBiomeAtQuart` 取 quart 坐标处的群系；`pos.scale(0.25)` 把方块坐标换到 quart 单位。

## 5. 缺口与风险（P3 实施前需定）

- **`attributes`（EnvironmentAttributeMap）通用解析**：属性 id → 值的映射，码表/类型较多。首版**只用 `calculateSkyColor(temperature)` 近似 `SKY_COLOR`**（对原版主世界群系精确等价），把"自定义群系属性覆盖天空色/其他属性"记为**受控偏差/后续**。
- **`STRING_RGB_COLOR`**：颜色是十六进制字符串，需解析 `"RRGGBB"`。
- **registry 同步来源**：生物群系 registry 经 `registry_data` 包同步；需确认本仓库注册表解析（T-042 目前面向方块状态）能通用到取群系条目，或新增 biome 专用解码。
- **温度/降水与 colormap 纹理**：`grass.png`/`foliage.png` 等 colormap（256×256，索引 `y<<8|x`）从资源包取；越界 default 色常量照抄。
- **`temperature_modifier`**：草/叶 colormap 不用；若将来做 `getTemperature(pos)`（冰雪冻结点）需补。

## 6. 证据

- `ref://mc-26.3-analysis/src/net/minecraft/network/protocol/game/ClientboundLevelChunkPacketData.java`（section 缓冲：block_states + biomes）
- `ref://mc-26.3-analysis/src/net/minecraft/world/level/chunk/PalettedContainer.java`、`.../PalettedContainer$Strategy.java`（`getIndex = (y<<4)|(z<<2)|x`）
- `ref://mc-26.3-analysis/src/net/minecraft/world/level/biome/Biome.java`（`ClimateSettings.CODEC`、`getGrassColorFromTexture`、network codec）
- `ref://mc-26.3-analysis/src/net/minecraft/world/level/biome/BiomeSpecialEffects.java`（effects codec 字段）
- `ref://mc-26.3-analysis/src/net/minecraft/world/level/{GrassColor,FoliageColor,DryFoliageColor,ColorMapColorUtil}.java`
- `ref://mc-26.3-analysis/src/net/minecraft/data/worldgen/biome/OverworldBiomes.java`（`SKY_COLOR` 属性 = calculateSkyColor(temperature)）
- `ref://mc-26.3-analysis/src/net/minecraft/world/attribute/EnvironmentAttributeProbe.java`、`GaussianSampler.java`
- `ref://mc-26.3-analysis/src/net/minecraft/client/renderer/BiomeColors.java`、`ClientLevel.java`（calculateBlockTint）
- `docs/modules/weather-and-sky-color.md`、`docs/analysis/common/registry-order.md`
