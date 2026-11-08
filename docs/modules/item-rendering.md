<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[已评审]
基线：Minecraft 26.3
证据：`ref://mc-26.3-analysis/src/net/minecraft/client/renderer/item/ItemModelResolver.java`、`.../item/ItemModels.java`、`.../item/CuboidItemModelWrapper.java`、`.../client/renderer/entity/layers/ItemInHandLayer.java`
最后更新：2026-11-08

# 物品模型与手持渲染

## 1. 职责范围

负责：

- 从 item id / `item_model` 组件解析 item model 定义；
- 解析 `models/item/*` 的父链、纹理槽与 `display` 变换；
- 为实体手持层（`ItemInHandLayer` / `WitchItemLayer`）提供 item model 选择与 display context 变换；
- 后续扩展为 GUI/掉落物/展示框共用同一套 item model 解析。

不负责：

- 完整物品栏 UI、容器逻辑；
- 方块物品的 block model 重导出（复用现有 block model resolver）；
- 附魔光效/特殊渲染器（特殊模型单独注册）。

## 2. 原版 26.3 源码映射表

| 本模块概念 / 方法 | 原版类与方法路径（`ref://...`） | 精确常量 / 公式 / 结构 | 位序 / 正负号 / 色彩空间约定 | 缺省 / 越界回退值 |
| --- | --- | --- | --- | --- |
| item model 选择 | `ItemModelResolver.appendItemLayers` | `ItemStack.get(DataComponents.ITEM_MODEL)`；非空时调用对应 `ItemModel.update` | item model id 为命名空间标识符 | `item_model` 缺失时由 vanilla 物品默认 model 兜底 |
| item model 类型表 | `ItemModels.bootstrap` | `empty`、`model`、`range_dispatch`、`special`、`composite`、`bundle/selected_item`、`select`、`condition` | `model` 类型包含 `model` id、可选 `transformation`、`tints` | 未知/缺失类型 → `MissingItemModel` |
| 模型父链 | `CuboidItemModelWrapper.Unbaked.bake` | `model` → `ModelBaker.getModel` → top texture slots / geometry / `ModelRenderProperties` | parent 递归；`builtin/generated` 为内置父 | 缺纹理槽 → missing sprite |
| display context | `ItemDisplayContext` | `none, thirdperson_lefthand, thirdperson_righthand, firstperson_lefthand, firstperson_righthand, head, gui, ground, fixed, on_shelf` | `getId()` 从 0 连续；`leftHand()` / `firstPerson()` 为布尔派生 | 未知 context → `NO_TRANSFORM` |
| display 变换 | `ItemTransform.apply` | rotation 度 → 弧度；translation JSON 单位 × `0.0625`；translation clamp ±5；scale clamp ±4；最后 translate `(-0.5,-0.5,-0.5)` | 左手修正：X 平移、Y/Z 旋转取反 | 空 `display` → `NO_TRANSFORM`；`thirdperson_lefthand`/`firstperson_lefthand` 缺失时回退右手 |
| 手持父层 | `ItemInHandLayer.submitArmWithItem` | 手骨定位 → X `-90°`、Y `180°`；baby offset `(0,1,-4.5)`，adult `(1,2,-10)` /16；STAB 时 `SpearAnimations.thirdPersonAttackItem` | 左手臂 X 平移取反 | 空 item stack 不绘制 |
| Witch 覆盖 | `WitchItemLayer.applyTranslation` | `isHoldingPotion`：root/head/nose 定位后 translate `(0.0625,0.25,0)`，Z 180°、X 140°、Z 10°、X 180° | — | 非药水走 `CrossedArmsItemLayer` |
| 模型纹理 | `Layer0` 等 texture slot | `layer0`/`layer1` 为 item atlas 精灵路径；potion 有 `potion_overlay` + `potion` | item atlas，不在 block atlas | 缺 layer → missing sprite |

## 3. 解析顺序与数据结构

### 3.1 item id → model id

