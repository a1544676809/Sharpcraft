<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 中期目标 —— 可验证画面基线 + 可加载真实着色器包
summary: 一段以证据定义的阶段目标：确定性基线、完整阶段、真实包可加载、性能门槛
status: 草稿
-->

# 中期目标（2026-10-30 提出）

现有里程碑（M1–M6，见 `roadmap.md`）是**能力闭环**；本文件补一个**横跨 P1–P3 的验收型目标**，
用来回答"渲染这块什么时候算真的做成了"。它是阶段目标，不替代里程碑。

## 优先级（2026-10-30 所有者补充）

**以还原原版着色器为标准**：先把我们自己的管线做到与原版可观测行为一致（天空/雾、方块、实体、半透明、光照）。
**着色器包不是第一优先级**，排在原版等效之后；它的接入点是"已预留但后做"。

## 一句话

**在确定性场景上，用真实着色器包画出可逐像素验证的画面，并且性能达标。**

## 出口条件（五条，全部要有证据）

| # | 条件 | 证据 |
| --- | --- | --- |
| 1 | **确定性基线**：固定场景（录制区块 + 钉死相机 + 冻结实体）连跑两次，`frame` 输出**逐像素 0 差异** | `scene:` 指纹相同 + `compare: identical`，写入 `benchmarks/render/` |
| 2 | **阶段完整**：天空与雾、不透明、方块实体、实体、半透明五个阶段可用，运动矢量可输出 | 出图 + 阶段开关对照 |
| 3 | **遮挡剔除**：Hi-Z 达 O3 出口（与不剔除参考帧逐像素 0 差异，剔除率 ≥60%）| QA-1.4 |
| 4 | **真实包可加载**：Complementary Unbound r5.9.3 的主干阶段子集可编译运行；不支持特性按 `BackendCapabilities` 拒绝并列出；失败回退内置管线不崩 | 包加载日志 + 回退测试 |
| 5 | **性能**：放大世界主循环 p99 ≤ 16.7 ms，剔除 ≤ 1 ms/帧，每帧场景提交 = 1 | `window:` 帧统计 + QA-1.3/1.4 |

## 为什么先要第 1 条

2026-10-30 实测：同一条 `frame 127.0.0.1:25565` 连跑两次，场景指纹 `0xE5A2…` 与 `0xAD10…` **不同**，
说明**世界本身每次都不同**（生成点/区块集合由服务器决定）。没有固定场景，第 3、4、5 条都无法验收。

## 关键路径（按依赖排序）

1. 录制区块 → 回放（`frame local` 走录制数据）+ 钉死相机 + 冻结实体 ⇒ 目标 1
2. 运动矢量 pass（数学已就绪）⇒ 目标 2 的一部分
3. Hi-Z O2 → O3（金字塔、剔除）⇒ 目标 3
4. 实体模型（P1 剩余，需 jar 提取模型 JSON）⇒ 目标 2
5. 着色器包加载器（维度×阶段、特性检查、回退）⇒ 目标 4

## 不做

- 不做动画骨骼（P1 已裁决排除）、不做 Vulkan 后端（ADR-0018 未批准实现）、不做帧生成（WebGPU 呈现模型不支持）。

## 全量复测记录（2026-10-30，第 36 轮）

同一次会话内**逐项重跑**，命令与输出原文如下（`MC_ASSETS_OUT=%TEMP%\mcs-assets-pack`、`MC_PACK_FORMAT=97.1`、
回放 `%TEMP%\scene5.bin`）：

| # | 检查项 | 命令要点 | 实测输出 | 判定 |
| --- | --- | --- | --- | --- |
| 1 | 确定性场景 | `frame replay` + `MC_FRAME_COMPARE` | `scene: chunks 9, sections 24, fingerprint 0xD9F5D3A48E05E9A3`；`compare: identical (max channel delta 0)` | ✅ |
| 2 | Hi-Z 金字塔 | `MC_FRAME_PYRAMID=1` | `9 levels, total mismatches 0, worst delta 0.000000`（9 级全 0）| ✅ |
| 3 | 运动矢量 | `MC_FRAME_MOTION=1` | `14 samples, mismatches 0, worst delta 0.0000 px` | ✅ |
| 4 | 遮挡剔除 | `MC_FRAME_OCCLUDER=1 MC_FRAME_CULLTEST=1` | `dropped 12 of 20, pixel mismatches 0`（**60.0%**，零误剔）| ✅（刚好达标）|
| 5 | 半透明 | `MC_FRAME_WATER=1 MC_FRAME_TRANSLUCENT=1 MC_FRAME_TRANSLUCENT_TEST=1` | `10 triangles, pixels changed 97344, first (324,114)` | ✅ |
| 6 | 方块实体 | `MC_FRAME_OCCLUDER=1 MC_FRAME_BLOCK_ENTITIES=1` + 比对 | `1 boxes, 12 triangles`；`329 pixel(s) differ, first (461,68), max delta 166` | ✅ |

