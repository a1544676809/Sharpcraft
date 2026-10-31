<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 流体渲染行为规格
summary: 水/岩浆的液面高度、面剔除、flow-UV、染色、逐面光、贴图动画与水下雾
status: 已评审
-->

# 流体渲染行为规格

状态：已评审（用户 2026-10-31 评审通过，并纠正：OIT 为 26.3 原版机制）。范围：水/岩浆等流体方块的几何生成与着色，及水下雾。
证据指针：`ref://mc-26.3-analysis/src/net/minecraft/client/renderer/block/FluidRenderer.java`、
`.../world/level/material/{FluidState,FlowingFluid,WaterFluid}.java`、
`.../world/level/block/LiquidBlock.java`、
`.../client/renderer/fog/environment/WaterFogEnvironment.java`、
`.../client/renderer/fog/FogRenderer.java`、`.../world/attribute/EnvironmentAttributes.java`。
方块状态解码见 `docs/modules/block-state.md`、`FluidStateCodec`。本文件只描述可观测行为。

## 1. 总览

流体**不走方块面贪婪合并**，而是按方块生成**可变液面高度**的独立几何，进半透明层；贴图是**动画的 `_still`/`_flow`**，带生物群系水色与逐面光照；相机在水中时切换为**水下雾**。

## 2. 流体状态（已实现，`FluidStateCodec`）

- 方块 `level` 0..15 → `amount`/`falling`：`0`=源(amount 8, 非下落)；`1..7`=流动(amount 8−level)；`8..15`=下落(amount 16−level)。
- `ownHeight = amount / 9`（**源水 = 8/9 ≈ 0.8889**）。
- 头顶同种流体时液面高度按 **1.0**（`getHeight = hasSameAbove ? 1.0 : ownHeight`）。

## 3. 液面高度（四角）

设本格 `hSelf`（§2）。若 `hSelf >= 1` 则四角均为 1；否则每角：
- 取 `hSelf`、两条相邻边格高度 `h1/h2`、对角高度 `hCorner`；
- 边/对角高度为 `getHeight(该格)`（非同种流体但为实心 → 视为 0；可穿过的非实心 → 0 参与）；
- `calculateAverageHeight`：若 `h1>=1` 或 `h2>=1` → 返回 1；否则把 `hSelf,h1,h2`（以及 `hCorner`，当 `h1>0||h2>0`）按**加权**求平均，权重规则 `addWeightedHeight`：**高度 ≥0.8 记权重 10，否则权重 1**（凸起处权重更大）。

顶面四角各减 `0.001` 防 z-fighting。

## 4. 面剔除

- **顶面**：仅当上方**非同种流体**时生成。
- **底面**：下方非同种流体、且**不被下方方块遮挡**时生成（贴地 `0.001` 偏移）。
- **四个侧面**：该向邻居**非同种流体**、且**不被邻居遮挡**时生成。
- 遮挡判定按原版 `isFaceOccludedByState`（VoxelShape 面遮挡）；水平相邻若是 `HalfTransparentBlock`/`LeavesBlock` 则改用 **overlay 贴图**（`water_overlay`）。
- **反向顶面**：`shouldRenderBackwardUpFace`（上方 3×3 中存在非同种流体且非实心渲染 → 顶面额外补一个反向面，供从水下看）。

## 5. 流动向量与 UV

- `getFlow`：对 4 个水平方向累加 `ownHeight(自身) − ownHeight(邻居)`（邻居高度为 0 且可穿过时取其下方），`falling` 时按朝向叠加偏置；最后归一化。零向量表示静水。
- **静水**（flow=0）：`_still` 贴图整张 UV。
- **流动**：`_flow` 贴图按 `angle = atan2(flow.z, flow.x) − π/2` 旋转，半径 `0.25`、中心 `0.5` 采样（`u/v` 为 4 个偏置点）。
- **侧面 v**：`v = (1 − h) * 0.5`（液面越低，向下取到贴图更靠下），保证侧面贴图底部对齐。

## 6. 贴图与动画（**依赖：图集动画，尚未实现**）

