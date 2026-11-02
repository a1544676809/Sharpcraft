<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 实体模型近似实现清单
summary: 已完成实体模型中仍需父子层级变换才能精确等价的部分
status: 已评审
updated: 2026-11-02
-->

# 实体模型近似实现清单

## 1. 判定标准

以下情况视为“近似实现”：

- 原版部件是另一个部件的子节点，但当前定义把它当成 root 下的绝对 part；
- 依赖手工计算出世界 pivot，而不是由父 pivot/rotation 递归组合；
- 因此当前只能保证单帧观感，不能保证动画/姿态变化时仍等价。

## 2. 当前已知近似

| 模型 | 近似位置 | 原版关系 | 影响 | 状态 |
| --- | --- | --- | --- | --- |
| Horse | `head_parts` 下的 head/mane/upper_mouth、ears；`body` 下的 tail | 多层父 pivot/rotation | 静态正面可看，但旋转/耳尾位置不完全精确 | 待层级化 |
| Fox | `tail` | `body` 子节点 | tail pivot 为手工合成 | 待层级化 |
| Wolf | `real_head/ears`、`real_tail` | head/tail 子节点 | 头部/尾巴静态近似 | 待层级化 |
| Rabbit | body/head/ears/frontlegs/backlegs | 多层 body/head/leg 子节点 | 当前近似模型**未注册**，不作为可用模型 | 待层级化 |
| Camel | hump/tail/head/ears | body 子节点 | 近似实现已撤销 | 待层级化 |

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

在实现通用的 `Parent` + 局部 `PartPose` 递归变换之前：

- 不把上述近似模型继续当作“已完成”；
- 不再添加新的近似模型；
- 下一步优先实现父子层级变换，然后重做 Horse/Fox/Wolf/Rabbit/Camel 的精确版本。
