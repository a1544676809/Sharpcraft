<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[已评审]
基线：Minecraft 26.3
证据：`docs/decisions/0022-client-entity-presentation-state.md`；分析工作区 `ref://mc-26.3-analysis/src/...`（各生物 `*Renderer.extractRenderState` / `*Model.setupAnim`）
最后更新：2026-11-04

# 客户端实体表现状态层

在客户端每 tick 由同步数据与本地可确定信息派生渲染所需的实体表现状态（非权威），为动画/表现提供类似原版 `extractRenderState` 的输入。

## 1. 职责范围

负责：

- 每个客户端 tick 对每个 `TrackedEntity` 维护"表现状态"（非权威）；
- 从服务端同步信息派生通用表现字段：`isInWater`、`onGround`、乘客/`isRiding`、`flap/flapSpeed`、`tentacleAngle`、`pose`（可判定部分）；
- 逐生物的可确定表现字段（如 `isSuffocating`、`isCharging` 的扩展）；
- 将这些字段写入 `EntityRenderState` / `IEntityAnimationState`，供 provider `Apply` 使用；
- 保证热路径（每 tick、每实体）零分配。

明确不负责：

- 任何权威逻辑：AI 目标、寻路、攻击命中/伤害、生成/掉落、Boss 专属权威状态；
- 网络协议/同步语义的改动（只消费 `Common` 已解码的数据）；
- 渲染本身（几何/材质/管线，属渲染层）；
- 用近似值替代无法确定的字段（必须报告并保持未完成）。

## 2. 侧别归属

- 归属：`Client`（纯表现，不改变实体模拟、不推进世界权威）。
- 判定依据：原则 4/5；ADR-0022。原版对应物是客户端 `*Renderer.extractRenderState` 会读取的客户端实体刻字段，而非服务端 AI。

## 3. 依赖关系

| 方向 | 对象 | 说明 |
| --- | --- | --- |
| 依赖 | `Client.World.EntityTracker` / `EntityRenderState` | 同步位置/速度/装备/entity_data/entity_event |
| 依赖 | `Client.World.ClientWorld`（只读方块查询） | `isInWater` 等本地可确定信息 |
| 依赖 | `Common.Network` | 已解码的同步数据，不新增协议 |
| 被依赖 | `EntityAnimationFrame` / provider `Apply` | 动画输入 |
| 被依赖 | 未来表现层（粒子/音效/名牌） | 复用同一派生状态 |

不得让 `Client.World` 反向依赖 `Client.Render`；该层属 `Client` 表现侧。

## 4. 对外契约

- 状态对象：每个 `TrackedEntity` 一份表现状态（随实体生命周期，首次创建、移除释放）。
- 更新入口：`Tick(in EntityPresentationInput input)`（tick 一次，读同步数据 + 本地查询）；
- 投影入口：`Project(in EntityRenderState baseState)` → 填充派生字段（或由 `EntityTracker.Interpolate` 汇总）；
- 不暴露：服务端权威对象、WGPU 句柄、网络原始缓冲。

## 5. 关键数据结构

| 结构 | 用途 | 生命周期 | 所属线程 |
| --- | --- | --- | --- |
| `EntityPresentationState` | 每实体表现派生状态 | 随 `TrackedEntity` | 主线程 |
| `WalkAnimationState`（已有） | 走路相位/速度 | 每实体 | 主线程 |
| 通用派生字段（`IsInWater`/`IsRiding`/`Flap`…） | 渲染输入 | 每实体、每 tick 更新 | 主线程 |

## 6. 行为对齐与偏离

对齐点：

- 字段语义逐项对照原版 `*Renderer.extractRenderState` 与实体类中的客户端派生逻辑（分析工作区证据指针）。
- `flap/flapSpeed`：按原版客户端 `calculateFlapping` 的"未落地且非乘客加速、否则衰减"规则；`tentacleAngle`：按 Squid 客户端刻规则。
- `isInWater`/`onGround`/`isRiding`：按本地方块查询与乘客关系；与服务器一致时无偏差。

有意偏离 / 未做：

- 依赖服务端 AI 且客户端无法确定的字段（如 Illager `armPose`、Piglin `PiglinArmPose`、`crossbowCharge`）暂不做，相关生物动画保持阻塞，不以近似冒充。

## 7. 分期与验收

