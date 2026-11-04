<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: ADR 0022：客户端实体表现状态层（非权威）
summary: 为何要建客户端实体表现状态层、明确排除权威 AI，以及影响面与复查条件
status: 已采纳
-->

# 0022：客户端实体表现状态层（非权威）

- 状态：已采纳
- 日期：2026-11-04
- 决策者：项目所有者（本会话确认）
- 关联：`docs/modules/entity-presentation-state.md`、`docs/traceability.md` T-575、`docs/modules/entity-animation.md`

## 背景

按 provider 架构移植生物动画时，Ravager/Hoglin/Witch/Vex 这类生物可在现有 `EntityAnimationFrame` 或 entity_event/synced data 上等效实现；但 **Parrot / Strider / Illager / Piglin / Allay / Tadpole / Ghast / Squid / Bat** 等生物的 `setupAnim` 读取的是**客户端 AI/实体刻派生字段**（`flap/flapSpeed`、`pose`、`isRiding/isSuffocating`、`armPose/crossbow/swing`、`isInWater`、`tentacleAngle`…）。这些字段既不在网络协议里，也不是现有渲染状态字段，导致动画只能停在沙盒或近似。

同时，现有 `Flap/FlapSpeed/TentacleAngle/IsResting` 在真机恒为 0（仅沙盒可设），是同一缺口的另一表现。

约束（AGENTS.md）：

- 原则 4：服务端权威沿用原版，自研服务端不在范围；实体 AI/寻路/战斗结算/生成掉落属权威逻辑。
- 原则 5：`Client` 只做表现，不做权威推进；`Client`/`Server` 不得互引。
- 原则 9/3.6：热路径零分配；性能与行为偏差受控并记录。
- 原则 3：证据来自仓库外分析工作区，文档只写行为不粘贴实现。

## 决策

在 `Client` 新增一个**非权威的客户端实体表现状态层**（`docs/modules/entity-presentation-state.md`）：每个客户端 tick，对每个被跟踪实体，**只**依据服务端同步的数据（位置/速度/entity_data/entity_event/装备/乘客）与本地可确定的物理/方块信息，维护并派生**渲染所需的表现字段**：

- P0 通用派生：`isInWater`、`onGround`、乘客/`isRiding`、`flap/flapSpeed`、`tentacleAngle`、`pose`（能由同步/本地状态判定的部分）；
- P1 逐生物表现状态（如 `isSuffocating`、`isCharging` 的扩展）；
- P2 仅在存在同步来源时接入 `armPose/crossbow/swing` 等；无来源者继续标记阻塞。

**明确不做**（仍属服务端权威）：AI 目标选择、寻路、攻击命中/伤害结算、生成/掉落、Warden 怒气、Creaking 心脏等。不得用近似值冒充等效；无法确定的字段必须报告并保持 `进行中`。

该层产出写入 `EntityRenderState`（或 provider 的 `IEntityAnimationState`），不得让 `Client.World` 反向依赖 `Client.Render`。

## 影响面

- 代码：新增 `src/Sharpcraft.Client/Entities/`（或 `World/` 内的表现状态子模块）；接线 `EntityTracker`/`EntityRenderState`/`EntityAnimationFrame`；可能新增少量通用字段（如 `IsInWater`、`IsRiding`、`FlapAngle`）。
- 测试：新增派生字段的单元测试与零分配测试；按生物补充动画测试；不改变既有测试语义。
- 文档：新增模块文档；`entity-animation.md` 的输入字段清单同步；traceability 新增任务行。
- 兼容性：不触碰协议/存档/模组 API；纯客户端表现，不改变与服务端的权威关系。

## 替代方案与被否原因

| 方案 | 否决原因 |
| --- | --- |
| 在客户端实现"整个行为层/AI" | 越过原则 4/5：权威逻辑属服务端；客户端也无法获得权威输入，必然与服务器冲突。 |
| 每个生物各自临时补字段/近似 | 字段碎片化、重复造轮子，且容易把近似当等效；难以审计。 |
| 不建层，只做网络/位移可派生生物 | 能解锁的生物很少（当前仅剩个位数），且 `Flap/TentacleAngle` 等真机恒 0 的问题无法解决。 |

## 复查条件

- 若后续引入集成服务端/客户端权威模拟（原则 4 待定项），需要重新评估该层与权威模拟的边界。
- 若某生物字段被证明可完全由新增同步数据获得，应优先走 synced data 而非客户端推算。
- 若该层出现与服务器状态长期不一致的可观测偏差，登记受控偏差并重评。
