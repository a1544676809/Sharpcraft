// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

<!-- doc
title: 实体模型改造点清单
summary: 从当前扁平 EntityModelPart 迁移到树定义 + 集中烘焙的逐项工程改造清单与验收条件
status: 已评审
updated: 2026-11-07
-->

# 实体模型改造点清单

## 1. 用途

本文把 [实体模型与原版差距总表](entity-model-parity-gap.md) 中的差距转换成**可逐项施工、可逐项验收**的改造点。目的是防止：

- 先按扁平 `EntityModelPart` 写完模型/装备，再做树结构返工；
- 只在 helper 里补 `GrowXyz`/scale，而没有进入 DSL；
- 只改 mesher，不改装备层和测试；
- 把 `EquipmentClientInfo` 等独立任务混入模型树迁移。

优先级：P0 = 不做就无法安全迁移下一个模型；P1 = 树/装备层完整等价必需；P2 = 独立能力或清理。

本文只列“要改什么、在哪改、如何验收”；原版事实与差距证据见 [实体模型与原版差距总表](entity-model-parity-gap.md)。

## 2. DSL 与模型定义层

| ID | 改造点 | 涉及文件 | 依赖 | 验收条件 | 优先级 |
| --- | --- | --- | --- | --- | --- |
| RF-01 | `VanillaPartDefinition` 补齐树操作：`AddOrReplaceChild` 替换/合并语义、`ClearChild`/`ClearRecursively`、`RetainPartsAndChildren`/`RetainExactParts`、可克隆/派生 | `VanillaMeshDefinition.cs` | 无 | 护甲槽位树、Breeze 多 layer 树可由同一 API 构造；零 cube 组节点保留 | P0 |
| RF-02 | `VanillaPartPose` 增加 `ScaleX/ScaleY/ScaleZ`（默认 1），Bake 参与父子变换 | `VanillaMeshDefinition.cs`、`VanillaMeshBake.cs` | RF-01 | `BabyModelTransform`/DragonHead/EndCrystal 等事实可无手工预缩放地表达 | P0 |
| RF-03 | `VanillaCubeDefinition` 增加 `GrowXyz`（`Vector3?` 或三轴字段）；保留标量 `Grow` 回退 | `VanillaMeshDefinition.cs`、`VanillaMeshBake.cs` | RF-02 | 幼年护甲 `(-0.1,0.5,0.3)` 可由 DSL 表达；UV 仍使用未膨胀尺寸 | P0 |
| RF-04 | 引入 `MeshTransformer` / `BabyModelTransform` 等价管线：root-only scaling、head/body root-child translate+scale；不在每个模型手算 | `VanillaMeshDefinition.cs` 或新 `VanillaMeshTransformer.cs` | RF-01/02 | 同一 transformer 可应用于任意树；不依赖扁平 part 数组 | P0 |
| RF-05 | 统一 root/child 旋转符号：由 Bake 负责 X/Y 共轭；迁移现有 root rotation 值，移除逐模型补偿 | `VanillaMeshBake.cs`、所有带 root rotation 的模型定义 | RF-04 | 原版 facts 全库通过；Fox/Rabbit/Pig/Chicken 等 root rotation 不再手工取反 | P0 |
| RF-06 | 扩展 `VanillaCubeDefinition` 的 `xTexScale/yTexScale` 与 `visibleFaces`（可先留接口） | `VanillaMeshDefinition.cs` | RF-03 | `PlayerCapeModel`/ChestModel 等复用 DSL 时不需要另写模型系统 | P2 |

## 3. 运行时模型与 Mesher

