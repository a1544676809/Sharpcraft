<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 实体模型近似实现清单
summary: 已完成实体模型中仍需父子层级变换才能精确等价的部分
status: 已评审
updated: 2026-11-02
-->

# 实体模型近似实现清单

> 本文是 2026-11-02 的近似清单，保留历史记录；当前完整差距与目标架构见 [`entity-model-parity-gap.md`](entity-model-parity-gap.md)。

## 1. 判定标准

以下情况视为“近似实现”：

- 原版部件是另一个部件的子节点，但当前定义把它当成 root 下的绝对 part；
- 依赖手工计算出世界 pivot，而不是由父 pivot/rotation 递归组合；
- 因此当前只能保证单帧观感，不能保证动画/姿态变化时仍等价。

## 2. 当前已知近似

| 模型 | 近似位置 | 原版关系 | 影响 | 状态 |
| --- | --- | --- | --- | --- |
| Horse | `head_parts` 下的 head/mane/upper_mouth、ears；`body` 下的 tail | 多层父 pivot/rotation | 已改用 `Parent` + 局部 `PartPose` 递归（T-470），静态几何逐顶点校验通过 | 已层级化 |
| Fox | `tail` | `body` 子节点 | 已改用局部 pose（T-470） | 已层级化 |
| Wolf | `real_head/ears`、`real_tail` | head/tail 子节点 | 增加空 group 节点（head/tail）承载 pivot，子部件局部 pose（T-470） | 已层级化 |
| Rabbit | body/head/ears/frontlegs/backlegs | 多层 body/head/leg 子节点 | 已改为 `Parent` + 局部 pose 并注册（T-471） | 已层级化 |
| Camel | hump/tail/head/ears | body 子节点 | 按 `AdultCamelModel` 从零层级化并注册（T-471） | 已层级化 |

## 3. 已判定为不需要近似的模型

- Player / Zombie / Husk / Skeleton / Drowned / Enderman
- Creeper / Slime / Magma Cube
- Pig / Sheep / Cow / Chicken
- Spider（腿为 root part）
- Cat / Ocelot（头部 cube 共 pivot）
- Snow Golem
- Llama（head 为 root，chest 为 root）
- Mooshroom / Donkey / Mule / Zombie Horse / Skeleton Horse（复用已定义几何）

## 4. 结论

在实现通用的 `Parent` + 局部 `PartPose` 递归变换之后（T-470/T-471）：

- Horse/Fox/Wolf/Rabbit/Camel 已全部改为层级定义，本节表格中的行不再被视为近似；
- Rabbit 重新注册（type id 111），Camel 重新实现并注册（type id 19）；
- 剩余仍需多 layer 提交的部分见 §5。


## 5. 渲染层近似

`Skeleton clothing`、Player overlays、Drowned outer、Slime outer 目前把独立 RenderLayer 合并进同一 DynamicGeometry/draw，不能保证原版 layer 的 RenderType、深度、顺序和透明度等效。详见[实体渲染层架构差距](entity-render-layers.md)。


