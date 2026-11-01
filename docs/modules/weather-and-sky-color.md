<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[已评审]（所有者 2026-11-01 评审通过）
基线：Minecraft 26.3
证据：ref://mc-26.3-analysis/src/net/minecraft/{world/attribute/WeatherAttributes.java, world/attribute/EnvironmentAttributeProbe.java, world/attribute/modifier/{FloatModifier,ColorModifier,FloatWithAlpha}.java, data/worldgen/biome/OverworldBiomes.java, world/level/biome/Biome.java, world/level/{GrassColor,FoliageColor,DryFoliageColor,ColorMapColorUtil}.java, client/renderer/{BiomeColors,ClientLevel}.java, network/protocol/game/ClientboundGameEventPacket.java}
最后更新：2026-11-01

# 天气与生物群系着色（Biome Colours & Weather）

所有**与生物群系挂钩的颜色**（草方块/树叶/干树叶/水的方块着色，以及天空盘基色）与**天气**（雨/雷）统一在"生物群系数据 + 环境属性合成"之上实现；天气再按雨/雷权重重叠到这些属性（天空色、雾色、天光系数/颜色、星亮度、日月亮度）。

> 前置说明：本模块较大且牵涉多处渲染消费，按 AGENTS 3.5 **先文档、评审通过后再编码**。当前只写规格与设计，不含实现。

## 1. 职责范围

负责：

- **生物群系数据**：每 quart/每方块的生物群系（来自 chunk 的 biome paletted container + biome registry），及其 `climateSettings`（temperature、downfall）、`specialEffects`（grassColorOverride / foliageColorOverride / waterColor / grassColorModifier）。
- **方块调色（biome tint）**：草方块、树叶、干树叶、水的颜色——`GrassColor/FoliageColor/DryFoliageColor` colormap 取色 + `grassColorModifier` + `calculateBlockTint` 的 `biomeBlendRadius` 平均；水色取 per-biome `waterColor`。喂给 `ChunkMesher` 的逐面 tint（草只染顶、叶染全脸、水染水色）。
- **天空盘基色** `SKY_COLOR` = `calculateSkyColor(biome temperature)` 的空间 gaussian 混合。
- **天气状态**：雨/雷等级 `rainLevel/thunderLevel`（0..1）及按帧插值。
- **环境属性合成**：基础（维度 + 生物群系空间混合）→ 时间轴 `MULTIPLY` 修饰 → 天气 `RAIN/THUNDER` 层按权重叠加，得到 `SKY_COLOR/FOG_COLOR/SKY_LIGHT_LEVEL·FACTOR·COLOR/STAR_BRIGHTNESS/SUNRISE_SUNSET_COLOR/CLOUD_COLOR` 与 `RAIN_BRIGHTNESS=1-rainLevel`（日月/星 alpha）。

明确不负责：

- 天气**模拟/触发**（服务端权威；客户端只接收等级）。
- 云层几何/云贴图渲染（只产出 `CLOUD_COLOR` 数值）。
- 末地/下界专属天空盒（`DimensionType.Skybox` 非 OVERWORLD）。
- 声音/粒子。

## 2. 侧别归属

- `Common`：纯函数与数据结构——colormap 索引 `ColorMapColorUtil`、`GrassColor/FoliageColor/DryFoliageColor` 取色、`calculateSkyColor`、`BlendToGray`/`AlphaBlend`、gaussian 权重、`calculateBlockTint` 平均、天气权重叠加；生物群系/天气的可序列化状态。
- `Client`：chunk biome 解析与存储、biome registry 读取、天气包接收与插值、把颜色/属性喂给 `ChunkMesher` 与天空/雾/lightmap/天体。
- `Server`（对齐参考，非当前范围）：天气权威模拟、生物群系生成权威。
- 判定依据：生物群系属性与天气是"世界状态→可视量"的共享数据；渲染消费属 Client。依赖方向 `Client → Common`，不得 Client↔Server 互引。

