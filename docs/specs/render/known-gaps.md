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
| **区块卸载** | 已实现：`ClientWorld.RemoveChunksOutside` + `ChunkMeshCache.Remove` + `ChunkGeometryStore.Remove`，按视距+2 释放 | — | 已关闭（T-327） |
| 旧 arena 死代码 | 已删除 `ChunkGeometryArena`/`ArenaSizing` 及其测试 | — | 已关闭（T-341） |
| Draw call 数量 | 每区块一次 `DrawIndexed`（solid/cutout/translucent），视距 32 约数千 | CPU 提交开销；当前 960×540 可接受 | profiling 后决定：分页 arena + GPU 间接剔除，或按层合批 |

## 2. 天空 / 光照 / 雾

| 项 | 现状 | 影响 | 建议 |
| --- | --- | --- | --- |
| 天空盘/地平线渐变 | 尚未绘制原版 sky disc；当前为平色 SKY_COLOR | 缺少地平线亮带/圆盘边界 | 实现半径 512、上盘 y=+16/下盘 y=-16 的天空盘；上盘 SKY_COLOR |
| 水下天空暗盘 | 水下渲染/水下视效尚未实现 | 无法验证 `shouldRenderDarkDisc` 与水下天空/雾 | **延后**到水下渲染切片，暂不实现 |
| `LightmapParameters` 传入网格器但从未被读 | **已删除（T-840）**。四层传递链（`ChunkMeshCache` → `LayerSinks` → `ChunkWorkerPool` → `ChunkMesher`）及 `ChunkMesher` 内部全部 `lighting` 参数、两处死局部变量均已移除 | — | 已关闭 |
| 雾的双壳只实现了一层 | 原版 `total_fog_value = max(linear(spherical, environmentalStart, environmentalEnd), linear(cylindrical, renderDistanceStart, renderDistanceEnd))`：球形壳走大气雾（`FOG_START_DISTANCE`，默认 1024 格），柱形壳走视距渐隐。**当前只实现柱形壳**，大气壳未接（正常视距 192 格下它贡献≈0，但**下雨雾**与**Boss 雾**依赖它把 `environmentalStart/End` 压到 10/96） | 雨天与 Boss 战时的雾浓度会不正确 | **延后**至天气系统；复查条件：实现降水时一并接 `environmentalStart/End` |
| 雾基色近似 | 目前雾色= `SKY_COLOR`（近似）；原版是 `FOG_COLOR`(默认黑) 与 `SKY_COLOR` 按 `skyFogEnd` 混合，含日出日落 lerp、雨雾 | 特定角度/天气下雾色偏差 | 按 `AtmosphericFogEnvironment.getBaseColor` 完整实现 |
| 夜间亮度 | 已修正色彩空间/原样通道，仍与参考观感"差一点" | 夜间略暗/对比不同 | 用同机位参考帧定标 gamma/ambient；必要时调 `BrightnessFactor` |

## 3. 云

| 项 | 现状 | 影响 | 建议 |
| --- | --- | --- | --- |
| 云 OIT / cloud fog | 已实现参考 OIT（cloud depth + blit_depth + bounds/transmittance/accumulate + 单次 composite）；RGBA32F 在 Float32Blendable 可用时启用，否则回退 16F | 无 Float32Blendable 时可能有轻微深度量化 | 保持开关与固定机位像素对照 |
| 云几何实现方式 | CPU 逐 cell 面（位置/亮度），而非原版 storage-buffer + shader 生成 | 结果等效性待像素对比；CPU 构建成本随视距增长 | profiling 与参考帧对比后再决定是否迁到 GPU 生成 |

## 4. 天体（太阳/月亮/星星/日出）

| 项 | 现状 | 影响 | 建议 |
| --- | --- | --- | --- |
| 天体 pass 已进窗口 | 窗口现在绘制日月/日出/星星，并随相机位移（`UpdateCamera`） | — | 继续按 `SkyRenderer` 核对尺寸/角度/颜色/混合的像素等效 |
| 天体细节等效 | 尺寸/朝向/OVERLAY 混合/星点数量与分布是否逐值一致未做像素对比 | 观感细节可能与参考不同 | 用同机位参考帧逐项对照（sun/moon 角度、star brightness/angle、sunrise alpha） |
| 云/天体在高分辨率 | 已测 2560×1334 / 视距 12 / OIT，40s 无 OOM（RSS ~1.47GB，GPU dedicated ~3.1GB）；`MC_OIT_LOW_PRECISION` 可降级 | — | 继续监控长时间高分辨率 |

## 5. 渲染质量与平台

| 项 | 现状 | 影响 | 建议 |
| --- | --- | --- | --- |
| 适配器选择 | WGPU 绑定未暴露 adapter 选项；本机有 Intel Iris Xe（共享）与 RTX 3060 | 可能选到核显导致显存更紧 | 评估升级绑定/环境变量（`WGPU_ADAPTER_NAME`）或在启动器选择高性能 GPU |
| 视距 24 | 12 与 32 已验证，24 未单独测 | — | 合并到下一次压测 |
| 名字牌高度与实体模型 | 当前玩家/生物仍是占位盒体，名字牌锚点只能按占位高度计算，无法与完整实体模型高度逐像素对齐 | 名字牌整体位置可能偏高/偏低 | **延后**到完整实体模型与碰撞高度切片，届时用原版实体 `nameTagAttachment` 重算锚点 |



| **遮挡遍历起点：相机在世界高度之外** | 临时把起点 section 的 Y 钳位到 `[minSectionY, maxSectionY]`，只种子一个节点。**原版不是这样**：`SectionOcclusionGraph.initializeQueueForFullUpdate` 在 `getRenderSection(cameraSection) == null` 时，会取 `minSectionY`（相机在下方）或 `maxSectionY`（在上方），然后**把该 Y 层整个 XZ 范围内的所有 section 都种子进队列**，每个节点预置"从上/从下进入"的方向位以及相对相机方向的 EAST/WEST/SOUTH/NORTH 位，最后按到相机的距离排序入队 | 相机在世界高度之外时（高空飞行、传送瞬间、虚空），可见集合与原版不同：原版能立刻铺满整层并从边界向内漫水，当前实现只从单点向外扩，且缺少方向位会改变 `CanSee` 判定 | **待补**：按原版实现整层种子 + 方向位 + 距离排序 | `SectionOcclusionTraversal.cs`；`SectionOcclusionState.cs` |