| ID | 改造点 | 涉及文件 | 依赖 | 验收条件 | 优先级 |
| --- | --- | --- | --- | --- | --- |
| RF-07 | 新增 `EntityModelCube`：一个 cube 的纯数值（box/UV/grow/mirror/kind/overlay） | 新 `EntityModelCube.cs` | RF-03 | cube 不再必须是 `EntityModelPart` 本身 | P0 |
| RF-08 | 新增 `EntityModelPartNode`：name、pose、cubes range、children range、parent index、可见性/纹理/emissive 元数据 | 新 `EntityModelPartNode.cs` | RF-07 | 零 cube 组节点、一个节点多 cube、真实父子关系可表达 | P0 |
| RF-09 | 新增集中 `VanillaMeshBakery`：树 → 编译后的节点/ cube 表；保留原版 part 身份和顺序；统一处理 root/child 变换、UV、grow、texture | 新 `VanillaMeshBakery.cs`；`VanillaMeshBake` | RF-01..08 | Breeze `head` 一个节点 2 cube、`wind_mid` 一个节点 3 cube；无伪 part | P0 |
| RF-10 | `EntityModelDefinition.Parts` 改为 `Root` + `Nodes`/`Cubes`（或树节点列表）；所有模型定义注册时统一 bakery | `EntityModelDefinition.cs`、`EntityModelResolver.cs`、全部 `*ModelDefinition.cs` | RF-09 | 模型定义不再保存手工 `EntityModelPart[]`；resolver 只取烘焙结果 | P0 |
| RF-11 | `EntityModelMesher` 改为按节点遍历：每节点一次 matrix，遍历 cube range；`VisibleWhen` 继承父节点；支持 `skipDraw` | `EntityModelMesher.cs` | RF-08/10 | Armadillo rolled 时 `head_cube`/`body_inner` 不发射；Breeze 全 part 数/UV 与 facts 一致 | P0 |
| RF-12 | `PartFrame` 移除按名字线性查父节点；使用 parent index / 显式 pose map；`PoseSource` 字符串前缀规则改为显式映射 | `EntityModelMesher.cs`、`EntityPose` | RF-08 | 大模型无 O(n²) 父查找；装备层 pose 不依赖 `equipment_*` 前缀猜测 | P1 |
| RF-13 | `EntityModelLayer` overlay 改为节点/cube 级 metadata；支持 `GrowXyz`、多 cube、`skipDraw`、可见性继承 | `PlayerModelDefinition.cs`、`VanillaMeshDefinition.cs`、`EntityModelMesher.cs` | RF-07/08 | hat/jacket/sleeve/pants、幼年护甲 outer 层不再靠扁平克隆 | P1 |

## 4. 装备与附加层

| ID | 改造点 | 涉及文件 | 依赖 | 验收条件 | 优先级 |
| --- | --- | --- | --- | --- | --- |
| RF-14 | 人形护甲改为四套独立槽位树（HEAD/CHEST/LEGS/FEET），树内保留零 cube 组节点；按槽 deformation 由树构建 | `EntityModelLayerHelpers.WithArmorSlot`、`BabyHumanoidArmorParts`、`HumanoidModel` 相关定义 | RF-01/09 | 护甲 part 数、父链、Grow 与 reference `createArmorMeshSet` facts 一致 | P1 |
| RF-15 | 马鞍/骆驼鞍/鹦鹉螺甲/快乐恶魂挽具改为“基础 body 树派生 + 额外节点”的独立层树；不再 `EquipmentModelParts.BuildFiltered` + fake parent | `EquipmentModelParts.cs`、`EntityModelLayerHelpers.WithEquipment` | RF-09/14 | 鞍/壳/挽具 part 树与 `EquineSaddleModel`/`CamelSaddleModel`/`NautilusArmorModel` 对照通过 | P1 |
| RF-16 | Elytra 改为独立两翼树层，附着到基础模型层；删除 `BuildElytraWings` 的扁平结果拼装和 `with` 扩展 | `EntityModelLayerHelpers.WithElytra` | RF-09/14 | 成人/幼年 Elytra facts、pivot、UV、crouch offset 全通过；仍是独立树 | P1 |
| RF-17 | Collar/Crackiness/Wolf armor/Horse markings 改为基础树派生层；保留原版 layer 的纹理/可见性/父链 | `EntityModelLayerHelpers.WithCollar/WithCopy/WithCrackiness/WithHorseMarkings` | RF-09/14 | 各层 part/cube/纹理 facts 与 reference 对照通过 | P1 |
| RF-18 | 新增 `EntityRenderLayerDefinition`：Name、Model tree、Texture、Render kind、Order、VisibleWhen；`BuildLayers` 不再固定 `Cutout→Elytra→Translucent→Emissive` | `EntityModelDefinition.cs`、`EntityModelMesher.BuildLayers`、`WindowedRenderer` | RF-10/14 | 装备层顺序/纹理/深度状态与 reference RenderLayer 顺序一致；实测无透明串层 | P1 |
| RF-19 | `EquipmentClientInfo` 独立资产层：19 个 LayerType、`Layer` 列表、dyeable/use_player_texture/trim_overrides/glint | `EquipmentTextureCatalog` 或新 `EquipmentAssetRegistry` | RF-18 | 静态 catalog 退化为 fallback；原版装备 JSON 可解析并驱动层列表 | P2（独立任务） |

