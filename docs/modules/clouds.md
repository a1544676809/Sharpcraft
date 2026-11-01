<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 云层渲染（CloudRenderer）
summary: 26.3 云的高度/尺寸/纹理/运动/颜色与混合，及本项目的等效实现方案
status: 已评审
updated: 2026-11-01
-->

# 云层渲染（clouds）

实现原版云层。证据来自实读 `net/minecraft/client/renderer/CloudRenderer.java`、`assets/minecraft/shaders/core/clouds.{vsh,fsh}`、`EnvironmentAttributes`、`data/minecraft/timeline/day.json`。

## 1. 原版行为

- **纹理**：`textures/environment/clouds.png`；像素 **alpha < 10 视为空格**（无云）。每个单元格 = **12 方块**（`CELL_SIZE_IN_BLOCKS=12`），纹理 cell 按方块网格铺贴。
- **高度**：属性 `visual/cloud_height` 默认 **192.33**（相机所在维度投影到该高度附近绘制）。
- **运动**：云沿一个水平轴滚动，`offset = (gameTime % (width*400) + partialTicks) * 0.03` 方块；即约 **0.6 方块/秒**；纹理在水平方向循环。
- **颜色**：属性 `visual/cloud_color`（ARGB）随时间 multiply：`#ffffffff`(133/11867) → `#ff191926`(13670/22330)，即白天纯白、夜里暗蓝灰。雨/雷另有天气调暗（`WeatherAttributes`）。
- **几何/明暗**：每个非空 cell 生成可见面；面亮度系数 **顶 1.0、底 0.7、南北 0.8、东西 0.9**；顶面可用 `FLAG_USE_TOP_COLOR`。内部相邻面剔除。
- **混合/雾**：alpha 混合；云雾按 `visual/cloud_fog_end_distance` 默认 **2048** 做线性淡出（`FogCloudsEnd`）。
- **距离**：绘制半径 `radiusCells = ceil(renderDistanceBlocks / 12)`；只建相机附近的云 mesh。

## 2. 本项目实现方案（逐 cell，还原原版）

按原版逐 cell 构建云四边形（成本可接受）：

1. 载入 `clouds.png`（RGBA，alpha<10 为空），打包每个 cell 的四邻空标志，生成 `cells[x + y*width]`。
2. 相机附近半径 `radiusCells = ceil(renderDistanceBlocks/12)` 内按 `buildMesh` 环形遍历（`ring` 0..2r，按 `x²+z²<=r²`）收集可见 cell。
3. 面规则（`buildExtrudedCell`）：`relativePos != BELOW` 加顶面、`!= ABOVE` 加底面、四向只在对应邻为空**且相对坐标符号匹配**时加；相机 ±1 cell 内额外加六个 `INSIDE_FACE` 面。
4. 位置：`faceVertex*CellSize(12,4,12) + (cellX,0,cellZ)*CellSize + CloudOffset`；`CloudOffset = (-xInCell, relativeBottomY, -zInCell)`，`cloudOffset=(gameTime% (w*400)+partial)*0.03`（≈0.6 块/s）。高度 `visual/cloud_height` 默认 **192.33**、厚 4。
5. 颜色/明暗：面亮度 **顶1/底0.7/南北0.8/东西0.9** × `visual/cloud_color`（昼 `#ffffffff`→夜 `#ff191926`，multiply）× 天气调暗。
6. 混合/雾：alpha 混合，按 `CLOUD_FOG_END=2048` 线性淡出；绘制在天空之后、地形之前。

渲染承载：本渲染器窗口侧目前**没有 celestial/云 pass**（只有离屏绘制太阳/月亮/星星），因此需先在窗口补该 pass，再接入云四边形（详见 §6）。

## 3. 纯逻辑测试（可量化部分）

- `CloudColorFactorAt`：昼=白 `(1,1,1,1)`，夜=`#191926`→`(25/255,25/255,38/255,1)`；中午/午夜取样。
- 滚动偏移：`offset(gameTime) = (gameTime*0.03)` 与周期回绕；固定 tick 手算。
- 云高度/尺寸常量（192.33 / 12）。

## 4. 渲染验证

- 离屏真服帧：白天 `MC_FORCE_DAYTIME=6000` 出图见白云；夜间见暗云。对比原版截图。
- 像素：云区 alpha 混合是否覆盖天空色；无云孔透出天空。

## 5. 评审决定（已通过 2026-11-01）

- **采用逐 cell 几何**（非贴图平面近似）；云开销可接受，优先还原原版行为。
- 无 `clouds.png` 时**不画云**（不崩溃）。
- 云绘制半径**跟随 `MC_VIEW_DISTANCE`**。

## 6. 前置：窗口 celestial/cloud pass

当前 `WindowedRenderer` **没有**绘制 celestial（太阳/月亮/星星）与云；这些只在离屏 `OffscreenRenderer` 实现。要在窗口里看到逐 cell 云（以及日月星），必须先给窗口补上同一套 celestial/云绘制 pass（管线、纹理、每帧 uniform：`CloudColor/CloudOffset/CellSize`、云四边形/顶底/侧向面），再接入本模块。实现顺序：① 纯逻辑 `CloudTexture`/`CloudMeshBuilder`/`CloudColorFactorAt` + 单测；② 窗口 celestial+cloud pass；③ 离屏同源复用；④ 出对照帧。
