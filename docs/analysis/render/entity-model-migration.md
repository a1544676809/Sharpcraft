<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 生物模型移植清单与进展
summary: 原版生物模型来源、按复杂度排序的移植顺序、当前进度与已知缺口
status: 已评审
updated: 2026-11-02
-->

# 生物模型移植清单与进展

> 历史快照说明：第 3–6 节是 2026-11-02 的移植计划与当时的缺口记录，已不作为当前状态依据。当前 DSL/模型迁移状态见第 8 节。

## 1. 原版模型来源

原版生物模型不是 `models/entity/*.json`，而是 Java 程序化 `LayerDefinition`：

- 每个模型类提供 `createBodyLayer()`，用 `MeshDefinition`、`CubeListBuilder`、`PartPose` 描述部件和立方体；
- `LayerDefinitions.createRoots()` 把所有 `ModelLayerLocation -> LayerDefinition` 注册起来；
- `EntityModelSet.bakeLayer(...)` 烘焙成 `ModelPart` 树；
- 对应 `EntityRenderer` 选择模型层和纹理 PNG（例如 `textures/entity/zombie/zombie.png`）。

证据：`ref://mc-26.3-analysis/src/net/minecraft/client/model/geom/LayerDefinitions.java`、
`ref://mc-26.3-analysis/src/net/minecraft/client/model/geom/EntityModelSet.java`、
`ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/cow/CowModel.java`、
`ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/zombie/ZombieModel.java`。

## 2. 模型复杂度分组

### A. Humanoid（最低复杂度）

可复用 `HumanoidModel` 的六部件和 `PartPose`，主要差异是纹理层与少量附加部件：

- Zombie（僵尸）
- Skeleton（骷髅）
- Drowned（溺尸）
- Husk（尸壳）
- Zombie Villager（僵尸村民）
- Bogged（沼骸）
- Villager（村民，含鼻子等附加 cube）
- Witch（女巫）
- Illager（灾厄村民）
- Enderman（末影人，长肢体）
- Piglin / Zombified Piglin（猪灵/僵尸猪灵）
- Giant（巨人，缩放的人形）

### B. 简单 Quadruped（需要多 cube/PartPose）

- Pig（猪）
- Sheep（羊）
- Cow（牛，含角/乳房等附加 cube）
- Goat（山羊）
- Horse（马）
- Donkey / Mule（驴/骡）
- Llama（羊驼）
- Wolf（狼）
- Fox（狐狸）
- Cat / Ocelot（猫/豹猫）
- Rabbit（兔子）
- Polar Bear（北极熊）
- Panda（熊猫）
- Armadillo（犰狳）
- Axolotl（美西螈）
- Camel（骆驼）
- Sniffer（嗅探兽，体型较大）

### C. 简单非人形/多肢体

- Chicken（鸡）
- Creeper（苦力怕）
- Spider / Cave Spider（蜘蛛/洞穴蜘蛛，8 条腿）
- Slime / Magma Cube（史莱姆/岩浆怪，内外层缩放）
- Squid / Glow Squid（鱿鱼/发光鱿鱼）
- Bat（蝙蝠）
- Bee（蜜蜂）
- Allay（悦灵）
- Vex（恼鬼）
- Parrot（鹦鹉）
- Cod / Salmon / Tropical Fish / Pufferfish（鱼类）
- Dolphin（海豚）
- Turtle（海龟）
- Frog / Tadpole（青蛙/蝌蚪）

### D. 复杂层级/Boss（最后）

- Blaze（烈焰人，旋转 rods）
- Breeze（旋风人）
- Ghast（恶魂，多触手）
- Ravager（劫掠兽）
- Warden（监守者）
- Creaking（嘎枝）
- Guardian / Elder Guardian（守卫者/远古守卫者）
- Shulker（潜影贝）
- Ender Dragon（末影龙）
- Wither（凋灵）

## 3. 移植顺序（按新增能力，不按生物热度）

| 阶段 | 模型 | 新增能力 | 出口 |
| --- | --- | --- | --- |
| 0 | Player | 人形部件、皮肤、UV、名字牌 | 已完成（T-355…T-387） |
| 1 | Zombie | 复用 HumanoidModel | 定义完成（T-389），纹理/渲染未接 |
| 2 | Skeleton / Drowned / Husk / Villager / Enderman | 纹理层切换、非玩家渲染路径 | 未开始 |
| 3 | Pig | 每部件多 cube、PartPose 旋转 | 未开始 |
| 4 | Sheep / Cow / Goat / Horse 等 | 多 cube、附加部件、羊毛/装备层 | 未开始 |
| 5 | Chicken / Creeper / Spider / Slime | 非对称部件、多腿、缩放层 | 未开始 |
| 6 | Blaze / Ghast / Warden / Ravager | 层级部件、静态姿态近似 | 未开始 |
| 7 | Ender Dragon / Wither | 骨骼层级，等待动画切片 | 未开始 |

