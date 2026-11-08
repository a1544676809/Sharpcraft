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
4. 以模型 id 读取 `assets/<namespace>/models/<path>.json`，沿 `parent` 递归合并：
   - `textures`：子层覆盖父层，保留 `layer0..layerN`；
   - `display`：子层 context 覆盖父层，缺失 context 继承父层；
   - `gui_light`、`elements`、`overrides` 等保留给后续 mesh 阶段。

### 3.2 display context

- 仅 `ItemDisplayContext` 的 10 个枚举值可查询；`NONE` / 未知返回 `NO_TRANSFORM`。
- 左手上下文缺失时回退同视角右手；这一点在 `ItemTransforms.Deserializer` 中完成，必须保留。
- `ItemTransform` 的 translation 在解析时乘以 `0.0625`，并 clamp 到 ±5；scale clamp 到 ±4。

### 3.3 手持层

- 实体手持层先选 item model（`ItemModelResolver`），再取 `display` 中对应 context 的 transform。
- 第三/第一人称 context 由渲染器传入；Witch 药水优先走 `WitchItemLayer` 的头/鼻锚点，其他物品走 `CrossedArmsItemLayer`。
- `SPEAR`/`STAB` 在 `ItemInHandLayer` 的 STAB 分支使用 `SpearAnimations.thirdPersonAttackItem`；T-631a 只记录，不在本刀实现。

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
| `ItemModelResolver`（T-631a） | `net.minecraft.client.renderer.item.ItemModelResolver` | 已实现数据解析 |
| `ItemModelResolution`（T-631a） | `ItemModel` / `CuboidItemModelWrapper` | 已实现 parent/texture/display |
| `ItemDisplayTransform`（T-631a） | `ItemTransform` / `ItemTransforms` | 已实现 |
| `ItemAtlas`（T-631c） | item texture atlas（独立于 entity atlas） | 数据层已实现 |
| `ItemInHandLayer` 的 hand anchor | `ItemInHandLayer` / `WitchItemLayer` | 待接 |
| `ItemComponentInfo.ItemModelId` | `DataComponents.ITEM_MODEL` | 已实现 |

## 6. 分阶段

- **T-631a**：item id → model id 解析、parent/texture/display 合并、10 个 display context、5 组 Golden Vectors；不生成 mesh。**已完成**（`965ab53`）。
- **T-631b**：`item/generated` / `item/handheld` 的最小 quad mesh 与 item atlas；Witch 药水验收。**进行中**：Witch 药水已用 flat quad（`EntityModelMesher.AddFlatQuad`，`item/potion` 进入实体 atlas）；独立 item atlas/通用 item model 纹理层仍待补。
- **T-631c**：其他 item model 类型（select/condition/composite/special）与掉落物/展示框/GUI 复用。**进行中**：`ItemAtlas` 数据层、WindowedRenderer 独立 item bind group、`EntityGeometryLayer.UsesItemAtlas` 与 Witch layer 接线已完成；其他 item model 类型与 drop/display/GUI 复用仍待补。

## 7. 风险与限制

- 26.3 的 `items/*.json` 与旧版 `models/item` 结构不同；必须按 26.3 的 item model 类型分发，不能只读旧 parent 字段。
- `ItemStackRenderState` 的层/foil/tint 结构未在仓库实现；T-631a 只建立解析数据，不提前造渲染状态。
- 物品 atlas 与 block atlas 分离；不能复用 block atlas 的 sprite 假设。
- 完整物品模型、特殊模型（弓、盾、三叉戟、地图等）不在 T-631a 范围。
