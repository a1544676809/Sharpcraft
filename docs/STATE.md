<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: STATE —— 跨会话进度锚点
summary: 当前里程碑、进行中、未提交改动、阻塞、下一步、总体行动大纲
status: 活文档
updated: 2026-11-09
-->

# STATE —— 跨会话进度锚点

> 本文件只记录**易变状态**，且受原则 14 与架构测试硬约束：**严格控制在 150 行以内**（历史排障记录由 Git 历史保存，禁止在此写流水账）。
> 设计结论见 `docs/modules/`，里程碑定义见 `docs/milestones/roadmap.md`，已知缺口见 `docs/specs/render/known-gaps.md`，验收矩阵见 `docs/specs/quality/vanilla-equivalence-matrix.md`。

最后更新：2026-11-10（T-786 渲染后端接缝：W5e encoder 整片已提交 91f0b86，棘轮 225→217；下一步按契约 §9.9 做 `UiRenderer` 整片迁移）

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
| **T-786 渲染后端接缝（已完成 2026-11-10）** | 已完成：全部 19 个 render pass 走 `IRenderBackend`（`encoder.BeginRenderPass` 全仓 0）、全部渲染管线走 `PipelineDesc`、encoder 生命周期整片入缝（`1d427f6` 管线布局整片 −33；`567c9cb` OIT 整片 −47）、compute 创建与录制入缝、`BindingKind` 支持 storage buffer。**下一步**：按 `docs/analysis/architecture/render-backend-seam.md` §9.9 做 `UiRenderer` + `WindowedRenderer` 持有字段 + `Run` 中 layout 的**整片**迁移（单改 `UiRenderer` 实测 +2，整片才净减）。**棘轮 225 → 1**（−99.6%）；`new WgpuRenderBackend` 9 → 2（每渲染器 1 个，理论下限）；`encoder.BeginRenderPass` 全仓 0；Rendering 外零 `Brew.Wgpu`。唯一保留项（交换链视图）已裁决**保持**，登记 `SIM-SURFACE-NOT-IN-SEAM`（T-788）。**后续（都已完成或结案）**：T-789/T-790 经复核均**不是缺陷**（游戏代码按原则 13 只读 `--debugOptionsFile`，env 设置静默无效）；改用 debug options 后 compute 链拿到运行时真值 `pyramid 9 levels mismatches 0` / `motion mismatches 0`。新增 `tools/verify/check-frame-baseline.ps1`（运行正常性检查，不比对哈希——昼夜与云使逐字节比较无意义）。**剩余真实缺口**：①compute 的 `mismatches 0` 是对本项目自写 CPU 参考，**非原版真值**；②OIT 的半透明地形/物品几何从未在任何验证中被实际绘制（场景恒为 `translucent 0 tri`）；③`ChunkCuller` 未接线。§9.8 记录 `ChunkCuller` 未接线；§9.10 记录数组化绘制集合需整体迁移；§9.12 记录 `TextureSampleType` 缺 `UnfilterableFloat`（已修，`7a40652`） |
| **实体模型与动画主线** | 真实部件模型 + 逐实体动画 provider 已大量落地（humanoid/baby/quadruped/aquatic/nether 等）；非人形装备层已树化（T-723）；成人/幼年 humanoid 四槽护甲与 Elytra 已树化（T-724/T-725）；Piglin 护甲树已接入（T-726）；剩余：T-631 held-item/item-model（解锁 SPEAR/STAB）、Parrot PARTY / Bee stinger / Guardian eye look / Piglin crossbow-hold（Happy Ghast ropes 已由 T-755、EnderDragon flight history 已由 T-757 接入） |
| **架构：原则 14 / Phase 0 / Phase 1** | 已从 main 合入 T-654..T-661（原则 14、STATE 门禁、`PlaySession.TryHandleTransport`、`MeshingContext`、方块实体 NBT/类型注册表、`OcclusionShape`）。剩余：P0.3 帧 Pass 单轨化、P1.4 `registry_data` 动态装配、P1.5 `OcclusionShape` 完整接入 `ChunkMesher` 替换 `IsSolid` |
| **物品透明与渲染类型（T-762/T-771/T-772/T-774/T-773）** | 已落地：按贴图 alpha + `force_translucent` 判 cutout/translucent（`ItemCutout` 0.1 阈值 + 对象式贴图项解析）、半透明物品接入**独立参考 OIT 集合**（`itemDepthBounds/Coeff/Accumulate` + 独立合成，对应原版 `OitPipelineSet("item")`）。**已解决（T-773）**：竖条根因不在渲染链路——`PngDecoder` 对 PNG colour type 4（灰度+alpha）的样本索引错误（首通道用 `x` 而非 `x*channelsPerPixel`），26.3 以该格式存储四种无彩色染色玻璃，故只有它们出问题。修复 + 回归测试 `GrayAlphaKeepsTheGrayOfItsOwnPixel`，实机黑/白/红染色玻璃方块与物品均正常。图集已统一到 `SpriteAtlasBuilder`（T-774），物品 OIT 集合已提取为 `ItemOitSet`。T-776 已完成（reference-OIT 序列提取为 `ReferenceOitPasses`，棘轮回落 2400）。T-776/T-777/T-778/T-780 均已完成（T-780 = F2 截图，实机验证通过；Ctrl+F2 全景图登记为 T-781 欠账）。下一步：**T-764 S3** —— 把 chest 接入 `ItemEntityMesher`：在 `Build` 里读 `resolution.SpecialRendererType`，命中 `minecraft:chest` 时用 `SpecialModelBake.Bake(SpecialModelDefinitions.Chest(openness), 64, 64, …)` 产出四边形，转成 `ItemModelQuad`（`ItemModelGeometry` 已有逐角 UV 字段，可直接承接 `SpecialQuad`）追加进 `_quads`/`_sections`，贴图路径 `entity/chest/normal`（变体：`ender`/`trapped`/`copper*`）需先加入 `ItemAtlas` 的特殊贴图集合；随后实机看掉落箱子。S1/S2 已完成并有 5 项测试。 |
| **物品渲染等效性层（L1–L4 已建）** | 审计工具 T-768/L1（1658 物品，未解析 0）、手持共享几何 T-765/L2、display 真值与旋转修正 T-767/L3、接触表 T-768/L4；**T-765 手持链路已收口**（锚点、STAB、副手左手修正、special 盾牌图层 `display·anchor`+`scale(1,-1,-1)`、盾牌正面朝外、`using_item` 条件求值、长矛使用姿态与手臂姿态、`ticksUsingItem` 部分刻插值）；剩余：special 模型掉落物（T-764，烘焙约定待评审）、`composite` 手持/GUI 路径（T-766 掉落已解决）、`tintindex` 染色、元素 `rotation` |

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
**实体模型 DSL 迁移（T-670–T-694，2026-11-06/07，合并记录）**：`VanillaMeshDefinition` DSL 核心（T-670）落地后，全部实体模型逐族迁移并保留各家特例：人形（T-671–T-675）、四足/羊驼/骆驼/马（T-676、T-677、T-685）、水生（T-680）、下界/猪灵（T-681、T-682）、村民/灾厄（T-683）、猫/狼/狐/熊猫（T-684）、幻翼/劫掠兽/北极熊/蠹虫（T-687）、史莱姆/岩浆怪/潜影贝/恼鬼（T-688）、守卫者/鱿鱼（T-689）、苦力怕/蜘蛛/凋灵（T-690）、末影龙/末影螨/鹦鹉螺（T-691）、铜傀儡/循声守卫/铁傀儡/雪傀儡（T-692）、鹦鹉/海龟/僵尸鹦鹉螺（T-693）、幼年下界（T-694）。逐任务细节见 `docs/traceability.md` 与 Git 历史。
9. **T-705 移除生产 legacy 工厂（2026-11-07）**：`PlayerModelDefinition.FromVanilla/FromVanillaChild` 生产调用已为 0，方法移除；测试 oracle 迁移到 `LegacyModelPartFactory`；全量测试通过。
10. **T-706 原版模型几何事实内化（2026-11-07）**：新增 `docs/analysis/render/entity-model-geometry-facts.md`；核实 `ModelPart` 变换、`CubeDeformation` 三轴、Cube UV、`MeshTransformer.scaling`、幼年 Elytra 与 BabyZombie 常量；同步更新 entity-render / coordinate / migration 文档。
11. **T-707 原版事实提取器（2026-11-07）**：新增 `tools/Sharpcraft.ReferenceVerifier`，默认提取 Elytra/Warden/Breeze/AdultRabbit/AdultFox 的纯数字模型事实到 `artifacts/reference/vanilla-model-facts.json`；`--write`/`--check` 可用。
12. **T-708 Golden Vector 模型事实回归（2026-11-07）**：新增 `ReferenceModelFactsTests`（4 项），用提取事实校验 Warden/AdultFox/AdultRabbit/Elytra；修复 Warden `left_ribcage` mirror 与 AdultFox 腿部 `grow=0.001` 两处旧迁移遗漏。
13. **T-709 原版模型树结构核对（2026-11-07）**：确认原版 `CubeListBuilder → PartDefinition.bake → ModelPart` 全程保存 part/cube 树，没有扁平化；当前 DSL 的树已保留结构，只有 `VanillaMeshBake.Bake.EmitCubes` 把每个 cube 摊平成 `EntityModelPart`。后续应由集中 bakery 批量产出运行时模型，而不是在模型定义里手工扁平化。
14. **T-710 实体模型与原版差距总表（2026-11-07）**：新增 `entity-model-parity-gap.md`，汇总模型定义/烘焙、运行时/动画、装备/附加层、测试差距；明确装备也采用“独立槽位树/基础树派生树 + 集中烘焙”，并将 M1–M12/R1–R6/E1–E12/T1–T4 作为后续返工边界。
15. **T-711 待验证问题核对（2026-11-07）**：确认原版 HashMap 顺序不等于插入顺序（Breeze rods 等）、PartPose 三轴 scale 使用范围、Armadillo 零组节点可见性遗漏、装备层固定 kind 顺序差异，以及 EquipmentClientInfo 19 种 layer/染料/trim/玩家纹理字段。
16. **T-712 实体模型改造点清单（2026-11-07）**：新增 `entity-model-refactor-checklist.md`，将差距拆为 RF-01..RF-28（DSL、节点表/bakery、Mesher、装备/附加层、动画、渲染层、facts/Golden/门禁与清理），并给出依赖顺序与禁止返工路径。
17. **T-713 树烘焙基础 + Breeze 多 cube 迁移（2026-11-07）**：新增 `EntityModelCube`/`EntityModelPartNode`/`CompiledEntityModel`/`VanillaMeshBakery`；Breeze DSL 改为原版 multi-cube part 结构；测试覆盖 Breeze 11 part/14 cube 与 Armadillo 零 cube 组节点。
18. **T-714 树渲染路径接入（2026-11-07）**：`EntityModelDefinition` 新增可选 `Compiled`；Breeze/Armadillo 使用编译树；`EntityModelMesher` 增加节点矩阵 + cube range 遍历与父节点可见性继承；树测试扩展为 4 项。
19. **T-715 农畜/四足批迁移（2026-11-07）**：Pig/Cow/Chicken/Sheep 及全部变体接入编译树，Fox/Rabbit 变体同步接入；修正树路径 root pose 平移抵消问题。
20. **T-716 简单模型 FromTree 批量迁移（2026-11-07）**：新增 `EntityModelDefinition.FromTree`；Panda/Goat/Frog/Tadpole/Bat/Allay/Bee/Vex 通过统一入口接入树路径，状态纹理 selector 保留。
21. **T-717 节肢/水生批迁移（2026-11-07）**：Creeper/Spider/Endermite/Shulker/Squid/Nautilus 通过 `FromTree` 接入编译树；CaveSpider 复用 Spider tree 与 0.7 scale；Spider eyes 保留。
22. **T-718 Cat/Wolf collar 树层（2026-11-07）**：新增 `WithCollar(CompiledEntityModel)` 递归树克隆；Cat 0.01 grow、Wolf 零 grow；Cat/Wolf 全部变体接入 compiled tree。
23. **T-719 Warden emissive 树层（2026-11-07）**：新增 `WithEmissiveLayer(CompiledEntityModel)`；Warden 四层 emissive 按 retainExactParts 语义接入 compiled tree；树路径按 cube flags 过滤 emissive pass。
24. **T-720 剩余无装备模型批（2026-11-07）**：Phantom/Ravager/PolarBear/Silverfish/IronGolem/SnowGolem/Creaking/CopperGolem 通过 `FromTree` 接入编译树；emissive eyes、1.2 scale、weathering 变体保留。
25. **T-721 Illager/Witch 迁移（2026-11-07）**：Illager 的 Vindicator/Evoker/Pillager/Illusioner 与 Witch 通过 `FromTree` 接入编译树。
26. **T-722 Camel 基础树 + 鞍装备树（2026-11-07）**：Camel 接入 compiled tree；新增 `WithEquipment(CompiledEntityModel)` 将 saddle/bridle/reins 节点挂到基础 body/head；Decorate 对 Camel 使用 compiled equipment tree。
27. **T-723 非人形装备树迁移（2026-11-07）**：新增 `EquipmentModelTrees` 与 `WithModelLayer`；马/驴/骡/骷髅马/僵尸马、骆驼、羊驼、鹦鹉螺、快乐恶魂、狼甲全部装备层改为独立烘焙树；马 markings、狼 crackiness 改树复制层；`EntityModelCube.TextureSelector` 支持装备槽位动态纹理；树测试 15 项通过。
28. **T-724 成人 humanoid 护甲/Elytra 树层（2026-11-07）**：HumanoidMob 八个定义与 Player 基础接入 `Compiled`；新增 `HumanoidArmorTrees` 四槽独立树（outer 1.0 / inner 0.5 / legs-0.1 / hat+0.5，Player 含空 sleeve/pants/jacket 组）；新增 `WithElytra(CompiledEntityModel)` 两翼树；修正树 root yOffset 双重位移；树测试 16 项通过。
29. **T-725 幼年 humanoid 护甲 / baby Elytra 树层（2026-11-07）**：Baby Zombie/Drowned/ZombieVillager/Villager 与变体接入 `Compiled`；`HumanoidArmorTrees.ForBaby` 四槽树（baby outer/inner deformation，waist/leg `extend(-0.1)`，foot 父链保留）；`WithElytra(..., baby)` 使用 5×10×1 翼与 10×20×2 UV；`EntityModelCube`/DSL 增加 UV 覆盖；树测试 17 项通过。
30. **T-726 Piglin 护甲树（2026-11-07）**：Piglin 的 player body/arms/legs + piglin head 组成 `Compiled`；`HumanoidArmorTrees.ForPiglin` 复用 player armor 四槽并补空 ears；type 102/103/158 使用独立 `PiglinArmorLayers`；树测试 18 项通过。
31. **T-727 Baby Piglin 专用护甲树（2026-11-07）**：baby Piglin/Zombified Piglin 接入 `Compiled`；`HumanoidArmorTrees.ForBabyPiglin` 使用 uniform 0.7、arm offset `(0.5,-0.5,0)`、空 ears；树测试 19 项通过。
32. **T-728 剩余 adult 模型树迁移（2026-11-07）**：Axolotl/Parrot/Salmon/TropicalFish/Villager/ZombieVillager 接入 `Compiled`；TropicalFish pattern、Villager 群系变体使用树复制层；树测试 20 项通过。
33. **T-729 第二批剩余 adult 模型树迁移（2026-11-07）**：Cod/Dolphin/Ghast/MagmaCube/Turtle 与 Pufferfish 三形态接入 `Compiled`；树测试 21 项通过。
34. **T-730 第三批剩余 adult 模型树迁移（2026-11-07）**：Blaze/Hoglin/Zoglin/Slime/Strider/Sniffer/Giant 接入 `Compiled`；Giant 复用 Zombie 树并保留 6× scale；树测试 22 项通过。
35. **T-731 第四批模型树迁移（2026-11-07）**：Guardian/ElderGuardian、EnderDragon、WitherBoss 接入 `Compiled`；ZombieNautilus temperate 复用 Nautilus 树；warm coral 变体待 coral 子树挂载；树测试 23 项通过。
36. **T-732 ZombieNautilus warm coral 树（2026-11-07）**：`AddChild` 挂载 coral 子树到基础 shell；保留穿甲隐藏可见性；树测试 24 项通过。
37. **T-733 显式层顺序计划 + 动态层缓冲（2026-11-07）**：`EntityRenderLayerDefinition`/`RenderLayers` 默认 0/10/20/30；`BuildLayers` 按批次并集排序；`WindowedRenderer` 层缓冲动态增长。
38. **T-734 幼年基础模型树迁移（2026-11-07）**：Baby Pig/Cow/Chicken 与 Baby Cat/Wolf/Fox/Rabbit 及变体接入 `Compiled`；baby collar 树层；树测试 25 项通过。
39. **T-735 幼年水生 / 马科模型树迁移（2026-11-07）**：Baby Squid/Bee/Dolphin/Axolotl 与 Baby Horse/Llama/Camel/Donkey 及全部变体接入 `Compiled`；Baby Horse markings 走 `WithModelLayer` 树复制层；树测试 26 项通过。
40. **T-736 Horse markings 生长值修复（2026-11-07）**：按 `HorseMarkingLayer` 语义去掉 legacy flat markings 的 `+0.05f` inflation，与编译树一致。
41. **T-737 剩余幼年模型树迁移（2026-11-07）**：Baby Misc（Goat/Panda/PolarBear/Turtle）、Odd（Strider/Husk/Snifflet/犰狳）、Final（Nautilus/HappyGhast/GlowSquid）及变体接入 `Compiled`；Husk/GlowSquid 复用已有编译树；树测试 27 项通过。
42. **T-738 模型级实体层（humanoid 护甲/Elytra）（2026-11-07）**：`EntityRenderLayerDefinition` 增加独立 Model/纹理/可见性；`BuildModelBackedLayers` 支持模型级层；humanoid 护甲与 Elytra 独立层接线；当前逐实体构建，性能分组待后续。
43. **T-739 全量非 baby 装备独立层（2026-11-07）**：马/驴/骡/骷髅马/僵尸马、骆驼、羊驼、鹦鹉螺、快乐恶魂、狼、Piglin 装备树改为模型级独立层；`AllCubes` 修复装备层 pipeline；Wolf crackiness 独立层；全量 Client 1554 项通过。
44. **T-740 baby 护甲独立层（2026-11-07）**：baby humanoid/Piglin 四槽护甲与 baby Elytra 改为模型级独立层，`HumanoidArmorBaby` 纹理族与 `AllCubes` 保持；其他 baby 装备仍 merged compiled；全量 Client 1556 项通过。
45. **T-741 模型树覆盖/层顺序门禁（2026-11-07）**：新增全注册表不变量测试 3 项；登记 baby Hoglin/Zoglin 的 flat 路径缺口（棘轮基线，不改生产代码），已由 T-742 收口；全量 Client 1559 项通过。
46. **T-742 baby Hoglin/Zoglin 树路径收口（2026-11-07）**：两者共享 `HoglinCompiled`；删除 T-741 棘轮基线、覆盖门禁恢复严格全量断言；全量 Client 1560 项通过。
47. **T-743 硬约束后续行动文档 + 外部审查稿存档（2026-11-07）**：新增 `next-actions-hard-constraints.md` 与 `external/` 两份原始审查稿；STATE 下一步指向新排期文档。
48. **T-744/T-745/T-746/T-747/T-748/T-748d/T-748i/T-748j/T-749/T-750/T-751/T-752/T-752a/T-752b（2026-11-07）**：UV 门禁扩展至 compiled/equipment；主场景 Pass 共用 `RecordScenePasses`；`EntityModelPart` 参数化构造 internal，全部模型定义/helper/mesher 的 baked-part `with` 已改 `With`（仅集中 transformer 保留）；ModHost 收口；`EquipmentClientInfo` 19 LayerType + `EquipmentAssetRegistry`，`equippable` asset id、dye/player-texture/glint 元数据与 `ResolveTrim` predicate 已透传，`dyed_color` 通过 `ResolveEquipmentTint` 进入模型 tint selector，`EquipmentTextureCatalog` 优先资产层纹理、静态 fallback；T-748d 起资产层纹理路径（`Assets.TexturePaths`）进入 `EntitySkinLibrary` atlas；glint 有独立 `Glint` layer + `fs_entity_glint` additive pass（host clock 滚动），trim texture/palette 见 T-754；T-748i 起 `ResolveEquipmentTextureInfo` 暴露完整 `EquipmentTexture` 元数据、`ResolveEquipmentTexture` 为薄委托；T-751 起 `BuildModelBackedLayers` 仅按连续相邻有效层模型分组批量建几何、复用字段避免每层分配，交错模型保持原提交顺序；EntityTracker stepped delta/ticks 回归通过；T-752 起 `use_player_texture` 已接玩家 `textures` 档案属性、cape/elytra 下载缓存与 atlas 注册、elytra > cape(showCape) 优先级及装备层第一层替换；T-752a 起 `PlayerSkinTextures.Slim` 经 `EntityRenderState.Slim` 选择 `PlayerModelDefinition.SlimDefinition`（3px 手臂），wide 路径不变，原版映射表/真值向量见 entity-equipment.md §12；T-752b 起 `PlayerSkinCache` 对 SKIN/body 执行参考 `processLegacySkin`（64×32→64×64、12 组 copyRect、alpha 修正），cape/elytra 不转换，映射表见 §13；T-752c 起中性共享库 `Sharpcraft.AuthLib` 提供 discovery/publickeys 加载、`SHA1withRSA` 验签、`ProfileSignatureState` 与纹理 URL 策略，`PlayerSkinCache` 接入本地/远程 `requireSecure`；T-752a-1 补全 `DefaultPlayerSkin.get(UUID)`：10 组 jshell 采样的 Java `UUID.hashCode()`+floorMod 真值向量驱动 18 个默认皮肤路径与 Slim 标记，无档案属性按 UUID 回退、纹理与模型一致（entity-equipment.md §12）；T-752a-2 核对 26.3 `PLAYER_SLIM_ARMOR`/`PLAYER_ARMOR` 共用一个 `playerArmor` mesh、slim/wide 护甲同树，按 AGENTS 4.1 不新增 slim 护甲树，仅更正 §12.3 陈旧描述并保留证据指针；T-754 起 trim texture/palette 已接（11 材质×18 图案×2 人形层，8 色 palette 映射，humanoid armor trim 层，equipment trim palette/texture override 与动态 TrimRegistryTable；glint 已用原版 0.16 矩阵/10° 旋转/时间偏移、`SRC_COLOR` 混合与 `EQUAL` 深度），R-019 独立跟踪。
49. **T-654..T-661 main 合并（2026-11-06）**：原则 14 + 单轨收敛 + 方块实体/遮挡地基并入本分支；`PlaySession` 冲突取 main 的“未处理包全部延迟”版本；traceability 编号顺延（原 main T-344..T-351 → T-654..T-661）。
50. **实体模型/动画、渲染与协议长尾（2026-11-02~06）**：全部 baby 模型族、villager/zombie/horse 变体、cat/wolf 项圈、panda 基因、动画 provider 与测试；Play Clientbound 覆盖审计 + P0/P1 包批次（T-642..T-647）、反相 Z、参考 OIT 云/流体与天空盘逐像素近似；T-648..T-650/T-651..T-653 完成 Elytra 翼盒/UV、流体面剔除、云 OIT depth-bounds 与 `Sharpcraft.Assets` 分层。

