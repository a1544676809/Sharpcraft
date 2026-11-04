<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[已评审]
基线：Minecraft 26.3
证据：`docs/analysis/render/entity-animation.md`
最后更新：2026-11-03

# 实体动画系统

在静态 bind pose 模型之上，按实体状态每帧计算部件的位置/旋转/缩放覆盖，供实体网格与装备层共同使用。

## 1. 职责范围

负责：

- 关键帧动画定义、关键帧求值与线性/Catmull-Rom 插值；
- 每个实体每帧的姿态覆盖（position/rotation/scale），以 part name 为 key；
- 动画状态/时间/循环语义；
- 动画与现有 `EntityModelPart` 层级、装备副本、可见性层的组合。

明确不负责：

- 动画资源（原版动画是 Java 行为数据，不是资产 JSON）；
- 物理/AI 状态机本身（走路速度等由实体状态输入）；
- 第一阶段的 IK、蒙皮、GPU 骨骼动画；
- 物品/方块动画（另立模块）。

## 2. 侧别归属

- 归属：`Client`（纯表现，不改变实体模拟）。
- 判定依据：`docs/analysis/render/entity-animation.md` 2、3；动画状态来自 render state，渲染器不得读 `Entity`。

## 3. 依赖关系

| 方向 | 对象 | 说明 |
| --- | --- | --- |
| 依赖 | `Client.Render.EntityModelPart` | pose 覆盖按 part name 应用 |
| 依赖 | `Client.World.EntityRenderState` | `AgeInTicks`、walk 参数、动画状态 |
| 提供 | `Client.World.EntityAnimationFrame` / `IEntityAnimationState` | 通用输入与每实体状态契约（World 侧，无渲染类型） |
| 被依赖 | `EntityModelMesher` / 实体渲染器 | 每帧取 pose |
| 被依赖 | 装备层 `WithEquipment`/`WithCopy` | 同名部件继承 pose |

## 4. 对外契约

- `AnimationDefinition`：长度、是否循环、bone → channels；
- `AnimationChannel`：target（Position/Rotation/Scale）、keyframes、interpolation；
- `KeyframeAnimation`：`Bake(rootPartNames)`、`Apply(seconds, targetScale, PoseBuffer)`；
- `AnimationState`：start tick、start/stop/animateWhen、elapsed millis；
- `EntityPose`：`TryGet(partName)` / `Add(partName, PoseOffset)`（position/rotation/scale 加性覆盖）；
- `EntityAnimationFrame`（`Client.World`）：通用动画输入，`From(EntityRenderState)` 投影；
- `IEntityAnimationState`（`Client.World`）：每实体可变状态的空标记，具体实现归各 provider；
- `IEntityAnimationProvider`（`Client.Render`）：`TypeId`、`Clips`、`CreateState()`、`Apply(state, frame, pose)`（热路径）、`ApplyClip(state, frame, clip, seconds, pose)`（沙盒/工具）；
- `EntityAnimationRegistry`（`Client.Render`）：`TryGet` / `For` / `CreateState` / `Clips` / `All`，按 typeId 查找 provider；
- `WalkAnimationState`（`Client.World`）：原版走路动画位置/速度的平滑与插值；
- `EntityAnimationTickContext` + `IEntityTickAnimationState`（`Client.World`）：每 tick 回调（计数递减、时钟 `animateWhen`）；
- `IEntityEventAnimationState`（`Client.World`）：`entity_event`（clientbound 34）触发的一次性 clip 时钟；
- `IEntityPoseAnimationState`（`Client.World`）：`set_entity_data` 的 `Pose`（accessor 6，serializer 20）变化时启动/切换 clip；
- `IEntitySyncedDataAnimationState`（`Client.World`）：把生物自己的整型 synced data（如 Creaking `CAN_MOVE`=16 / `IS_TEARING_DOWN`=18）交给 state 解释；
- `AnimationClipPlayer`（`Client.Render`）：从 `AnimationState` 时钟按 `(age-startTick)*50ms` 播放 baked clip。

`EntityTracker` 通过注入的 `AnimationStateFactory` 为每个 `TrackedEntity` 创建一次 state，`EntityRenderState.Animation` 携带该引用；`EntityModelMesher` 每帧为每个实体准备一份复用 `EntityPose` 并调用一次 provider `Apply`。原 `EntityAnimationController` 的静态 switch 已删除。

