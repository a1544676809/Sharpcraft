<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: STATE —— 跨会话进度锚点
summary: 当前里程碑、进行中、未提交改动、阻塞、下一步、总体行动大纲
status: 活文档
updated: 2026-11-07
-->

# STATE —— 跨会话进度锚点

> 本文件只记录**易变状态**，且受原则 14 与架构测试硬约束：**严格控制在 150 行以内**（历史排障记录由 Git 历史保存，禁止在此写流水账）。
> 设计结论见 `docs/modules/`，里程碑定义见 `docs/milestones/roadmap.md`，已知缺口见 `docs/specs/render/known-gaps.md`，验收矩阵见 `docs/specs/quality/vanilla-equivalence-matrix.md`。

最后更新：2026-11-07

## 当前里程碑

| 里程碑 | 状态 | 出口证据摘要 |
| --- | --- | --- |
| M1 基础逻辑地基 | **已完成** | 架构门禁、随机数/注册表/状态 ID 真值向量、真实服务器进 play |
| M2 世界数据闭环 | **已完成** | NBT、`level_chunk_with_light` 解析、WGPU 首帧出图 |
| M3 资产管线 + 启动器闭环 | **已完成** | 官方 26.3 jar 校验打包、多账号离线闸门、真实服务器带纹理窗口 |
| M4 实体系统根基 + 输入输出 | **已完成** | 20 Hz tick、移动同步（4.318 b/s）、走进去挖方块闭环、流式区块加载 |
| M5 协议深化（5.1/5.2/5.3） | **进行中** | 5.1 实体同步完成；5.2 聊天收发完成；5.3 三层地形/流体/时间天气/参考 OIT/天空盘/视距 32 完成；实体模型与动画长尾进行中 |
| M6 模组与着色器包 | **进行中** | 模组宿主与着色器包契约层就位；ALC 回收验证与包执行器接线待收口 |

## 进行中

| 任务 | 状态 / 关键点 |
| --- | --- |
| **实体模型与动画主线** | 真实部件模型 + 逐实体动画 provider 已大量落地（humanoid/baby/quadruped/aquatic/nether 等）；非人形装备层已树化（T-723）；成人/幼年 humanoid 四槽护甲与 Elytra 已树化（T-724/T-725）；Piglin 护甲树已接入（T-726）；剩余：T-631 held-item/item-model（解锁 SPEAR/STAB）、Parrot PARTY / Bee stinger / Guardian eye look / Piglin crossbow-hold（Happy Ghast ropes 已由 T-755、EnderDragon flight history 已由 T-757 接入） |
| **架构：原则 14 / Phase 0 / Phase 1** | 已从 main 合入 T-654..T-661（原则 14、STATE 门禁、`PlaySession.TryHandleTransport`、`MeshingContext`、方块实体 NBT/类型注册表、`OcclusionShape`）。剩余：P0.3 帧 Pass 单轨化、P1.4 `registry_data` 动态装配、P1.5 `OcclusionShape` 完整接入 `ChunkMesher` 替换 `IsSolid` |

## 未提交改动

- 无。
- 后台任务：无。

## 已完成（近期）

