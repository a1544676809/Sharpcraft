<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 渲染性能与等效性优化计划
summary: 云 OIT/云几何、OIT 目标、天体与绘制路径的分阶段优化与验收标准
status: 已评审
updated: 2026-11-01
-->

# 渲染性能与等效性优化计划

依据：`docs/specs/render/known-gaps.md`、ADR-0020、`docs/modules/clouds.md`、`docs/specs/render/oit-reference.md`。
原则：先纯逻辑/可量测，再像素对照；每阶段 build + test + docs 门禁 + traceability；不改变已通过的天体/云默认观感（除非开关显式选择）。

## 阶段 1（最高收益）：cloud OIT 常驻几何 + 专用着色器

**问题**：`CloudOitResources.Update` 每帧把 7-float 云顶点重新展开成 14-float 地形顶点并整块上传；128 区块时是 O(顶点数) CPU + GPU 带宽。

**做法**：
1. 新增 `cloud_oit.wgsl`：输入 7-float（position3 + colour4），用 uniform transform（mat4 model + vec4 tint）把相对坐标变换到世界；alpha 也用 vertex colour.a。
2. `CloudOitResources` 在 `CloudDrawCache` 变更时才重建 vertex/index buffer；每帧只写 transform uniform。
3. OIT 三阶段（depth bounds/transmittance/accumulate）使用该 shader 的 pipeline，仍复用现有 OIT 目标与 group1 绑定。
4. 保留 `MC_CLOUD_OIT=0` 回退；`MC_CLOUD_OIT=1` 默认走新路径。

**验收**：
- 顶点上传从“每帧 O(顶点数)”降为 0（仅 uniform）；同一机位帧时间下降明显；
- OIT 云颜色/遮挡与当前版本像素一致（固定机位对照）；
- 全量测试通过。

## 阶段 2：云几何构建与自适应范围

**做法**：
- 将 `CloudMeshBuilder` 的环形遍历按半径分块，支持增量/预算构建，避免单帧尖峰；
- `MC_CLOUD_RANGE` 支持自适应：根据帧时间/显存预算在 64/96/128 间调整（显式记录的受控偏差）；
- 评估 LOD（远处更大 cell）仅作为可选开关，默认关闭以保持等效。

**验收**：128 区块首次构建尖峰明显下降；帧时间/内存曲线记录在 `benchmarks/`；等效开关默认行为不变。

## 阶段 3：OIT 目标生命周期与显存

**做法**：
- OIT 目标仅在“有半透明地形或云 OIT”时创建；窗口关闭/切换关闭时释放；
- 高分辨率下提供半分辨率/低精度目标选项（显式开关）；
- 记录显存峰值与目标格式对画质影响。

**验收**：无半透明场景不再分配 OIT 目标；2560×1334 云 OIT 不 OOM。

## 阶段 4：独立 cloud depth 与像素等效

**做法**：
- 按原版 `CloudRenderer.renderOit` 使用独立 cloud depth texture 与 cloud OIT 参数（而非复用 terrain depth）；
- 固定机位/时间/视距的参考帧像素对比（云边界、透明排序、天体遮挡）；
- 记录与当前共享目标的差异；若差异可接受，保留开关选择。

**验收**：像素差异报告；默认选择文档化。

## 阶段 5：天体/天空与绘制路径

- 天体几何按“时间/天气变化”缓存，仅相机位移更新 uniform；天空盘/地平线渐变补齐；
- 视距 32 的 draw call 若成为瓶颈，评估分页 arena + GPU 间接剔除（ADR-0020 备选方案），默认保持每区块缓冲。

## 度量与记录
- 每阶段记录：帧时间（avg/p95/worst）、RSS、GPU dedicated/shared、云顶点数与上传量（`window: ...` 日志）；
- 对照场景固定：960×540 / 2560×1334，视距 12/32，`MC_CLOUD_MODE=fancy`、`MC_CLOUD_OIT=1`；
- 任何行为偏差进入 `docs/risks.md` 或 known-gaps，不得静默接受。

## 风险
- OIT 共享目标与独立 cloud depth 的视觉差异；
- 128 区块 CPU/GPU 内存与构建时间；
- 自适应范围引入的非确定性观感（必须可关闭并记录）。

## 阶段 4 详细设计（独立 cloud depth OIT）

目标：不再让云与半透明地形共享同一 depth 与同一 OIT 分辨率；按原版 `CloudRenderer.renderOit` 为云单独走三阶段，再在 terrain composite 之后单独 composite 云。

1. **目标**：新增 `cloudDepthTexture`（Depth32Float，随窗口 resize 重建）；`cloudDepthBounds`（RGBA16F，MAX）、`cloudCoeff0/1`（RGBA16F，ADD）、`cloudAccum`（RGBA16F，ADD）。可复用尺寸/格式辅助函数。
2. **深度初始化**：在每个云 OIT 帧开始，用一次全屏 blit 将 terrain 主深度复制到 `cloudDepthTexture`（`BLIT_DEPTH`），后续云 OIT 用 `cloudDepthTexture` 作为 depth attachment；这样云与地形深度解耦。
3. **三阶段**：使用现有 `cloud_oit.wgsl` 与 cloud pipeline，分别渲染到 cloud 目标；depth bounds 阶段前绘制主深度 blit（与地形 OIT 的 `BLIT_DEPTH_DURING_DEPTH_BOUNDS` 等价）。
4. **cloud composite**：新增 cloud 版 `oit_composite` pipeline/bind group，读取 cloud depth bounds/coeff/accum，在 terrain composite **之后**、正常云 pass 之前合成；`MC_CLOUD_DEPTH=1` 开启，默认 0（沿用当前共享目标路径，避免观感回退）。
5. **像素对照**：固定机位/时间/视距，`MC_CLOUD_DEPTH=0/1` 各出一帧，记录差异（云与水面交叠、云边缘、深度排序）；无法自动取窗口像素时由用户截图记录。
6. **验收**：默认路径不变；开关开启时无 GPU 校验错误；像素差异报告写入 `benchmarks/` 或 STATE；全量测试通过。
