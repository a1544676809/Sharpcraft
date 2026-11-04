<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 客户端实体 AI 表现状态
summary: AI 依赖动画所需的客户端只读派生字段、来源与分批
status: 已评审
-->

# 客户端实体 AI 表现状态

状态：[已评审]（2026-11-04）

基线：Minecraft 26.3。关联：ADR-0022、ADR-0023、ADR-0024。

## 1. 职责范围

负责：把**同步数据 + 事件 + 本地可确定信息**派生为渲染所需的 AI 类字段（坐下/睡眠/潜行/扑击/不悦/打喷嚏/站立/跳跃/多头朝向），不实现寻路、目标选择或战斗结算。

不负责：服务端 AI 语义、寻路、目标/攻击判定、掉落/伤害计算。

## 2. 字段来源清单（首批）

| 生物 | 字段 | 来源 | 状态 |
| --- | --- | --- | --- |
| Wolf | `isSitting` | TamableAnimal flags（index 17 byte bit0） | 已有 `IsSitting` |
| Wolf | `isAngry` | Mob flags bit2 | 已有 `IsAggressive` |
| Wolf | `isSleeping` | 待确认（姿势/同步） | 待分析 |
| Fox | `isCrouching` / `isSitting` / `isSleeping` / `isPouncing` / `isFaceplanted` | `Fox` 客户端字段；部分由 entity data（variant/flags）与本地状态派生 | 待分析 |
| Panda | `isUnhappy` / `isSneezing` / `sneezeTime` | Panda 客户端字段（AI 状态） | 待分析 |
| PolarBear | `standScale` | PolarBear 客户端字段（站立 AI） | 待分析 |
| Rabbit | `hopAnimationState` / `idleHeadTiltAnimationState` | `jumpTicks` + `idleAnimationTimeout`（本地计时） | 待分析 |
| Wither | `yHeadRots[]` / `xHeadRots[]` | Wither 客户端每头朝向 | 待分析 |
| Parrot | party（唱片机） | 需 jukebox 状态 | 阻塞 |

## 3. 设计约束

- 每个字段必须对应参考 `setupAnim` 的输入，并有"输入→输出"测试。
- 无法找到同步/本地来源的字段保持 `进行中`，不得用常量或猜测填充。
- 派生代码集中在 `Client/World`（presentation/provider state），渲染层只读帧字段。

## 4. 分批

1. **A：通用布尔/数值基础设施（已完成）**：Fox flags（sit/crouch/pounce/sleep/faceplant）、Tamable sitting（cat/ocelot/parrot/wolf）、Panda unhappy/sneeze counters、PolarBear standing、LivingEntity health（若 decode）→ presentation + frame；tick context 增加 `VerticalDistance`/`IsOnGround`。
2. **B：本地计时器**：Rabbit hop/idle、Panda sneeze、PolarBear stand（复刻客户端计时，逐项测试）。
3. **C：Wither 多头**：按参考维护每头 yaw/pitch 数组。
4. **D：Parrot party**（若 jukebox 状态可得）。

## 5. 验收与风险

- 每批：全量构建 + Client/Architecture 测试；每个新增帧字段有测试。
- 风险：客户端派生与服务器状态长期不一致；对策：字段级记录与复查条件（ADR-0024）。

## 6. 实施记录（2026-11-04）

| 生物 | provider | 已实现 | 阻塞（未近似） |
| --- | --- | --- | --- |
| Wolf | `WolfAnimationProvider` | tail yaw、四腿/坐姿、head look、angry/wild tail pitch | 驯服尾巴角（缺 max health）、shake roll、headRollAngle |
| Fox | `FoxAnimationProvider` | 走/蹲/睡/坐/扑/趴腿摆（faceplant 计数器）、head look、睡姿覆盖 | headRollAngle、crouchAmount/ageScale、腿可见性 |
| Panda | `PandaAnimationProvider` | unhappy/sneeze、四足走/head look、**sit/eat/lie/roll（精确 `PandaPoseAnimationState` 缓冲）** | IsScared（雷暴/基因输入） |
| PolarBear | `PolarBearAnimationProvider` | 基础四足 + **完整站立缓动（`PolarBearStandAnimationState`：±1/tick, 0..6, lerp/6）** | — |
| Rabbit | `RabbitAnimationProvider` | idle-not-started 的 head look + **hop（entity_event 1 → 15-tick 时钟，`JumpCompletion` 精确映射 0.75s 循环 keyframes）** | idle-head-tilt 计时（random/leash）、baby 模型 |
| Wither | `WitherAnimationProvider` | ribcage/tail age bob、中心头 look | 左右头 per-head aim：需要每头替代目标实体 id（同步）**加上目标眼高/头部世界坐标**；`EntityAnimationFrame` 无目标位置/眼高，且参考每头 `rotlerp` 是客户端 AI 瞄准状态，无法从单一 HeadYaw/Yaw 精确推出 |
| Parrot | 既有 `ParrotAnimationProvider` | flying/standing/sitting/on_shoulder | PARTY：由客户端 `LevelEventHandler` 的 jukebox 播放事件驱动（非 entity data）；当前未解码该 level event/jukebox 歌曲状态 |