## 5. 动画与渲染

| ID | 改造点 | 涉及文件 | 依赖 | 验收条件 | 优先级 |
| --- | --- | --- | --- | --- | --- |
| RF-20 | `EntityPose` 支持 bind scale 与绝对/增量语义明确化；支持父节点 scale/可见性传递 | `EntityAnimation.cs`、`EntityModelMesher.cs` | RF-02/08 | 带 bind scale 的模型可正确动画；无手工预缩放依赖 | P1 |
| RF-21 | 动画 providers 与装备层 `PoseSource` 解耦：按节点身份/显式映射，不按 `equipment_` 前缀猜 | `*AnimationProvider.cs`、`EntityPose`、装备层 | RF-12 | 装备层动画与基础模型同源；改前缀不会破坏动画 | P1 |
| RF-22 | `WindowedRenderer` 的 entity layer 数量/顺序从固定 `MaxEntityLayers=4` 改为动态/显式队列；每层独立 pipeline/depth/texture | `WindowedRenderer.cs`、`EntityGeometryLayer.cs` | RF-18 | 多于 4 个 equip/render layer 时不截断；顺序与定义一致 | P1 |
| RF-23 | `EntityModelDefinition.Scale` 与 `PartPose` subtree scale 分离；整模型 scale 仅作为模型级变换 | `EntityModelDefinition.cs`、`VanillaMeshBakery` | RF-04 | Giant/Husk 等整模型缩放与局部 bind scale 不再混淆 | P2 |

## 6. 测试、工具与清理

| ID | 改造点 | 涉及文件 | 依赖 | 验收条件 | 优先级 |
| --- | --- | --- | --- | --- | --- |
| RF-24 | `ReferenceVerifier` 扩展：多 cube part、零 cube group、PartPose scale、MeshTransformer、装备树；facts schema 增加 `part.cubes[]`/`children[]` | `tools/Sharpcraft.ReferenceVerifier` | RF-09 | Breeze/Warden/装备模型的 facts 能直接驱动 Golden 断言 | P0 |
| RF-25 | 新增树/几何 Golden：按 node 身份比较、multi-cube 顶点/UV、零组节点、树顺序；替代 `LegacyModelPartFactory` 自证 | `tests/Sharpcraft.Client.Tests/Render` | RF-24 | Breeze 不再只能做事实提取；所有新迁移模型有 reference facts 断言 | P0 |
| RF-26 | 增加不变量门禁：父/子可见性继承、UV 越界/跨度、Z-fighting、装备 clearance、part 顺序 | `Sharpcraft.Architecture.Tests` | RF-24/25 | 全注册模型/装备组合自动扫描，违规失败 | P1 |
| RF-27 | 清理旧路径：删除 `BuildParts()` 字典重排、fake parent、`With*` 的 `part with` 克隆、`PoseSource` 前缀补丁、旧 `EntityModelPart[]` 注册 | `src/Sharpcraft.Client/Render` | RF-10..23 | `git grep` 无 fake/legacy 扁平定义；全量测试绿 | P1 |
| RF-28 | 文档同步：模块/装备/动画/风险/STATE/traceability 与实现同提交 | `docs/modules`、`docs/analysis`、`docs/STATE.md`、`docs/traceability.md` | 每项改造 | docs index/coverage 通过 | P0（持续） |