1. 优先读取 `ItemComponentInfo.ItemModelId`（`item_model` 组件，字符串资源 id）。
2. 缺失时读取 `assets/<namespace>/items/<item-path>.json` 的 `model` 对象；`type = minecraft:model` 时取 `model` 字段。
3. `type = minecraft:empty` → 不绘制；未知 type → missing model。
4. **special 描述符**：`type = minecraft:special` 时保留 `base` 模型与 `model.type`（SpecialModelRenderer 类型），但 T-631d 之前不生成 special geometry；该模型不进入 flat-quad held-item 路径。
5. 以模型 id 读取 `assets/<namespace>/models/<path>.json`，沿 `parent` 递归合并：
   - `textures`：子层覆盖父层，保留 `layer0..layerN`；
   - `display`：子层 context 覆盖父层，缺失 context 继承父层；
   - `elements`：子层声明则**整体替换**父层，不合并；
   - `gui_light`、`overrides` 等保留给后续 mesh 阶段。

### 3.2 display context

- 仅 `ItemDisplayContext` 的 10 个枚举值可查询；`NONE` / 未知返回 `NO_TRANSFORM`。
- 左手上下文缺失时回退同视角右手；这一点在 `ItemTransforms.Deserializer` 中完成，必须保留。
- `ItemTransform` 的 translation 在解析时乘以 `0.0625`，并 clamp 到 ±5；scale clamp 到 ±4。

### 3.3 手持层

- 实体手持层先选 item model（`ItemModelResolver`），再取 `display` 中对应 context 的 transform。
- 第三/第一人称 context 由渲染器传入；Witch 药水优先走 `WitchItemLayer` 的头/鼻锚点，其他物品走 `CrossedArmsItemLayer`。
- `SPEAR`/`STAB` 在 `ItemInHandLayer` 的 STAB 分支使用 `SpearAnimations.thirdPersonAttackItem`；T-631a 只记录，不在本刀实现。

### 3.4 模型包围盒（extents）与空间约定

原版在 `ItemStackRenderState.visitExtents` 里先 `layer.applyTransform(pose)` 再收集角点，因此 `getModelBoundingBox()` 得到的是**应用 display 变换之后**的盒子（含 `-0.5` 居中与 scale）。掉落物的 `minOffsetY = -bbox.minY + 0.0625` 与 `modelDepth > 0.0625` 都以此为准。

本仓对应实现：`ItemModelExtents.FromResolution`（元素模型）与 `FromModel`（special 模型）产出模型空间盒子，`Transformed(matrix)` 变换 8 角后重取 AABB，`ForDisplay(resolution, context)` 套用该 context 的 `ItemTransform.ToMatrix()`。

**两类模型处于不同的"作者空间"，这不是缺陷，原版同样如此**：

| 模型族 | 作者空间 | 例子 | 证据 |
| --- | --- | --- | --- |
| 方块 / 元素模型（`elements`） | Y 向上，0–16 | `block/cube` 的 `from [0,0,0] to [16,16,16]` | `FaceBakery.defaultFaceUV` 直接用元素坐标 |
| 实体型 special 模型 | Y 向下（原点以上为负） | `SkullModel.createHeadModel` 的 `addBox(-4,-8,-4, 8,8,8)` | `ModelPart.getExtentsForGui` **不施加** `scale(-1,-1,1)`，直接输出作者坐标 |

因此 `FromModel` 会把烘焙存储的 Y 向上坐标取反回作者空间，`FromResolution` 保持 Y 向上；同一个 item model 的所有层只来自同一族，不会混用。

### 3.5 元素模型解析

- `elements` 由**子模型整体替换**父链贡献，不做合并。
- 面纹理 `#variable` 的解析 MUST 在整条父链合并完成后进行（原版同样后处理），否则子模型引用父层纹理槽会解析失败。
- `uv` 缺失时按 `FaceBakery.defaultFaceUV` 的六方向投影自动生成；`rotation`、`tintindex`、`cullface` 原样保留。

