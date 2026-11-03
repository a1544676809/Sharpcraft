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
| 被依赖 | `EntityModelMesher` / 实体渲染器 | 每帧取 pose |
| 被依赖 | 装备层 `WithEquipment`/`WithCopy` | 同名部件继承 pose |

## 4. 对外契约

- `AnimationDefinition`：长度、是否循环、bone → channels；
- `AnimationChannel`：target（Position/Rotation/Scale）、keyframes、interpolation；
- `KeyframeAnimation`：`Bake(rootPartNames)`、`Apply(seconds, targetScale, PoseBuffer)`；
- `AnimationState`：start tick、start/stop/animateWhen、elapsed millis；
- `EntityPose`：`TryGet(partName)` → `PoseOffset(Position, Rotation, Scale)`；
- `EntityAnimationController`：按实体类型与 render state 选择动画、计算 `EntityPose`。

契约不暴露：原版 `ModelPart` 对象、WGPU 句柄、实体模拟对象。

## 5. 关键数据结构

| 结构 | 用途 | 生命周期 | 所属线程 |
| --- | --- | --- | --- |
| `AnimationDefinition` | 静态动画表 | 进程级只读 | 任意 |
| `AnimationChannel` / `Keyframe` | 关键帧与插值 | 进程级只读 | 任意 |
| `AnimationState` | 动画开始 tick | 每实体 | 主线程 |
| `EntityPose` | 每帧部件覆盖 | 每帧、可复用缓冲 | 主线程 |
| `PoseOffset` | 位置/旋转/缩放增量 | 值类型 | 主线程 |

## 6. 行为对齐与偏离

对齐点：

- 每帧先回到 `initialPose`，再叠加动画增量（与 `Model.setupAnim → resetPose` 一致）；
- `posVec` 的 Y 取反、`scaleVec` 是 `x-1` 的加性缩放；
- 时间秒数、循环取模、二分找关键帧、`targetScale = min(walkSpeed*scale,1)`；
- 父子部件层级继承。

偏离/限制：

- 第一版只覆盖 idle/walk 和少量实体，不一次性移植全部实体动画；
- 动画状态中的交互/攻击状态先用客户端可观测的简化输入，后续补 AI 状态同步。

## 7. 分阶段与验收

1. **A1 求值内核**：`AnimationDefinition`/`KeyframeAnimation`/`AnimationState`/`EntityPose`；单元测试 Camel walk 和 Armadillo roll 的时间/循环/插值。
2. **A2 网格接入**：`EntityModelMesher` 支持 pose buffer，装备副本同名继承；固定帧 screenshot/数值测试。
3. **A3 四足样板**：Pig/Camel 的 idle+walk，接入真实 render state。
4. **A4 两足样板**：Zombie/Villager 手臂与头部。
5. **A5 实体扩展**：按 backlog 逐类移植；每类独立验收。

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