## 3. 依赖关系

| 方向 | 对象 | 说明 |
| --- | --- | --- |
| 依赖 | chunk **biome paletted container** 解析 + biome registry | 草/叶/水/天空色全部依赖；**当前客户端未解析 biome，硬前置** |
| 依赖 | 资源包 colormap 纹理（`grass.png`/`foliage.png` 等，256×256） | colormap 取色；已有 `Client/Assets/Colormap`(T-252) 可复用 |
| 依赖 | `ClientboundGameEventPacket`（天气事件） | 雨/雷等级来源 |
| 依赖 | 时间轴（`Timelines`，已实现 `SkyAttributeTracks`） | 基础属性时间修饰 |
| 依赖 | 已有 `BiomeTint`/`BlockTintTable`/`ChunkMesher` tint 接口 | 现为**近似全局色**，本模块升级为**逐生物群系** |
| 被依赖 | 天空盘、雾、lightmap（`LightmapUniform`）、方块网格 tint、天体（日月 alpha/星亮度/日出色） | 消费颜色/属性 |

## 4. 对外契约

- 接口：`BiomeColorResolver`（草/叶/干叶/水，输入 (biome, x, z) → RGB）；`BlockTintResolver`（`calculateBlockTint` + blend radius）；`SkyAttributeResolver`（生物群系权重 + 天气 + 时间 → 各属性当前值）；`WeatherState`（rain/thunder + 插值）。
- 数据：`BiomeClimate`（temp/downfall）、`BiomeSpecialEffects`（overrides/waterColor/modifier）、colormap 像素。
- 事件/回调：`game_event` 更新 `WeatherState`。
- 明确不暴露：WGPU 句柄、渲染管线。

## 5. 关键数据结构

| 结构 | 用途 | 生命周期 | 所属线程 |
| --- | --- | --- | --- |
| `BiomeRegistryEntry`（climateSettings, specialEffects） | 逐生物群系颜色参数 | 会话 | 只读主线程 |
| `ChunkBiomeMap`（每 quart 的生物群系 id） | 位置→生物群系 | 区块 | 主线程 |
| `BlockTintCache`（pos→RGB，按 resolver） | `calculateBlockTint` 缓存 | 会话 | 主线程 |
| `WeatherState`（rain, thunder, 旧/新值） | 天气等级与插值 | 会话 | 网络回写/主线程读 |
| `EnvironmentAttributeSet` | 一帧各渲染属性值 | 每帧 | 主线程 |

## 6. 行为对齐与偏离

### 6.1 方块调色（草/叶/水）

- **Colormap 取色** `ColorMapColorUtil.get(temp, rain, pixels, default)`：`rain *= temp`；`x=(int)((1-temp)*255)`、`y=(int)((1-rain)*255)`、`index=y<<8|x`；越界返回 default。`GrassColor.get(temp,rain)` 用 65536 像素数组 + default `-65281`；`FoliageColor`/`DryFoliageColor` 各有自己的数组。
- **草色** `Biome.getGrassColor(x,z)`：`base = grassColorOverride ?? GrassColor.get(clamp(temp,0,1), clamp(downfall,0,1))`，再经 `grassColorModifier.modifyColor(x,z,base)`（`NONE` / `DARK_FOREST` / `SWAMP`）。
  - **查表前必须把 temperature、downfall clamp 到 0..1**（原版 `getGrassColorFromTexture` 如此）；漏掉会让温度越界的群系索引越界、返回品红默认色（-65281），表现为紫色草方块。
  - 实现：`NONE` 直通；`DARK_FOREST` = `ARGB.opaque(((base & 0xFEFEFE) + 2634762) >> 1)`；`SWAMP` 依赖 `Biome.BIOME_INFO_NOISE`（未实现）→ 暂返回基色，记**受控偏差**。
