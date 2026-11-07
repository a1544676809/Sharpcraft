<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 模型几何不变量门禁策略
summary: 为什么先做 UV/面积全量扫描，而把 Z-fighting 与装备 clearance 留到诊断稳定之后
status: 已评审（排期策略）
updated: 2026-11-07
-->

# 模型几何不变量门禁策略

## 1. 决策

| 检查 | 当前处理 | 原因 |
| --- | --- | --- |
| UV 边界与面积 | 立即升级为架构门禁 | 静态、可用原版贴图尺寸精确判定，误报低 |
| 幼年/成年共享贴图 UV 跨度 | 保留并扩展 | 直接覆盖 T-662/T-663 类事故 |
| Z-fighting 共面扫描 | 暂不做硬门禁，先做诊断报告 | 合法 overlay/armor/collar 大量共面，直接报警会失真 |
| 装备 clearance | 暂不做全量硬门禁，先做 rest-pose 诊断 | 装备包裹身体是合法重叠，且动画姿态需要单独定义容差 |

## 2. UV/面积扫描为什么必须做

当前已有 `ModelUvInvariantTests`，但只扫描 `EntityModelDefinition.Parts`，而模型已经全部迁移到 `Compiled` 树，装备层又通过独立 `EntityRenderLayerDefinition.Model` 生成 cube。flat parts 与编译 cube 可能不再同源，因此必须扫描：

- `Compiled.Cubes`；
- 全部注册变体；
- adult / baby 两套解析结果；
- 装备树 `Model` 的 compiled cubes；
- 非零厚度的 UV 面积与贴图边界。

这是纯几何数据检查，不需要动画、相机或像素夹具，适合作为持续门禁。

## 3. Z-fighting 为什么暂不做全量硬门禁

原版允许大量有意的共面/重叠：

- hat、jacket、sleeve、pants 等 skin overlay；
- armor 外层与 base body；
- saddle、harness、collar、markings、crackiness；
- Elytra 中缝重叠；
- 多个零厚度 fin/平面。

仅按“共面 + 相反法线”检测会把正常覆盖层全部报出。必须先有诊断报告，按模型、part、layer、三角形对和间距分类，再定义合法覆盖基线。否则门禁会退化为维护大 allowlist，掩盖真实问题。

## 4. Equipment clearance 为什么暂不做全量硬门禁

装备与身体本来就有合法的包含关系：

- helmet/chestplate/leggings/boots 包住基础模型；
- horse saddle / camel saddle / nautilus shell / happy ghast harness 必须贴合；
- 动画会改变 clearance，静态 bind pose 只能发现极端穿模。

合理路径是：

1. 先对 rest pose 输出每个装备层与目标身体/头部的最近距离/穿透深度；
2. 对 humanoid armor、horse saddle、wolf armor、nautilus shell 等已知层补局部回归；
3. 等容差与合法重叠规则稳定后，再升级为架构门禁。

## 5. 当前落地

- `ModelUvInvariantTests` 增加 `Compiled.Cubes` 与 resolved equipment tree 扫描；
- `EntityModelResolver.RegisteredTypeIds` 为门禁提供完整类型 id；
- Z-fighting/clearance 诊断任务在后续排期单独实施，不在 UV 扫描提交中混入。