## 阻塞项

- **无硬阻塞**（T-752c 已由 `Sharpcraft.AuthLib` 实现，详见 entity-equipment.md §14）。
- **R-017**：XSTS 端点偶发网络抖动，联网全链路验收脚本含重试策略。
- **R-019**：模组 `AssemblyLoadContext` 热卸载后的 GC 回收探针尚未稳定转绿（排入 Phase 4 P4.1）。

## 下一步具体动作

> 下一阶段执行清单见 [`docs/analysis/render/next-actions-hard-constraints.md`](analysis/render/next-actions-hard-constraints.md)；下面为原有 backlog。

51. **T-631 held-item/item-model**：T-631a 数据层 + T-631b Witch 药水 + T-631c `ItemAtlas`/独立 item bind group/held_item + T-631d special 描述符与 **10/13** special 模型已接。T-631e 已落地：pattern/base color 解码、盾牌 sprite 选择、逐立方体 `TextureSelector`/`TintSelector`、`DyeColors` 真值表、banner/shield 图案叠加层（6 层共面）、`SpearAnimations`（STAB 施加到 `held_item`）。**T-761 掉落物渲染已在真实服务器验收**（正版 online-mode 与会话链路已单独验证；offline 复测走 RCON summon）：自转、单剑外观、扁平物品侧面厚度均与原版一致，尺寸经真值确认（生成型 0.5×0.5×0.03125 格）。**剩余（按影响排序）**：① special 模型掉落物（T-764）；② `composite` 多子模型（T-766，床只画一半）；③ `tintindex` 染色（树叶/草类颜色）；④ 元素 `rotation`；⑤ 侧面只扫静态首帧；⑥ 展示框与 GUI。另：**T-759** 方块实体真实几何、**T-760** 网格 DSL `visibleFaces` 已独立登记待做。
52. **EnderDragon flight history**：已接（T-757）；剩余动画长尾（Bee stinger / Guardian eye look）按 backlog 推进。
53. **P1.5（`Client/Render/ChunkMesher`）**：用 `OcclusionShape.IsFaceOccludedByState/Self` 替换 `IsSolid` 近似；配 `FluidMesherTests` 真值回归。
54. **P1.4（T-042）**：提取 26.3 `registry_data` 真值向量，把服务器下发注册表动态装配进 `BlockStateLayout`。
55. **P0.3（`Framework/Rendering`）**：`OffscreenRenderer` 与 `WindowedRenderer` 收敛为共用世界 Pass 序列。