**仍需收口的偏差（不计为已达成）**：

1. 剔除率 **60.0%** 是"刚好"达标，且场景只有 20 个区块；更大世界的剔除率未测。
2. 半透明**按距离降序**尚未接线（当前透明层是一次 draw），混合顺序正确性缺证据。
3. 方块实体缺**数据包级**解析用例（T-240 仍为进行中）。
4. 实体仍是 6 个盒体占位（尺寸已按参考校准），**纹理面与真实模型**未做。
5. 天空仅渐变与雾参数对齐，**天体（日/月/星/日出）**未做。

## 达成判定（2026-10-30 第 40 轮，最终）

逐条对照出口条件，**结论：未达成**。五阶段管线各自"跑得起来、有证据"，但三条有实质缺口。

| 出口条件 | 状态 | 证据 / 缺口 |
| --- | --- | --- |
| 确定性场景（录制/回放）| ✅ 达成 | `fingerprint 0xD9F5D3A48E05E9A3`，`compare: identical (max delta 0)` |
| ① 天空与雾 | 🟡 部分 | 渐变色与雾参数已对齐并有像素采样；**天体（日/月/星/日出）未做** |
| ② 不透明地形 | ✅ 达成 | 确定性帧 + 纹理采样 + 面 UV 对齐 |
| ③ 方块实体 | 🟡 部分 | 解析 + 绘制，实测 `329 像素不同`；**缺数据包级解析用例** |
| ④ 实体 | 🟡 部分 | 6 部件、尺寸按参考校准；**无真实模型与纹理** |
| ⑤ 半透明 | 🟡 部分 | 分类 + 双网格 + 混合（`0.7w+0.3bg` 误差 ≤1）+ 降序接线；**排序效果未证明**（场景仅 1 个透明区块）|
| 运动矢量输出 | ✅ 达成 | `14 samples, mismatches 0, worst delta 0.0000 px` |
| Hi-Z 遮挡剔除至 O3 | ✅ 达成（薄）| `9 levels, mismatches 0`；`dropped 12 of 20`（**60.0%**，刚好达标）、零误剔、双向强制对照 |
| 着色器包接入 | ⏸ 未开始 | 按优先级排在原版等效之后，且原版等效尚未收口 |

**判定**：目标**保持未完成**。三条部分达成项（③④①）与半透明排序证据是下一步的明确清单；
不把"带缺口的阶段"记成达成，是这份文档存在的意义。

## 全面验证（2026-10-30 最终轮）

### 一、工程状态

| 检查 | 结果 |
| --- | --- |
| 全量构建（warnings as errors）| **0 warning / 0 error** |
| 单元测试（7 个测试工程）| **1074 通过 / 0 失败** |
| 文档门禁（索引 + 覆盖率 + SPDX + 漂移）| `source coverage: ok`、`generated indexes: ok` |
| 仓库卫生 | 无 jar / png / ogg / nbt；`docs/analysis/assets/` 下仅 8 个 `.md`；无机器绝对路径 |
| 工作区 | 干净（提交后 0 dirty）|

### 二、六项测量（本轮全部重跑）

| # | 项 | 结果 |
| --- | --- | --- |
| 1 | 确定性 | `fingerprint 0xD9F5D3A48E05E9A3`，`compare: identical (max delta 0)` |
| 2 | 金字塔 | `9 levels, total mismatches 0, worst delta 0.000000` |
| 3 | 运动矢量 | `14 samples, mismatches 0, worst delta 0.0000 px` |
| 4 | 剔除 | `dropped 12 of 20, pixel mismatches 0`（60.0%，需 `MC_FRAME_DEPTH=1` 才有深度数据）|
| 5 | 半透明 | `10 triangles, 97344 px changed @ (324,114)`；alpha 0.7 探针 `rgba(51,62,85)` vs alpha 1.0 `rgba(15,12,12)` |
| 6 | 方块实体 | `1 boxes, 12 triangles`；`311 pixel(s) differ @ (461,70)` |

### 三、原版等效性核查

见 `docs/analysis/render/vanilla-equivalence-check.md`。本轮**发现并修正一处真偏差**：箱子占位整体抬高 1/16，
与参考 `ChestModel` 的 `addBox(1,0,1,14,10,14)` + `PartPose.ZERO`（贴地）不符；改为贴地后证据由 329 → 311 像素、首个差异由 (461,68) → (461,70)。

### 四、仍未达成的项（不因本轮验证而改变）

方块实体数据包级用例、半透明排序效果证明、实体真实模型与纹理、天空天体、剔除率余量。
**目标仍未完成。**
