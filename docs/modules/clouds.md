<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 云层渲染（CloudRenderer）
summary: 26.3 云的高度/尺寸/纹理/运动/颜色与混合，及本项目的等效实现方案
status: 草稿
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

## 2. 本项目实现方案（行为等效，过程简化）

受控偏差（记录于 STATE）：不逐 cell 建带朝向剔除的完整 mesh，而是**用云纹理做水平贴图平面**近似：

1. 载入 `clouds.png` 为纹理（RGBA，alpha 作云孔）。
2. 以相机 XZ 为中心、`y = cloudHeight(192.33)` 放置水平四边形，尺寸覆盖渲染距离所需（≈ `2*renderDistanceBlocks + 若干 cell`，纹理按 cell=12 平铺/滚动）。
3. UV 由 **世界 XZ / (纹理宽*12)** 生成并按 `offset` 滚动；采样 alpha<阈值丢弃 → 得到云块轮廓。
4. 颜色 = 云纹理 RGB × `cloudColor`（`CloudColorFactorAt(dayTime)` × 天气调暗）；alpha 混合。
5. 绘制顺序：天空之后、地形之前；深度比较 `LessEqual`（云在地形之后被遮挡），不写深度。
6. 雾：按 `CLOUD_FOG_END(2048)` 线性淡出。

这样能得到"晴天白云 / 夜间暗云 / 随时间移动"的等效观感；牺牲逐 cell 的侧面阴影与精确剔除（远距不明显）。

## 3. 纯逻辑测试（可量化部分）

- `CloudColorFactorAt`：昼=白 `(1,1,1,1)`，夜=`#191926`→`(25/255,25/255,38/255,1)`；中午/午夜取样。
- 滚动偏移：`offset(gameTime) = (gameTime*0.03)` 与周期回绕；固定 tick 手算。
- 云高度/尺寸常量（192.33 / 12）。

## 4. 渲染验证

- 离屏真服帧：白天 `MC_FORCE_DAYTIME=6000` 出图见白云；夜间见暗云。对比原版截图。
- 像素：云区 alpha 混合是否覆盖天空色；无云孔透出天空。

## 5. 待确认（评审项）

- 采用"贴图平面近似"（§2）是否可接受，还是要求逐 cell 几何（成本高、收益在晴天边缘/侧面阴影）。
- 云纹理是否需要在资源缺省时回退（无 clouds.png 时不画云）。
- 是否接入 `MC_VIEW_DISTANCE` 控制云绘制半径（默认跟视距）。