## 7. 推荐施工顺序（依赖图）

```text
RF-01..05 (DSL)
  -> RF-07..10 (Cube/Node/Bakery/Definition)
    -> RF-11..13 (Mesher/PartFrame/Layer)
      -> RF-14..17 (Armor/Saddle/Elytra/Other equipment)
        -> RF-18/22 (Layer order/renderer)
          -> RF-19 (EquipmentClientInfo, separate)
RF-20/21 (Animation) after RF-08/12
RF-24..26 (Facts/Golden/Invariants) run before each migration; RF-27 cleanup last
```

## 8. 明确禁止的返工路径

- 不允许新增模型继续手写 `EntityModelPart[]`；
- 不允许在 helper 里直接 `part with { GrowXyz = ... }` 代替 DSL；
- 不允许用 `BuildParts()` 字典/名字重排解决顺序；
- 不允许用 fake parent 过滤解决零 cube 组节点；
- 不允许用 `LegacyModelPartFactory` 作为 Golden 真值；
- 不允许在 `EquipmentModelParts` 里继续堆额外盒子来模拟完整装备树；
- 不允许把 `EquipmentClientInfo` 的全量资产解析混入本轮树迁移；
- 不允许在未补 facts 前把任何模型标记为“原版等效”。

## 9. 当前进度

### 2026-11-07 — RF-07..RF-09 基础落地（T-713）

已落地：

- `EntityModelCube`：多 cube 的纯数据类型；
- `EntityModelPartNode`：节点拥有 cube 列表 + children，零 cube 组节点可保留；
- `CompiledEntityModel`：root + pre-order part/cube 快照；
- `VanillaMeshBakery`：从 `VanillaMeshDefinition` 树构建结构模型；
- Breeze DSL 已改为原版结构：`head`/`eyes`/`wind_mid`/`wind_top` 为 multi-cube part；
- `EntityModelTreeBakeryTests`：Breeze 11 part/14 cube、Armadillo 14 part 且 `head` 零 cube 组节点保留。

未完成（下一刀）：

- RF-10：`EntityModelDefinition` / resolver 接入 `CompiledEntityModel`；
- RF-11/RF-12：Mesher 按节点/cube range 遍历，移除 `PartFrame` 名字查找；
- RF-03/RF-04：`GrowXyz`/`PartPose` scale 的完整 bake；
- RF-14..：装备树迁移。

### 2026-11-07 — RF-10/RF-11 树渲染接入（T-714）

已落地：

- `EntityModelDefinition` 新增可选 `Compiled`；
- Breeze/Armadillo 的定义同时提供 `CompiledEntityModel`（旧 `Parts` 保留为兼容数据）；
- `EntityModelMesher` 增加树路径：按节点递归计算矩阵、遍历 cube、父节点 `VisibleWhen` 继承到子树；
- `EntityModelTreeBakeryTests` 增加 Breeze 几何 smoke 与 Armadillo rolled/unrolled 可见性测试。

仍未完成：

- RF-12：树路径的 `PoseSource`/装备层 mapping 尚未接入；旧 `PartFrame` 仅保留给未迁移的扁平模型；
- 其余模型仍走 `EntityModelPart` 扁平路径，需逐模型迁移；
- RF-14 起装备树仍未开始。

### 2026-11-07 — T-715 farm/quadruped 树路径批量迁移

已迁移到 `Compiled` 路径：

- Pig（temperate/warm/cold）；
- Cow（temperate/warm/cold + Mooshroom）；
- Chicken（temperate/warm/cold）；
- Sheep；
- Fox（red/snow，T-714 已接入）；
- Rabbit（全部变体，T-714 已接入）。

同时修正树路径 root pose 平移：`PartMatrix` 的 root 反平移必须使用**未加动画 offset 的 base pivot**，否则根节点动画位移被抵消；`EntityLayerTests.AnimationPoseOffsetsAreAppliedBeforeMeshing` 已重新通过。