- 静水贴图 = `<name>_still`，流动 = `<name>_flow`（岩浆同理）。**两者都是动画贴图**：`.mcmeta` 的 `animation` 段（`frametime` 默认 1 tick、`interpolate`、可枚举 `frames`）。
- 现状：`SpriteAtlasBuilder` 只读动画**帧尺寸/帧数**（`AnimationFrameSize`、`SpriteRect.FrameCount`），**没有按时间切帧**。因此要实现"水在流动/波动"必须先补**图集动画**（按游戏时间选帧 → 取该帧在纵向帧带里的 UV 子区），属于本规格的前置任务。

## 7. 染色

- 颜色 = **生物群系水色**（`BiomeTint.Water`，默认 `0x3F76E4`）；岩浆不染色（用自身贴图）。
- 逐面按朝向系数缩放着色：顶面 = `up`(1.0)，侧面 = `up × side`（0.6/0.8），底面 `down`(0.5)——与方块朝向口径一致。

## 8. 光照（**与方块不同：逐面，非逐顶点**）

- 流体每面的光 = `max(light(pos), light(pos.above()))`（取本格与上方之较亮），**整面一个光值**，不做逐顶点平滑。
- 实现注意：我们现有地形是逐顶点平滑光；流体要单独走"逐面光"，不能直接复用方块顶点着色路径。

## 9. 水下雾（相机在水中时）

- 触发：相机所在流体为水（`FogType.WATER`）。
- 雾色 = `WATER_FOG_COLOR` 环境属性（生物群系水雾色）。
- `environmentalStart = WATER_FOG_START_DISTANCE`（默认 −8，即相机身后）；
  `environmentalEnd = WATER_FOG_END_DISTANCE`（默认 96）× `max(0.25, waterVision)`；
  `skyEnd = cloudEnd = environmentalEnd`（水下天空也被雾遮住）。
- 与现有着色器的关系：现有 `fog` uniform 只有单一颜色+起止；水下需把颜色切到水雾色并把起止换成上面的值（`skyEnd/cloudEnd` 一并）。

## 10. 与渲染管线的关系

- 流体进**半透明层**（S1 分类：`water`/`lava` 已属 translucent）。
- 混合：**原版 26.3 半透明走 OIT**（depth-bounds / transmittance / accumulate / composite，顺序无关）。本项目已实现该 reference OIT，因此流体半透明混合**与原版一致**；OIT 是原版机制，不是我们的偏差。
- 流体几何**不参与方块贪婪合并**；需独立网格/独立缓冲。

## 11. 等效性测试计划（纯逻辑优先）

1. `FluidStateCodec`：level→amount/falling/ownHeight（已实现，9 例）。
2. **四角高度**：对"单块源水/2×2 水池/阶梯落差"手算四角值，断言 `calculateAverageHeight`（含 0.8 权重）逐值一致。
3. **flow 向量**：给定邻居高度差，断言方向与归一化；静水为 0。
4. **level→高度**：level 0→8/9、level 4→4/9、level 8→8/9(下落)、level 15→1/9。
5. **面剔除**：单块水 6 面？源水贴地时底面被遮挡→5 面；2×2 内部面互消；手算面数断言。
6. **染色/逐面光**：给定 sky/block 与朝向，断言颜色 = 水色 × 朝向系数（线性）。
7. 出图：离屏固定机位水面场景 + 像素探针，随时间推进纹理帧变化（动画落地后）。

## 12. 落地顺序与依赖

1. **图集动画**（前置）：`.mcmeta` 动画（frametime/interpolate/frames）→ 运行时按游戏时间选帧 UV。没有它水只能静态。
2. **流体网格**：四角高度 + 面剔除/遮挡 + flow-UV + 染色 + 逐面光 + overlay + 反向顶面；进半透明层。
3. **逐面光照**通道（流体专用，区别于方块逐顶点）。
4. **水下雾**：相机在水 → 切雾色/距离。
5. 回归：面数、既有测试、离屏像素。

## 13. 已知偏差 / 范围

- 水/岩浆的**流动纹理动画**依赖图集动画（未实现前水为静态）。
- 未覆盖：waterlogged 方块内的流体（方块属性 `waterlogged`）、流体与实体的交互、气泡柱等。
