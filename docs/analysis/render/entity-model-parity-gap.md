// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

<!-- doc
title: 实体模型与原版差距总表
summary: 实体模型定义、烘焙、运行时、动画与装备/附加层相对 Minecraft 26.3 的完整差距清单
status: 已评审
updated: 2026-11-07
-->

# 实体模型与原版差距总表

## 1. 目的、范围与结论

本文是实体模型子系统的**单一差距清单**，用于在继续迁移模型/装备前判断哪些结构必须先修，避免“旧扁平接口写一遍、树结构再返工一遍”。

范围：

- `src/Sharpcraft.Client/Render` 的模型定义、烘焙、运行时 `EntityModelPart`、`EntityModelMesher`；
- 实体动画 `EntityPose` / `PoseSource`；
- 人形护甲、Elytra、鞍、马甲、鹦鹉螺壳、快乐恶魂挽具、项圈/裂痕等装备与附加层；
- 模型事实测试与 Golden Vector。

不覆盖：方块实体模型、物品/手持模型、玩家皮肤/披风、UI、shader 管线。它们如果复用同一套模型 DSL，需要另立条目，不能混入本表。

核心结论：

- 原版从 `CubeListBuilder → PartDefinition → ModelPart` **全程保持树结构**；
- 当前项目在 `VanillaPartDefinition` 层保留了树，但 `VanillaMeshBake.Bake.EmitCubes` 把**每个 cube 摊成一个 `EntityModelPart`**，造成多 cube part、零 cube 组节点、父子可见性和装备层结构丢失；
- 正确目标是 **树用于模型定义与烘焙输入；集中 bakery 产出运行时节点表**。扁平数组只能是渲染边界的派生结果，不能继续作为定义层接口。

## 2. 原版事实摘要

| 事实 | 原版依据 |
| --- | --- |
| `CubeListBuilder` 为一个 part 累加多个 `CubeDefinition` | `ref://mc-26.3-analysis/src/net/minecraft/client/model/geom/builders/CubeListBuilder.java` |
| `PartDefinition` = cube 列表 + children map；`bake` 递归产出 `ModelPart` 树 | `ref://.../builders/PartDefinition.java` |
| `ModelPart` = cube 列表 + children map；每个节点应用一次 pose，再绘制全部 cube、递归 children | `ref://.../ModelPart.java` |
| `EntityModelSet.bakeLayer` 直接返回 `ModelPart` 根节点，无扁平转换 | `ref://.../EntityModelSet.java` |
| `PartPose` 带 xScale/yScale/zScale | `ref://.../PartPose.java` |
| `CubeDeformation` 支持三轴 grow | `ref://.../builders/CubeDeformation.java` |
| `MeshTransformer.scaling` 只改 root pose；`BabyModelTransform` 可分别变换 head/body root children | `ref://.../builders/MeshTransformer.java`、`ref://.../BabyModelTransform.java` |
| 人形护甲是四套独立 `MeshDefinition`，不是从基础 skin 模型复制 | `ref://.../HumanoidModel.java` 的 `createArmorMeshSet` |
| 马鞍从 `createBodyMesh` 的树派生并挂鞍节点 | `ref://.../animal/equine/EquineSaddleModel.java` |
| 骆驼鞍从 `createBodyMesh` 派生并挂 saddle/reins/bridle | `ref://.../animal/camel/CamelSaddleModel.java` |
| 鹦鹉螺护甲从基础树派生并挂 shell | `ref://.../animal/nautilus/NautilusArmorModel.java` |
| Elytra 是独立 `ElytraModel` 树 | `ref://.../object/equipment/ElytraModel.java` |

## 3. 差距矩阵

状态说明：

- **已对齐**：当前实现与原版行为等价，或已有 Golden 证据；
- **结构差异**：几何可能等价，但表示/接口与原版不同，会在后续模型或装备迁移时返工；
- **缺失**：当前无法表达或行为不等价；
- **范围外**：不属于当前实体 mob 规模，但要保留扩展点；
- **待验证**：已有实现但缺少原版真值证据。