## 4. 真值向量（Golden Vectors）

采样来源：`assets/minecraft/models/item/*.json`、`assets/minecraft/items/*.json`、`ItemDisplayContext`、`ItemTransforms` / `ItemTransform`。测试落点：`tests/Sharpcraft.Client.Tests/Render/ItemModelResolverTests.cs`（T-631a）。

1. **`minecraft:iron_sword`**：`items/iron_sword.json` → `minecraft:item/iron_sword`；模型 parent `minecraft:item/handheld`，`textures.layer0 = minecraft:item/iron_sword`；`thirdperson_righthand` rotation `[0,-90,55]`、translation `[0,4.0,0.5]`、scale `[0.85,0.85,0.85]`。
2. **`minecraft:potion`**：`items/potion.json` → `minecraft:item/potion`；模型 parent `minecraft:item/generated`，`textures.layer0 = minecraft:item/potion_overlay`、`layer1 = minecraft:item/potion`；默认 `firstperson_righthand` rotation `[0,-90,25]`、translation `[1.13,3.2,1.13]`、scale `[0.68,0.68,0.68]`（来自 `item/generated`）。
3. **`minecraft:bow`**：`items/bow.json` → `minecraft:item/bow`；模型 parent `item/generated`，`textures.layer0 = item/bow`；`thirdperson_righthand` rotation `[-80,260,-40]`、translation `[-1,-2,2.5]`、scale `[0.9,0.9,0.9]`；`thirdperson_lefthand` rotation `[-80,-280,40]`。
4. **`minecraft:torch`**：`items/torch.json` → `minecraft:item/torch`；模型 parent `minecraft:item/generated`，`textures.layer0 = minecraft:block/torch`；默认 `ground` translation `[0,2,0]`、scale `[0.5,0.5,0.5]`（来自 `item/generated`）。
5. **`minecraft:apple`**：`items/apple.json` → `minecraft:item/apple`；模型 parent `minecraft:item/generated`，`textures.layer0 = minecraft:item/apple`；默认 `head` rotation `[0,180,0]`、translation `[0,13,7]`、scale `[1,1,1]`。
6. **左手回退**：模型只声明 `thirdperson_righthand` 时，`thirdperson_lefthand` 使用右手 transform；`firstperson_lefthand` 同理。
7. **单位**：JSON translation `[0,4.0,0.5]` 经过 `×0.0625` 后为 `[0,0.25,0.03125]`；rotation 为角度，应用时转弧度。

## 5. 实现与原版类对应

| 本仓实现 | 原版对应 | 状态 |
| --- | --- | --- |
| `ItemModelResolver`（T-631a） | `net.minecraft.client.renderer.item.ItemModelResolver` | 数据解析已实现；分支求值与渲染状态简化（见 §7.1 SIM-ITEM-MODEL-STATIC-FALLBACK / SIM-ITEM-RENDER-STATE） |
| `ItemModelResolution`（T-631a） | `ItemModel` / `CuboidItemModelWrapper` / `ItemStackRenderState` | 只有描述符，无层/foil/tint |
| `ItemDisplayTransform`（T-631a） | `ItemTransform` / `ItemTransforms` | 已解析但无消费者（SIM-ITEM-DROP-DISPLAY-GUI） |
| `ItemDisplayContext`（T-631a） | `net.minecraft.world.item.ItemDisplayContext` | 枚举与顺序一致 |
| `ItemAtlas`（T-631c） | `net.minecraft.client.renderer.texture.ItemTextureAtlas` | 数据层已实现，视觉验收待补（SIM-ITEM-ATLAS-VALIDATION） |
| `HeldItemModelDefinition`（T-631c/d） | `ItemInHandLayer` | 近似锚点（SIM-ITEM-HELD-ANCHOR） |
| `WitchModelDefinition.HeldPotionDefinition`（T-631b） | `WitchItemLayer` | 已实现 flat quad |
| `SpecialItemModelDefinition`（T-631d） | `SpecialModelRenderers` 的 trident/shield/head/player_head/chest/shulker_box/banner/decorated_pot/conduit/end_cube | 10/13 种（SIM-ITEM-SPECIAL-MODELS / SIM-ITEM-SPECIAL-STATE） |
| `ItemComponentInfo.ItemModelId` | `DataComponents.ITEM_MODEL` | 已实现 |
| — | `ItemEntityRenderer` / `ItemFrameRenderer` / GUI item 渲染 | 未实现（SIM-ITEM-DROP-DISPLAY-GUI） |