- **P0a 通用派生（已实现 2026-11-04）**：`EntityPresentationState`（每实体）承载 `WalkAnimationState` + `FlapAnimationState` + `IsOnGround`；`EntityTracker.Tick` 派生 walk/flap/ground 并投影到 `EntityRenderState`/`EntityAnimationFrame`（新增 `IsOnGround`）。`FlapAnimationState` 逐式复刻 Chicken/Parrot 的 `flap/flapSpeed/flapping` 规则；已知偏差：乘客项（骑乘鹦鹉不扇翅）因暂无乘客同步，暂以 `!onGround` 代替，登记为受控偏差。验收：公式逐值测试、每 tick 零分配。
- **P0b（完成 2026-11-04）**：`isInWater` 已由 `ClientWorld.IsWaterAt`（本地方块 `minecraft:water` 查询）派生；`isRiding`/`isVehicle`/`isPassenger` 已由 `set_passengers`（clientbound 109）解码并维护乘客图，投影为 `IsInWater`/`IsPassenger`/`IsVehicle`。
- **P1 逐生物可判定字段（进行中 2026-11-04）**：Squid `tentacleAngle`（客户端 `tentacleMovement/tentacleSpeed`，事件 19 重置）、Bat `IsResting`（`DATA_FLAGS` bit0）、Parrot `pose`（FLYING/STANDING/ON_SHOULDER/SITTING/PARTY；PARTY 由 `level_event` 1010/1011 派生）与 Strider `isRidden`（`IsVehicle`）均已派生并在热路径生效。Strider `isSuffocating` 仅影响纹理不影响 setupAnim。
- **P2 同步来源字段（进行中）**：P2-A 已实现通用 swing 通道 —— 解码 `ClientboundSwingAnimationPacket`(123) 并在 `SwingAnimationState`（参考 `LivingEntity.SwingState`）中派生 `EntityAnimationFrame.SwingAnimation`；另补 **used-item 状态**（`DATA_LIVING_ENTITY_FLAGS` bit0 + 本地每 tick 计时）→ `IsUsingItem`/`TicksUsingItem`，十字弩蓄力进度按默认 25 tick 驱动（快速装填附魔未建模）。armPose 剩余 AI 派生布尔见 ADR-0023，未实现者保持阻塞、不近似。
- **P3 SCALE / 属性同步（SCALE 已实现；通用属性系统未完成，2026-11-04）**：
  - **已实现**：解码 `update_attributes`(134)；`AttributeTypes` 提供 40 个属性名/注册 id（`scale`=30）；提取 `SCALE` 的 base + `add_value`/`add_multiplied_base`/`add_multiplied_total`，写入 `EntityPresentationState.Scale`；`TrackedEntity.EyeY` 与 `EntityModelTransform` 使用 Scale。
  - **未实现（重要缺口）**：
    1. **没有每实体 `AttributeMap`**：`update_attributes` 中除 SCALE 外的所有快照（`max_health`、`movement_speed`、`attack_damage` 等）解析后丢弃，不能查询；
    2. 没有通用 `GetAttribute(id)` / modifier 重算 API，也没有属性更新/替换的生命周期语义；
    3. **float 类型的 entity data 尚未解码**：`LivingEntity` health（index 9, serializer 3）当前不进入 `EntityPresentationState.Health`（`EntityDataItem.Value` 为 NotAnInteger），因此 health 也不可用于动画；
    4. 因此 **Wolf 的驯服尾巴角**仍无法计算（需要 `max_health` 与 health 比值）；这是当前唯一由属性/数值缺失导致的动画阻塞点。
  - **建议后续（按序）**：
    1. `TrackedAttributeMap`：每实体保存 `attributeId → (base, modifiers)`，`update_attributes` 整表替换（或按语义更新），提供 `GetAttribute(id)`；modifier 三类 operation 统一计算；
    2. 扩展 `EntityDataPackets` 对 FLOAT（serializer 3）解码，把 health 投影到 presentation；
    3. Wolf provider 使用 `max_health`/health 计算 `tailAngle` 的 tame 分支；
    4. 测试：属性包多属性/多 modifier、属性替换、health、Wolf tail angle；热路径零分配。
  - 复查条件：属性映射/生命周期与参考 `AttributeMap` 对齐；若服务端使用自定义属性 id，按未知属性安全跳过并记录。

## 8. 未解决问题与风险

- 客户端派生不可避免依赖本地世界快照，若与服务器不一致会带来表现偏差；需要诊断计数与受控偏差登记。
- 乘客关系需要网络中的乘客同步（`set_passengers`），当前可能未解码，需先确认数据来源。
- `flap/tentacle` 的客户端刻派生必须与原版逐值一致；否则 Squid/Bat/Parrot 会"看起来差不多但不等效"。

## 9. 参考资料

- `docs/decisions/0022-client-entity-presentation-state.md`
- `docs/modules/entity-animation.md`
- `docs/modules/entities.md`
- `ref://mc-26.3-analysis/src/net/minecraft/client/renderer/entity/*Renderer.java`（`extractRenderState`）
