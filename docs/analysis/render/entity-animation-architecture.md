<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 实体动画架构与团队并行方案
summary: 现有动画 switch/共享 RenderState 的瓶颈，provider + 每生物状态上下文的组织方式与效率判断
status: 草稿
updated: 2026-11-03
-->

# 实体动画架构与团队并行方案

## 1. 问题

当前实现把动画控制在 `EntityAnimationController` 的一个 typeId `switch` 中，把状态放在单一 `EntityRenderState`/`TrackedEntity` 上。单生物可行，但扩展全部实体时会出现：

- `switch` 与共享文件持续增长，每个新生物都要改同一文件；
- `EntityRenderState` 变成所有生物字段的并集；
- 多个 teammate 不能安全地并行改这些共享文件；
- “数据表移植”与“状态提取/控制器接线”分散在不同轮次，后一轮很难验证前一轮的行为。

结论：**当前结构适合样板，不适合按生物批量补全。**

## 2. 原版对照

原版不是 provider registry：

- 每个实体有独立 `EntityRenderer` / Model 类；
- `setupAnim(state)` 在 Model 类内，Model 直接持有自己的部件和动画字段；
- RenderState 按实体拆分（`WolfRenderState`、`WardenRenderState` 等）；
- `EntityRenderers` 负责 type → renderer 映射。

这与项目“行为等效而非结构复刻”的原则不冲突：我们可以在 C# 侧用 registry/provider 适配，但不能把不同生物的全部状态压进一个全局记录。

## 3. 目标架构

### 3.1 核心抽象

```text
IEntityAnimationProvider
  int TypeId
  IReadOnlyList<string> Clips
  IEntityAnimationState CreateState()
  void Update(IEntityAnimationState state, in EntityAnimationFrame frame)
  void Apply(IEntityAnimationState state, string clip, float seconds, EntityPose pose)

IEntityAnimationState
  每个生物自己的可变状态（动画时钟、上次姿态、特殊因子）
```

- `EntityAnimationFrame` 只携带**所有生物共用**的输入：年龄、walk 参数、朝向/俯仰、baby、是否隐藏等；
- 特殊字段（例如 Axolotl 的 inWater/onGround/moving、Frog 的 jump/croak AnimationState、Armadillo 的 roll state）由对应 `IEntityAnimationState.Update` 从通用帧与网络数据派生/保存；
- `EntityAnimationRegistry` 只做 `TypeId → provider` 查找；
- `EntityRenderState` 只增加一个 `IEntityAnimationState? Animation` 引用（每实体一个，不每帧分配），Mesher 调用 provider `Apply`。

### 3.2 每生物文件边界

每个生物一个垂直切片，全部新建文件：

- `src/Sharpcraft.Client/Render/<Creature>Animations.cs`：关键帧/程序化数据；
- `src/Sharpcraft.Client/Render/<Creature>AnimationProvider.cs`：`IEntityAnimationProvider` 实现与自己的 `IEntityAnimationState`；
- `tests/Sharpcraft.Client.Tests/Render/<Creature>AnimationTests.cs`：数据、状态、控制器行为测试；
- 若模型缺部件，才修改自己的 `<Creature>ModelDefinition.cs`。

**禁止**修改 `EntityAnimationController`、`EntityTracker`、`EntityRenderState`、`SandboxWindow`；Lead 统一接线和验收。

### 3.3 状态流

```text
network/entity state
  → TrackedEntity / generic EntityAnimationFrame
  → provider.IEntityAnimationState.Update(frame)
  → provider.Apply(state, clip, seconds, EntityPose)
  → EntityModelMesher.Build(..., pose)
  → 装备副本按原名继承同一 pose
```

Provider 的 `State` 是每实体一份的可变对象；`Apply` 只写 `EntityPose`，不修改模型定义或全局状态。

## 4. 效率判断

### 4.1 一次性改造成本

- 接口 + registry + 通用 frame + `EntityRenderState.Animation`：一次 Lead 改造；
- 把现有 Pig/Camel/Horse/Zombie/Villager 等 controller 移入 provider：一次性迁移；
- 之后每生物只新增文件，Lead 只增加一行注册。

### 4.2 并行收益

- 每个 agent 拥有完整垂直切片，不需要 Lead 每次补 switch/状态字段；
- 文件不重叠，teammate 可以安全并行；
- 每个生物的测试和状态清单在同一个任务内闭环，等价性更容易审计。

### 4.3 不适用的情况

- 需要改 shared registry/state 基础设施的架构任务仍由 Lead 串行；
- 小生物不必单独开 agent，可合批；
- 多个 agent 同时跑完整 `dotnet build/test` 会争用 bin 锁，最终构建/全量测试由 Lead 执行。

**判断：一次性做 provider/state 改造后，按生物派单是高效且合理的；不改共享结构直接堆 switch，不可持续。**

## 5. 单个生物任务模板

任务书必须包含：

1. 原版状态清单：`setupAnim` 中所有状态、选择条件、使用的 render-state 字段；
2. 交付文件（仅本生物三个新文件，必要时模型定义）；
3. 关键帧/程序化公式来源与逐项对照结果；
4. 测试：每个状态的至少一个代表时间点、循环/one-shot 长度、缺失状态报告；
5. 沙盒可用 clip 名称与切换验证；
6. 明确禁止修改共享文件、禁止 commit。

## 6. 迁移步骤

1. Lead 完成 `IEntityAnimationProvider` / registry / frame / state 引用；
2. 迁移现有 Animal/procedural controller，保留行为测试；
3. 沙盒改为从 registry 列出 clips；
4. 按生物派单（2–3 个并行）；
5. Lead 每批做全量构建/测试/截图，更新 traceability 与 STATE。

## 7. 待评审问题

- `EntityAnimationFrame` 需要覆盖哪些通用字段，特殊状态字段由 provider state 保存还是由通用帧扩展；
- provider state 的创建/释放时机（随 `TrackedEntity` 生命周期）；
- 是否引入 `IEntityAnimationState` 接口到 `Client.World` 以避免 World→Render 依赖；
- 是否将装备层动画继承也纳入 provider 的 pose 名称规则（当前用 part 名前缀剥离）。

## 8. 参考资料

- `docs/analysis/render/entity-animation.md`
- `docs/modules/entity-animation.md`
- `docs/analysis/render/entity-render-backlog.md`