1. **T-662 / T-663 Elytra baby 收尾（2026-11-06）**：`ElytraModel.BABY_TRANSFORMER = MeshTransformer.scaling(0.5F)` 的 scale 后 y 偏移 `24.016×(1−0.5)=12.008` 已计入（`Bottom=2`、`PivotY=12`）；**T-663 修复 UV**：`MeshDefinition.transformed` 只改 root pose，子 CubeDefinition 仍 10×20×2，`EntityModelPart.UvWidth/UvHeight/UvDepth=10/20/2` 保持与成人相同采样；补 `YOffsetSelector` crouch 位移（婴儿 1.5px / 成人 3px）。15 项 Elytra 测试通过。
2. **T-664 VecDelta.Stepped（2026-11-06）**：`EntityMove` 携带逐步 fixed-point delta，`EntityTracker.ApplyMove` 逐 step 解码并按其 ticks 入队，不再丢失多步轨迹。
3. **T-665 模型 UV 不变量门禁（2026-11-06）**：`ModelUvInvariantTests` 全注册表 UV 边界/面积扫描 + 成人/幼年鞘翅 48 顶点 UV 逐值一致 + `UvWidth/UvHeight/UvDepth` 同键一致性；4 项通过。
4. **T-666 协议分发覆盖门禁（2026-11-06）**：`ProtocolDispatchGateTests` 反射枚举 63 个 PlayClientbound 常量，强制 `PlaySession` 的解析包都有 live consumer（5 个 transport/preload-owned 白名单 + Transfer 显式未处理）；3 项通过。
5. **T-667 MeshTransformer scaling 集中化（2026-11-06）**：新增 `VanillaMeshBake.Scale`，统一 `MeshTransformer.scaling` 的 `yOffset=24.016×(1−factor)`、几何/pivot/grow 与 UV 原始盒；`WithElytra` baby 分支已迁移，6 项 bake 测试 + Elytra 测试通过。
6. **T-668 LivePlaySession 有界队列（2026-11-06）**：64 MiB `BoundedReceiveQueue` + 原子字节计数；`PumpAsync` 的异常统一写入 `_failure`，队列溢出直接判定会话失效；5 项队列/格式化测试通过。
7. **T-669 根/子骨骼坐标烘焙集中化（2026-11-06）**：`VanillaMeshBake.BakeRoot/BakeChild` 收纳 `FromVanilla/FromVanillaChild` 转换，`PlayerModelDefinition` 改为薄委托；3 项/4 cases 转换等价测试通过。
8. **T-670 Vanilla MeshDefinition DSL 核心（2026-11-06）**：新增 `VanillaPartPose/VanillaCubeDefinition/VanillaPartDefinition/VanillaMeshDefinition` + `VanillaMeshBake.Bake`；pre-order 展平、纹理继承/覆盖、重复名/空 root 校验；4 项测试通过。
9. **T-671 首个真实模型 DSL 迁移（2026-11-06）**：`PlayerModelDefinition.AdultParts` 改为 `VanillaMeshDefinition` 树 + `VanillaMeshBake.Bake`；`FromVanilla/FromVanillaChild` 保留；新增结构化值比较器，`OverlayLayers` 按值比较；3 项测试通过。
10. **T-672 Zombie/Skeleton 人形 DSL 迁移（2026-11-06）**：两套六部件模型改走 DSL；抽出共享 `ModelPartAssertions`；WitherSkeleton/Stray/Bogged 兼容性测试通过；3 项新测试。
11. **T-673 Enderman/Drowned/Husk DSL 迁移（2026-11-06）**：三套六部件模型改走 DSL，保留 Enderman modelHeight 25、DrownedOuter overlay、Husk scale/mirror；新增 1 项 legacy 等价测试。
12. **T-674 BabyModelDefinitionsHumanoid DSL 迁移（2026-11-06）**：Zombie/ZombieVillager/Villager 改为嵌套 DSL 树，Drowned 保留 outer_* 复制；6 项值等价/顺序/Parent/变体测试通过。
13. **T-675 WithClothing DSL 迁移（2026-11-06）**：`VanillaCubeDefinition.PoseSource` 透传；Stray/Bogged 六个 outer_* 部件改 DSL；Humanoid 全家族迁移完成；5 项 humanoid 测试通过。
14. **T-676 Pig/Cow/Chicken DSL 迁移（2026-11-06）**：三套 Normal/Warm/Cold 四足模型改走 DSL，保留 cold overlays/snout UV/horn rotations/zero-width fin；4 项测试通过。
15. **T-677 Sheep/Rabbit/Frog/Tadpole DSL 迁移（2026-11-06）**：12/13/18/2 部件（含深层 child 树与 zero-size pivot）全部改走 DSL；7 项值/顺序/Parent/变体测试通过。
16. **T-678 Allay/Bat/Bee DSL 迁移（2026-11-06）**：8/9/10 部件树改走 DSL，Allay Translucent、Bee textureSelector 保留；7 项测试通过；Breeze 留待 DSL 增加 EmissiveOnly/per-part texture 字段。
17. **T-679 Breeze DSL 迁移（2026-11-06）**：DSL 增加 `IncludeInEmissive/EmissiveOnly/TextureWidth/TextureHeight` per-cube 字段；Breeze 17 部件（EmissiveOnly eyes + 7 个 wind shell）迁移完成；4 项测试通过。
18. **T-680 Aquatic 家族 DSL 迁移（2026-11-06）**：Cod/Salmon/Pufferfish/TropicalFish/Dolphin 全部迁移；Salmon scale/顺序、Pufferfish 三态、TropicalFish pattern copies 保持；8 项测试通过。
19. **T-681 Nether 模型 DSL 迁移（2026-11-06）**：Hoglin/Strider/Blaze/Ghast/HappyGhast 五套模型迁移，保留 Zoglin 共享、cold/shooting selector、程序化 rods/tentacles 与 scales；7 项测试通过。
20. **T-682 Piglin DSL 迁移（2026-11-06）**：复用 AdultParts 的 body/arms/legs 前缀 + 6 部件 piglin head DSL 树；Definition/Brute/Zombified 元数据不变；3 项测试通过。
21. **T-683 Villager/Illager 系列 DSL 迁移（2026-11-06）**：DSL 增加 MirrorX/VisibleWhen；Villager/ZombieVillager/Witch/Illager 迁移完成；变体/VariantMap/scale 保持；7 项测试通过。
22. **T-684 Cat/Wolf/Fox/Panda DSL 迁移（2026-11-06）**：四套动物模型迁移，保留 Cat/Wolf collar copylayer、Fox sleeping selector、Panda variants；7 项测试通过。
23. **T-685 Camel/Horse/Llama/Goat DSL 迁移（2026-11-07）**：四套大型四足模型迁移；Horse `WithHorseMarkings`/5 个 equine 定义、Llama 4 毛色保持；7 项测试通过。
24. **T-686 Armadillo/Axolotl/Sniffer/Warden DSL 迁移（2026-11-07）**：四套模型迁移；Armadillo visible predicates、Sniffer legacy reorder、Warden 14 个 emissive 层保持；5 项测试通过。
25. **T-687 Phantom/Ravager/PolarBear/Silverfish DSL 迁移（2026-11-07）**：四套模型迁移；Phantom emissive eyes、PolarBear 1.2 scale 保持；5 项测试通过。
26. **T-688 Slime/MagmaCube/Shulker/Vex DSL 迁移（2026-11-07）**：四套模型迁移；Slime/Vex Translucent、Shulker 16 色变体、Vex charging selector 保持；5 项测试通过。
27. **T-689 Guardian/Squid DSL 迁移（2026-11-07）**：Guardian 23 部件（12 spike）、Squid 8 tentacles 迁移；Elder 2.35 scale/GlowSquid 保持；3 项测试通过。
28. **T-690 Creeper/Spider/WitherBoss DSL 迁移（2026-11-07）**：三套模型迁移；Spider leg rotations/mirrorX、Wither ribcage 子层保持；4 项测试通过。
29. **T-691 EnderDragon/Endermite/Nautilus DSL 迁移（2026-11-07）**：EnderDragon 66 部件（5 neck/12 tail/4 三段腿，legacy reorder）、Endermite 4、Nautilus 9 迁移；4 项测试通过。
30. **T-692 CopperGolem/Creaking/IronGolem/SnowGolem DSL 迁移（2026-11-07）**：四套模型迁移；CopperGolem 4 风化变体、Creaking emissive eyes 保持；5 项测试通过。
31. **T-693 Parrot/Turtle/ZombieNautilus DSL 迁移（2026-11-07）**：Parrot/Turtle 及 ZombieNautilus coral 子层迁移；ZombieNautilus 护甲 hidden VisibleWhen 保持；3 项测试通过。
32. **T-694 Baby Nether DSL 迁移（2026-11-07）**：baby Piglin/Hoglin 迁移，Piglin/ZombifiedPiglin、Hoglin/Zoglin 共享关系保持；2 项测试通过。
33. **T-695 Baby Misc DSL 迁移（2026-11-07）**：baby Goat/Panda/PolarBear/Turtle 迁移，Panda 变体与 child 结构保持；4 项测试通过。
34. **T-696 Baby Aquatic DSL 迁移（2026-11-07）**：baby Squid/Bee/Dolphin/Axolotl 迁移，Bee selector、deep child trees 保持；4 项测试通过。
35. **T-697 Baby Equine DSL 迁移（2026-11-07）**：baby Horse/Llama/Camel/Donkey 迁移；Horse markings、Llama/Camel/Donkey child 结构保持；4 项测试通过。
36. **T-698 Baby Final DSL 迁移（2026-11-07）**：baby Nautilus/HappyGhast 迁移；GlowSquid 共享 Squid parts；3 项测试通过。
37. **T-699 Baby Extra DSL 迁移（2026-11-07）**：baby Cat/Wolf/Fox/Rabbit 迁移；collar/selector 与多级腿链保持；4 项测试通过。
38. **T-700 Baby Odd DSL 迁移（2026-11-07）**：baby Strider/Snifflet/Armadillo 迁移；Armadillo visible predicates、Husk 共享保持；3 项测试通过。
39. **T-701 Baby 基础 DSL 迁移（2026-11-07）**：baby Pig/Cow/Chicken 迁移；全部 warm/cold 变体共享 parts 与 *_baby 纹理；3 项测试通过。
40. **T-702 BabyArmorParts DSL 迁移（2026-11-07）**：9 个 baby humanoid armor 部件迁移；baby head/body/waist/arms/legs/feet UV 保持；1 项测试通过。
41. **T-703 EquipmentModelParts DSL 迁移（2026-11-07）**：Horse/Camel saddle、Nautilus armor/saddle、HappyGhast harness 迁移；fake parent nodes 保留父链；4 项测试通过。
42. **T-704 Elytra 装备层 DSL 迁移（2026-11-07）**：`WithElytra` 两翼构造改走 DSL，adult/baby modelHeight/scale/layerZ/YOffset/RotationSelector 保持；18 项 Elytra 回归通过。
43. **T-705 移除生产 legacy 工厂（2026-11-07）**：`PlayerModelDefinition.FromVanilla/FromVanillaChild` 生产调用已为 0，方法移除；测试 oracle 迁移到 `LegacyModelPartFactory`；全量测试通过。
44. **T-706 原版模型几何事实内化（2026-11-07）**：新增 `docs/analysis/render/entity-model-geometry-facts.md`；核实 `ModelPart` 变换、`CubeDeformation` 三轴、Cube UV、`MeshTransformer.scaling`、幼年 Elytra 与 BabyZombie 常量；同步更新 entity-render / coordinate / migration 文档。
45. **T-707 原版事实提取器（2026-11-07）**：新增 `tools/Sharpcraft.ReferenceVerifier`，默认提取 Elytra/Warden/Breeze/AdultRabbit/AdultFox 的纯数字模型事实到 `artifacts/reference/vanilla-model-facts.json`；`--write`/`--check` 可用。
46. **T-708 Golden Vector 模型事实回归（2026-11-07）**：新增 `ReferenceModelFactsTests`（4 项），用提取事实校验 Warden/AdultFox/AdultRabbit/Elytra；修复 Warden `left_ribcage` mirror 与 AdultFox 腿部 `grow=0.001` 两处旧迁移遗漏。
47. **T-709 原版模型树结构核对（2026-11-07）**：确认原版 `CubeListBuilder → PartDefinition.bake → ModelPart` 全程保存 part/cube 树，没有扁平化；当前 DSL 的树已保留结构，只有 `VanillaMeshBake.Bake.EmitCubes` 把每个 cube 摊平成 `EntityModelPart`。后续应由集中 bakery 批量产出运行时模型，而不是在模型定义里手工扁平化。
48. **T-710 实体模型与原版差距总表（2026-11-07）**：新增 `entity-model-parity-gap.md`，汇总模型定义/烘焙、运行时/动画、装备/附加层、测试差距；明确装备也采用“独立槽位树/基础树派生树 + 集中烘焙”，并将 M1–M12/R1–R6/E1–E12/T1–T4 作为后续返工边界。
49. **T-711 待验证问题核对（2026-11-07）**：确认原版 HashMap 顺序不等于插入顺序（Breeze rods 等）、PartPose 三轴 scale 使用范围、Armadillo 零组节点可见性遗漏、装备层固定 kind 顺序差异，以及 EquipmentClientInfo 19 种 layer/染料/trim/玩家纹理字段。
50. **T-712 实体模型改造点清单（2026-11-07）**：新增 `entity-model-refactor-checklist.md`，将差距拆为 RF-01..RF-28（DSL、节点表/bakery、Mesher、装备/附加层、动画、渲染层、facts/Golden/门禁与清理），并给出依赖顺序与禁止返工路径。
51. **T-713 树烘焙基础 + Breeze 多 cube 迁移（2026-11-07）**：新增 `EntityModelCube`/`EntityModelPartNode`/`CompiledEntityModel`/`VanillaMeshBakery`；Breeze DSL 改为原版 multi-cube part 结构；测试覆盖 Breeze 11 part/14 cube 与 Armadillo 零 cube 组节点。
52. **T-714 树渲染路径接入（2026-11-07）**：`EntityModelDefinition` 新增可选 `Compiled`；Breeze/Armadillo 使用编译树；`EntityModelMesher` 增加节点矩阵 + cube range 遍历与父节点可见性继承；树测试扩展为 4 项。
53. **T-715 农畜/四足批迁移（2026-11-07）**：Pig/Cow/Chicken/Sheep 及全部变体接入编译树，Fox/Rabbit 变体同步接入；修正树路径 root pose 平移抵消问题。
54. **T-716 简单模型 FromTree 批量迁移（2026-11-07）**：新增 `EntityModelDefinition.FromTree`；Panda/Goat/Frog/Tadpole/Bat/Allay/Bee/Vex 通过统一入口接入树路径，状态纹理 selector 保留。
55. **T-717 节肢/水生批迁移（2026-11-07）**：Creeper/Spider/Endermite/Shulker/Squid/Nautilus 通过 `FromTree` 接入编译树；CaveSpider 复用 Spider tree 与 0.7 scale；Spider eyes 保留。
56. **T-718 Cat/Wolf collar 树层（2026-11-07）**：新增 `WithCollar(CompiledEntityModel)` 递归树克隆；Cat 0.01 grow、Wolf 零 grow；Cat/Wolf 全部变体接入 compiled tree。
57. **T-719 Warden emissive 树层（2026-11-07）**：新增 `WithEmissiveLayer(CompiledEntityModel)`；Warden 四层 emissive 按 retainExactParts 语义接入 compiled tree；树路径按 cube flags 过滤 emissive pass。
58. **T-720 剩余无装备模型批（2026-11-07）**：Phantom/Ravager/PolarBear/Silverfish/IronGolem/SnowGolem/Creaking/CopperGolem 通过 `FromTree` 接入编译树；emissive eyes、1.2 scale、weathering 变体保留。
59. **T-721 Illager/Witch 迁移（2026-11-07）**：Illager 的 Vindicator/Evoker/Pillager/Illusioner 与 Witch 通过 `FromTree` 接入编译树。
60. **T-722 Camel 基础树 + 鞍装备树（2026-11-07）**：Camel 接入 compiled tree；新增 `WithEquipment(CompiledEntityModel)` 将 saddle/bridle/reins 节点挂到基础 body/head；Decorate 对 Camel 使用 compiled equipment tree。
61. **T-723 非人形装备树迁移（2026-11-07）**：新增 `EquipmentModelTrees` 与 `WithModelLayer`；马/驴/骡/骷髅马/僵尸马、骆驼、羊驼、鹦鹉螺、快乐恶魂、狼甲全部装备层改为独立烘焙树；马 markings、狼 crackiness 改树复制层；`EntityModelCube.TextureSelector` 支持装备槽位动态纹理；树测试 15 项通过。
62. **T-724 成人 humanoid 护甲/Elytra 树层（2026-11-07）**：HumanoidMob 八个定义与 Player 基础接入 `Compiled`；新增 `HumanoidArmorTrees` 四槽独立树（outer 1.0 / inner 0.5 / legs-0.1 / hat+0.5，Player 含空 sleeve/pants/jacket 组）；新增 `WithElytra(CompiledEntityModel)` 两翼树；修正树 root yOffset 双重位移；树测试 16 项通过。
63. **T-725 幼年 humanoid 护甲 / baby Elytra 树层（2026-11-07）**：Baby Zombie/Drowned/ZombieVillager/Villager 与变体接入 `Compiled`；`HumanoidArmorTrees.ForBaby` 四槽树（baby outer/inner deformation，waist/leg `extend(-0.1)`，foot 父链保留）；`WithElytra(..., baby)` 使用 5×10×1 翼与 10×20×2 UV；`EntityModelCube`/DSL 增加 UV 覆盖；树测试 17 项通过。
64. **T-726 Piglin 护甲树（2026-11-07）**：Piglin 的 player body/arms/legs + piglin head 组成 `Compiled`；`HumanoidArmorTrees.ForPiglin` 复用 player armor 四槽并补空 ears；type 102/103/158 使用独立 `PiglinArmorLayers`；树测试 18 项通过。
65. **T-727 Baby Piglin 专用护甲树（2026-11-07）**：baby Piglin/Zombified Piglin 接入 `Compiled`；`HumanoidArmorTrees.ForBabyPiglin` 使用 uniform 0.7、arm offset `(0.5,-0.5,0)`、空 ears；树测试 19 项通过。
66. **T-728 剩余 adult 模型树迁移（2026-11-07）**：Axolotl/Parrot/Salmon/TropicalFish/Villager/ZombieVillager 接入 `Compiled`；TropicalFish pattern、Villager 群系变体使用树复制层；树测试 20 项通过。
67. **T-729 第二批剩余 adult 模型树迁移（2026-11-07）**：Cod/Dolphin/Ghast/MagmaCube/Turtle 与 Pufferfish 三形态接入 `Compiled`；树测试 21 项通过。
68. **T-730 第三批剩余 adult 模型树迁移（2026-11-07）**：Blaze/Hoglin/Zoglin/Slime/Strider/Sniffer/Giant 接入 `Compiled`；Giant 复用 Zombie 树并保留 6× scale；树测试 22 项通过。
69. **T-731 第四批模型树迁移（2026-11-07）**：Guardian/ElderGuardian、EnderDragon、WitherBoss 接入 `Compiled`；ZombieNautilus temperate 复用 Nautilus 树；warm coral 变体待 coral 子树挂载；树测试 23 项通过。
70. **T-732 ZombieNautilus warm coral 树（2026-11-07）**：`AddChild` 挂载 coral 子树到基础 shell；保留穿甲隐藏可见性；树测试 24 项通过。
71. **T-733 显式层顺序计划 + 动态层缓冲（2026-11-07）**：`EntityRenderLayerDefinition`/`RenderLayers` 默认 0/10/20/30；`BuildLayers` 按批次并集排序；`WindowedRenderer` 层缓冲动态增长。
72. **T-734 幼年基础模型树迁移（2026-11-07）**：Baby Pig/Cow/Chicken 与 Baby Cat/Wolf/Fox/Rabbit 及变体接入 `Compiled`；baby collar 树层；树测试 25 项通过。
73. **T-735 幼年水生 / 马科模型树迁移（2026-11-07）**：Baby Squid/Bee/Dolphin/Axolotl 与 Baby Horse/Llama/Camel/Donkey 及全部变体接入 `Compiled`；Baby Horse markings 走 `WithModelLayer` 树复制层；树测试 26 项通过。
74. **T-736 Horse markings 生长值修复（2026-11-07）**：按 `HorseMarkingLayer` 语义去掉 legacy flat markings 的 `+0.05f` inflation，与编译树一致。
75. **T-737 剩余幼年模型树迁移（2026-11-07）**：Baby Misc（Goat/Panda/PolarBear/Turtle）、Odd（Strider/Husk/Snifflet/犰狳）、Final（Nautilus/HappyGhast/GlowSquid）及变体接入 `Compiled`；Husk/GlowSquid 复用已有编译树；树测试 27 项通过。
76. **T-738 模型级实体层（humanoid 护甲/Elytra）（2026-11-07）**：`EntityRenderLayerDefinition` 增加独立 Model/纹理/可见性；`BuildModelBackedLayers` 支持模型级层；humanoid 护甲与 Elytra 独立层接线；当前逐实体构建，性能分组待后续。
77. **T-739 全量非 baby 装备独立层（2026-11-07）**：马/驴/骡/骷髅马/僵尸马、骆驼、羊驼、鹦鹉螺、快乐恶魂、狼、Piglin 装备树改为模型级独立层；`AllCubes` 修复装备层 pipeline；Wolf crackiness 独立层；全量 Client 1554 项通过。
78. **T-740 baby 护甲独立层（2026-11-07）**：baby humanoid/Piglin 四槽护甲与 baby Elytra 改为模型级独立层，`HumanoidArmorBaby` 纹理族与 `AllCubes` 保持；其他 baby 装备仍 merged compiled；全量 Client 1556 项通过。
79. **T-741 模型树覆盖/层顺序门禁（2026-11-07）**：新增全注册表不变量测试 3 项；登记 baby Hoglin/Zoglin 的 flat 路径缺口（棘轮基线，不改生产代码），已由 T-742 收口；全量 Client 1559 项通过。
80. **T-742 baby Hoglin/Zoglin 树路径收口（2026-11-07）**：两者共享 `HoglinCompiled`；删除 T-741 棘轮基线、覆盖门禁恢复严格全量断言；全量 Client 1560 项通过。
81. **T-743 硬约束后续行动文档 + 外部审查稿存档（2026-11-07）**：新增 `next-actions-hard-constraints.md` 与 `external/` 两份原始审查稿；STATE 下一步指向新排期文档。
82. **T-744/T-745/T-746/T-747/T-748/T-748d/T-748i/T-748j/T-749/T-750/T-751/T-752/T-752a/T-752b（2026-11-07）**：UV 门禁扩展至 compiled/equipment；主场景 Pass 共用 `RecordScenePasses`；`EntityModelPart` 参数化构造 internal，全部模型定义/helper/mesher 的 baked-part `with` 已改 `With`（仅集中 transformer 保留）；ModHost 收口；`EquipmentClientInfo` 19 LayerType + `EquipmentAssetRegistry`，`equippable` asset id、dye/player-texture/glint 元数据与 `ResolveTrim` predicate 已透传，`dyed_color` 通过 `ResolveEquipmentTint` 进入模型 tint selector，`EquipmentTextureCatalog` 优先资产层纹理、静态 fallback；T-748d 起资产层纹理路径（`Assets.TexturePaths`）进入 `EntitySkinLibrary` atlas；glint 有独立 `Glint` layer + `fs_entity_glint` additive pass（host clock 滚动），trim texture/palette 见 T-754；T-748i 起 `ResolveEquipmentTextureInfo` 暴露完整 `EquipmentTexture` 元数据、`ResolveEquipmentTexture` 为薄委托；T-751 起 `BuildModelBackedLayers` 仅按连续相邻有效层模型分组批量建几何、复用字段避免每层分配，交错模型保持原提交顺序；EntityTracker stepped delta/ticks 回归通过；T-752 起 `use_player_texture` 已接玩家 `textures` 档案属性、cape/elytra 下载缓存与 atlas 注册、elytra > cape(showCape) 优先级及装备层第一层替换；T-752a 起 `PlayerSkinTextures.Slim` 经 `EntityRenderState.Slim` 选择 `PlayerModelDefinition.SlimDefinition`（3px 手臂），wide 路径不变，原版映射表/真值向量见 entity-equipment.md §12；T-752b 起 `PlayerSkinCache` 对 SKIN/body 执行参考 `processLegacySkin`（64×32→64×64、12 组 copyRect、alpha 修正），cape/elytra 不转换，映射表见 §13；T-752c 起中性共享库 `Sharpcraft.AuthLib` 提供 discovery/publickeys 加载、`SHA1withRSA` 验签、`ProfileSignatureState` 与纹理 URL 策略，`PlayerSkinCache` 接入本地/远程 `requireSecure`；T-752a-1 补全 `DefaultPlayerSkin.get(UUID)`：10 组 jshell 采样的 Java `UUID.hashCode()`+floorMod 真值向量驱动 18 个默认皮肤路径与 Slim 标记，无档案属性按 UUID 回退、纹理与模型一致（entity-equipment.md §12）；T-752a-2 核对 26.3 `PLAYER_SLIM_ARMOR`/`PLAYER_ARMOR` 共用一个 `playerArmor` mesh、slim/wide 护甲同树，按 AGENTS 4.1 不新增 slim 护甲树，仅更正 §12.3 陈旧描述并保留证据指针；T-754 起 trim texture/palette 已接（11 材质×18 图案×2 人形层，8 色 palette 映射，humanoid armor trim 层，equipment trim palette/texture override 与动态 TrimRegistryTable；glint 已用原版 0.16 矩阵/10° 旋转/时间偏移、`SRC_COLOR` 混合与 `EQUAL` 深度），R-019 独立跟踪。
83. **T-654..T-661 main 合并（2026-11-06）**：原则 14 + 单轨收敛 + 方块实体/遮挡地基并入本分支；`PlaySession` 冲突取 main 的“未处理包全部延迟”版本；traceability 编号顺延（原 main T-344..T-351 → T-654..T-661）。
49. **实体模型/动画、渲染与协议长尾（2026-11-02~06）**：全部 baby 模型族、villager/zombie/horse 变体、cat/wolf 项圈、panda 基因、动画 provider 与测试；Play Clientbound 覆盖审计 + P0/P1 包批次（T-642..T-647）、反相 Z、参考 OIT 云/流体与天空盘逐像素近似；T-648..T-650/T-651..T-653 完成 Elytra 翼盒/UV、流体面剔除、云 OIT depth-bounds 与 `Sharpcraft.Assets` 分层。