## 6. 分阶段

- **T-631a**：item id → model id 解析、parent/texture/display 合并、10 个 display context、5 组 Golden Vectors；不生成 mesh。**已完成**（`965ab53`）。
- **T-631b**：`item/generated` / `item/handheld` 的最小 quad mesh 与 item atlas；Witch 药水验收。**进行中**：Witch 药水已用 flat quad（`EntityModelMesher.AddFlatQuad`，`item/potion` 进入实体 atlas）；独立 item atlas/通用 item model 纹理层仍待补。
- **T-631c**：其他 item model 类型（select/condition/composite/special）与掉落物/展示框/GUI 复用。**进行中**：`ItemAtlas` 数据层、WindowedRenderer 独立 item bind group、`EntityGeometryLayer.UsesItemAtlas`、Witch layer 与成人/幼年 humanoid `held_item_right`/`held_item_left` 层已完成；`ItemModelResolver` 已支持 `select`/`condition`/`range_dispatch`/`composite` 的静态 fallback 模型，`layer0/layer1` 已由 WindowCommand 按可见实体按需装入 item atlas。`special` 模型、动态 property 与 drop/display/GUI 复用仍待补。
- **T-631d**：`minecraft:special` 描述符解析已接（base 模型 + `model.type` + 字符串属性），不把 special 物品误当 flat quad；当前已移植 trident/shield、head/player_head skull、chest、shulker_box、banner、decorated_pot、conduit 与 end_cube 的 special 模型与成人主/副手 held 层，bell/book/copper_golem_statue 仍待实现。

## 7. 风险与限制

### 7.1 已知刻意的简化（必须后续解决）

对照对象为 26.3 原版（`ref://mc-26.3-analysis/src/...`）。逐项登记：原版行为 → 当前实现 → 差异 → 状态。

