<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[待评审]
基线：Minecraft 26.3
证据：ref://mc-26.3-analysis/src/net/minecraft/{world/attribute/WeatherAttributes.java, world/attribute/EnvironmentAttributeProbe.java, world/attribute/modifier/{FloatModifier,ColorModifier,FloatWithAlpha}.java, data/worldgen/biome/OverworldBiomes.java, network/protocol/game/ClientboundGameEventPacket.java}
最后更新：2026-11-01

# 天气与群系天空色（Weather & Sky Color）

用服务器下发的雨/雷等级叠加在「维度/生物群系基础属性 × 时间轴」之上，产出天空色、雾色、天光系数/颜色、星亮度、日月亮度（RAIN_BRIGHTNESS）等**环境属性**，供天空盘、雾、lightmap、天体渲染消费。

> 前置说明：本模块较大且牵涉多处渲染消费，按 AGENTS 3.5 **先文档、评审通过后再编码**。当前只写规格与设计，不含实现。

## 1. 职责范围

负责：

- 客户端**天气状态**：雨等级 `rainLevel`、雷等级 `thunderLevel`（0..1）及其按帧插值。
- **环境属性合成**：基础（维度默认 + 生物群系空间混合）→ 时间轴 `MULTIPLY` 修饰 → 天气 `RAIN/THUNDER` 层按权重叠加，得到每个属性的当前值。
- 派生消费量：`SKY_COLOR`（天空盘基色）、`FOG_COLOR`、`SKY_LIGHT_LEVEL/FACTOR/COLOR`（lightmap）、`STAR_BRIGHTNESS`、`SUNRISE_SUNSET_COLOR`、`RAIN_BRIGHTNESS = 1 - rainLevel`（日月/星 alpha）。
- 生物群系温度→天空色的 `calculateSkyColor` 纯函数与空间混合。

明确不负责：

- 天气**模拟/触发**（下雨、雷暴的权威判定在服务端；客户端只接收）。
- 云层几何/云色贴图渲染（仅产出 `CLOUD_COLOR` 数值，云渲染另行）。
- 末地/下界专属天空盒（`DimensionType.Skybox` 非 OVERWORLD 时走各自路径）。
- 声音/粒子（`AmbientAdditionsSettings` 等）。

## 2. 侧别归属

- `Common`：属性值与纯函数（`calculateSkyColor`、`BlendToGray`、`ALPHA_BLEND`、gaussian 权重、属性层合成），客户端与服务端可共用；天气状态的可序列化表示。
- `Client`：天气包的接收与插值、生物群系数据的读取、把属性值喂给渲染（天空盘/雾/lightmap/天体）。
- `Server`（仅对齐参考，非当前范围）：天气权威模拟。
- 判定依据：环境属性是"世界状态→可视量"的共享数据（`EnvironmentAttributeSystem`/`WeatherAttributes`），属于两侧共享；渲染消费属 Client。

依赖方向：`Client → Common`；不得 Client↔Server 互引。

## 3. 依赖关系

| 方向 | 对象 | 说明 |
| --- | --- | --- |
| 依赖 | 生物群系数据（chunk 的 biome paletted container + biome registry 温度） | 计算 `SKY_COLOR` 基础；**当前客户端未解析 biome，硬前置** |
| 依赖 | `ClientboundGameEventPacket`（天气事件） | 雨/雷等级来源 |
| 依赖 | 时间轴（`Timelines`，已实现 `SkyAttributeTracks`） | 基础属性的时间修饰 |
| 被依赖 | 天空盘、雾、lightmap（`LightmapUniform`）、天体渲染（日月 alpha/星亮度/日出色） | 消费属性值 |

## 4. 对外契约

- 接口：`WeatherState`（RainLevel/ThunderLevel + `Advance(dt)` 插值）；`SkyAttributeResolver`（输入：生物群系权重、天气、时间；输出各属性当前值）。
- 数据：纯函数 `SkyColor.FromTemperature(float temp)`、`ColorModifier.BlendToGray`/`AlphaBlend` 的 C# 对应。
- 事件/回调：收到 `game_event` 时更新 `WeatherState`（START/STOP_RAINING、RAIN_LEVEL_CHANGE、THUNDER_LEVEL_CHANGE）。
- 明确不暴露：WGPU 句柄、渲染管线（属性解析不接触 GPU）。

## 5. 关键数据结构

| 结构 | 用途 | 生命周期 | 所属线程 |
| --- | --- | --- | --- |
| `WeatherState` (rain, thunder, 旧值/新值) | 天气等级与每帧插值 | 世界会话 | 主线程（读取）；网络回调写入 |
| `BiomeAttributeSample`（若干 (权重, 温度)） | 生物群系空间混合输入 | 每帧 | 主线程 |
| `EnvironmentAttributeSet`（各渲染属性的当前值） | 一帧解析结果 | 每帧 | 主线程 |
| `WeatherLayer`（RAIN/THUNDER 修饰表） | 权重重叠规则 | 常量 | — |