- **落叶/干叶**：`getFoliageColor()` = `foliageColorOverride ?? FoliageColor.get(temp,downfall)`；`getDryFoliageColor()` 同理用 `DryFoliageColor`。
- **水色** `getWaterColor()` = `specialEffects.waterColor()`（每生物群系常量；默认 `4159204`）。
- **空间平均** `ClientLevel.calculateBlockTint(pos, resolver)`：`dist = options.biomeBlendRadius`（默认 **2**，范围 0..7）；`dist==0`→单点；否则对 `(2dist+1)²` 方形区域的每格 `resolver.getColor(biome, x, z)` 做 **RGB 逐通道整数求和后除以 count**（`ARGB.color(totalRed/count,…)`）。
- **消费**：`ChunkMesher` 逐面 tint——草方块仅顶面、树叶/植物全脸、水用群系水色（tint 已知的近似在 `BlockTintTable`，需升级为按位置生物群系）。
- **现状与偏离**：已有 `Colormap`(T-252)、`BiomeTint`、`BlockTintTable` 是"参考常量/全局近似"，**未按位置生物群系**取色；本模块改为逐生物群系 + `biomeBlendRadius` 平均（受控偏差收敛为对齐）。

### 6.2 天空盘基色 `SKY_COLOR`

- `SKY_COLOR` = `ARGB.vector3fFromRGB24(OverworldBiomes.calculateSkyColor(temperature))`；`calculateSkyColor(t)`：`tt=clamp(t/3,-1,1)`，HSV `(0.62222224-0.05tt, 0.5+0.1tt, 1.0)`。
- **空间混合**：`EnvironmentAttributeProbe.tick` 用 `GaussianSampler.sample(position.scale(0.25), getNoiseBiomeAtQuart, …)`，7 抽头核 `{0,1,4,6,4,1,0}`（三轴可分离、乘积归一）在生物群系属性间加权。
- **时间修饰**：`Timelines` 对 `SKY_COLOR` 用 `ColorModifier.MULTIPLY_RGB` 轨（133/11867 白 → 13670/22330 黑）。

### 6.3 天气层（叠加到 6.1/6.2/光照）

- `WeatherAttributes.RAIN/THUNDER` 对属性做修饰，按 `rainWeight=rainLevel-thunderLevel`、`thunderWeight=thunderLevel` 依次 `stateChangeLerp(weight, base, modified)`：
  - `SKY_COLOR` `BlendToGray(factor,brightness)`：RAIN `(0.6,0.75)`、THUNDER `(0.24,0.94)`；`BlendToGray(s)=srgbLerp(factor, s, greyscale(s)·brightness)`。
  - `FOG_COLOR` `MULTIPLY_RGB`：RAIN `(1,0.5,0.5,0.6)`、THUNDER `(1,0.25,0.25,0.3)`。
  - `CLOUD_COLOR` `BlendToGray`：RAIN `(0.24,0.5)`、THUNDER `(0.095,0.94)`。
  - `SKY_LIGHT_LEVEL` `ALPHA_BLEND`（`lerp(alpha,subject,value)`）：`FloatWithAlpha(4.0, 0.3125/0.52734375)`。
  - `SKY_LIGHT_COLOR` `ALPHA_BLEND_RGB`（`ARGB.alphaBlend`）：夜间色 `(0.48,0.48,1)`、alpha `0.3125/0.52734375`。
  - `SKY_LIGHT_FACTOR` `ALPHA_BLEND`：`FloatWithAlpha(0.24, 0.3125/0.52734375)`。
  - `STAR_BRIGHTNESS` `set 0.0`；`SUNRISE_SUNSET_COLOR` `MULTIPLY_ARGB`（RAIN `(1,0.5,0.5,0.6)`、THUNDER `(1,0.25,0.25,0.3)`）。
