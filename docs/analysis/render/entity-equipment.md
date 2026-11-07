<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 实体装备与物品渲染差距
summary: set_equipment 包、装备槽、物品栈编码、装备资产层与各实体装备层的实现边界
status: 已评审
updated: 2026-11-03
-->

# 实体装备与物品渲染差距

> 完整的模型/装备结构差距见 [`entity-model-parity-gap.md`](entity-model-parity-gap.md) §3.3；本文只保留装备资产、槽位与物品渲染的实现边界。

## 1. 目的与范围

本文记录“实体身上穿戴/手持的物品如何同步并渲染”的行为规格与当前差距。它覆盖鞍、马甲（horse armor）、羊驼装饰、骆驼鞍、鹦鹉螺鞍/甲、快乐恶魂挽具、狼甲，以及手持物品层；不覆盖方块物品、容器 UI、玩家皮肤。

当前仓库在 `src/Sharpcraft.Client` 与 `src/Sharpcraft.Common` 内 **没有任何** `SetEquipment`、`EntityEquipment`、`ItemStack` 或物品渲染实现（检索结果为零），因此所有相关层都还没有落地。

## 2. 包与槽位（行为事实）

`SetEquipment` 是 clientbound 包 id **104**（`docs/specs/network/entity-sync.md` 已登记，原为 M5 后续项）。

布局：

```
VarInt entityId
repeat:
  byte slotByte             // 低 7 位是 EquipmentSlot ordinal，最高位 0x80 表示后面还有条目
  ItemStack OPTIONAL_STREAM_CODEC
```

`EquipmentSlot` 的 ordinal 与 id：

| ordinal | 槽位 | 说明 |
| --- | --- | --- |
| 0 | mainhand | 主手 |
| 1 | offhand | 副手 |
| 2 | feet | 靴 |
| 3 | legs | 腿甲 |
| 4 | chest | 胸甲 |
| 5 | head | 头盔 |
| 6 | body | 动物身体装备（马甲、狼甲、鹦鹉螺甲等） |
| 7 | saddle | 鞍 |

`ItemStack.OPTIONAL_STREAM_CODEC`：

```
VarInt count                 // <= 0 表示空栈，后面没有字段
VarInt itemHolderId          // 物品注册表 holder id
DataComponentPatch patch
```

`DataComponentPatch` 是可变长度组件列表：先 VarInt“新增组件数”和 VarInt“移除组件数”，再按组件类型逐项读取值。组件值本身可以嵌套任意数据，**没有通用跳过长度**；要完整跳过必须实现组件类型表与各组件 codec。

### 2.1 分阶段解码策略（有意限制）

第一阶段只支持 **空 patch**（新增数 = 移除数 = 0），这是普通鞍、原版马甲、羊驼地毯等最常见形态。若遇到非空 patch：

- 该包标记为 `unsupported`，停止继续读取并保留上一条有效装备状态；
- 记录计数与诊断，不猜测长度、不破坏后续包边界；
- 在后续阶段再补组件表（自定义名称、染色、附魔等）。

这是刻意的行为降级，必须进入受控偏差清单；不能把“部分支持”写成已完成。

## 3. 物品身份与装备资产

### 3.1 物品注册表

仓库目前没有物品注册表，也没有从资产包读取 `Items` 注册顺序的能力。第一阶段用**受控的静态物品 id 表**覆盖需要的物品：`saddle`、`leather_horse_armor`、`iron_horse_armor`、`golden_horse_armor`、`diamond_horse_armor`、`copper_horse_armor`、`netherite_horse_armor`、各颜色地毯、`wolf_armor`、鹦鹉螺相关装备与快乐恶魂挽具。表必须由分析工作区的 `Items` 注册顺序生成并在文档中记来源；不能凭记忆。

**风险**：静态表会随基线版本升级漂移；应在后续阶段替换为由数据驱动的物品注册表，而不是长期手写。

### 3.2 装备资产（EquipmentClientInfo）

原版渲染层不是直接用固定路径绑定物品，而是通过客户端 `EquipmentClientInfo`（装备资产定义）把“物品 + 层类型”映射到纹理与模型。该资产定义的 `.json` 已确认位于分析工作区 `assets/minecraft/equipment/`（例如 `iron.json`、`leather.json`、`saddle.json`、`elytra.json`）。2026-11-07 已新增 `EquipmentClientInfo` 解析模型、`ResourceKind.Equipment` 与 `EquipmentAssetRegistry`；`EntitySkinLibrary.Load` 会从资源栈加载已知装备资产，`EquipmentTextureCatalog.Resolve` 优先使用解析出的层纹理，静态 catalog 作为 fallback；T-748d 起 `EntitySkinLibrary.Load` 还会遍历 `EquipmentTextureCatalog.Assets.TexturePaths`，把资源包装备层纹理路径一并载入 skin atlas（复用 `LoadTexture` 的按路径去重）。`equippable` 组件的 asset id 已从 `set_equipment` 解码并传入 render state/texture selector；`EquipmentTexture` 已携带 `UsePlayerTexture` 与 `ColourWhenUndyed` 元数据，`EquipmentClientInfo.ResolveTrim` 已支持 material/pattern trim predicate 选择；dye/glint/player-texture/trim 的最终着色仍未完成。

