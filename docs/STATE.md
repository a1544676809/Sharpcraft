<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: STATE —— 跨会话进度锚点
summary: 当前里程碑、进行中、阻塞、下一步、总体行动大纲
status: 活文档
updated: 2026-11-06
-->

# STATE —— 跨会话进度锚点

> 本文件只记录**易变状态**，且受原则 14 与架构测试硬约束：**严格控制在 150 行以内**（历史排障记录由 Git 历史保存，禁止在此写流水账）。
> 设计结论见 `docs/modules/`，里程碑定义见 `docs/milestones/roadmap.md`，已知缺口见 `docs/specs/render/known-gaps.md`，验收矩阵见 `docs/specs/quality/vanilla-equivalence-matrix.md`。

最后更新：2026-11-06

## 当前里程碑

| 里程碑 | 状态 | 出口证据摘要 |
| --- | --- | --- |
| M1 基础逻辑地基 | **已完成** | 架构门禁、随机数/注册表/状态 ID 真值向量、真实服务器进 play |
| M2 世界数据闭环 | **已完成** | NBT、`level_chunk_with_light` 解析、WGPU 首帧出图 |
| M3 资产管线 + 启动器闭环 | **已完成** | 官方 26.3 jar 校验打包、多账号离线闸门、真实服务器带纹理窗口 |
| M4 实体系统根基 + 输入输出 | **已完成** | 20 Hz tick、移动同步（4.318 b/s）、走进去挖方块闭环、流式区块加载 |
| M5 协议深化（5.1/5.2/5.3） | **进行中** | 5.1 实体同步完成；5.2 聊天收发完成；5.3 三层地形/流体/时间天气/参考 OIT/天空盘/视距 32 完成 |
| M6 模组与着色器包 | **进行中** | 模组宿主与着色器包契约层就位；ALC 回收验证与包执行器接线待收口 |

## 进行中

正在执行 **游戏本体（`Common` / `Client` / `Framework` / `Modding`）单轨收敛与防呆推进大纲**（遵循所有者指示：忽略启动器门户细节，聚焦游戏本体；Team Lead 把握架构/真值/验收，微切片派发给 `auto/deepseek-v4.1-flash` 与 `auto/hy4free` 队友执行）：

| 阶段 | 核心目标（聚焦游戏本体） | 关键子任务 | 状态 |
| --- | --- | --- | --- |
| **Phase 0** | 架构单轨收敛与防呆基建 | P0.1 宪章与 `STATE.md` 门禁（完成）；P0.2 `PlaySession.TryHandleTransport` 单轨化（完成）；P0.4 `MeshingContext` 网格构建入口单轨化（完成）；P0.3 `Framework/Rendering` 帧 Pass 单轨收敛 | **仅剩 P0.3 待做** |
| **Phase 1** | 核心数据地基补齐（`Common`/`Client`） | P1.1 方块实体类型注册表（完成 T-348）；P1.2 `block-shape` `OcclusionShape` 逐面遮挡（完成 T-349）；P1.3 方块实体 NBT 保留（完成 T-347）；P1.4 `registry_data` 动态装配 `BlockStateLayout`（T-042，待做）；P1.5 把 `OcclusionShape` 接入 `ChunkMesher` 替换 `IsSolid`（前置切片 T-351 完成，完整接入待做） | **进行中（1.1/1.2/1.3 完成；P1.5 前置切片完成；全量 1281 通过）** |
| **Phase 2** | M5.3 渲染长尾缺口清零（`Client`/`Framework`） | P2.1 `AtmosphericFogEnvironment`/`WaterFogEnvironment` 精确混合与水下暗盘；P2.2 流体 `water_overlay`/反向顶面/图集动画；P2.3 实体真实模型与名字牌 | 待开始 |
| **Phase 3** | M5.2 界面层与 HUD 闭环（`Client`/`Framework`） | P3.1 统一渲染管线接入 UI Pass（T-104）；P3.2 `chat_type` 注册表装饰（T-127）与基础 HUD | 待开始 |
| **Phase 4** | M6 模组与着色器包收口（`Modding`/`Framework`） | P4.1 定位并修复 `ModLoadContext` ALC 卸载强引用（R-019）；P4.2 `ShaderPackDescription` 驱动统一管线执行与故障回退 | 待开始 |