- **日月亮度** `RAIN_BRIGHTNESS = 1 - level.getRainLevel(partialTicks)`（日月/星 alpha）。
- **天气包** `ClientboundGameEventPacket` = `unsignedByte type + float param`：`START_RAINING(1)`/`STOP_RAINING(2)`/`RAIN_LEVEL_CHANGE(7)`/`THUNDER_LEVEL_CHANGE(8)`；`ClientLevel` 保存 rain/thunder 并对 `partialTicks` 插值。

### 有意偏离（现有已交付状态登记）

- 当前天空=全屏渐变（`sky.wgsl`），非 `SKY_COLOR` 天空盘；`RAIN_BRIGHTNESS` 恒 1；方块 tint 为全局近似而非逐生物群系。均待本模块修正（几何/天空盘与暗盘判定已就绪：`CelestialGeometry.SkyDisc`、`SkyVisibility`）。

## 7. 未解决问题与风险

- **生物群系数据缺失（硬前置）**：客户端尚未解析/存储 chunk 的 biome paletted container，也无 biome registry 的 climateSettings/specialEffects 查询。需先做 biome 数据管线（解析、按 quart 采样、registry 读取），两块消费（方块调色、天空色）共用。
- **colormap 纹理来源**：`grass.png`/`foliage.png` 等需从资源包取（已有 `Colormap` 资产层）；越界 default 色 `-65281` 等常量需照抄。
- **`grassColorModifier`（DARK_FOREST/SWAMP）公式**需逐一实读复刻（本文档已定 `NONE` 直通；其余实现时以 `ref://` 校准，测试优先覆盖 NONE）。
- **`biomeBlendRadius`** 为客户端选项（默认 2）；测试覆盖 `dist=0` 与 `dist=1` 平均手算，默认值读 Options。
- **属性层系统重构**：现有 `SkyAttributeTracks` 为手写时间轨捷径；天气叠加需更通用「基础+时间+天气层」合成器（建议 Common 新增，逐步替换）。
- **`ARGB.alphaBlend`/`greyscale`/`srgbLerp`** 精确实现需逐一对齐（已给公式，实现时 `ref://` 校准）。
- **天气包 id 校准**：`CLIENTBOUND_GAME_EVENT` 线上 id 需锚点交叉校准。
- **渲染等效难量化**：天空盘/云/水体颜色像素与参考帧对照需真实服务器固定机位；本模块以**纯逻辑公式等效**为主证（§9），渲染等效为受控偏差。

## 8. 参考资料

- `ref://mc-26.3-analysis/src/net/minecraft/world/attribute/WeatherAttributes.java`
- `ref://mc-26.3-analysis/src/net/minecraft/world/attribute/EnvironmentAttributeProbe.java`
- `ref://mc-26.3-analysis/src/net/minecraft/world/attribute/GaussianSampler.java`
- `ref://mc-26.3-analysis/src/net/minecraft/world/attribute/modifier/{FloatModifier,ColorModifier,FloatWithAlpha}.java`
- `ref://mc-26.3-analysis/src/net/minecraft/data/worldgen/biome/OverworldBiomes.java`
- `ref://mc-26.3-analysis/src/net/minecraft/world/level/biome/Biome.java`
- `ref://mc-26.3-analysis/src/net/minecraft/world/level/{GrassColor,FoliageColor,DryFoliageColor,ColorMapColorUtil}.java`
- `ref://mc-26.3-analysis/src/net/minecraft/client/renderer/{BiomeColors,ClientLevel}.java`
- `ref://mc-26.3-analysis/src/net/minecraft/world/timeline/Timelines.java`
- `ref://mc-26.3-analysis/src/net/minecraft/network/protocol/game/ClientboundGameEventPacket.java`
- `docs/specs/render/sky-celestial.md`、`docs/specs/render/block-layer-and-tint.md`、`docs/specs/render/lighting.md §6`、`docs/modules/time.md`

## 9. 等效性测试计划（纯逻辑、可量化）