## 6. 行为对齐与偏离

对齐点（附原版证据）：

- **基础天空色** `SKY_COLOR` = `ARGB.vector3fFromRGB24(OverworldBiomes.calculateSkyColor(temperature))`；`calculateSkyColor(t)`：`tt=clamp(t/3,-1,1)`，HSV `(0.62222224-0.05·tt, 0.5+0.1·tt, 1.0)` → 不透明 RGB24（ref：`data/worldgen/biome/OverworldBiomes.java`）。
- **生物群系空间混合**：`EnvironmentAttributeProbe.tick` 用 `GaussianSampler.sample(position.scale(0.25), getNoiseBiomeAtQuart, …)` 以 7 抽头核 `{0,1,4,6,4,1,0}`（三轴可分离，权重乘积归一）在生物群系属性间加权（ref：`EnvironmentAttributeProbe.java`、`GaussianSampler.java`）。
- **时间修饰**：`Timelines` 对 `SKY_COLOR` 用 `ColorModifier.MULTIPLY_RGB` 轨（133/11867 白 → 13670/22330 黑），已在 `SkyAttributeTracks` 记录同类轨（ref：`world/timeline/Timelines.java`）。
- **天气层**：`WeatherAttributes.RAIN/THUNDER` 对多属性做修饰，按 `rainWeight=rainLevel-thunderLevel`、`thunderWeight=thunderLevel` 依次 `stateChangeLerp(weight, base, modified)` 叠加（ref：`WeatherAttributes.java`）：
  - `SKY_COLOR`：`BlendToGray(factor,brightness)`，RAIN `(0.6,0.75)`、THUNDER `(0.24,0.94)`；`BlendToGray(s)=srgbLerp(factor, s, greyscale(s)·brightness)`。
  - `FOG_COLOR`：`MULTIPLY_RGB`，RAIN `(1,0.5,0.5,0.6)`、THUNDER `(1,0.25,0.25,0.3)`。
  - `CLOUD_COLOR`：`BlendToGray`，RAIN `(0.24,0.5)`、THUNDER `(0.095,0.94)`。
  - `SKY_LIGHT_LEVEL`：`ALPHA_BLEND`，`FloatWithAlpha(4.0, 0.3125/0.52734375)`；`ALPHA_BLEND = lerp(alpha, subject, value)`。
  - `SKY_LIGHT_COLOR`：`ALPHA_BLEND_RGB`，参数=夜间天光色 `(0.48,0.48,1)`、alpha `0.3125/0.52734375`（`ARGB.alphaBlend`）。
  - `SKY_LIGHT_FACTOR`：`ALPHA_BLEND`，`FloatWithAlpha(0.24, 0.3125/0.52734375)`。
  - `STAR_BRIGHTNESS`：`set 0.0`（下雨/雷时星星不可见）。
  - `SUNRISE_SUNSET_COLOR`：`MULTIPLY_ARGB`，RAIN `(1,0.5,0.5,0.6)`、THUNDER `(1,0.25,0.25,0.3)`。
- **日月亮度** `RAIN_BRIGHTNESS = 1 - level.getRainLevel(partialTicks)`（`SkyRenderer.extractRenderState`），作为日月四边形/星空的 alpha 乘子。
- **天气包**：`ClientboundGameEventPacket`＝`unsignedByte eventType + float param`；`START_RAINING(1)`/`STOP_RAINING(2)`/`RAIN_LEVEL_CHANGE(7)`/`THUNDER_LEVEL_CHANGE(8)`（ref：`network/protocol/game/ClientboundGameEventPacket.java`）。`ClientLevel` 保存 rain/thunder 等级并对 `partialTicks` 插值（旧值→新值）。
- **消费方**：天空盘基色 `SKY_COLOR`、地形/大气雾 `FOG_COLOR`、lightmap `SKY_LIGHT_LEVEL/FACTOR/COLOR`、星亮度、日出色、日月 alpha。

有意偏离（现有已交付状态的登记）：

- 当前天空是**全屏渐变**（`sky.wgsl`）而非 `SKY_COLOR` 天空盘——因本模块未落地，属受控偏差（`docs/specs/render/sky-celestial.md §8`）。
- 当前 `RAIN_BRIGHTNESS` 恒 1（无天气），`SKY_LIGHT_FACTOR` 只走时间轨、无天气叠加。均待本模块修正。

## 7. 未解决问题与风险