### 3.1 模型定义与烘焙

| # | 差异 | 原版 | 当前实现 | 影响 | 状态 |
| --- | --- | --- | --- | --- | --- |
| M1 | 模型是树，不是扁平 part 数组 | `PartDefinition`/`ModelPart` 均为 cube 列表 + children map | `EntityModelPart` 是“one static model cuboid”；`Bake` 每个 cube 产生一个 part，父链为 `Parent` 字符串 | 多 cube、组节点、动画/装备层都会丢失结构 | 结构差异 |
| M2 | 一个 part 可拥有多个 cube | `CubeListBuilder.addBox` 多次追加；Breeze `head` 2 个、`eyes` 2 个、`wind_mid` 3 个、`wind_top` 3 个；Camel saddle 3 个、bridle 5 个 | 一个 `EntityModelPart` 一个 cube；多 cube part 被拆成伪 part | Breeze 只能做几何级对比；part 数量/身份不等价 | 结构差异 |
| M3 | 零 cube 组节点必须存在 | `CubeListBuilder.create()` 创建的 head/hat/head_parts 等空节点；`PartDefinition.bake` 保留 | `EmitCubes` 不产生对应 `EntityModelPart`；靠 fake parent 过滤 | 父 pivot/rotation 丢失，装备/护甲无法正确挂载 | 结构差异 |
| M4 | `addOrReplaceChild` 的替换与合并语义 | `PartDefinition.addOrReplaceChild` 可替换并合并旧 children；有 `clearRecursively`/`retainExactParts`/`retainPartsAndChildren` | `VanillaPartDefinition.AddOrReplaceChild` 遇到重名直接抛异常；没有 retain/clear | 无法表达原版护甲槽位裁剪和派生树 | 缺失 |
| M5 | `PartPose` 三轴 scale | `PartPose` 有 `xScale/yScale/zScale`；`BabyModelTransform`、`DragonHeadModel`、`EndCrystalModel`、`ArrowModel`、`SpinAttackEffectModel` 使用 | `VanillaPartPose` 只有 offset + rotation；`EntityPose` 的 Scale 是动画增量 | 不能原始等价地构建带 bind scale 的模型；后续模型需手工预缩放 | 缺失 |
| M6 | `MeshTransformer` / `BabyModelTransform` 通用变换 | `scaling` 只改 root pose；`BabyModelTransform` 按 head/body root child 分别 translate+scale | 只有 `VanillaMeshBake.Scale`，语义是“扁平 part 逐 part 缩放”；仅用于 Elytra root-only 两翼 | 嵌套模型缩放、幼年变换、Giant/Husk 等 scale 不通用；容易再次手算 | 缺失 |
| M7 | `CubeDeformation` 三轴 grow | 支持 `(growX,growY,growZ)`；幼年护甲 `(-0.1,0.5,0.3)` / `(-0.1,0.3,0.3)` | `VanillaCubeDefinition` 只有标量 `Grow`；`EntityModelPart.GrowXyz` 只能由 helper 直接写 | 完整 DSL 无法表达幼年护甲和未来非均匀变形 | 结构差异 |
| M8 | `CubeDefinition` 可见面集合 | `CubeDefinition` 存 `Set<Direction> visibleFaces`，可只生成部分面 | `VanillaCubeDefinition` 无 visibleFaces；mesher 总是生成六面 | 当前 mob 模型未使用；方块实体 ChestModel 使用；未来复用 DSL 会缺功能 | 范围外 |
| M9 | `CubeDefinition` xTexScale/yTexScale | 支持每个 cube 的 UV scale；PlayerCape 使用 `1.0, 0.5` | DSL 无该字段；只有 texture width/height 覆盖 | 玩家披风等非 mob 模型不能复用；当前 mob 范围外 | 范围外 |
| M10 | 子节点顺序 | `PartDefinition` 用 `HashMap` 保存 children；原版渲染遍历 map | DSL 用有序 `List`，并按 pre-order 烘焙 | 渲染顺序可能与原版 HashMap 顺序不同；透明/覆盖顺序需要参考帧验证 | 待验证 |
| M11 | `ModelPart.visible` 子树继承 | 父节点 `visible=false` 时整个子树不渲染 | `VisibleWhen` 是每个 part 单独求值，子 part 不会因父 part false 自动跳过 | 组级可见性/换装层可能露出本应隐藏的子部件 | 缺失 |
| M12 | `ModelPart.skipDraw` | 节点可跳过自身 cube 但仍递归 children | 没有 `skipDraw` 字段 | 目前 mob 模型未使用；未来层模型可能用 | 范围外 |

