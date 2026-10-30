<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# ADR-0014：GUI 贴图与九宫格渲染

- 状态：**提议（待评审）**
- 日期：2026-10-30
- 相关：原则 3（不提交原版资产）、原则 6（渲染后端隔离）、原则 9（行为对齐）

## 背景

提示框（tooltip）在原版由两张 GUI 贴图叠成：`tooltip/background` 与 `tooltip/frame`，后者为
`nine_slice`（`border = 10`、`stretch_inner = true`）。矩形填充无法复现其黑边与四角形状。
本仓库当前既未提取 `textures/gui/sprites/**`，也无九宫格绘制能力。

## 决策（提议）

1. **资产**：提取器新增 `textures/gui/sprites/**`（PNG + `.mcmeta`），产物仍位于仓库外的资源包，仓库只存清单与提取器代码（原则 3）。
2. **加载**：`Client` 侧新增 GUI 贴图加载器，读取图片与 `nine_slice` 描述（type/width/height/border/stretch_inner）。
3. **渲染**：实现九宫格绘制（四角原尺寸、四边单向拉伸、中心双向拉伸），作为通用 UI 能力，供 tooltip / 按钮 / 面板等复用。
4. **不受支持即降级**：`mcmeta` 缺失或类型不认识时，回退为按原尺寸平铺并在诊断中记录，不崩溃。

## 实现步骤（已核对代码结构）

`WindowedRenderer` 的 UI 绘制循环已是“按 `Range.Material` 切换 pipeline + bind group”，因此贴图材质不需要新架构：

1. `UiMaterial` 增 `Sprite`；`UiGeometry.Range` 增 `SpriteIndex`；新增 `AddSpriteRectangle`（切换材质并记录索引）。
2. 新 shader 采样 RGBA（不是字形那样只取 alpha）再乘顶点色；`UiRenderer.CreateSpritePipeline` 复用 text 布局。
3. `WindowedRenderer` 接收 `IReadOnlyList<TextureData>`，为每张建 texture + bind group；循环里 `Sprite` 分支按 `range.SpriteIndex` 切换。
4. `Client` 从资源包读 `tooltip/background|frame.png` 与 `.mcmeta`（`GuiSpriteScaling` 已就绪），组成贴图列表传入。
5. `Tooltip` 用 `NineSlice.Cells` + `AddSpriteRectangle` 画两张图（几何 `x-3-9 / w+6+18`，文字内边距 3）。

边界：贴图列表可为空，此时 `Sprite` 材质不使用，`Tooltip` 回退到现有矩形实现。

## 影响

- `Tooltip` 改为"文字内容区 + 3 内边距 + 9 贴图边"，并按 `x-3-9 / w+6+18` 定位。
- 后续 `按钮/文本框/面板` 可复用同一九宫格能力，避免各自画矩形。

## 备选（未采纳）

用矩形 + 手绘斜角近似：无法复现贴图特有的边角像素，且每个控件都要单独调参。

## 回退

新增能力独立于既有 UI 路径；`Tooltip` 保留矩形实现作为回退分支，切换只需一处。