## 最近提交

- `4db8cec` fix(render): interpolate ticksUsingItem with the partial tick (T-765)
- `3600aa1` feat(render): apply the spear arm pose from SpearAnimations (T-765)
- `065027c` feat(render): apply the spear use-item animation from kinetic weapon values (T-765)
- `587d738` feat(render): resolve the shield hand transform per using-item state (T-631e)
- `8464111` fix(render): apply the reference shield model flip so the face points outward (T-769)
- `de725b1` feat(render): bake the display and anchor chain into the shield hand layers (T-769)

- `bd424b3` fix(render): decouple baby elytra UVs and preserve stepped move deltas (T-663/T-664)
- `5a5a9c9` docs: record the latest commits in STATE
- `7b68fe3` feat(render): finish the baby elytra variant (ELYTRA_BABY) (T-662)
- `b1b65df` merge: main (principle 14 guardrails, Phase 0/1 foundation)
- `b4d32e2` docs: reconcile the equivalence matrix with the branch state
**物品渲染等效性检查层（L1–L4，2026-11-08）**：新增 `tools/Sharpcraft.ItemRender.Audit`（1658 物品解析审计 + 棘轮基线；未解析 17→0，修 `composite` 子项解析 T-766）、`ItemInHandMesher` 让手持物品复用共享物品几何并删除假 8×8 quad（T-765）、display 变换改四元数构建并引入 JOML 金标准 6×6 点（修所有带旋转上下文的朝向，T-767）、`tools/Sharpcraft.ItemRender.Sheet` 接触表（CPU 软光栅 + asset-free sidecar 金标准，T-768）。**下一步**：special 模型掉落物（T-764）、手持锚点 STAB/使用中姿态（T-765）、`composite` 多子模型（T-766）
