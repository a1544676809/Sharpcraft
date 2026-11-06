<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: STATE —— 跨会话进度锚点
summary: 当前里程碑、进行中、未提交改动、阻塞、下一步、总体行动大纲
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
| M5 协议深化（5.1/5.2/5.3） | **进行中** | 5.1 实体同步完成；5.2 聊天收发完成；5.3 三层地形/流体/时间天气/参考 OIT/天空盘/视距 32 完成；实体模型与动画长尾进行中 |
| M6 模组与着色器包 | **进行中** | 模组宿主与着色器包契约层就位；ALC 回收验证与包执行器接线待收口 |

## 进行中

| 任务 | 状态 / 关键点 |
| --- | --- |
| **Elytra baby 收尾（ELYTRA_BABY）** | 当前工作树有未提交改动：baby 翼按 0.5 缩放、全宽重叠对（`CentreX=0`、`PivotX=±2.5`、`CentreZ=body+2.5`、`PivotZ=body+2`、`Kind=Elytra`）。待：用户目视确认 → 删除 `EntityModelMesher`/`WindowCommand` 临时诊断 → 更新 `docs/modules/entity-equipment.md`（旧 T-650“各自半边”结论已过时，成人/婴儿均为参考全宽镜像翼） → 全量测试 → 提交 |
| **实体模型与动画主线** | 真实部件模型 + 逐实体动画 provider 已大量落地（humanoid/baby/quadruped/aquatic/nether 等）；剩余：T-631 held-item/item-model（解锁 SPEAR/STAB）、Happy Ghast ropes、EnderDragon flight history、Parrot PARTY / Bee stinger / Guardian eye look / Piglin crossbow-hold |
| **架构：原则 14 / Phase 0 / Phase 1** | 已从 main 合入 T-654..T-661（原则 14、STATE 门禁、`PlaySession.TryHandleTransport`、`MeshingContext`、方块实体 NBT/类型注册表、`OcclusionShape`）。剩余：P0.3 帧 Pass 单轨化、P1.4 `registry_data` 动态装配、P1.5 `OcclusionShape` 完整接入 `ChunkMesher` 替换 `IsSolid` |

## 未提交改动

- 工作树：`EntityModelLayerHelpers.cs`、`EntityModelMesher.cs`、`WindowCommand.cs`、`ElytraLayerTests.cs`、`ElytraWingGeometryTests.cs` —— baby elytra 几何与诊断（见上）。
- 后台任务：无。

## 已完成（近期）

1. **T-654..T-661 main 合并（2026-11-06）**：原则 14 + 单轨收敛 + 方块实体/遮挡地基并入本分支；`PlaySession` 冲突取 main 的“未处理包全部延迟”版本；traceability 编号顺延（原 main T-344..T-351 → T-654..T-661）。
2. **T-648..T-650 / T-651..T-653 渲染与解耦（2026-11-05~06）**：Elytra 翼盒与 UV 原版等价、双翼镜像全宽；流体面剔除修复（玻璃后水可见）；云 OIT depth-bounds 写深度；`Sharpcraft.Assets` 分层 + 启动器/客户端解耦（ADR-0026/0027）。
3. **实体模型/动画批量落地（2026-11-02~05）**：全部 baby 模型族、villager/zombie/horse 变体、cat/wolf 项圈、panda 基因、大量动画 provider 与测试。
4. **协议与渲染长尾（2026-11-01~05）**：Play Clientbound 覆盖审计 + P0/P1 包批次（T-642..T-647）；反相 Z 迁移；参考 OIT 云/流体；天空盘逐像素近似。

## 阻塞项

- 无硬阻塞。
- **R-017**：XSTS 端点偶发网络抖动，联网全链路验收脚本含重试策略。
- **R-019**：模组 `AssemblyLoadContext` 热卸载后的 GC 回收探针尚未稳定转绿（排入 Phase 4 P4.1）。

## 下一步具体动作

1. **Elytra baby 收尾**：等用户目视确认后，按“进行中”清单删除诊断、修正 `entity-equipment.md`、跑全量门禁并提交。
2. **T-631 held-item/item-model**：先按原则 14 补 `docs/modules/entity-equipment.md` 的原版源码映射表与 ≥5 组 Golden Vectors（`ItemInHandRenderer`/`ItemModelResolver`），再实现 SPEAR/STAB。
3. **Happy Ghast ropes / EnderDragon flight history**：按实体渲染 backlog 顺序推进。
4. **P1.5（`Client/Render/ChunkMesher`）**：用 `OcclusionShape.IsFaceOccludedByState/Self` 替换 `IsSolid` 近似；配 `FluidMesherTests` 真值回归。
5. **P1.4（T-042）**：提取 26.3 `registry_data` 真值向量，把服务器下发注册表动态装配进 `BlockStateLayout`。
6. **P0.3（`Framework/Rendering`）**：`OffscreenRenderer` 与 `WindowedRenderer` 收敛为共用世界 Pass 序列。

## 最近提交

- `12ed409` feat(render): elytra baby variant (ELYTRA_BABY, 0.5-scaled wings)
- `114cb4e` refactor(launcher): drop Sharpcraft.Client reference; dev commands defer to standalone client (R-025 done)
- `93996a8` feat(assets): extract Sharpcraft.Assets low-level layer; mount external asset index (ADR-0027)
- `94a9f0b` feat(render): reversed-Z migration, elytra wings, OIT cloud/fluid fixes