### 2026-11-07 — T-716 simple model batch

新增 `EntityModelDefinition.FromTree(...)` 作为迁移入口：一次生成 legacy `Parts` 和 `Compiled`，模型定义不再手写两个数组。

已通过 `FromTree` 迁移：

- Panda（全部变体）；
- Goat；
- Frog（全部变体）；
- Tadpole；
- Bat；
- Allay；
- Bee（状态纹理 selector）；
- Vex（充能纹理 selector）。

### 2026-11-07 — T-717 arthropod/aquatic batch

通过 `FromTree` 迁移：

- Creeper；
- Spider（保留 emissive eyes）；
- Endermite；
- Shulker（全部 dye 变体共享 compiled tree）；
- Squid（GlowSquid 共享 compiled tree）；
- Nautilus；
- CaveSpider 复用 Spider compiled tree，并保留模型级 `Scale=0.7`。

### 2026-11-07 — T-718 Cat/Wolf collar 树层

- 新增 `EntityModelLayerHelpers.WithCollar(CompiledEntityModel, ...)`：递归克隆树节点/cube，保留父子关系；
- Cat collar 使用 reference `AdultCatModel.COLLAR_DEFORMATION = new CubeDeformation(0.01F)`；
- Wolf collar 使用同一模型几何、collar texture、零 grow；
- Cat/Wolf 所有变体接入 compiled tree；collar 节点通过 `VisibleWhen: HasCollar` 和 `CollarTint` 工作。

### 2026-11-07 — T-719 Warden emissive 树层

- 新增 `EntityModelLayerHelpers.WithEmissiveLayer(CompiledEntityModel, ...)`；
- 按 reference `retainExactParts` 语义只保留指定 source 节点的 cube，保留祖先 group 以维持 pose；
- Warden 四层：
  - `bioluminescent_`：head/arms/legs；
  - `spots_`：body/head/arms/legs；
  - `heart_`：body；
  - `tendril_glow_`：left/right tendril；
- 基座 + 四层 emissive 全部进入 `Compiled`；`EmissiveOnly` / `EmissiveTexturePath` 由树路径过滤。

### 2026-11-07 — T-720 剩余无装备模型批

通过 `FromTree` 迁移：

- Phantom（保留 emissive eyes）；
- Ravager；
- PolarBear（1.2 scale）；
- Silverfish；
- IronGolem；
- SnowGolem；
- Creaking（保留 emissive eyes）；
- CopperGolem（四种 weathering 纹理 + eye texture）。

### 2026-11-07 — T-721 Illager/Witch

通过 `FromTree` 迁移：

- Illager 共享模型的 Vindicator/Evoker/Pillager/Illusioner；
- Witch（0.9375 scale）。

### 2026-11-07 — T-722 Camel 基础树 + 鞍装备树

- `CamelModelDefinition` 接入 `Compiled`；
- 新增 `WithEquipment(CompiledEntityModel, ...)`：把 `EquipmentModelParts` 的额外 saddle/bridle/reins 节点挂到基础 `body`/`head` 节点，不扁平化基础模型；
- `EntityModelResolver.Decorate` 对 Camel（type 19）使用 compiled equipment tree；
- 额外装备节点保留 `ResolveEquipmentTexture` 可见性。

### 2026-11-07 — T-723 非人形装备层独立树迁移

