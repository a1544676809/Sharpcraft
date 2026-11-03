<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# ADR-0021：实体多 RenderLayer 提交（替代单 geometry 单 draw）

状态：已接受（2026-11-03，所有者确认按两步实施；Emissive eyes 留待后续）
相关：`docs/analysis/render/entity-render-layers.md`、`docs/modules/entity-render.md` §9、ADR-0020

## 背景

当前实体渲染把基础模型与所有 overlay/外层（Player hat/jacket/sleeves/pants、Skeleton clothing、
Drowned outer、Slime outer、未来的 Enderman eyes）合并进一个 `DynamicGeometry`，用一条
entity pipeline 一次 draw。原版是“一个基础模型 + 零到多个 RenderLayer，每层有独立 RenderType、
纹理、可见性、顺序、深度/混合状态”。合并实现只能算静态观感近似：透明叠层、衣物/盔甲重叠、
发光眼睛都会有可观察偏差。

## 决策

引入**分层提交**契约，分两步落地：

1. **客户端层拆分（无渲染行为变化）**：`EntityModelMesher` 增加 `BuildLayers`，按 `EntityLayerRenderKind`
   返回多个 `EntityGeometryLayer`（每个自带可复用的 `DynamicGeometry`）；`Build` 保留为“把所有层拼成一份”的兼容入口。
   层的来源：基础部件 → `Cutout`；overlay/外层部件按定义声明 `Kind`（Player overlays / Drowned / Slime = `Translucent`，
   Skeleton clothing = `Cutout`）。这一阶段不改变现有画面。
2. **框架分层绘制**：`WindowedRenderer` 接受 `entityLayers`，为每层维护常驻缓冲并按 `Kind` 选 pipeline：
   `Cutout`（alpha test、无混合、深度写）、`Translucent`（混合、深度写关、按层顺序）、
   `Emissive`（eyes：深度 `>=`、不写、加色；单独一步，避免一次改动过大）。
   多个层的绘制顺序由 `Order` 决定；同一层内仍然一次 draw。

## 备选与被否决

- **每层独立 geometry + 独立 pipeline，但只加 `Translucent`**：不覆盖 Skeleton clothing（cutout），会引入新的偏差，否决。
- **保留单 draw，用顶点属性编码 RenderType**：需要逐片元分支/多套状态，等价于在 shader 内重造 pipeline 状态机，复杂且不可诊断，否决。
- **一次把 Emissive 也做进来**：eyes 需要新的深度/混合状态与纹理，风险集中；拆成两步，先让衣物/外层正确。

## 后果

- 渲染器多一层“按 entity layer 选择 pipeline + 顺序提交”的代码；每层一份常驻缓冲（层数很小，≤ 4）。
- `FrameRequest`/`WindowedRenderer.Run` 新增可选 `entityLayers`；旧的 `dynamicGeometry` 单层路径保留给占位盒体。
- 验收（第一步）：现有画面逐像素不变（回归）；`BuildLayers` 单测断言每层几何与层标签。
- 验收（第二步）：Skeleton clothing / Player overlay / Drowned / Slime outer 的层标签、顺序、混合状态可断言；
  与合并路径的截图差异只出现在这些层的透明/重叠区域。
