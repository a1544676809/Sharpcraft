<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: src/Sharpcraft.Common/World/Blocks
summary: 方块行为的预留接缝：IBlockBehaviour、BlockTickContext 与空实现注册表
status: [已评审]
-->

# Sharpcraft.Common/World/Blocks

状态：[已评审]
基线：Minecraft 26.3
证据：`docs/decisions/0031-block-behaviour-layering.md`；`ref://mc-26.3-analysis/src/net/minecraft/world/level/block/Block.java`
最后更新：2026-11-16

## 1. 职责

为**算法型方块行为**预留接缝。声明式事实（是否带方块实体、光辐射、遮挡族等）不在这里——那属于方块注册表数据（ADR 0031 第 1 层）。

本目录当前**没有任何实现**，只提供接口与空注册表，使服务端开工时能挂上行为而不改调用点。

## 2. 内容

| 类型 | 作用 |
| --- | --- |
| `IBlockBehaviour` | 随机刻 / 计划刻 / 邻居变化的默认空实现 |
| `BlockTickContext` | 行为需要的上下文：世界视图 + 坐标 + 状态 id |
| `IBlockWorldView` | 行为可触碰的最小世界接口（读/写方块状态） |
| `BlockBehaviours` | 按方块 id 查行为，未注册时返回 `Default`（空操作） |

## 3. 与原版的对应

原版把数据与行为放在同一个 `Block` 类上（`BlockBehaviour.hasBlockEntity()` 用 `instanceof EntityBlock` 判定）。本项目把数据放进注册表、行为留在这里，按 ADR 0031 的分层：

| 原版 | 本仓 |
| --- | --- |
| `Block` 的字段与 `Properties` | 方块注册表数据（第 1 层） |
| `Block` 的方法覆盖 | `IBlockBehaviour`（第 3 层，未实现） |
| `BlockEntityRenderer` 注册表 | `BlockEntityRenderers`（第 2 层，已实现） |

## 4. 未实现的部分

- 没有任何方块注册行为（`BlockBehaviours.Count == 0`）
- 没有刻调度器驱动这些方法（服务端开工时补）
- `IBlockWorldView` 尚无实现方

## 5. 何时会动

触发条件写在 ADR 0031 的「复查条件」：服务端开工、同一能力族出现超过 3 处按方块名分支、或数据表列数失控。

<!-- BEGIN GENERATED: class-map -->

| Project | Sharpcraft type | Kind | Status | Reference class | Reference path | Note |
| --- | --- | --- | --- | --- | --- | --- |
| Sharpcraft.Common | Sharpcraft.Common.World.Blocks.BlockBehaviours | Adapter | Partial | net.minecraft.core.registries.BuiltInRegistries | ref://mc-26.3-analysis/src/net/minecraft/core/registries/BuiltInRegistries.java | lookup of the behaviour half of a block, keyed by block id |
| Sharpcraft.Common | Sharpcraft.Common.World.Blocks.BlockTickContext | Adapter | Partial | net.minecraft.world.level.Level | ref://mc-26.3-analysis/src/net/minecraft/world/level/Level.java | the position and state a block method receives, without the whole level object |
| Sharpcraft.Common | Sharpcraft.Common.World.Blocks.IBlockBehaviour | Mirror | Partial | net.minecraft.world.level.block.Block | ref://mc-26.3-analysis/src/net/minecraft/world/level/block/Block.java | behaviour half only; data lives in the block registry |
| Sharpcraft.Common | Sharpcraft.Common.World.Blocks.IBlockWorldView | Adapter | Partial | net.minecraft.world.level.LevelReader | ref://mc-26.3-analysis/src/net/minecraft/world/level/LevelReader.java | read and write of block states only |

<!-- END GENERATED: class-map -->
