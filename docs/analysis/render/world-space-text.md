<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 世界空间文字渲染调查
summary: 原版世界空间文字（名字牌、告示牌、文本展示实体等）的提交契约、坐标、字体、背景、朝向与光照语义
status: 草稿
updated: 2026-11-02
-->

# 世界空间文字渲染调查

本文件记录对 Minecraft 26.3 世界空间文字渲染的行为级调查，作为实现 `WorldTextRenderer` 的规格输入。范围不限于玩家名字牌：凡是**文字四边形随 Pose 进入三维世界**的用途都属于本主题；屏幕空间聊天、HUD、工具提示不在范围内。

## 1. 用途清单

| 用途 | 原版提交点 | 关键空间参数 |
| --- | --- | --- |
| 实体名字牌 | `EntityRenderer.submitNameDisplay` → `SubmitNodeCollector.submitNameTag` | 实体附着点 + 0.5 Y，旋转到相机，缩放 0.025 |
| 告示牌文字 | `AbstractSignRenderer` → `submitText` | 方块局部 Pose，行高来自 `SignRenderState.textLineHeight` |
| 文本展示实体 | `DisplayRenderer` → `submitText` | 局部 Pose 旋转 π/缩放 -0.025，按对齐方式偏移 |
| 地图上的文字 | `MapRenderer` → `submitText` | 地图纹理/局部 Pose（后续地图切片） |
| 其他实体自定义名/分数 | `EntityRenderer.submitNameDisplay` 同名通道 | 与名字牌共用提交路径 |

## 2. 公共提交契约

`SubmitNodeCollector.submitText(...)` 最终生成 `TextFeatureRenderer.Content.Text`，字段语义为：

- `x`, `y`：文本局部坐标（字体像素单位，不是屏幕像素）；
- `FormattedCharSequence string`：带样式/颜色的字符序列；
- `dropShadow`：是否绘制阴影；
- `color`：文字 ARGB（alpha 参与混合）；
- `backgroundColor`：背景 ARGB；alpha 为 0 表示无背景；
- `outlineColor`：轮廓色；0 表示无轮廓；
- 另有 `displayMode`、`lightCoords` 和提交时的 `pose`。

`TextFeatureRenderer` 的渲染顺序是：

1. `backgroundColor` 非透明时，先提交一个覆盖文本包围盒的**背景 effect**（z 略小于文字，原版为 -0.01），再提交 glyph；
2. glyph 按字体准备结果逐个提交，使用同一个 Pose 和光照；
3. `outlineColor` 非 0 时，先画 8 邻域轮廓（`prepare8xTextOutline`），再画主体文字；
4. `displayMode` 可为 `NORMAL`、`SEE_THROUGH`、`POLYGON_OFFSET`；`SEE_THROUGH` 用于名字牌先画遮挡可见副本的场景；
5. 颜色按文本顶点色与 alpha 混合，阴影是默认字体阴影效果，不是独立后处理。

## 3. 字体与背景几何

- `Font.prepareText` 负责把字符序列变成 glyph 列表，同时按字符 advance 计算**背景包围盒**：背景左/上为 `x - 1, y - 1`，右/下由最后一个字符的 advance 与字体高度（9 像素）决定。
- 背景不是固定矩形：它跟随实际文本宽度和行高；多行文本每行各自计算包围盒（告示牌/文本展示实体按行提交）。
- glyph 使用字体图集；字符 advance 可小于 8 像素（例如空格 4 像素），所以**文本宽度必须使用 `Font.width` / advance 度量**，不能用 cell 宽度乘字符数。
- 字体存在双向文本 shaping 与样式（粗体右移重绘、颜色继承）；这一点在 UI 文本实现中已有部分，但世界文字需要相同的字符序列→glyph 转换。

## 4. 名字牌（EntityRenderer / SubmitNodeCollector）

原版名字牌的行为参数：

- 锚点：`nameTagAttachment.y + 0.5`（实体的名字牌附着点，不是脚点 + 固定值）；
- 姿态：每帧绕相机朝向旋转（billboard），然后 `scale(0.025, -0.025, 0.025)`；Y 取负是因为字体像素 y 向下、世界 y 向上；
- 水平位置：`x = -font.width(name) / 2`，即按实际字体宽度居中；
- 背景 alpha：`options.getBackgroundOpacity(0.25)`，默认 **0.25**；背景色 `ARGB(backgroundAlpha, black)`；
- 文字 alpha：`max((backgroundAlpha + 0.75) * 0.5, 0.5)`，默认 **0.5**；文字色 `ARGB(textAlpha, white)`；
- 阴影：名字牌调用 `Content.Text(..., dropShadow=false, ...)`，**不画阴影**；
- 可见性：`seeThrough` 为真时先提交 `NORMAL` 文字，再提交 `SEE_THROUGH` 文字（同一内容、同一 Pose），从而实现透过方块可见；
- 排序：`TextFeatureRenderer.Submit.distanceToCameraSq()` 参与半透明提交排序。