| ID | 简化项 | 原版对照（`ref://mc-26.3-analysis/`） | 原版行为 | 当前实现 | 差异 / 等价性缺口 | 状态 | 计划 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| SIM-ITEM-MODEL-STATIC-FALLBACK | item model 分支求值 | `src/net/minecraft/client/renderer/item/ItemModelResolver.java`、`.../item/` 下 `select`/`condition`/`range_dispatch` 各 `ItemModel` 实现 | 每个 item model 按当前 `ItemStack`/组件/实体/关卡状态**求值**分支，光这条路径就分 `list`/`select`/`condition`/`range_dispatch`/`composite`/`special` 多级 | `ItemModelResolver.ResolveElement` 只做**静态**分支：优先 `fallback`、否则首个 `cases`/`entries` 子模型 | `minecraft:using_item`、`use_duration`、`charge_type`、`crossbow/pull`、`local_time`（`MM-dd`）、`has_component`、`display_context`、`block_state` 等 property 全部未求值；弓/弩/时钟/指南针/蜂巢等持有状态永远渲染默认姿态 | 规格已就绪 | `T-631e` |
| SIM-ITEM-RENDER-STATE | item 渲染状态 | `src/net/minecraft/client/renderer/item/ItemStackRenderState.java` | 持有 `displayContext`、`LayerRenderState[]`（每层含 quads / itemTransform / foilType / tintLayers / specialRenderer / extents / usesBlockLight / particleMaterial）、`animated`、`oversizedInGui`、模型包围盒 | 仅有 `ItemModelResolution(ModelId, Textures, Display, SpecialRendererType, SpecialProperties)` 描述符 | 没有层列表、`FoilType`（NONE/STANDARD/SPECIAL）、染色层、粒子材质、`visitExtents`/`getModelBoundingBox`、`oversizedInGui`；无法表达双层物品（如 potion 的 `layer0` + `potion_overlay`）与 glint | 规格已就绪 | `T-631e` |
| SIM-ITEM-HELD-ANCHOR | 手持物品锚点 | `src/net/minecraft/client/renderer/entity/layers/ItemInHandLayer.java`、`.../model/effects/SpearAnimations.java` | `translateToHand(state, arm)` → `rotateDegrees(XP, -90)` → `rotateDegrees(YP, 180)` → 平移 `(±1, 2, -10)/16`（成人）或 `(0, 1, -4.5)/16`（baby 且非 armor stand）；STAB 挥击再叠加 `thirdPersonAttackItem`（绕 `(0,-0.125,0.125)` 沿 −X 转 `70°·(attack−retract)`） | `HeldItemModelDefinition` 在 `right_arm`/`left_arm` 枢轴下挂 `held_item` 零深度 quad，成人偏移 `(0, -10, -4)`、幼年 `(0, -5, -3)`，**无 X/Y 旋转**；`SpearAnimations` 已移植（`progress`/`inQuad`/`inOutExpo`/`thirdPersonAttackItem` 逐式对应）并按 STAB 施加到 `held_item` 姿态 | 缺少 `translateToHand` 的逐模型手部锚点与 90°/−180° 旋转；偏移轴向与量级与原版不一一对应；`useBabyOffset` 的 armor stand 例外未处理；STAB 目前只作用于扁平 `held_item` 模型（special 模型部件名不同，尚未覆盖），转向符号待视觉验收 | 规格已就绪 | `T-631e` |
| SIM-ITEM-SPECIAL-MODELS | special 模型覆盖 | `src/net/minecraft/client/renderer/special/SpecialModelRenderers.java`（注册 13 种：bell/banner/book/conduit/chest/copper_golem_statue/head/player_head/shulker_box/shield/trident/decorated_pot/end_cube） | 13 种 `SpecialModelRenderer` 各自烘焙模型并提交几何 | 已移植 10 种：trident/shield/head/player_head/chest/shulker_box/banner（本体+旗面）/decorated_pot（base+sides）/conduit/end_cube | 缺 `bell`/`book`/`copper_golem_statue` 三种；`banner` 未做 `attachment`（ground/wall）与摆动相位，`decorated_pot` 未读 `POT_DECORATIONS` 选 side 图案，`end_cube` 用 `entity/end_portal/end_portal` 贴图近似原版 `endPortal`/`endGateway` shader | 规格已就绪 | `T-631e` |
| SIM-ITEM-SPECIAL-STATE | special 参数与组合 | `.../special/ChestSpecialRenderer.java`、`.../special/ShulkerBoxSpecialRenderer.java`、`.../special/ShieldSpecialRenderer.java`、`.../blockentity/BannerRenderer.java` | `ChestSpecialRenderer.Unbaked(texture, openness, chestType)` 按 `chest_type` 烘焙单体/左/右模型并 `setupAnim(openness)`；`ShulkerBoxSpecialRenderer` 用 `openness` 抬升 lid；`ShieldSpecialRenderer` 依 `BannerPatternLayers`+`BASE_COLOR` 选基准 sprite，`BannerRenderer.submitPatterns` 逐层以 `bannerPattern` 渲染类型 + dye 色再提交同一模型 | `banner_patterns`/`base_color` 已解码并透传到 `EntityRenderState`；盾牌按 `hasPatterns` 选 sprite；**图案叠加层已实现**：banner 旗面/shield 各烘焙 6 份共面副本，逐层用 `entity/banner/<asset>`、`entity/shield/<asset>` sprite + `DyeColors` tint，按层索引 `VisibleWhen` 裁剪；`GreaterEqual` 深度模式已验证支持共面 | **已核实（仅限物品形态）**：26.3 全部 `assets/minecraft/items/*.json` 中 `openness` / `attachment` / `chest_type` 出现次数均为 **0**（对 `items/*.json` 逐文件 grep），因此**物品 special 渲染器**恒取缺省 `0.0F` / `ground` / `single`，当前静态烘焙对物品形态等效。**世界内的箱子/潜影盒是另一条路径**：`ChestRenderer` 用 `ChestBlock.opennessCombiner(blockEntity).get(partialTicks)`、`ShulkerBoxRenderer` 用 `blockEntity.getProgress(partialTicks)` 在运行时取开合度，本仓方块实体目前只有 `BlockEntityPlaceholderMesher` 占位盒（T-241），**没有任何真实箱子/潜影盒几何与开盖动画**，属于独立的方块实体建模任务（T-759）而非 `openness` 细节；届时还需为网格 DSL 补 `visibleFaces` 逐面隐藏能力（原版双箱 `allOfEnumExcept(EAST/WEST)`、饰纹陶罐侧面 `EnumSet.of(NORTH)` 依赖它，见 T-760） | 规格已就绪 | `T-631e` |
| SIM-ITEM-ATLAS-VALIDATION | item atlas 验收 | `src/net/minecraft/client/renderer/texture/ItemTextureAtlas.java`（原版 item 纹理走独立 atlas 与 sprite 系统） | 原版 item sprite 由 atlas 打包 + sprite 元数据决定 UV | 自研 `ItemAtlas` 网格打包，按可见实体主/副手纹理**按需增量**加入 | 无真实资源包下的 UV/尺寸/missing sprite 回归；`WithTexture` 每次重建整张图，未做与原版 atlas 的逐 sprite 对照 | 规格已就绪 | `T-631e` |
| SIM-ITEM-DROP-DISPLAY-GUI | 掉落物 / 展示框 / GUI | `.../item/ItemDisplayContext.java`（10 个上下文）、`.../entity/item/ItemEntityRenderer.java`、`.../entity/ItemFrameRenderer.java` | 同一套 item model 在 10 个 display context 下复用，含掉落物旋转、展示框、GUI 图标 | 只有实体手持路径；`ItemTransform` 已解析但**未被任何渲染路径消费** | 掉落实体、展示框、GUI 内物品均无渲染；`ItemDisplayContext` 与 `ItemTransform` 只有数据、无消费者 | 规格已就绪 | `T-631e` |

