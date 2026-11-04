<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: ADR 0024：客户端实体 AI 表现状态（P2）
summary: 为剩余 AI 依赖动画建立只读、非权威的客户端派生状态层
status: 提议
-->

# 0024：客户端实体 AI 表现状态（P2）

- 状态：提议（待项目所有者评审）
- 日期：2026-11-04
- 决策者：项目所有者（待确认）
- 关联：ADR-0022（客户端实体表现状态层）、ADR-0023（武装生物表现状态）、`docs/modules/entity-ai-presentation.md`

## 背景

剩余生物动画（Wolf/Fox/Panda/PolarBear/Rabbit/Wither/Parrot 等）读取 `setupAnim` 的
`isSitting`/`isSleeping`/`isCrouching`/`isPouncing`/`isUnhappy`/`sneezeTime`/`standScale`/`hop`/
`yHeadRots[]` 等字段。这些字段在参考实现里由**客户端实体刻**维护，部分来自同步 entity data，部分来自本地动画计时器（jumpTicks、sneezeTime、standScale、hop state）。

现有表现状态层（ADR-0022/0023）只覆盖位置/朝向/行走/游泳/装备等通用输入，因此这些动画只能阻塞。

## 决策

在 ADR-0022 的非权威表现状态层内新增 **P2 子层：客户端实体 AI 表现状态**：

1. **只读、非权威**：只复刻"渲染可观测"的派生态；不实现寻路、目标选择、攻击判定或服务端 AI。
2. **数据来源优先级**：
   - 同步 entity data（布尔/枚举/字节）优先；
   - entity event（已有解码）其次；
   - 本地可确定信息（位置、onGround、移动距离、pose）再次；
   - 仍无法确定的字段 → 保持阻塞，不近似。
3. **状态归属**：每个 provider 的 State 实现 `IEntityTickAnimationState`/`IEntitySyncedDataAnimationState` 维护自己的小状态；通用字段（`IsSitting`/`IsAngry`/`IsLeftHanded` 等）进 `EntityPresentationState` + `EntityAnimationFrame`。
4. **逐生物窄判定**：每个生物的派生函数必须对应参考 `setupAnim` 的输入语义，并有逐分支测试；不可同一化。
5. **不引入客户端完整 AI**：不创建 Goal、不消费路径/目标；只做"从同步状态推出渲染字段"。

## 影响面

- `Client/World`：新增少量 presentation 字段与 tick 派生（或 provider state）。
- `Client/Render`：Wolf/Fox/Panda/PolarBear/Rabbit/Wither/Parrot provider。
- 测试：每个派生字段的输入→输出测试 + 零分配测试。
- 文档：`docs/modules/entity-ai-presentation.md` 记录每个字段来源与阻塞。

## 替代方案与被否原因

| 方案 | 否决原因 |
| --- | --- |
| 客户端实现完整 AI（Goal/Target） | 越过原则 4/5，且无服务端权威输入。 |
| 按"看起来像"近似状态 | 违反不近似原则，且难以审计。 |
| 维持阻塞 | 保留缺口；仅在确实无同步来源时采用。 |

## 复查条件

- 若服务端同步了更多字段（如 sneeze/stand 状态），改为纯同步实现并删除本地派生。
- 若本地派生与服务器表现长期不一致，回退为阻塞并记录。
