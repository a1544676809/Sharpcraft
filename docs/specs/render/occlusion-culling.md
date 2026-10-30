<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: GPU 侧遮挡剔除（规格）
summary: Hi-Z 两阶段方案、分期、预算与验收；不依赖硬件遮挡查询
status: 草稿
-->

# GPU 侧遮挡剔除

加入计划：2026-10-30（所有者要求）。

## 1. 为什么选 Hi-Z 而不是硬件遮挡查询

WebGPU 的遮挡查询能力受限且跨平台不一致；**Hi-Z（分层深度）+ compute 剔除**只依赖 compute 与 indirect draw，
两者都是当前后端的核心能力，且能用「与未剔除参考帧逐像素一致」来验收。**不需要为它引入能力门禁。**

## 2. 方案

| 阶段 | 内容 | 输出 |
| --- | --- | --- |
| 1. 深度金字塔 | 上一帧深度做 2x2 下采样，逐级取最远深度，直到 1x1 | hiZ mip 链（compute） |
| 2. 剔除 | 每个候选区块/实体的 AABB 投影成屏幕矩形，用最近深度与该矩形覆盖区域的最远深度比较 | 可见列表 + indirect 计数 |
| 3. 绘制 | drawIndexedIndirect，CPU 不回读 | 单次提交 |

保守性：只允许漏剔（保留本来被挡住的），**不允许误剔**（会露洞）。

## 3. 分期

| 期 | 交付 | 出口判据 |
| --- | --- | --- |
| O1（已完成数学） | `OcclusionCulling.TryProjectBox` / `IsOccluded` | 5 项单测 |
| **O1.5（已完成）** | **CPU 参考实现** `DepthPyramid.Build`（逐级 2x2 取最远）+ `TryCoveringDepth`（选最细且单 texel 覆盖矩形的层级）| 4 项单测：层级数、最远子节点、覆盖矩形取值、退化矩形拒绝 |
| **O1.7（已完成）** | **深度纹理可回读**（`RenderAttachment | CopySrc`，行按 256 字节对齐）| 实测 960x540：`nearest 0.9986 / farthest 1.0000 / centre 1.0000` |
| **O1.8（已完成）** | **深度线性化** `DepthLinearisation`（`ToDistance`/`ToDepth`/`InPlace`，默认近 0.1 / 远 2000）| 4 项测试（端点、典型值 0.9986 → 40–90 方块、互为逆、单调）；实测 `nearest 0.9986 → 70.3 方块` |
| **O2.1（已完成）** | `pipelines/depth_pyramid.wgsl`（compute，`cs_downsample`，workgroup 8×8，binding 4 输入/5 输出/6 参数）+ 登记进 `ShaderPipelines`；参考实现 `DepthPyramid` 迁入 Framework 便于同处比对 | 参考 4 项测试；着色器通过启动期校验 |
| **O2.2（已完成，第 1 层）** | 离屏路径实际 dispatch：深度双拷贝（CPU 参考一份、compute 一份）、参数 uniform（宽/高/行跨度/近远）、`CopyBufferToBuffer` 回读 | 实测 480×270 第 1 层 **mismatches 0、worst delta 0.000000**（容差 0.05 方块，float32 在 2000 方块的量化极限）|
| O2 | GPU compute 版金字塔（输入为**线性化距离**），与 O1.5 逐 texel 一致 | 任意深度图下与 CPU 参考完全相同 |
| O3 | 剔除 compute 产出可见列表（先 CPU 回读） | 与不剔除参考帧逐像素一致，剔除率达标 |
| O4 | indirect draw，去掉回读 | 每帧提交次数仍为 1；剔除率与 O3 相同 |

## 4. 预算（QA-1.4）

| 指标 | 阈值 |
| --- | --- |
| Hi-Z 构建 | <= 0.5 ms/帧 |
| 剔除计算 | <= 0.5 ms/帧 |
| 剔除率（放大世界） | >= 60% |
| 正确性 | **0 处误剔** |

## 5. 验收方式

1. 固定位姿的离屏帧：cull=off 与 cull=on 逐像素比较，差异必须为 0；
2. 连续 32 帧移动序列，任一侧差异即失败；
3. 剔除率与耗时写入基准表。

## 6. 与其它模块的关系

- 着色器包 `occlusion.culling` 指令：实现后可如实响应。
- 顺序：先 Hi-Z 剔除决定可见集合，再生成运动矢量（仅可见像素）。
- 与 CPU 侧 `ViewFrustum` 叠加：视锥剔除先行，Hi-Z 二次剔除。

### 精度观察（重要）

回读实测：960×540 场景中深度落在 **0.9986–1.0000**。这是标准投影（近 0.1 / 远 256）的必然结果——深度按 `1/z` 分布，
远处几乎饱和到 1.0，**用它做遮挡比较的分辨率极差**。

⇒ O2 的金字塔应以**线性化深度**（`(near*far)/(far - d*(far-near))` 的倒数形式）构建，而不是直接用 NDC 深度；
否则"最远深度"在远处几乎无差别，剔除判定会退化为随机。这条在实现 compute 版前必须落实。

### O2.2 过程中遇到的四条真实约束（记录以免重犯）

1. `MAP` 用途只能与**单一方向**的 `COPY` 组合 ⇒ 回读缓冲与存储缓冲必须分开（多一次 `CopyBufferToBuffer`）。
2. 缓冲区不能同时既 `MapRead` 又作 `CopySrc` ⇒ 深度纹理**拷贝两次**（CPU 参考一份、compute 一份），而不是缓冲区互相拷。
3. WGSL 中 `target` 是**保留字** ⇒ 改名 `destination`。
4. 金字塔必须在**距离**域比较：着色器内做线性化（`near*far/(far - d*(far-near))`，`d>=1` 取 `far`），否则第 1 层全是 1.0。