### 3.2 运行时、动画与查询

| # | 差异 | 原版 | 当前实现 | 影响 | 状态 |
| --- | --- | --- | --- | --- | --- |
| R1 | 运行时节点身份 | `ModelPart` 对象树，children 直接引用 | `EntityModelPart` 扁平记录 + `Parent` 字符串 | 每次 `PartFrame` 线性查父节点；组节点缺失 | 结构差异 |
| R2 | 父链复杂度 | 对象引用直接递归 | `PartFrame` 在每个 part 的 parts 数组中按名字找 parent，最坏 O(n²) | 大模型/装备层的帧遍历成本与结构缺陷叠加 | 结构差异 |
| R3 | 动画 pose 是绝对字段 | `ModelPart` 的 x/y/z/xRot/yRot/zRot/xScale... 是绝对 bind+animation 值 | `EntityPose` 存 `PoseOffset` 增量；mesher 把增量叠加到烘焙值 | 大部分动画已用手工增量适配，但全部等价性依赖逐模型测试 | 待验证 |
| R4 | bind scale 与动画 scale | `PartPose.xScale...` 初始 bind scale，动画可叠加 | `PoseOffset.Scale` 是相对 1 的增量；`EntityModelPart` 无 bind scale | 带 bind scale 的模型/装备无法等价 | 缺失 |
| R5 | `PoseSource` 字符串映射 | 原版装备模型继承基础模型，`setupAnim` 直接操作同名 ModelPart | 装备复制层用 `PoseSource` 字符串（及 `PosePartName` 前缀剥离）关联基础 part | 伪 part/前缀命名耦合；装备动画可能漏映射 | 结构差异 |
| R6 | `EntityModelSet` 层注册 | `ModelLayerLocation → LayerDefinition` 统一 bake | `EntityModelResolver` 静态 type id 字典 + 各模型手写定义 | 与树/bakery 无直接冲突，但装备层没有统一 layer 模型入口 | 可接受差异 |

### 3.3 装备与附加层