**通用状态提取（2026-11-04）**：
- `EntityTracker.Tick()` 用每 tick 实际水平位移驱动 `TrackedEntity.WalkAnimation`（原版 `calculateEntityAnimation` → `updateWalkAnimation`：默认 `min(dist*4,1)`/factor 0.4/幼年位置 ×3，Creaking 覆写 `min(dist*25,3)`），`Interpolate` 输出 `WalkAnimationPos/Speed`，因此走路动画在真机生效（此前恒为 0）。
- `entity_event`（id 34）解码为 `entityId + eventId`，路由到 `IEntityEventAnimationState.OnEntityEvent`；Warden 以此启动 attack(4)/sonic_charge(62)/tendril(61)，Creaking 启动 attack(4)/invulnerability(66)。
- `Tick()` 每 tick 调用 `IEntityTickAnimationState.OnTick`，让 Creaking 的攻击/无敌计数递减并 `animateWhen`，Warden 的触须倒计时递减；`EntityAnimationFrame.PartialTick` 提供帧内插值，触须按原版双帧 `lerp` 取值。
- `set_entity_data` 的 `Pose`（index 6 / serializer 20）路由到 `IEntityPoseAnimationState`：Warden `EMERGING(13)/DIGGING(14)/ROARING(11)/SNIFFING(12)` 启动对应 clip；`IEntitySyncedDataAnimationState` 把整型字段交给 state，Creaking 用 `CAN_MOVE(16)` 门控走路、`IS_TEARING_DOWN(18)` 驱动死亡 clip。

## 5. 关键数据结构

| 结构 | 用途 | 生命周期 | 所属线程 |
| --- | --- | --- | --- |
| `AnimationDefinition` | 静态动画表 | 进程级只读 | 任意 |
| `AnimationChannel` / `Keyframe` | 关键帧与插值 | 进程级只读 | 任意 |
| `IEntityAnimationProvider` | 每生物控制器（单例） | 注册期创建、进程级 | 任意 |
| `IEntityAnimationState` | 每实体可变状态 | 随 `TrackedEntity`，首次出现创建 | 主线程 |
| `WalkAnimationState` | 走路位置/速度平滑 | 每实体、随 `TrackedEntity` | 主线程 |
| `EntityAnimationFrame` | 每帧通用输入（值类型） | 每帧、栈上 | 主线程 |
| `AnimationState` | 动画开始 tick（状态实现内） | 每实体 | 主线程 |
| `EntityPose` | 每实体部件覆盖（Mesher 复用缓冲） | 每实体、跨帧复用 | 主线程 |
| `PoseOffset` | 位置/旋转/缩放增量 | 值类型 | 主线程 |

## 6. 行为对齐与偏离

对齐点：

- 每帧先回到 `initialPose`，再叠加动画增量（与 `Model.setupAnim → resetPose` 一致）；
- `posVec` 的 Y 取反、`scaleVec` 是 `x-1` 的加性缩放；
- 时间秒数、循环取模、二分找关键帧、`targetScale = min(walkSpeed*scale,1)`；
- 走路位置/速度来自真实位移（`WalkAnimationState`，含幼年 ×3 与 Creaking 覆写），不再由沙盒伪造；
- 一次性 clip 由 `entity_event` 与每 tick 计数驱动 `AnimationState`，再经 `AnimationClipPlayer` 按 `(age-startTick)*50ms` 采样；
- 父子部件层级继承。

偏离/限制：

- 第一版只覆盖 idle/walk 和少量实体，不一次性移植全部实体动画；
- 动画状态中的交互/攻击状态先用客户端可观测的简化输入，后续补 AI 状态同步。

## 7. 分阶段与验收

0. **A0 provider 基座（已完成 2026-11-03）**：`IEntityAnimationProvider` + 每生物 `IEntityAnimationState` + `EntityAnimationRegistry` + `EntityAnimationFrame` + `EntityRenderState.Animation` 接线；现有 controller 全部迁入 provider，行为与旧测试不变；沙盒改从 registry 取 clip；每 provider 稳态零分配测试。
1. **A1 求值内核**：`AnimationDefinition`/`KeyframeAnimation`/`AnimationState`/`EntityPose`；单元测试 Camel walk 和 Armadillo roll 的时间/循环/插值。
2. **A2 网格接入**：`EntityModelMesher` 支持 pose buffer，装备副本同名继承；固定帧 screenshot/数值测试。
3. **A3 四足样板**：Pig/Camel 的 idle+walk，接入真实 render state。
4. **A4 两足样板**：Zombie/Villager 手臂与头部。
5. **A5 实体扩展**：现有已移植者已在 A0 迁入 provider；其余按 backlog 逐类新增 provider，每类独立验收。批次 1（Warden / Creaking / Breeze）已完成数据 + provider + 沙盒 clip，热路径仅通用帧可表达部分；one-shot 与 Breeze 六 clip 待 render-state 动画状态接线（T-568）。

## 8. 未解决问题与风险

- 动画定义是行为数据，移植工作量大且容易把角度/Y 轴符号写反；每类必须有数值测试。
- 动画状态与 AI 交互同步未完成，第一版 walk 参数需从客户端移动速度近似。
- 装备/透明/可见性组合需要多帧回归。
- 性能：每帧每实体每通道求值必须避免分配；bake 表缓存，pose 缓冲复用。

## 9. 参考资料

- `docs/analysis/render/entity-animation.md`
- `docs/analysis/render/entity-render-backlog.md`
- `docs/modules/entity-render.md`

- docs/analysis/render/entity-animation-architecture.md
