<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 天空天体渲染行为规格（日/月/星/日出）
summary: 原版 SkyRenderer 的天空盘、日月星几何/变换/贴图与环境属性依赖
status: 待评审
-->

# 天空天体渲染行为规格

状态：草案（待评审）。范围：天空盘、太阳、月亮（含月相）、星空、日出/日落条带。
证据指针：`ref://mc-26.3-analysis/src/net/minecraft/client/renderer/SkyRenderer.java`（常量 48–58、变换/绘制 160–404）、`.../world/attribute/EnvironmentAttributes.java`、`.../Timelines.java`。
本文件只描述可观测行为。

## 1. 常量（原版实测）

| 项 | 值 |
| --- | --- |
| 天空盘半径 `SKY_DISC_RADIUS` | 512 |
| 天空盘顶点数 | 10（+ 一个下方"暗盘"，y=+12，颜色黑） |
| 星星数 `STAR_COUNT` | 1500，距离 100 |
| 太阳尺寸/高度 | 30 / 100 |
| 月亮尺寸/高度 | 20 / 100 |
| 末地闪光尺寸/高度 | 60 / 100（本规格不做，登记） |
| 日出条带 `SUNRISE_STEPS` | 16（三角扇，中心 (0,100,0)，环半径 X=sin·120、Y=cos·120、Z=−cos·40） |
| 月相 | `moon/<phase>` 共 8 相，每相一个四边形 |

## 2. 几何与变换

- 日/月/末地闪光：**XZ 平面 `-1..1` 的四边形（y=0）**，UV 取对应精灵；变换 = 先 `Y −90°`，再绕 `X` 旋转 `angle`，再 `translate(0, height, 0)`、`scale(size,1,size)`。
- 角度：`sunAngle/moonAngle/starAngle` 均为环境属性（度→弧度）；太阳、月亮、星空分别用各自角度绕 X 旋转。
- 日出/日落：三角扇（18 顶点，中心白 α=1、环白 α=0），变换 = `X 90°`，`Z` 旋转 `(sin(sunAngle)<0 ? 180 : 0) + 90°`，再按 `sunriseColor.a` 缩放 Z；颜色来自 `SUNRISE_SUNSET_COLOR`（RGBA），alpha≤0.001 时不画。
- 天空盘：半径 512 的 10 顶点盘，颜色 `SKY_COLOR`；相机在地平线下且非水下时画一个 y=+12 的黑色暗盘。

## 3. 贴图与颜色

- 贴图：来自**天体贴图集（CELESTIALS）**：`sun`、`moon/<phase>`；日出条带**不用贴图**（顶点色）。
- 颜色：日/月乘 `rainBrightness`（RGBA 的 alpha 作为整体亮度）；星星乘 `starBrightness`；天空盘 `SKY_COLOR`。

## 4. 环境属性依赖（时间轴驱动）——**当前硬缺口**

以下均为 `EnvironmentAttributes`，由**维度 + 时间轴(Timelines)/天气**计算（与"日夜光照"同一机制）：
`SUN_ANGLE`、`MOON_ANGLE`、`STAR_ANGLE`、`MOON_PHASE`、`STAR_BRIGHTNESS`、`SUNRISE_SUNSET_COLOR`、`SKY_COLOR`。

**我们目前解析不到世界时间**（`ClientWorld` 无时间字段、未解析 `set_time/update_time`），因此天体角度无法按真实时间计算。这是本模块的**前置依赖**，与 `docs/specs/render/lighting.md §6`（日夜）同一缺口。

## 5. 现状（2026-11-01）