1. **`ColorMapColorUtil` 索引**：给定 temp/rain 手算 `x=(1-temp)*255,y=(1-temp*rain)*255,idx=y<<8|x` 与越界 default。
2. **草/叶/干叶取色**：colormap 已知像素点断言；`grassColorOverride` 优先于 colormap；`NONE` modifier 直通。
3. **水色**：`getWaterColor` 返回 per-biome 常量（默认 4159204）。
4. **`calculateBlockTint`**：`dist=0` 单点；`dist=1`（3×3）逐通道整数平均手算；越界/大半径行为。
5. **`calculateSkyColor`**：temperature∈{-1,0,1} 的 HSV→RGB 手算；单调/范围。
6. **`BlendToGray`/`ALPHA_BLEND`**：`srgbLerp(factor,subject,greyscale·brightness)`、`lerp(alpha,subject,value)` 手算；边界 alpha=0/1。
7. **Gaussian 权重**：核 `{0,1,4,6,4,1,0}` 三轴乘积归一双总计=1；对称相等。
8. **天气权重叠加**：`(0,0)`=基准；`rain=1`=RAIN；`thunder=1`=THUNDER；`(0.5,0.3)` 按 `rainWeight=0.2/thunder=0.3` 顺序 `stateChangeLerp` 手算。
9. **`RAIN_BRIGHTNESS`**=`1-rainLevel`；跨帧 rain 插值与 `getRainLevel(partial)` 一致。
10. **`SKY_LIGHT_FACTOR` 天气叠加**：昼 `factor=1`、RAIN alpha 0.3125 → 手算。

## 10. 落地分期（评审通过后）

1. **P1 纯逻辑（Common，无数据）**：`ColorMapColorUtil`、草/叶/干叶/水取色、`calculateBlockTint`（含 blend radius）、`calculateSkyColor`、`BlendToGray`/`AlphaBlend`、gaussian 权重、天气权重叠加、`RAIN_BRIGHTNESS` —— 全部单测。
2. **P2 天气同步**：`ClientboundGameEventPacket` 解析（id 校准）+ `WeatherState` 插值；把 `RAIN_BRIGHTNESS` 接日月/星 alpha（修恒 1 偏差）。
3. **P3 生物群系数据（硬前置）**：chunk biome 解析 + registry（climateSettings/specialEffects）；先用它把**方块调色升级为逐生物群系 + blend radius**（草/叶/水），再用 `SKY_COLOR` 空间混合**替换渐变天空盘**（几何已就绪）。
4. **P4 其余消费**：`FOG_COLOR`、`CLOUD_COLOR`、天气叠加进 `LightmapParameters`（`SKY_LIGHT_*`）。

## 11. P3 数据格式调查（见分析文档）

已实读并写入 `docs/analysis/common/biome-data.md`：

- 区块每 section 为「方块状态容器 + 生物群系容器（4×4×4=64、局部调色板上限 3、全局调色板=群系 id、索引 `(y<<4)|(z<<2)|x`)」；本仓库 `ChunkPacketParser` 现已读入并**丢弃**，P3 改为按 section 存 `int[64]`。
- 群系注册表条目：`has_precipitation`、`temperature`、`temperature_modifier`(默认 NONE)、`downfall`、可选 `attributes`（含 `visual/sky_color`）、`effects`{`water_color` 必填、`foliage_color`?、`dry_foliage_color`?、`grass_color`?、`grass_color_modifier`?}；颜色为 `"RRGGBB"`。
- 颜色派生：草/叶/干叶 = override ?? colormap(temp,downfall) 再经 modifier；水=effects.water_color；天空色首版用 `calculateSkyColor(temperature)`（对原版主世界群系精确等价；自定义 `attributes` 覆盖记受控偏差/后续）。
- 位置采样：block→section quart `(>>2 &3)`，环境属性在相机附近用 7 抽头 gaussian 空间混合（核 `{0,1,4,6,4,1,0}`，总权重 4096）。
