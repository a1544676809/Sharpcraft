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
