<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 渲染已知缺口与待办（backlog）
summary: 当前渲染管线尚未做或仍为受控偏差的事项清单（几何/天空/雾/云/性能）
status: 草稿
updated: 2026-11-01
-->

# 渲染已知缺口与待办

本文件登记尚未完成或仍为受控偏差的渲染事项；完成一项后从此表移除并写入 traceability/STATE。

## 1. 几何 / 区块管理

| 项 | 现状 | 影响 | 建议 |
| --- | --- | --- | --- |
| **区块卸载** | `ClientWorld` 不会移除离视距区块；`ChunkGeometryStore.Remove` 已具备但无人调用 | 长时间大范围移动会持续累积世界与显存（无上限增长） | 玩家跨区块时按视距+余量卸载 `ClientWorld.RemoveChunk`，同步 `ChunkMeshCache` 删条目、`ChunkGeometryStore.Remove` 释放缓冲 |
| 旧 arena 死代码 | `ChunkGeometryArena`/`ArenaSizing` 已不被渲染器使用，仅旧单测覆盖 | 增加维护面/误导 | 确认无外部引用后删除文件与对应测试，更新 traceability |
| Draw call 数量 | 每区块一次 `DrawIndexed`（solid/cutout/translucent），视距 32 约数千 | CPU 提交开销；当前 960×540 可接受 | profiling 后决定：分页 arena + GPU 间接剔除，或按层合批 |

## 2. 天空 / 光照 / 雾

| 项 | 现状 | 影响 | 建议 |
| --- | --- | --- | --- |
| 天空为纯 `SKY_COLOR` 平色 | 跳过了原版 sky disc 的细微几何/地平线渐变 | 天空缺少地平线亮带/圆盘边界（远看仍接近） | 用 `CelestialGeometry.SkyDisc` + `SKY_COLOR` 绘制天空盘，或按高度做轻微渐变 |
| 雾基色近似 | 目前雾色= `SKY_COLOR`（近似）；原版是 `FOG_COLOR`(默认黑) 与 `SKY_COLOR` 按 `skyFogEnd` 混合，含日出日落 lerp、雨雾 | 特定角度/天气下雾色偏差 | 按 `AtmosphericFogEnvironment.getBaseColor` 完整实现 |
| 夜间亮度 | 已修正色彩空间/原样通道，仍与参考观感"差一点" | 夜间略暗/对比不同 | 用同机位参考帧定标 gamma/ambient；必要时调 `BrightnessFactor` |

## 3. 云

| 项 | 现状 | 影响 | 建议 |
| --- | --- | --- | --- |
| 云无 OIT / 精确 cloud fog | 逐 cell 面 + 简单 alpha 淡出（`CLOUD_FOG_END` 近似） | 与半透明地形/远处云的混合不完全一致 | 接入 OIT 云 pass 与 `linear_fog_value(vertexDistance,0,FogCloudsEnd)` |
| 云几何实现方式 | CPU 逐 cell 面（位置/亮度），而非原版 storage-buffer + shader 生成 | 结果等效性待像素对比；CPU 构建成本随视距增长 | profiling 与参考帧对比后再决定是否迁到 GPU 生成 |

## 4. 天体（太阳/月亮/星星/日出）

| 项 | 现状 | 影响 | 建议 |
| --- | --- | --- | --- |
| 天体 pass 已进窗口 | 窗口现在绘制日月/日出/星星，并随相机位移（`UpdateCamera`） | — | 继续按 `SkyRenderer` 核对尺寸/角度/颜色/混合的像素等效 |
| 天体细节等效 | 尺寸/朝向/OVERLAY 混合/星点数量与分布是否逐值一致未做像素对比 | 观感细节可能与参考不同 | 用同机位参考帧逐项对照（sun/moon 角度、star brightness/angle、sunrise alpha） |
| 云/天体在高分辨率 | 2560×1334 时 OIT 目标 + 几何重传曾 OOM | 大窗口不稳定 | 显存预算/分页/降低目标格式后复测 |

## 5. 渲染质量与平台

| 项 | 现状 | 影响 | 建议 |
| --- | --- | --- | --- |
| 适配器选择 | WGPU 绑定未暴露 adapter 选项；本机有 Intel Iris Xe（共享）与 RTX 3060 | 可能选到核显导致显存更紧 | 评估升级绑定/环境变量（`WGPU_ADAPTER_NAME`）或在启动器选择高性能 GPU |
| 视距 24 | 12 与 32 已验证，24 未单独测 | — | 合并到下一次压测 |
