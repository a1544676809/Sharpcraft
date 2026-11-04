<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: ADR 0023：客户端武装生物表现状态（armPose / crossbow / swing）
summary: 剩余 Illager/Piglin 等武装生物动画所需的手部姿态数据来源、分期与边界
status: 已采纳
-->

# 0023：客户端武装生物表现状态（armPose / crossbow / swing）

- 状态：已采纳
- 日期：2026-11-04
- 决策者：项目所有者（本会话确认）
- 关联：`docs/decisions/0022-client-entity-presentation-state.md`、`docs/modules/entity-presentation-state.md`、`docs/traceability.md` T-588

## 背景

Illager 系（Vindicator / Pillager / Evoker / Illusioner）与 Piglin 系（Piglin / PiglinBrute / ZombifiedPiglin）的 `setupAnim` 读取手部姿态状态：`armPose`、`mainArm`、`swingAnimation`、`ticksUsingItem` / `maxCrossbowChargeDuration`，以及 `isAggressive` / `isCelebrating` / `isSpellCasting` / `isDancing` / `isAdmiring` 等布尔。它们不在现有 `EntityAnimationFrame` 上，因此这些生物动画只能阻塞。

约束（AGENTS.md）：

- 原则 4/5：服务端权威沿用原版；客户端只做表现，不得实现权威 AI / 寻路 / 战斗结算。
- 原则 3：不粘贴实现，只写行为与数据来源；证据来自分析工作区 `ref://mc-26.3-analysis/...`。
- 原则 9/3.6：热路径零分配；行为偏差可记录。

数据来源分为三类（依据分析工作区）：

| 字段 | 来源 | 说明 |
| --- | --- | --- |
| `swingAnimation` | **新增网络包解码** | `ClientboundSwingAnimationPacket`（clientbound **123**）携带实体、手、动画类型与时长；当前尚未解码。`LivingEntity.getSwingAnimation(partial)` 由客户端 `SwingState` 派生（旧的 `ClientboundAnimatePacket`(2) 现在只含 wake/crit，不再承载挥臂）。 |
| `mainArm` | 同步数据 | `LivingEntity.getMainArm`（实体属性，可同步/可配置）。 |
| `ticksUsingItem` / `maxCrossbowChargeDuration` | 客户端使用物品状态 | 参考由实体 `getUseItem` / `CrossbowItem.getChargeDuration` 派生；需要实体 used-item 状态（部分可由 synced flags + 手中物品推断）。 |
| Piglin `isChargingCrossbow` / `isDancing` | **synced boolean** | `DATA_IS_CHARGING_CROSSBOW` / `DATA_IS_DANCING`，可经 `IEntitySyncedDataAnimationState` 直接取得。 |
| Illager/Piglin `isAggressive` / `isCelebrating` / `isSpellCasting` / `isAdmiring` | **客户端 AI 派生** | 参考里由实体 AI/客户端刻字段决定，既非网络字段也非现有帧字段；需要"窄的客户端判定"或找到同步来源。 |

## 决策

在 ADR-0022 的**非权威表现状态层**内新增一个 P2 子层"武装生物表现状态"，按数据来源优先选最省且可审计的方式：

1. **先补通用数据通道（一次投入，多处复用）**
   - 解码 `ClientboundSwingAnimationPacket`（clientbound 123）→ 维护每实体 `swingAnimation`（参考 `LivingEntity.SwingState` 曲线），加入 `EntityAnimationFrame.SwingAnimation`；
   - 增加 `EntityAnimationFrame.MainArm`（枚举 Left/Right）；
   - 增加 `EntityAnimationFrame.UsingItem` / `CrossbowChargeTicks` / `MaxCrossbowChargeDuration`（由 synced flags/手中物品 + 本地计时派生）。
2. **能同步的字段一律走同步**
   - Piglin 的 `DATA_IS_CHARGING_CROSSBOW` / `DATA_IS_DANCING` 经 `IEntitySyncedDataAnimationState` 取得；
   - 手中物品用已有的 `HasMainHandItem/HasOffHandItem`。
3. **无法同步的 AI 派生布尔：按生物写"窄判定函数"，明确限定为表现层**
   - 例如 Illager 的 `isAggressive/isCelebrating/isSpellCasting`、Piglin 的 `isAdmiring`；
   - 判定输入只能是**已同步数据 + 事件 + 本地可确定信息**；不得引入目标选择、寻路、伤害结算；
   - 每个判定函数必须有原版 `getArmPose` 的逐分支测试；无法等效的项继续标 `进行中`，不得近似。
4. **`armPose` 由各 provider 自己的枚举 + 判定函数选择**，`EntityAnimationFrame` 只携带通用原始输入（swing / mainArm / charge / has-item），不塞入生物专用枚举（避免"万能包"）。

分期：

- **P2-A**：`ClientboundAnimatePacket` 解码 + `SwingAnimation` + `MainArm` + 十字弩计时 → 先做 **Vindicator / Pillager / Evoker / Illusioner** 的 armPose；
- **P2-B**：Piglin 系（synced charging/dancing + 窄判定）；
- 每期由 Lead 做全量构建/测试与沙盒多帧验收。

## 影响面

- 代码：`Common/Network`（AnimatePacket 解码）、`Client/World`（presentation/state、frame 字段）、`Client/Render`（Illager/Piglin provider）；`InteractiveSession` 分发。
- 测试：新增 animate 解码、swing 曲线、armPose 分支、零分配测试；既有测试不改变语义。
- 文档：`entity-presentation-state.md` 增加 P2 章节；`entity-animation.md` 输入字段清单；traceability 新增任务行。
- 兼容性：纯客户端表现，不改协议（只解码已存在的包）/存档/模组 API；不影响服务端权威。

## 替代方案与被否原因

| 方案 | 否决原因 |
| --- | --- |
| 在客户端实现完整 AI/战斗状态 | 越过原则 4/5；客户端无权威输入，必然与服务器不一致。 |
| 直接按"看起来像"近似 armPose | 违反"不近似冒充等效"；臂姿错误明显且难审计。 |
| 暂不做，保持阻塞 | 保留缺口，但可作为兜底；本 ADR 选择在受限范围内推进。 |
| 把 armPose 枚举塞进通用 frame | 变成生物专用"万能包"；应由各 provider 的 state + 判定函数承担。 |

## 复查条件

- 若确定服务端会额外同步 `isAggressive` 等字段，则优先改为纯同步实现，删除窄判定。
- 若 `ClientboundSwingAnimationPacket` 与参考 swing 曲线无法逐值对齐，登记受控偏差并重评。
- 若该子层与服务器表现长期不一致，回到"仅同步"实现。
