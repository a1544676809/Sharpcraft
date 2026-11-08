<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 实体渲染剩余清单
summary: 装备层之后的实体渲染欠账：动画、装备特例、资产与验收顺序
status: 草稿
updated: 2026-11-07
-->

# 实体渲染剩余清单

本文把“目前还差什么”固定下来，避免只存在于会话中。范围是实体模型与实体渲染层；方块、UI、着色器包不在本清单内。

## 1. 已完成基线（用于判断后续差距）

- 静态 bind pose 的成年/幼年实体模型已覆盖到当前清单（包括 Panda 变体、Villager 群系纹理、Cat/Wolf 项圈）。
- 装备基础设施：`SetEquipment` 解码、8 槽状态、`ItemRegistry`、受控装备纹理映射、Horse/Camel/Nautilus/Wolf/Happy Ghast 装备层。
- 参考：`docs/analysis/render/entity-equipment.md`、`docs/modules/entity-equipment.md`、`docs/modules/item-registry.md`。

## 2. 高优先级：动画系统

**状态：provider 基础已大量落地，真实场景多帧验收仍未完成。**

- 已存在大量 `*AnimationProvider`（Humanoid、Quadruped、Feline、Equine、Axolotl、Nautilus、Warden、Creaking、Breeze、Sniffer 等）；动画通道/关键帧/姿态覆盖已有基础实现。
- 仍未完成的是：真实服务端场景下的多帧姿态对比、动画与装备层的统一变换验收，以及 EnderDragon flight history 等长尾状态。
- 验收不能只看静态截图：需要固定场景下多帧姿态对比，或按参考动画的关键帧数值验证。
- 动画完成后，装备层必须跟随同一套变换。

## 3. 装备层剩余

| 项 | 现状 | 依赖 / 验收 |
| --- | --- | --- |
| Happy Ghast `RopesLayer` | 已接（T-755）：`IsLeashed` + body harness 判定、adult/baby 0.2 外扩绳索模型 | 真机视觉验收 |
| Wolf armor 裂纹 | 已接入独立 crackiness 层 | 继续做损伤状态真机验证 |
| Trader Llama 地毯 | 普通 16 色地毯与 trader 专用纹理已接 | 继续做装备槽联动测试 |
| Zombie Nautilus 珊瑚 | 暖变体 coral 层已接入，`Equipment[6]` 有甲时隐藏 | 继续做 body armor 联动测试 |
| 人形装备 | 已接入四槽独立护甲层 | 继续补 dye/trim/玩家纹理视觉验收 |
| Witch 手持物品 | 未接 | 需要物品模型渲染（后置）；`WitchItemLayer`/`CrossedArmsItemLayer` |
| Elytra | 已接入独立两翼层 | `use_player_texture` 已接（T-752）；glint 独立层已接 |

## 4. 装备资产与组件

| 项 | 现状 | 后续 |
| --- | --- | --- |
| `EquipmentClientInfo` JSON | 已解析 19 LayerType、dyeable/use_player_texture/trim_overrides，并从资源栈加载 | 继续替换剩余静态 fallback 路径 |
| `DataComponentPatch` | 已覆盖 equippable、dyed_color、glint override 等常用组件；非空未知 patch 仍会降级 | 继续补组件表并保持未知组件可诊断 |
| 皮革染色 / trim | `dyed_color` 已进入 tint selector；`ResolveTrim` 已选择 predicate；trim texture/palette、装备 palette/texture override 与完整 GLINT pass 已接入（T-754） | 视觉验收与自定义 trim entry NBT 仍待补 |
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

1. **动画系统真实场景验收**：provider 已大量存在，优先做固定场景多帧姿态对比。
2. 装备特例：Happy Ghast ropes 已接（T-755）；crackiness/trader carpet/zombie coral 只差联动验收。
3. Witch 手持物品（依赖物品模型，后置）。
4. 装备资产收尾：trim texture/palette、palette/texture override 与完整 GLINT pass 已接（T-754），剩余视觉验收与自定义 trim entry NBT 解析。

每项落地时在 `docs/traceability.md` 增加独立任务行，测试列不得留空；完成前状态用 `进行中`，不要提前写 `已完成`。

## 8. 参考资料

- `docs/analysis/render/entity-equipment.md`
- `docs/modules/entity-equipment.md`
- `docs/modules/item-registry.md`
- `docs/analysis/render/entity-special-layers.md`
- `docs/milestones/midterm-goal.md`