## 4. 当前进展表

| 模型 | 定义 | 纹理 | 渲染接入 | 状态 |
| --- | --- | --- | --- | --- |
| Player | 完成 | 完成（默认/自定义 skin） | 完成 | 已完成 |
| Zombie | 完成 `HumanoidMobModelDefinition.Zombie` | 未接 `textures/entity/zombie/zombie.png` | 未接 | 进行中 |
| Skeleton 等 Humanoid | 未开始 | 未开始 | 未开始 | 未开始 |
| Pig/Cow 等 Quadruped | 未开始 | 未开始 | 未开始 | 未开始 |
| Spider/Slime/Chicken | 未开始 | 未开始 | 未开始 | 未开始 |
| Ghast/Dragon/Wither | 未开始 | 未开始 | 未开始 | 未开始 |

## 5. 已知缺口

1. **非玩家实体在 skin 路径下会消失**：`EntityModelMesher` 对未注册类型 `continue`，没有回退到 `EntityPlaceholderMesher`；必须先修。
2. **单 cube 限制**：当前 `EntityModelPart` 每部件只有一个 cube，无法表达 Cow 的头角、Chicken 的喙/翅膀等；Pig 之前必须先扩展 `EntityModelDefinition`。
3. **每个模型的纹理路径**：`EntityModelDefinition` 需要携带 texture path / size，非玩家不能继续共用玩家 skin。
4. **静态姿态**：P1 只做静态姿态，动画系统另行立项；Boss 模型不进入当前切片。
5. **实体类型 id**：Zombie 暂按实体注册序 `154`，需要和网络实体类型表核对后再固化。

## 6. 建议下一刀

1. 先修非玩家回退：未注册模型 → `EntityPlaceholderMesher`，避免皮肤路径下实体消失；
2. 为 Zombie 接 `textures/entity/zombie/zombie.png`，完成第一个非玩家渲染闭环；
3. 扩展 `EntityModelDefinition` 支持多 cube + `PartPose`，然后做 Pig。


## 7. 特殊层记录

Enderman 眼睛、Player 双层透明、Drowned outer 等不属于普通 cube overlay 的特例，统一记录在
[实体特殊层与材质](entity-special-layers.md)，模型移植完成后再补。

## 8. 2026-11-07 当前状态（覆盖第 3–6 节历史快照）

- **DSL 迁移完成**：`PlayerModelDefinition`、`HumanoidMobModelDefinition`、全部 `BabyModelDefinitions*`、`Pig/Cow/Chicken`、`Sheep/Rabbit/Frog/Tadpole`、`Cat/Wolf/Fox/Panda`、`Camel/Horse/Llama/Goat`、`Cod/Salmon/Pufferfish/TropicalFish/Dolphin`、`Allay/Bat/Bee/Breeze`、`Hoglin/Strider/Blaze/Ghast/HappyGhast`、`Enderman/Drowned/Husk`、`Villager/Witch/Illager`、`Armadillo/Axolotl/Sniffer/Warden`、`Guardian/Squid`、`EnderDragon/Endermite/Nautilus`、`Creeper/Spider/WitherBoss`、`Phantom/Ravager/PolarBear/Silverfish`、`Slime/MagmaCube/Shulker/Vex`、`Parrot/Turtle/ZombieNautilus`、`CopperGolem/Creaking/IronGolem/SnowGolem` 等均已通过 `VanillaMeshDefinition` 构建。
- **装备/护甲层**：`EquipmentModelParts`、`BabyHumanoidArmorParts`、`WithElytra` 的两翼也已 DSL 化；`LegacyModelPartFactory` 仅存在于测试工程，作为独立 oracle。
- **生产 legacy 工厂**：`PlayerModelDefinition.FromVanilla/FromVanillaChild` 已无生产调用并移除。
- **剩余事实缺口**：DSL 仍缺三轴 `GrowXyz` 与 `PartPose` 三轴 scale；root/child 旋转共轭仍需 Golden Vector 验证；真正的原版事实提取器尚未落地。详见 [原版实体模型几何事实基线](entity-model-geometry-facts.md)。
- **验收基线**：最近全量 `dotnet test` 通过（2325），`class-map`、`docs index/coverage` 均通过；实机客户端已启动验证。
