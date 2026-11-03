<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 实体动画系统（Entity animation）差距分析
summary: 26.3 的 AnimationDefinition / KeyframeAnimation / ModelPart 姿态覆盖语义，以及本项目现状
status: 草稿
updated: 2026-11-03
-->

# 实体动画系统差距分析

## 1. 现状

当前所有实体模型都按 `createBodyLayer()` 的 **bind pose** 发射几何；没有 `setupAnim`、动画状态、关键帧或每帧姿态覆盖。模型与装备层（`EntityModelPart`、`EntityModelLayerHelpers.WithEquipment`）都使用静态部件变换。

因此走路、尾巴、耳朵、翅膀、触手、Armadillo 翻滚、Warden/Creaking/EnderDragon 等可见运动全部缺失。静态 bind pose 的模型移植本身已完成，动画是下一个独立能力。

## 2. 原版动画模型

### 2.1 数据结构

- `AnimationDefinition`：`lengthInSeconds`、`looping`、`boneName → List<AnimationChannel>`。
- `AnimationChannel`：
  - `Target` 三种：`POSITION`、`ROTATION`、`SCALE`；
  - `Keyframe[]`：每帧有 `timestamp`、`preTarget`、`postTarget`、`Interpolation`。
- `Interpolation`：`LINEAR` 使用 `postTarget(prev) → preTarget(next)` 线性插值；`CATMULLROM` 用相邻四点做 Catmull-Rom。
- `KeyframeAnimations` 的构造语义：
  - `posVec(x,y,z)` 返回 `(x, -y, z)`（动画的 Y 轴方向与模型空间不同，容易写反）；
  - `degreeVec` 把角度转弧度；
  - `scaleVec` 返回 `(x-1, y-1, z-1)`，是 **加性缩放偏移**。

### 2.2 求值

`KeyframeAnimation.bake(root)` 先按名字解析骨骼部件；`apply(millisSinceStart, targetScale)`：

1. 秒数 = 毫秒 / 1000；`looping` 时对 `lengthInSeconds` 取模，否则直接使用；
2. 对每个通道用二分找前一个关键帧，计算 `lerpAlpha`，再用该帧的插值函数取目标向量；
3. 乘以 `targetScale`；
4. 调 `Target.apply(part, vector)`。

`applyWalk(animationPos, animationSpeed, speedFactor, scaleFactor)` 把 walk 参数映射为时间与 `targetScale = min(animationSpeed * scaleFactor, 1)`；`applyStatic()` 等价于时间 0、scale 1。

### 2.3 姿态生命周期

- `Model.setupAnim(state)` 首先调用 `resetPose()`：所有 `ModelPart` 回到 `initialPose` 的位置/旋转/缩放。
- `ModelPart` 的动画 target 是 **加性**：
  - `offsetPos` 加到 x/y/z；
  - `offsetRotation` 加到 xRot/yRot/zRot；
  - `offsetScale` 加到 xScale/yScale/zScale。
- 因此每帧必须“重置到初始姿态 → 应用动画覆盖 → 生成几何”，不能把上一帧的偏移继续累加。

### 2.4 状态来源

- `AnimationState` 只保存 `startTick`，`start/stop/animateWhen`，`getTimeInMillis(ageInTicks)` = `(ageInTicks - startTick) * 50`。
- 原版实体在 `extractRenderState` 时把 `ageInTicks`、walk 参数、交互/状态计时写入 render state；具体动画类再从状态选择 animation definition 与 `AnimationState`。
- 例：`CamelAnimation.CAMEL_WALK` 是 1.5 秒循环，腿的 rotation + position 用 CATMULLROM；`ArmadilloAnimation.ARMADILLO_ROLL_UP` 是 0.5 秒线性序列。

## 3. 本项目的接入点

- 模型部件：`EntityModelPart` 已含 pivot/rotation/scale 以外的全部几何；需要在渲染前叠加一个 **pose offset**（position/rotation/scale），且父子层级继承。
- 网格生成：`EntityModelMesher` 当前 `PartMatrix(part, ...)` 只用静态 `PartPose`；动画偏移必须在矩阵组合前应用。
- 装备层：`WithEquipment`/`WithCopy` 生成的副本也应按同一 part 名字继承同一动画 pose；否则装备会与身体脱节。
- 状态：`EntityRenderState` 需要增加 `AgeInTicks`、`WalkAnimationPos/Speed`、`AnimationState` 快照或可直接派生的状态；这些字段来自网络移动速度/客户端 tick，不得读取 `Entity` 引用。
- 性能：动画每帧对每个实体、每个部件、每个通道求值必须零/低分配；关键帧和 bake 表应在模型级缓存，render state 只携带瞬时参数。

## 4. 迁移策略

1. **先做纯求值内核**：`AnimationDefinition`、`Keyframe`、`AnimationChannel`、`KeyframeAnimation`、`KeyframeAnimations` 的 C# 等价实现；用 Camel walk 和 Armadillo roll 的关键帧做数值测试（时间 0/中间/循环回绕）。
2. **骨骼姿态**：为每个模型建立 part-name → pose offset 的轻量结构；`EntityModelMesher` 在静态 PartFrame 上叠加 pose；装备副本按同名继承。
3. **一个四足样板**：Camel 或 Pig 的 walk/idle，先在沙盒多帧截图验证，再接真实实体状态。
4. **一个两足样板**：Zombie/Villager 的手臂摆动、头部朝向。
5. **按实体扩展**：翅膀/尾巴/触手、Armadillo、Warden、Creaking、EnderDragon；每个动画在独立类中登记，不在渲染器写分支。
6. **装备/可见性**：动画 pose 与现有装备层、`VisibleWhen` 正交组合。

## 5. 未解决问题

- 动画定义是原版 Java 中的硬编码行为数据，不是资源 JSON；本项目需以行为规格重写成 C# 数据表，不能复制实现。
- `AnimationState` 的 startTick 需要与实体 AI/交互同步；第一版可先用客户端 tick 与 movement speed 派生 idle/walk，再补交互动画状态。
- 不同实体动画数量巨大，必须分批；不能把“generic walk”当作所有实体完成。
- 动态装备层/透明层与动画组合后，需要多帧回归，不仅是首帧截图。

## 6. 参考资料

- `docs/modules/entity-animation.md`
- `docs/analysis/render/entity-render-backlog.md`
- `docs/analysis/render/entity-equipment.md`