## 已完成

1. **T-347 / T-348 / T-349 数据地基三项（2026-11-06）**：`auto/hy4free` 队友 `be-worker`、`shape-worker` 并行完成方块实体 NBT 保留、26.3 49 条方块实体类型注册表、`OcclusionShape` 逐面遮挡（5 组真值向量）。Lead 中央集成时用 using 别名消解 `BlockFace`（Assets 纹理分桶 vs Common 六方向）二义性；全量 **1278 用例通过，0 警告**。
2. **T-345 / T-346 网络传输与网格上下文单轨化（2026-11-06）**：`auto/deepseek-v4.1-flash` 队友 `net-worker` 与 `mesh-worker` 并行完成 `PlaySession.TryHandleTransport` 与 `MeshingContext` 统一入口；全量 7 套 **1269 用例通过，0 警告**。
3. **T-344 宪章原则 14 与防呆门禁落地（2026-11-06）**：`AGENTS.md` 新增原则 14 与 3.7 节；`docs/_templates/module-doc.md` 强制要求原版源码映射表与 Golden Vectors；`STATE.md` 压缩至 150 行内并由 `DocumentationDriftTests` 守护。
4. **T-330..T-343 天空盘、OIT 与视距 32 压测（2026-11-01）**：原版逐像素 fog 天空盘落地（采样 `(136,169,255)` vs 原版 `(137,170,255)`）；每区块独立缓冲支持视距 32（1770 区块 / 154 FPS）与古城室内（155 FPS）；删除旧单缓冲 Arena 死代码；视距+2 外区块自动卸载释放显存（T-327）。
5. **T-250..T-326 云层、参考 OIT、时间天气与流体通道（2026-10-31~11-01）**：三阶段小波 OIT + 逐 cell 云；`set_time` 时钟、Lightmap UBO（ADR-0019）、雨雷同步、67 群系着色、水与岩浆独立流体通道。
6. **T-350 方块实体占位尺寸（2026-11-06）**：`BlockEntityPlaceholderMesher` 按 `VanillaBlockEntityTypes.IsChestLike` 分类取 14px，非箱子类 12px；新增 1 用例。
7. **T-351 流体面半透明邻居（2026-11-06）**：`ChunkMesher` 流体面剔除排除 translucent 邻居（P1.5 前置切片）；新增 2 用例；全量 1281 通过。

## 阻塞项

- 无硬阻塞。
- **R-017**：XSTS 端点偶发网络抖动，联网全链路验收脚本含重试策略。
- **R-019**：模组 `AssemblyLoadContext` 热卸载后的 GC 回收探针尚未稳定转绿（排入 Phase 4 P4.1）。

## 下一步具体动作

1. **P1.5（`Client/Render/ChunkMesher`）**：用新 `OcclusionShape.IsFaceOccludedByState/Self` 替换当前 `IsSolid` 近似，接入流体面剔除（`docs/modules/block-shape.md §6`）；派发给队友，配 `FluidMesherTests` 真值回归。
2. **P1.4（T-042 `Common`/`Client` 动态注册表装配）**：按原则 14 提取 26.3 `registry_data` 真值向量，把服务器下发注册表动态装配进 `BlockStateLayout`，替换基线静态快照。
3. **P0.3（`Framework/Rendering` 帧 Pass 单轨化）**：`OffscreenRenderer` 与 `WindowedRenderer` 收敛为共用世界 Pass 序列，使窗口帧可回读、离屏帧特性完整。

## 最近提交

- `1133183` merge: cloud OIT, rendering optimizations, and sky/fog equivalence
- `3d1351c` test(render): profile the ancient-city indoor scene (T-343)
- `232779f` test(render): count DrawIndexed calls per geometry store (T-342)