因此第一阶段采用**受控静态映射**：

- 马：`entity/equipment/horse_body/<material>`、`entity/equipment/horse_saddle/saddle`；
- 羊驼：`entity/equipment/llama_body/<color>`（trader 地毯单独）；
- 骆驼：`entity/equipment/camel_saddle/saddle`、`camel_husk_saddle/saddle`；
- 鹦鹉螺：`entity/equipment/nautilus_body/*`、`nautilus_saddle/*`（具体子目录需在实现前核对包里实际路径）；
- 快乐恶魂：`entity/equipment/happy_ghast_body/<color>_harness` 与 `happy_ghast_ropes`；
- 狼：`entity/wolf/wolf_armor` 及 `wolf_armor_crackiness_*`。

实现前必须补一份“路径与物品对应表”到本文件，并在 `docs/traceability.md` 关联到对应实体验收项。**2026-11-03 已完成 E2 受控表：`EquipmentTextureCatalog`（`Client/Render`）以 `ItemInfo.Id` 为 key，覆盖 6 种马甲材质、9 类鞍、16 色地毯/挽具、5 种鹦鹉螺甲、狼甲；leather 与 wolf 甲带 overlay 路径。**

## 4. 各实体装备层清单

| 实体 | 槽位/触发 | 原版层 | 第一阶段的纹理来源 | 状态 |
| --- | --- | --- | --- | --- |
| Horse / Donkey / Mule / undead horse | `body`、`saddle` | HorseRenderer 装备层 | `horse_body/*`、`horse_saddle/saddle` | 盔甲已验证；鞍专用几何待补 |
| Llama / TraderLlama | `body`（decor） | LlamaDecorLayer | `llama_body/*`，trader 地毯单独 | 纹理层已接入 |
| Camel | `saddle` | CamelRenderer 鞍层 | `camel_saddle/saddle` | 已验证（CamelSaddleModel） |
| Nautilus / ZombieNautilus | `body`、`saddle` | NautilusRenderer/NautilusArmorModel | `nautilus_*` | 已验证（NautilusArmor/Saddle） |
| Happy Ghast | `body`、挽具/绳索 | HappyGhastHarnessModel、RopesLayer | `happy_ghast_body/*`、`happy_ghast_ropes` | 已验证（专用 harness/goggles 几何） |
| Wolf | `body` | WolfArmorLayer + crackiness | `wolf_armor`、`wolf_armor_crackiness_*` | 已验证（修复 64×32） |
| Witch | `mainhand` | WitchItemLayer / CrossedArmsItemLayer | 需要物品模型（药水） | 未开始（后置） |

Witch 的手持物品依赖完整物品模型渲染，不属于第一阶段；装备/鞍系列不需要物品模型，只需物品身份与装备槽。

## 5. 与现有模型渲染的关系

- 现有 `EntityModelDefinition` 已支持独立纹理的部件副本（`EntityModelLayerHelpers.WithCopy`）、颜色 tint 与可见性；装备层可复用这套机制生成“整模型副本 + 装备纹理”。
- 装备层必须在同一实体变换与动画下绘制；当前所有实体仍是 bind pose，装备层也应保持同一静态姿势，不提前引入动画。
- 装备层纹理通常带透明像素，应使用带 alpha 的 cutout/translucent 层，并轻微外扩（当前约定 +0.05 模型像素）避免与本体同面。
- 装备槽状态通过 `EntityRenderState` 扩展字段传入，渲染器不得读取 `Entity` / 网络对象。

## 6. 未解决问题与风险

1. **物品注册表缺失**：静态物品 id 表存在漂移风险。
2. **DataComponentPatch 非空**：第一阶段不支持，遇到即降级。
3. **装备资产 JSON 未在资产包中找到**：需要确认 `EquipmentClientInfo` 的定义来源（是否应从 jar 的 data/assets 另提取），否则静态映射无法长期维护。
4. **多装备层叠**：同一实体 simultaneous body+saddle、horse armor+saddle、狼甲裂纹等需要明确绘制顺序与冲突规则。
5. **染色/自定义名称**：属于组件系统，第一阶段不渲染。\n6. **Happy Ghast harness**：已确认 `HappyGhastHarnessModel` 只包含 `harness` + `goggles` 两个专属部件（不复制基础 body）；已改为只追加这两个部件、保留基础 body 原纹理，纹理按模型声明 64×64（PNG 128×128）归一化，沙盒目视通过。

## 7. 参考资料

- `docs/specs/network/entity-sync.md`（包 id、SetEquipment 登记）
- `docs/analysis/render/entity-special-layers.md`（特殊层总表）
- `docs/modules/entity-equipment.md`（设计文档）