- 已有**全屏渐变天空 + 距离雾**（`sky.wgsl`）。
- **太阳已接入设备管线**：`environment/celestial/sun` 纹理经 `celestial.wgsl`（pos3+uv2；每帧相机 + 每 draw 的 model/tint uniform）绘于天空渐变之后、地形之前；深度恒通过且不写，故被地形遮挡。合成采用原版 `BlendFunction.OVERLAY`（颜色 `SrcAlpha/One`、alpha `One/Zero`），使贴图的黑色/透明像素不贡献颜色。
- 天体贴图在 26.3 位于 `textures/environment/celestial/`：`sun.png` 与 `moon/<phase>.png`（8 张独立月相贴图，**不是** 4×2 月相表），与方块图集独立。已按此更正标识符。
- **月亮已接入同一管线**：按 `SkyAttributeTracks.MoonPhaseIndex(overworld.totalTicks)` 选 `moon/<phase>` 贴图，角度 `MOON_ANGLE`、尺寸 20 / 高度 100，与太阳共用 OVERLAY 合成。
- **日出条带已接入**：position-colour 三角扇（18 顶点，中心白 α=1、环白 α=0），变换 `RotX(90°)·RotZ((sin(sunAngle)<0?180:0)+90°)` 且 Z 乘 alpha，颜色 `SUNRISE_SUNSET_COLOR`，`TRANSLUCENT` 混合；alpha≤0.001 不画。
- **星空已接入**：复刻 `buildStars`（LCG seed 10842、候选 1500、长度∈(0.010000001,1) 保留、距离 100、随机自旋），按 `STAR_ANGLE` 旋转、`STAR_BRIGHTNESS` 作 `OVERLAY` 色调。
- **尚未做**：天空盘/暗盘（沿用全屏渐变作为受控偏差）、`RAIN_BRIGHTNESS`（日月 alpha，晴天=1）、镜像到 `WindowedRenderer`；随后日夜光照。

## 6. 实现分期（评审通过后）

1. **资产**：从 pack 读取 celestials 精灵（`environment/celestial/sun`、`environment/celestial/moon/<phase>` 共 8 张），独立于方块图集。
2. **天空盘 + 暗盘**（颜色 `SKY_COLOR`，半径512）。
3. **日/月四边形 + 变换**（高度100、缩放30/20；月相选 UV）。
4. **星空**（1500 四边形、距离100、`starBrightness`）。
5. **日出扇形**（16 段、`SUNRISE_SUNSET_COLOR`、alpha 缩放）。
6. **角度驱动**：`SUN/MOON/STAR_ANGLE` 等——**依赖世界时间**（见 §4）。
   - 过渡方案（待定）：先接受**宿主注入的时间/角度**（调试开关）以验证渲染正确性；正式版仍需补时间包解析。

## 7. 等效性测试计划

1. **变换数学**（纯逻辑，无 GPU）：给定 `angle/height/size`，断言日/月四边形顶点世界坐标与手算一致（绕 X 旋转 + translate + scale）。
2. **日出扇形**：给定 `sunAngle` 与 alpha，断言 18 顶点坐标公式与手算一致，alpha≤0.001 时零绘制。
3. **出图**：离屏固定相机+固定角度，抽像素（日/月中心颜色、星空亮度、日出条带边缘 alpha 过渡）。
4. 尺寸/位置：太阳在天空占角、月亮为太阳的 20/30 等相对关系。

## 8. 风险 / 未解决

- **世界时间缺失**（§4）：无时间则角度只能取固定值/宿主注入，属"渲染正确但时间不真"的受控偏差，需与日夜光照一并解决。
- 天体贴图集抽取（独立于方块图集）需资产管线小改。
- 月相：26.3 为 8 张独立贴图 `moon/<phase>.png`（`MoonPhase` 枚举 0..7），无需 4×2 切表；按 `MOON_PHASE` 选贴图即可。
- 末地闪光、末地天空本规格不做（登记）。

## 9. 参考资料

- `ref://mc-26.3-analysis/src/net/minecraft/client/renderer/SkyRenderer.java`
- `ref://mc-26.3-analysis/src/net/minecraft/world/attribute/EnvironmentAttributes.java`
- `docs/analysis/render/sky-and-fog.md`、`docs/specs/render/lighting.md §6`