| # | 差异 | 原版 | 当前实现 | 影响 | 状态 |
| --- | --- | --- | --- | --- | --- |
| E1 | 人形护甲是独立四套树 | `HumanoidModel.createArmorMeshSet` 生成 head/chest/legs/feet 四套 `MeshDefinition`；`PlayerModel` 补 sleeve/pants 空节点 | `WithArmorSlot` 从**已烘焙基础 parts** `with` 克隆可见槽位，按槽设置 Grow/Texture | 护甲与基础模型同结构但非独立树；hat 等 overlay 只能特判；zero-cube 组节点丢失 | 结构差异 |
| E2 | 幼年护甲使用独立 baby mesh | `createBabyArmorMeshSet` 生成完整 baby 树，non-uniform deformation | `BabyHumanoidArmorParts` 用 DSL 生成独立部件，但 `WithBabyArmorSlot` 再从基础 parts 克隆/拼装 | 几何接近，但父链、group 节点、装备层结构不保真 | 结构差异 |
| E3 | 马鞍从基础 body 树派生 | `EquineSaddleModel` 从 `createBodyMesh` 起，在 body/head_parts 挂 saddle/mouth/line/head_saddle/wrap | `WithEquipment` 复制基础 parts + `EquipmentModelParts` 只补额外盒子；fake parent 过滤 | 基础树与鞍节点分离，`ridingParts` 可见性/父链无法自然表达 | 结构差异 |
| E4 | 骆驼鞍从基础树派生 | `CamelSaddleModel` 从 `createBodyMesh` 起，在 body/head 挂 saddle/reins/bridle；`reins.visible = state.isRidden` | 复制基础 parts + 额外 saddle 盒；`reins` 没有 isRidden 状态可见性 | 骑乘时缰绳可见性不等价；多 cube saddle/bridle 只能拆 | 缺失/结构差异 |
| E5 | 鹦鹉螺甲/鞍从基础树派生 | `NautilusArmorModel` 从基础树挂 shell（3 cube）；Saddle 独立 | 复制基础 parts + 额外 shell/saddle 盒；fake root 过滤 | shell 归属与父链、多 cube 不保真 | 结构差异 |
| E6 | 快乐恶魂挽具/绳索模型 | `HappyGhastHarnessModel` 独立 harness 树；body ropes 使用带 deformation 的基础树 | 额外 harness 盒 + `WithEquipment` 基础复制；ropes 目前仍为待办/近似 | 挽具层结构、绳索层缺失 | 结构差异/缺失 |
| E7 | Elytra 独立树 | `ElytraModel` 两翼独立 `MeshDefinition`；WingsLayer 单独实例 | `BuildElytraWings` 建两翼树后 `Bake`，再 `with` 扩展为装备层；加到基础 flat parts | 几何当前已验证；但 wing 与 body 无树关系，儿童/潜行偏移靠在 helper 中补偿 | 结构差异（几何已对齐） |
| E8 | Collar/项圈、狼甲裂痕等副层 | 原版各为独立 LayerDefinition，通常从基础树变换/追加 cube | `WithCollar`/`WithCopy`/`WithCrackiness` 对 flat parts `with` 复制并加 Grow | 结构/层级不可见；多 cube/组节点将来会丢 | 结构差异 |
| E9 | 装备纹理解析 | `EquipmentClientInfo` 的 layer 数据 | 静态 `EquipmentTextureCatalog` + item id/名称兜底 | 模组/新装备资产不能完整表达；已有受控偏差 | 缺失（已知） |
| E10 | 装备层每部件可见性 | 模型 `setupAnim` 可设置 `reins.visible` 等 | `WithEquipment` 的 `VisibleWhen` 只看纹理是否存在；没有 `isRidden`/槽位状态 | 缰绳等部件可能在不该显示时显示 | 缺失 |
| E11 | 装备层渲染顺序/类型 | 独立 RenderLayer、RenderType、层顺序 | `EntityGeometryLayerKind` + 合并几何 | 透明/覆盖顺序可能不等价；已有 `entity-render-layers.md` 记录 | 部分验证 |
| E12 | 护甲/装备与基础模型的 deform 语义 | 每个槽位独立的 `CubeDeformation`/PartPose 作用在完整树 | 多数用标量 Grow + base copy；幼年用 `GrowXyz` | 非均匀/槽位组合会继续手写 | 结构差异 |

### 3.4 测试与验收