- 新增 `EquipmentModelTrees`：按原版装备模型类分别烘焙完整装备树（马甲 `HorseModel` 0.1 deformation、马鞍 `EquineSaddleModel`、骆驼鞍 `CamelSaddleModel`、羊驼装饰 `LlamaModel` 0.5、鹦鹉螺甲/鞍、快乐恶魂挽具、狼甲 0.2）；
- 新增 `EntityModelLayerHelpers.WithModelLayer`：把独立烘焙的装备树按 root 级 layer 合并，节点名加 `equipment_<kind>_<slot>_` 前缀、`PoseSource` 保留原节点名；装备纹理通过 `EntityModelCube.TextureSelector` 按槽位动态选择；
- 马：`Horse/Donkey/Mule/ZombieHorse/SkeletonHorse` 全部接入 `Compiled`；Donkey/Mule 使用独立 donkey 树（胸部箱 + donkey 耳朵）；Horse markings 改为完整树复制层（translucent、无 grow）；补上 reference `livingHorseScale = 1.1F`（普通马，骷髅/僵尸马不加）；
- 羊驼：head/neck/ears 改为原版 multi-cube part，`LlamaBody` 装饰层使用 0.5 deformation tree；Trader Llama 默认 trader 纹理层接入；
- 鹦鹉螺：shell/body 改为原版 multi-cube part；Armor shell 使用 0.01 tree、Saddle shell 使用单 cube 0.2 tree；
- 快乐恶魂：base 接入 `Compiled`，harness/goggles 独立树；
- 狼甲：`WolfBody` 使用 0.2 deformation 全树；crackiness 改为基于 0.2 狼甲树的 damage-texture 复制层；
- 鞍座 riding parts（马左右 saddle line、骆驼 reins）按 `IsVehicle` 控制可见性；
- `EntityModelTreeBakeryTests` 新增 Horse/Donkey/Llama/Nautilus/HappyGhast/Wolf 装备树断言，树测试共 15 项。

### 2026-11-07 — T-724 成人 humanoid 护甲/Elytra 树层

- `HumanoidMobModelDefinition` 的 Enderman/Drowned/Husk/Skeleton/Stray/Bogged/WitherSkeleton/Zombie 全部接入 `Compiled`；Stray/Bogged 的 clothing overlay 用 `WithModelLayer` 合并为独立树；Player 基础模型也接入 `Compiled` 并挂四槽护甲树；
- 新增 `HumanoidArmorTrees`：四槽独立树
  - HEAD：head + hat（outer 1.0，hat 1.5）
  - CHEST：body/left_arm/right_arm（outer 1.0）
  - LEGS：body/left_leg/right_leg（inner 0.5，腿 cube 0.4）
  - FEET：left_leg/right_leg（outer 1.0，腿 cube 0.9）
  - 非槽 root parts 保留零 cube 空组；Player 额外保留空 jacket/sleeve/pants 组
- 新增 `EntityModelLayerHelpers.WithElytra(CompiledEntityModel)`：从基础 body cube 派生两翼树，`Elytra` kind、动态纹理、wing rotation selector、crouch `YOffsetSelector`；
- 修正树路径 root 节点 `YOffsetSelector` 与 pivot 抵消：`basePivot` 含 yOffset，使鞍/翼的 crouch 位移只生效一次；
- `EntityModelTreeBakeryTests` 增加 humanoid compiled/armor/Elytra 断言，树测试共 16 项。

### 2026-11-07 — T-725 幼年 humanoid 护甲 / baby Elytra 树层

- `BabyModelDefinitionsHumanoid` 的 Zombie/Drowned/ZombieVillager/Villager 基础与 villager 变体全部接入 `Compiled`；Drowned outer、baby villager 变体用 `WithModelLayer` 树复制；
- 新增 `HumanoidArmorTrees.ForBaby(slot)`：按 `HumanoidModel.createBabyArmorMeshSet` 生成四套 baby 槽位树：
  - outer deformation `(-0.1, 0.5, 0.3)`，inner `(-0.1, 0.3, 0.3)`；
  - waist / leg cube 在对应槽位再 `extend(-0.1)`；
  - left_leg/right_leg 的 foot 子节点按 reference `retainExactParts` 保留；
- `EntityModelLayerHelpers.WithElytra(CompiledEntityModel, baby)` 增加 baby 分支：root scale 0.5 → 5×10×1 翼，Bottom 2 / PivotY 12，UV 仍按 10×20×2 采样；`EntityModelCube`/DSL 增加 UvWidth/UvHeight/UvDepth 覆盖；
- `EntityModelResolver.Decorate` baby 分支改用 `HumanoidArmorBaby` 纹理族和 baby 槽位树；
- `EntityModelTreeBakeryTests` 增加 baby armor 断言，树测试共 17 项。

