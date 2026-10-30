<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: GUI 贴图与九宫格 —— 提示框背景
summary: 提示框底色/边框来自两张 nine_slice 贴图，含几何与拉伸规则
status: 已核对（行为级）
updated: 2026-10-30
-->

# GUI 贴图与九宫格：提示框背景

证据指针：`ref://mc-26.3-analysis/src/net/minecraft/client/gui/screens/inventory/tooltip/TooltipRenderUtil.java`

## 1. 行为规格（不含实现）

提示框的外框与底色**不是**矩形填充，而是叠加两张 GUI 贴图（sprites）：

| 贴图 | 用途 | 绘制顺序 |
| --- | --- | --- |
| `minecraft:tooltip/background` | 深色底 | 先 |
| `minecraft:tooltip/frame` | 外框（含黑边与紫色内框） | 后 |

几何（均为 **canvas 像素**，`x,y,w,h` 为文字内容区）：

- 贴图左上角：`x - 3 - 9`，`y - 3 - 9`
- 贴图尺寸：`w + 3 + 3 + 18`，`h + 3 + 3 + 18`
- 由此推得：内容区四周先留 **3** 的内边距（`PADDING`），贴图再向外扩 **9**（`MARGIN`）。

九宫格拉伸（frame）：`border = 10`、`stretch_inner = true`。四角**不拉伸**，因此观感上外框四角是"缺像素/圆角"而非直线。
这就是"矩形画不出来"的原因。

其它常量：`MOUSE_OFFSET = 12`（提示框相对指针的默认偏移）。

## 2. 资源事实（原版 jar 内实测）

| 路径 | 大小 |
| --- | --- |
| `assets/minecraft/textures/gui/sprites/tooltip/background.png` | 126 B |
| `assets/minecraft/textures/gui/sprites/tooltip/background.png.mcmeta` | 165 B |
| `assets/minecraft/textures/gui/sprites/tooltip/frame.png` | 542 B |
| `assets/minecraft/textures/gui/sprites/tooltip/frame.png.mcmeta` | 201 B |

`frame.png.mcmeta` 实测：

```json
{ "gui": { "scaling": { "type": "nine_slice", "width": 100, "height": 100, "border": 10, "stretch_inner": true } } }
```

`background.png.mcmeta` 尚未读取，按同一机制处理；提取实现时须一并核对（记为待确认项）。

## 3. 本仓库现状（差距）

- 资产提取**不含** `textures/gui/sprites/**`：资源包里没有这两张图。
- 渲染层**没有** GUI sprite 加载器，也没有 `nine_slice` 绘制能力。
- 现有 `Tooltip` 用矩形近似：无黑边、无缺角，必然与原版不符。

## 4. 影响与风险

- 需要新增"GUI 贴图 + 九宫格"这一渲染能力，属跨模块特性，需 ADR（`0014`）。
- 章节规格：`docs/modules/ui.md` §8.8（待评审）。
