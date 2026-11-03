<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 实体渲染剩余清单
summary: 装备层之后的实体渲染欠账：动画、装备特例、资产与验收顺序
status: 草稿
updated: 2026-11-03
-->

# 实体渲染剩余清单

本文把“目前还差什么”固定下来，避免只存在于会话中。范围是实体模型与实体渲染层；方块、UI、着色器包不在本清单内。

## 1. 已完成基线（用于判断后续差距）

- 静态 bind pose 的成年/幼年实体模型已覆盖到当前清单（包括 Panda 变体、Villager 群系纹理、Cat/Wolf 项圈）。
- 装备基础设施：`SetEquipment` 解码、8 槽状态、`ItemRegistry`、受控装备纹理映射、Horse/Camel/Nautilus/Wolf/Happy Ghast 装备层。
- 参考：`docs/analysis/render/entity-equipment.md`、`docs/modules/entity-equipment.md`、`docs/modules/item-registry.md`。

## 2. 高优先级：动画系统

**状态：未开始；这是当前最大的可观察差距。**

- 所有实体目前仍是 `createBodyLayer()` 的静态 bind pose；没有 `setupAnim` 的逐帧骨骼动画。
- 需要先定义：骨骼/部件变换模型、动画通道（位置/旋转/缩放）、关键帧与插值、状态机（idle/walk/attack/eat 等）、动画资源或数据来源。
- 典型欠账：四足腿摆动、尾巴/耳朵、鸟翅、蝙蝠/恶魂触手、Axolotl 游动、Armadillo 翻滚、Creaking/Warden/EnderDragon、僵尸/村民手臂、Happy Ghast 触手等。
- 验收不能只看静态截图：需要固定场景下多帧姿态对比，或按参考动画的关键帧数值验证。
- 动画完成后，静态 bind pose 的装备层也必须跟随同一套变换。

## 3. 装备层剩余

| 项 | 现状 | 依赖 / 验收 |
| --- | --- | --- |
| Happy Ghast `RopesLayer` | 挽具/眼罩已通过；绳索层未做 | 需要 `isLeashHolder` 状态；按 `HappyGhastRopes` 层渲染并验证 |
| Wolf armor 裂纹 | 基础 wolf armor 已通过；裂纹未做 | 需要护甲损伤/`Crackiness` 状态，接 `wolf_armor_crackiness_*` |
| Trader Llama 地毯 | 普通 16 色地毯已接 | `trader_llama` 专用装饰层/纹理 |
| Zombie Nautilus 珊瑚 | 模型与纹理已接 | 有 body armor 时隐藏 coral 层（装备状态联动） |
| 人形装备 | 未接 | Player/Zombie/Skeleton 等头盔、胸甲、腿甲、靴子 |
| Witch 手持物品 | 未接 | 需要物品模型渲染（后置）；`WitchItemLayer`/`CrossedArmsItemLayer` |
| Elytra | 未接 | `entity/equipment/wings/elytra` 与飞行姿态 |

## 4. 装备资产与组件

| 项 | 现状 | 后续 |
| --- | --- | --- |
| `EquipmentClientInfo` JSON | 当前为静态受控映射；jar 有 `assets/minecraft/equipment/*.json` 但提取包未带 | 增加装备资产加载/校验，替换部分静态表 |
| `DataComponentPatch` | 只支持空 patch | 实现组件表与非空 patch；未知组件要可降级 |
| 皮革染色 / trim | 未做 | `dyeable` overlay、trim palette；依赖组件 |
| 物品注册表 | 26.3 固定快照，已内嵌 | 升级需重导快照；模组/自定义物品只做 unknown 降级 |

## 5. 特殊层与状态依赖

- Wither invulnerable（`isPowered`/生命值驱动）：当前无 health 同步，需先补生命状态。
- Creeper 充能/护甲层：需要充能状态。
- Blaze/Guardian/其它动态发光、旋转、beam 层：与动画/状态系统一起处理。
- Enderman/Drowned/Spider 等已有 eyes/outer layer 的基础机制；新增实体沿用现有 `EmissiveOnly`/`WithCopy`/`WithEquipment`。

## 6. 游戏内验收与性能

- 当前装备层主要用沙盒验证；需要真实服务端场景回归 `set_equipment` 同步。
- `EntitySkinLibrary` 目前预加载全部装备纹理；后续应观察图集体积/首帧耗时，必要时按实体按需加载。
- 仍需补：装备层与 animation/可见性/透明排序组合后的真机截图。

## 7. 建议顺序

1. **动画系统**（先做骨架/通道/状态机与一个四足 + 一个人形样板）。
2. 装备特例：ropes → crackiness → trader carpet → zombie nautilus coral。
3. 人形装备 / Witch / Elytra（依赖物品模型时后置）。
4. `EquipmentClientInfo` 资产化与组件 patch。

每项落地时在 `docs/traceability.md` 增加独立任务行，测试列不得留空；完成前状态用 `进行中`，不要提前写 `已完成`。

## 8. 参考资料

- `docs/analysis/render/entity-equipment.md`
- `docs/modules/entity-equipment.md`
- `docs/modules/item-registry.md`
- `docs/analysis/render/entity-special-layers.md`
- `docs/milestones/midterm-goal.md`