## 阻塞项

- **无硬阻塞**（T-752c 已由 `Sharpcraft.AuthLib` 实现，详见 entity-equipment.md §14）。
- **R-017**：XSTS 端点偶发网络抖动，联网全链路验收脚本含重试策略。
- **R-019**：模组 `AssemblyLoadContext` 热卸载后的 GC 回收探针尚未稳定转绿（排入 Phase 4 P4.1）。

## 下一步具体动作

> 下一阶段执行清单见 [`docs/analysis/render/next-actions-hard-constraints.md`](analysis/render/next-actions-hard-constraints.md)；下面为原有 backlog。

1. **T-631 held-item/item-model**：T-631a 数据层 + T-631b Witch 药水 + T-631c `ItemAtlas`/独立 item bind group/held_item + T-631d special 描述符与 **10/13** special 模型（trident/shield/head/player_head/chest/shulker_box/banner/decorated_pot/conduit/end_cube）已接。T-631e 已落地：`banner_patterns`/`base_color` 解码（`BannerPatternTable` 43 项 + `registry_data` 动态排序）、`EquipmentHasBannerPatterns` 透传、盾牌按 `hasPatterns` 在 `shield_base`/`shield_base_nopattern` 间切换、网格 DSL 新增**逐立方体 `TextureSelector` + `TintSelector`**（图案叠加的前置）。**下一步（按序）**：① 用新 DSL 烘焙 banner/shield 图案叠加层（逐层 pattern sprite + dye tint）；② chest `openness`/`chest_type` 与 shulker `openness`；③ SPEAR/STAB；④ 掉落物/展示框/GUI；⑤ bell/book/copper_golem_statue。
2. **EnderDragon flight history**：已接（T-757）；剩余动画长尾（Bee stinger / Guardian eye look）按 backlog 推进。
3. **P1.5（`Client/Render/ChunkMesher`）**：用 `OcclusionShape.IsFaceOccludedByState/Self` 替换 `IsSolid` 近似；配 `FluidMesherTests` 真值回归。
4. **P1.4（T-042）**：提取 26.3 `registry_data` 真值向量，把服务器下发注册表动态装配进 `BlockStateLayout`。
5. **P0.3（`Framework/Rendering`）**：`OffscreenRenderer` 与 `WindowedRenderer` 收敛为共用世界 Pass 序列。

## 最近提交

- `bd424b3` fix(render): decouple baby elytra UVs and preserve stepped move deltas (T-663/T-664)
- `5a5a9c9` docs: record the latest commits in STATE
- `7b68fe3` feat(render): finish the baby elytra variant (ELYTRA_BABY) (T-662)
- `b1b65df` merge: main (principle 14 guardrails, Phase 0/1 foundation)
- `b4d32e2` docs: reconcile the equivalence matrix with the branch state