## 5. 告示牌与文本展示实体

- 告示牌：
  - 行宽居中：`x = -font.width(line) / 2`；
  - y：`lineIndex * textLineHeight - signMidpoint`，行高来自 `SignRenderState.textLineHeight`；
  - 使用 `POLYGON_OFFSET` 避免与木板表面 z-fighting；
  - 发光文字使用固定亮度和轮廓色；普通文字使用方块光照并压暗颜色；
  - `backgroundColor = 0`（告示牌本身无文字背景）。
- 文本展示实体（TextDisplay）：
  - 局部 Pose 旋转 π（面向观察者）后 `scale(-0.025, -0.025, -0.025)`；
  - 行高 10（9 像素字高 + 1 行距），按 LEFT/CENTER/RIGHT 计算每行偏移；
  - `backgroundColor != 0` 时先提交 `submitTextBackground`，覆盖整个文本块；
  - 文字 opacity 由实体数据低 24 位 alpha 控制，`shadow` 由实体数据决定。

## 6. 与当前 Sharpcraft 实现的差距

当前窗口路径的名字牌走的是 UI 路径：

- `NameplateProjection.TryProject` 只产出**归一化屏幕坐标 + 深度**；
- `WindowCommand.uiGeometry` 用 `UiScreenLayout.AddText` 在屏幕空间绘制，字号/位置用临时距离缩放近似；
- 这不可能复现原版的相机朝向 billboard、世界尺度 0.025、字体宽度居中、背景包围盒和透视缩放；
- 当前 UI 字体路径只能处理屏幕空间文本，不能让文字随世界位置旋转/缩放。

**结论**：应当新增独立的 `WorldTextRenderer`（或等价的世界文字 pass），把字体图集、glyph 四边形、背景 effect 和 Pose 一起提交到三维管线；UI 文本继续服务 HUD/聊天，两者共享字体度量但渲染通道分开。

## 7. 实现规格与验收

实现应至少满足：

1. **Pose 驱动**：输入 `Matrix4x4 pose`（相机相对或世界空间）、字符串、颜色、背景色、阴影、显示模式、光照；
2. **字体度量**：宽度用真实 advance，换行/多行按行计算，背景覆盖实际包围盒；
3. **名字牌**：相机朝向 billboard，锚点 `attachment + 0.5`，`scale=0.025`，默认背景 alpha 0.25、文字 alpha 0.5、无阴影；see-through 两遍语义；
4. **告示牌/文本展示**：复用同一世界文字渲染器，传入各自的 Pose、行高、对齐和背景/轮廓参数；
5. **测试**：固定相机下比较四角投影、文本宽度、背景矩形尺寸、名字牌锚点；至少覆盖名字牌、告示牌、文本展示三条路径；
6. **性能**：每帧提交的 glyph 四边形复用顶点缓冲；世界文字与 UI 文本共享字体图集，不重复上传。

## 8. 证据

- `ref://mc-26.3-analysis/src/net/minecraft/client/renderer/feature/TextFeatureRenderer.java`
- `ref://mc-26.3-analysis/src/net/minecraft/client/renderer/SubmitNodeCollection.java`
- `ref://mc-26.3-analysis/src/net/minecraft/client/renderer/entity/EntityRenderer.java`
- `ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/AbstractSignRenderer.java`
- `ref://mc-26.3-analysis/src/net/minecraft/client/renderer/entity/DisplayRenderer.java`
- `ref://mc-26.3-analysis/src/net/minecraft/client/gui/Font.java`

## 9. 补充：glyph 四边形生成（BakedSheetGlyph）

- 每个 glyph 的四边形不是“cell 矩形”：由 `GlyphInfo` 的 `left/right/up/down` 决定；
- 粗体向右/向下扩展 `0.1` 像素；斜体按 `up/down` 做 `1 - 0.25 * metric` 的剪切；
- 阴影通过 `shadowOffset` 额外提交一份 glyph（原版在 `renderChar` 中先画阴影，再画正文）；
- 背景不是固定 cell：`Font.PreparedTextBuilder.markBackground` 用实际 advance 与字体高度 9 计算包围盒，背景 effect 放在 z=-0.01；
- 因此，正确实现必须从字体度量生成**每个 glyph 的四角**，不能用 UI 的 `AddText`/`AddRectangle` 逐字近似。

当前 `NameplateProjection + UiScreenLayout.AddText` 的名字牌仅作为临时观感验证，**应被世界文字 pass 替换**；告示牌/文本展示实体也不能用 UI 屏幕文本实现。
