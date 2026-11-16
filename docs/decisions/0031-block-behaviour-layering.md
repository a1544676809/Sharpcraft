<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: ADR 0031：方块数据、渲染行为与模拟行为分层
summary: 方块"是什么"用生成的数据表，"客户端怎么画"用渲染器注册表，"服务端怎么模拟"用行为类；三层单向依赖
status: 已采纳
-->

# 0031：方块数据、渲染行为与模拟行为分层

- 状态：已采纳
- 日期：2026-11-16
- 决策者：项目所有者
- 关联：原则 4（服务端沿用原版）、原则 5（三侧边界）、原则 14（真值先行）；Issue #95、#96

## 背景

26.3 有约 **1100 个方块**，其中 **49 个 `BlockEntityType`**，覆盖约 **180–200 个方块**（`BlockEntityTypes.java` 中 132 处 `Blocks.*` 引用，加上 `Blocks.BANNER.asList()` 这类按颜色的展开）。

原版把"方块是什么"和"方块做什么"放在同一个 `Block` 类上：

```java
public boolean hasBlockEntity() {
    return this.getBlock() instanceof EntityBlock;   // BlockBehaviour.java:860
}
```

创建路径由接口多态决定（`LevelChunk.setBlockState` 调 `EntityBlock.newBlockEntity`），`BlockEntityType.validBlocks` 只是注册期生成的 `Set<Block>`，用于校验。

我们这边没有"每种方块一个对象"这层——方块是一个整数 id 与一份注册表数据。上一轮为了让箱子类方块能创建方块实体，在 `ClientWorld.SetBlockState` 里挂了一张**手工类型清单**（`IsChestLike || IsShulkerBox`），直接导致：进服时已存在的方块实体可见（区块包自带类型 id），进服后新放的陶罐、铜傀儡、钟、头颅、潮涌核心因为查不到类型而不创建记录、永远不显示（Issue #95）。

手工清单是症状。真正的问题是**没有回答"这个方块带不带方块实体"的唯一来源**。

同时，`Server` 侧是宪章保留但未开工的范围（原则 4），而服务端最终需要真正的逐方块模拟逻辑（红石、流体、活塞、生长）。

## 决策

方块相关能力分三层，**单向依赖**，任何一层都不反向依赖上层：

| 层 | 程序集 | 内容 | 形态 |
| --- | --- | --- | --- |
| 1. 注册表数据 | `Common` | 方块 id、状态属性、方块实体类型、光辐射、遮挡族、碰撞族、渲染提示 | **生成的数据表** |
| 2. 客户端渲染行为 | `Client` | 渲染器选择、模型解析、方块实体几何 | 注册表 + 已有渲染器 |
| 3. 模拟行为 | `Server`（未来） | 红石、流体、活塞、生长、刻调度 | **按方块 id 挂载的行为类** |

- 第 1 层是**唯一真相源**：任何"这个方块有没有 X"的问题都问它，不得在调用点维护类型清单。
- 第 2 层只读第 1 层；渲染器按**方块实体类型**注册，不按方块名分支。
- 第 3 层用接口 `IBlockBehaviour` 预留接缝（`Sharpcraft.Common.World.Blocks.IBlockBehaviour`），默认实现为空操作；服务端开工时按方块 id 注册具体实现，**第 1 层不改**。
- 表由仓库外的生成器从 26.3 的 `BlockEntityTypes` 等注册数据提取；仓库内只存方块名字，符合原则 3。

## 影响面

- 代码：
  - `Sharpcraft.Common/World/Blocks/IBlockBehaviour.cs`：行为接缝，零实现。两种注册：`Register`（弱引用，模组用，卸载后自然失效）与 `RegisterStrong`（强引用，服务端自己的行为用）。
  - `Sharpcraft.Common/World/Blocks/BlockEntityBlockIndex.cs`：**生成**的"方块 → 方块实体类型"索引，191 行，覆盖 49 个类型。生成器在仓库内：`tools/blocks/generate-block-entity-index.ps1`，支持 `-Check` 校验是否过期。
  - `ClientWorld.SetBlockState` 改为查该索引，删除 `IsChestLike` / `IsShulkerBox` 清单。
- 测试：`BlockEntityBlockIndexTests`（正例含 `chiseled_bookshelf`→38、`oak_hanging_sign`→8 边界、负例 10、`Count == 191`、排序去重、49 个类型全覆盖）、`BlockBehavioursTests`（默认/弱注册/强注册 GC 存活/回收清理/注销）、`Repro_022` 新增五个"放置任意方块实体方块即建记录"（含 type 0 的熔炉）与"普通活塞不建记录"。
- 文档：本 ADR；`docs/src/sharpcraft-common/world/blocks.md`（模块文档）。`docs/modules/block-state.md` 的"方块行为在哪里"一节**尚未**补，随服务端开工（Issue #97）一并写。
- 兼容性：不影响协议与存档。**行为接口不是冻结契约**：它只覆盖简单的邻居变化与刻回调。

## 接口范围（明确收窄）

`IBlockBehaviour` 当前只表达：

| 回调 | 参数 |
| --- | --- |
| `RandomTick` / `ScheduledTick` | `BlockTickContext` |
| `NeighbourChanged` | `BlockTickContext`、改变来源方向（0..5 / -1）、`movedByPiston` |
| `Placed` | `BlockTickContext`、旧状态 |
| `Broken` | `BlockTickContext`、新状态、`movedByPiston` |

**尚缺**（实现第一个真实行为前必须补，见 Issue #97）：

- 原版 `neighborChanged` 的 `Orientation` 参数（活塞用）
- `RandomSource`（`RandomTick` / `ScheduledTick` 需要随机源）
- `IBlockWorldView` 的信号查询（红石读邻居强度）
- 实体/方块刻调度本身

因此本接口**不得**被描述为"已足够红石/活塞"。

## 落地状态

| 项 | 状态 |
| --- | --- |
| 第 1 层（生成的方块 → 类型索引） | 已落地，Issue #96 关闭条件已满足 |
| 第 2 层（客户端渲染） | 既有 |
| 第 3 层（行为实现） | **未实现**，仅接缝，跟踪于 Issue #97 |
| `BlockTickContext` 是否够用于红石/活塞 | 未验证（无任何实现），见 Issue #97 的开工前置 |

## 替代方案与为何不选

| 方案 | 不选的理由 |
| --- | --- |
| **逐方块类**（照搬原版） | 为 180–200 个方块写类，而它们的差别只是一个整数；客户端一行都用不上；服务端边界未定（原则 5），现在写的类一半会写错；最容易滑向逐行翻译（原则 3） |
| **纯数据表，无行为层** | 算法型行为（红石、流体、活塞）无法表达为数据；一旦服务端开工就要大改 |
| **按方块名 `switch`** | 就是这次踩的坑：清单会漏、会漂移，且分散在多处 |

## 复查条件

出现以下任一情况时，重新评估本 ADR：

1. 服务端开工，且单个方块的行为无法用 `IBlockBehaviour` 的一个实现表达
2. 同一"能力族"内出现超过 3 处 `switch (blockPath)` 分支
3. 数据表的列超过约 15 个，开始出现"大多数方块为空"的列

## 备注

本 ADR 不改变原则 4 的范围约定：第 3 层当前**不实现**，只预留接口。任何提前实现的服务端逻辑仍须遵守"只作为客户端内部需要的、被清晰隔离的逻辑存在"。