- **生物群系数据缺失（硬前置）**：客户端尚未解析/存储 chunk 的 biome paletted container，也无 biome registry 的 temperature 查询。需先做 biome 数据管线（解析、按 quart 采样、registry 温度），工作量最大。
- **属性层系统重构**：现有 `SkyAttributeTracks` 是"手写时间轨"的捷径；忠实天气叠加需要更通用的「基础+时间修饰+天气层」合成器。建议在 Common 新增 `EnvironmentAttributeSet` 解析器，逐步替换捷径，避免一次性大改。
- **`ARGB.alphaBlend` / `greyscale` / `srgbLerp` 精确实现**需按原版逐一对齐（文档已给公式，实现时以 `ref://` 校准）。
- **天气包 id 校准**：`GamePacketTypes.CLIENTBOUND_GAME_EVENT` 的线上 id 需像 `set_time` 那样用锚点交叉校准，不能凭记忆。
- **渲染等效难量化**：天空盘/云的颜色像素与参考帧对照需真实服务器+固定机位；本模块以**纯逻辑公式等效**为主证（见 §9），渲染等效标注为受控偏差。
- 逐属性 `stateChangeLerp` 对颜色/浮点的精确语义（`LerpFunction`）需实现时按 `AttributeType` 复刻。

## 8. 参考资料

- `ref://mc-26.3-analysis/src/net/minecraft/world/attribute/WeatherAttributes.java`
- `ref://mc-26.3-analysis/src/net/minecraft/world/attribute/EnvironmentAttributeProbe.java`
- `ref://mc-26.3-analysis/src/net/minecraft/world/attribute/GaussianSampler.java`
- `ref://mc-26.3-analysis/src/net/minecraft/world/attribute/modifier/{FloatModifier,ColorModifier,FloatWithAlpha}.java`
- `ref://mc-26.3-analysis/src/net/minecraft/data/worldgen/biome/OverworldBiomes.java`
- `ref://mc-26.3-analysis/src/net/minecraft/world/timeline/Timelines.java`
- `ref://mc-26.3-analysis/src/net/minecraft/network/protocol/game/ClientboundGameEventPacket.java`
- `docs/specs/render/sky-celestial.md`、`docs/specs/render/lighting.md §6`、`docs/modules/time.md`

## 9. 等效性测试计划（纯逻辑、可量化）

1. **`calculateSkyColor`**：`temperature ∈ {-1,0,1}` 的手算 HSV→RGB 值逐一断言；单调性/范围。
2. **`BlendToGray`**：给定 subject/factor/brightness，断言 `srgbLerp(factor, subject, greyscale(subject)·brightness)` 手算值。
3. **`ALPHA_BLEND`**：`lerp(alpha, subject, value)` 手算；边界 alpha=0 → subject、alpha=1 → value。
4. **Gaussian 权重**：核 `{0,1,4,6,4,1,0}` 三轴乘积归一后总和=1；对称点权重相等。
5. **天气权重叠加**：`rain=0,thunder=0` → 等于基准；`rain=1,thunder=0` → 等于 RAIN 修饰；`thunder=1` → THUNDER；`rain=0.5,thunder=0.3` → 按 `rainWeight=0.2, thunder=0.3` 顺序 `stateChangeLerp` 手算。
6. **RAIN_BRIGHTNESS**：`= 1 - rainLevel`；rain=0→1、rain=1→0。
7. **SKY_LIGHT_FACTOR 天气叠加**：昼 `factor=1`、RAIN alpha 0.3125 → `lerp(0.3125,1,0.24)=…` 手算；夜基准 0.24 同理。
8. **跨帧插值**：rain 从 0→1 按 partial 的旧/新值插值与手算一致（对齐 `getRainLevel(partial)`）。

## 10. 落地分期（评审通过后）

1. **P1 纯逻辑**：`calculateSkyColor`、`BlendToGray`/`AlphaBlend`、gaussian 权重、天气权重叠加、`RAIN_BRIGHTNESS`——先在 Common 实现并单测（不接数据）。
2. **P2 天气同步**：`ClientboundGameEventPacket` 解析（id 校准）+ `WeatherState` 插值；把 `RAIN_BRIGHTNESS` 接到日月/星 alpha（修掉恒 1 偏差）。
3. **P3 生物群系数据**：chunk biome 解析 + registry 温度；`SKY_COLOR` 基础 + 空间混合；然后**用天空盘替换渐变**（几何已就绪，见 `CelestialGeometry.SkyDisc`/`SkyVisibility`）。
4. **P4 其余消费**：`FOG_COLOR`、`CLOUD_COLOR`、天气叠加进 `LightmapParameters`（`SKY_LIGHT_*`）。