### 2026-11-07 — T-726 Piglin 护甲树

- `PiglinModelDefinition` 接入 `Compiled`：player 的 body/arms/legs + piglin head 组成独立树，Piglin/Brute/Zombified 共享；
- `HumanoidArmorTrees.ForPiglin(slot)`：复用 player armor 四槽几何，并在 head 下补空 `left_ear`/`right_ear` 组（`AbstractPiglinModel.removeEars`）；
- `EntityModelResolver` 为 type 102/103/158 增加独立 `PiglinArmorLayers`（不与 humanoid 的 `HumanoidArmorLayers` 共享引用，避免误加 Elytra）；
- 树测试增加 piglin armor 断言，树测试共 18 项。

### 2026-11-07 — T-727 Baby Piglin 专用护甲树

- `BabyModelDefinitionsNether` 的 baby Piglin/Zombified Piglin 接入 `Compiled`；
- `HumanoidArmorTrees.ForBabyPiglin(slot)`：按 `AbstractPiglinModel.createBabyArmorMeshSet` 使用 uniform 0.7 deformation、arm offset `(0.5,-0.5,0)`、空 ears 组；
- `EntityModelResolver` 对 baby piglin 传入 piglin 标志并使用 `HumanoidArmorBaby` 纹理族；
- 树测试增加 baby piglin armor 断言，树测试共 19 项。

### 2026-11-07 — T-728 剩余 adult 模型树迁移

- Axolotl、Parrot、Salmon、TropicalFish、Villager、ZombieVillager 全部接入 `Compiled`；
- 各变体共享对应 compiled tree：
  - Salmon 的 small/medium/large 仍通过定义级 `Scale` 区分；
  - TropicalFish 的 pattern copy 通过 `WithModelLayer` 生成树层，并保留 `PatternTint` tint selector 与 pattern 纹理 selector；
  - Villager 的群系变体通过 `WithModelLayer` 生成 `copylayer_` 树层（+0.05 grow）；
  - Axolotl/Parrot/ZombieVillager 变体直接共享 compiled tree；
- `EntityModelTreeBakeryTests` 增加剩余模型 compiled/variant 断言，树测试共 20 项。

### 2026-11-07 — T-729 第二批剩余 adult 模型树迁移

- Cod、Dolphin、Ghast、MagmaCube、Turtle 接入 `Compiled`；
- Pufferfish 的 small/medium/large 三个独立 mesh 分别接入 `Compiled`；
- Ghast 保留 4.5 scale、charging texture selector 与 shooting additional path；
- Turtle 保留 egg_belly 可见性名与 128×64 纹理尺寸；
- 树测试增加第二批 compiled/part-count 断言，树测试共 21 项。

### 2026-11-07 — T-730 第三批剩余 adult 模型树迁移

- Blaze、Hoglin/Zoglin、Slime、Strider、Sniffer、Giant 接入 `Compiled`；
- Giant 复用 `HumanoidMobModelDefinition.Zombie.Compiled` 并保留 6× scale；
- Strider 保留 cold texture selector 与 additional path；Slime outer_cube 保留 Translucent kind；
- 树测试增加第三批 compiled/part-count 断言，树测试共 22 项。

### 2026-11-07 — T-731 第四批模型树迁移

- Guardian / ElderGuardian 共享同一 `Compiled`，Elder 保留 2.35 scale；
- EnderDragon 接入 `Compiled`（256×256 纹理，完整头/颈/尾/翼/腿树）；
- WitherBoss 接入 `Compiled`；
- ZombieNautilus temperate 接入 `Compiled`（复用 Nautilus 编译树）；warm coral 变体仍为 flat（coral 子树需挂到基础 shell 节点，后续处理）；
- 树测试增加第四批 compiled/part-count 断言，树测试共 23 项。

### 2026-11-07 — T-732 ZombieNautilus warm coral 树