| # | 差异 | 当前状态 | 风险 | 状态 |
| --- | --- | --- | --- | --- |
| T1 | 原版真值覆盖 | `ReferenceVerifier` 默认只覆盖 Elytra/Warden/Breeze/AdultRabbit/AdultFox；fixture 只覆盖测试选择的模型 | 其他模型仍可能“legacy 全绿但原版不等价” | 部分 |
| T2 | 树结构 Golden | 当前 `ReferenceModelFactsTests` 只比较 part-by-part 可对齐的 Warden/Fox/Rabbit；Breeze 因多 cube 只做事实提取 | 树/multi-cube 改动没有完整回归 | 待补 |
| T3 | 几何不变量 | `ModelUvInvariantTests` 有；Z-fighting/clearance 尚无 | 装备穿模/多 cube 共面问题未被门禁捕获 | 待补 |
| T4 | 旧迁移测试的 oracle | `LegacyModelPartFactory` 仍是 `BakeRoot/BakeChild` 的旧实现值；新 facts 测试只覆盖部分模型 | 旧测试不能单独作为原版等效证据 | 已识别 |

## 4. 明确不在本轮范围

以下不是“可以忽略”，而是**不能混入 mob 树重构**，需要单独设计：

- **方块实体模型**（ChestModel 的 visibleFaces、方块实体光照/动画）；
- **玩家披风/耳朵模型**（`PlayerCapeModel`、`PlayerEarsModel`，带 xTexScale 和非标准变形）；
- **手持/物品模型**（单独模型系统，不属于实体骨骼）；
- **完整 RenderLayer/RenderType 管线**（`entity-render-layers.md` 已记录）；
- **EquipmentClientInfo 的完整资产 JSON 解析**（已有装备文档的受控偏差）。

## 5. 避免返工的实施顺序

1. **先做运行时节点表**：`EntityModelPartNode`（cube range + child range + parent index）与 `EntityModelCube`；`VanillaMeshBakery` 从树编译。
2. **再补 DSL 值能力**：`VanillaPartPose` 三轴 scale、`VanillaCubeDefinition.GrowXyz`、`MeshTransformer`/`BabyModelTransform` 语义；不要先在 helper 里继续手算。
3. **迁移基础模型**：Breeze（多 cube）→ Warden（零组节点/多 layer）→ Fox/Rabbit（Golden 已有）→ 其余；删除 `BuildParts()` 的名字重排。
4. **迁移装备层**：人形护甲四槽树 → 马鞍/骆驼鞍 → 鹦鹉螺/快乐恶魂 → Elytra → collar/crackiness。
5. **最后清理**：`WithArmorSlot`/`WithEquipment`/`WithCopy`/`WithCrackiness` 的 `part with` 克隆、fake parent、`PoseSource` 字符串补丁。
6. **测试前置**：每个阶段先加 ReferenceVerifier facts 和几何断言，再改代码；没有原版真值的模型不允许标为“已对齐”。

## 6. 关键决策（先固定，避免再分叉）

- **模型定义层必须保留树**；不允许新的逐 part 扁平定义。
- **扁平化只在集中 bakery/renderer 边界发生**；不允许模型文件自行 `BuildParts()` 重排。
- **装备层也必须是树**：护甲是独立槽位树，鞍/壳/挽具是基础树派生树，Elytra 是独立树。
- **`EntityModelPart` 不再作为“一个 cube”的长期公共模型**；它要么升级为节点，要么拆成 `EntityModelPartNode` + `EntityModelCube`。
- **所有新模型先过 facts/Golden**；不能用 `LegacyModelPartFactory` 自证。

## 7. 待验证问题

1. 原版 `PartDefinition.children` 的 HashMap 遍历顺序是否影响渲染顺序；当前列表顺序是否只是“视觉等价”而非严格等价。
2. `PartPose` scale 在现有模型中的实际作用范围；是否所有 bind scale 都能由 `BabyModelTransform`/`scaling` 解释。
3. `ModelPart.visible` 子树隐藏在现有 mob 中的具体使用点；当前 `VisibleWhen` 是否有遗漏。
4. 装备层各 `RenderLayer` 的绘制顺序、深度和透明度语义；需与 `entity-render-layers.md` 对照。
5. `EquipmentClientInfo` 的完整资产字段与当前静态 catalog 的差距。