<!-- simplification:SIM-ITEM-MODEL-STATIC-FALLBACK task:T-631e -->
<!-- simplification:SIM-ITEM-RENDER-STATE task:T-631e -->
<!-- simplification:SIM-ITEM-HELD-ANCHOR task:T-631e -->
<!-- simplification:SIM-ITEM-SPECIAL-MODELS task:T-631e -->
<!-- simplification:SIM-ITEM-SPECIAL-STATE task:T-631e -->
<!-- simplification:SIM-ITEM-ATLAS-VALIDATION task:T-631e -->
<!-- simplification:SIM-ITEM-DROP-DISPLAY-GUI task:T-631e -->

这些简化不得以“已完成”收口；在视觉/真值验收前对应进度保持 `进行中`。解决后从代码移除 `[Simplification]` 标记，但本节的记录与上面的机器标记 MUST 保留。

### 7.2 其他限制

- 26.3 的 `items/*.json` 与旧版 `models/item` 结构不同；必须按 26.3 的 item model 类型分发，不能只读旧 parent 字段。
- `ItemStackRenderState` 的层/foil/tint 结构未在仓库实现；T-631a 只建立解析数据，不提前造渲染状态。
- 物品 atlas 与 block atlas 分离；不能复用 block atlas 的 sprite 假设。
- 完整物品模型、特殊模型（弓、盾、三叉戟、地图等）不在 T-631a 范围。