- `VanillaPartDefinition.AddChild` 支持把已构建子树挂到指定节点；
- `ZombieNautilusModelDefinition` 新增 `BuildCoralBodyMesh`：基础 Nautilus mesh 的 `shell` 节点下挂 `corals` 子树，生成 warm variant 的完整 `Compiled`；
- coral 节点保留 `Equipment[6] <= 0` 的 `VisibleWhen`（穿甲时隐藏）；
- 树测试增加 warm coral 父子关系/可见性断言，树测试共 24 项。

### 2026-11-07 — T-733 显式层顺序计划 + 动态层缓冲

- 新增 `EntityRenderLayerDefinition { Name, Kind, Order }`；`EntityModelDefinition.RenderLayers` 默认给出
  `cutout 0 / elytra 10 / translucent 20 / emissive 30`；
- `EntityModelMesher.BuildLayers` 使用当前批次模型定义的并集按 `Order` 排序，不再使用固定 `LayerKinds` 数组；
- `WindowedRenderer` 的 entity layer 缓冲从固定 4 组改为按层数增长，避免超过 4 层被截断（RF-22）；
- `EntityLayerTests` 增加显式 order 断言；RF-18 的独立 per-layer model/texture/visible 仍待接线。

### 2026-11-07 — T-734 幼年基础模型树迁移

- `BabyModelDefinitions` 的 Pig/Cow/Chicken 及 warm/cold 变体接入 `Compiled`；
- `BabyModelDefinitionsExtra` 的 Cat/Wolf/Fox/Rabbit 及全部变体接入 `Compiled`；
- Baby Cat/Wolf 的 collar 通过 `WithCollar(CompiledEntityModel)` 生成树层；
- 树测试增加 baby farm/pet compiled 断言，树测试共 25 项。

### 2026-11-07 — T-736 Horse markings 生长值修复

- 核查 `HorseMarkingLayer`：原版直接用 parent model 的同一几何重绘 translucent marking 纹理，没有额外 inflation；
- 修正 legacy flat `WithHorseMarkings(EntityModelPart[])` 的 `Grow + 0.05f`，改为与基础 part 相同；
- 编译树 helper 原本已无 inflate，保持一致；更新 markings 断言，增加 tree 侧 grow 相等断言。

### 2026-11-07 — T-735 幼年水生 / 马科模型树迁移

- `BabyModelDefinitionsAquatic` 的 Squid/Bee/Dolphin/Axolotl 及 axolotl 全部色型变体接入 `Compiled`；
- `BabyModelDefinitionsEquine` 的 Horse/Llama/Camel/Donkey 及马/羊驼毛色变体接入 `Compiled`；
- Baby Horse 的四种 markings 通过 `WithHorseMarkings(CompiledEntityModel, ..., baby: true)` 生成 `WithModelLayer` 树复制层，保留 `marking_*` 纹理 selector、`Markings` 可见性与 Translucent kind；
- Bee 的状态纹理 selector、各模型扁平 `Parts`、变体名、纹理路径与动画 provider 均保持不变；
- 树测试增加 baby aquatic/equine compiled 断言，树测试共 26 项。

### 2026-11-07 — T-737 剩余幼年模型树迁移

- `BabyModelDefinitionsMisc` 的 Goat/Panda/PolarBear/Turtle 及 panda 全部基因变体接入 `Compiled`（Turtle 使用 16×16 纹理尺寸）；
- `BabyModelDefinitionsOdd` 的 Strider/Snifflet/犰狳接入 `Compiled`；Husk 复用 `BabyModelDefinitionsHumanoid.Zombie` 编译树；犰狳的 unrolled/rolled `VisibleWhen` 由树节点继承保留；
- `BabyModelDefinitionsFinal` 的 Nautilus/HappyGhast 接入 `Compiled`（HappyGhast 保留 0.95 定义级 scale）；GlowSquid 复用 `BabyModelDefinitionsAquatic.Squid` 编译树；
- 三个文件均无 `WithCopy`/collar/overlay 变体，故未新增树复制层；扁平 `Parts`、变体名、纹理路径与动画 provider 保持不变；
- 树测试增加剩余 baby 模型 compiled/变体/复用关系断言，树测试共 27 项。
