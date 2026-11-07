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
