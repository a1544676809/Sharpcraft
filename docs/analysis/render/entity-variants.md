<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 实体变体（variant）——数据来源、索引与纹理/模型选择
summary: 26.3 变体的三条来源、实体数据访问器索引表、注册表驱动解析、几何变体差异与首批范围
status: 分析已定稿（依据 26.3 源码）
updated: 2026-11-03
-->

# 实体变体（variant）

> 结论都可由 `ref://mc-26.3-analysis/src/...` 指到的源码复核。本文只写**行为**，不粘贴实现。

## 1. 变体不是一种机制，而是三种

26.3 里“外观变体”由三套互不相同的机制表达：

| 机制 | 代表 | 值类型 | 外观由什么决定 |
| --- | --- | --- | --- |
| 注册表变体（holder registry） | Pig / Cow / Chicken / Cat / Wolf / Frog | VarInt：**注册表内位置 id** | `ModelAndTexture`（模型类型 + 资源 id）；Cat 只有 adult/baby 资源 id |
| 整型枚举 | Horse、Llama、Fox | VarInt：枚举 id | 渲染器内的常量表（纹理，Fox 还按睡眠状态分） |
| 组件（component） | 部分实体也接受 `*_VARIANT` 组件 | 仅供刷怪蛋/命令 | 与运行时同步无关 |

要点：**注册表变体的值不是固定枚举**，是“服务器在本连接内发送的注册表里的下标”。下标按连接而定，
不能写死；名字（注册表条目的 `ResourceKey`）才是稳定身份。

## 2. 客户端如何拿到变体值

- 变体是同步实体数据（`SynchedEntityData`）的一个字段，走 `set_entity_data`（play clientbound **101**）。
  包体：`VarInt entityId`，随后逐项 `u8 index` + `VarInt serializerId` + 值，`0xFF` 结束。
- 生成包（`add_entity`，1）**不携带**实体数据；所以新实体先以默认变体出现，`set_entity_data` 到达后才切换。
- 变体字段**只在非默认时发送**：默认变体（注册表默认项 / 枚举 0）不会出现在包里，客户端必须回落到默认。

### 2.1 访问器索引（跨继承链累计）

`SynchedEntityData.defineId` 的 id 由 `ClassTreeIdRegistry` 按“父类先、子类后”累计，所以索引是整个继承链的
累计序号，不是单类内部序号（`ref://mc-26.3-analysis/src/net/minecraft/util/ClassTreeIdRegistry.java`）。
本基线公共前缀为：`Entity` 8 项（0–7）、`LivingEntity` 7 项（8–14）、`Mob` 1 项（15）、
`AgeableMob` 2 项（16–17）。

| 实体 | 变体访问器索引 | 类型（serializer） | 说明 |
| --- | --- | --- | --- |
| Pig | 19 | PIG_VARIANT（holder） | 18 是 boost time |
| Cow | 18 | COW_VARIANT（holder） | |
| Chicken | 18 | CHICKEN_VARIANT（holder） | |
| Cat | 20 | CAT_VARIANT（holder） | 18/19 来自 TamableAnimal |
| Wolf | 23 | WOLF_VARIANT（holder） | 需要 wild/tame/angry 三态，暂缓 |
| Horse | 19 | INT | 低 8 位是 variant，高位是 markings |
| Llama | 20 | INT | 0 creamy / 1 white / 2 brown / 3 gray |
| Fox | 18 | INT | 0 red / 1 snow；纹理还按睡眠状态分，暂缓 |

## 3. 注册表变体：值 → 名字 → 资源

`registry_data`（configuration clientbound **7**）把整张变体注册表发下来，条目带 NBT 载荷
（`NETWORK_CODEC`，不含 `spawn_conditions`）：

| 注册表 | 条目 NBT 键 | 语义 |
| --- | --- | --- |
| `minecraft:pig_variant` / `cow_variant` / `chicken_variant` | `model`（可选，默认 `normal`）、`asset_id`、`baby_asset_id` | 模型类型 + 成年/幼年纹理 |
| `minecraft:cat_variant` | `asset_id`、`baby_asset_id` | 只有纹理 |
| `minecraft:wolf_variant` | `assets{wild,tame,angry}`、`baby_assets{...}` | 三态纹理 |

`asset_id` 是**标识符**（如 `minecraft:entity/pig/pig_temperate`），纹理实际路径是
`textures/<asset_id 的 path>.png`。所以注册表本身就是“变体 → 纹理”的权威来源，客户端不另存数据文件。

### 3.1 模型类型（几何）随变体变化

`ModelAndTexture.model` 决定用哪份 `LayerDefinition`，因此**变体不只是换纹理**：

| 实体 | normal | warm | cold |
| --- | --- | --- | --- |
| Pig | `PigModel` | 用 normal 几何 | `ColdPigModel`：body 换成 10×16×8 + 0.5 膨胀外层（UV 28,8 / 28,32） |
| Cow | `CowModel` | `WarmCowModel`：头多 4 个方块（两侧耳/角，含 mirror） | `ColdCowModel`：body 三盒（含 0.5 膨胀外层 20,32），头多两根旋转过的角 |
| Chicken | `AdultChickenModel` | 用 normal 几何 | `ColdChickenModel`：body 多一片 0×3×5（38,9），head 多一层 6×3×4（44,0） |

这些变体几何都只用到平移/单轴旋转，**不含父子嵌套旋转**，因此可在现有扁平部件模型里精确表达。

> **旋转符号约定（冷牛角曾经踩过）**：原版模型是 y-down，本仓库把 X、Y 取反（等价于绕 Z 转 180°）烘焙进部件坐标，
> 因此单轴旋转要按 `rotationX = -原版 xRot`、`rotationY = -原版 yRot`、`rotationZ = +原版 zRot` 换算。
> `ColdCowModel` 的两根角是 `xRot = +90°`，漏掉取反会让它们在 Z 方向偏 3 px（陷进头里而不是从头顶前方伸出）。
> 回归用例 `TheColdCowHornsSitAtTheVanillaPositionAfterTheRotationSignConversion` 用独立算出的包围盒钉住这一点。

## 4. 整型枚举变体：常量表

- Horse：0 white / 1 creamy / 2 chestnut / 3 brown / 4 black / 5 gray / 6 dark_brown，纹理
  `entity/horse/horse_<name>`；默认 0（white）。
- Llama：0 creamy / 1 white / 2 brown / 3 gray，纹理 `entity/llama/llama_<name>`；默认 0（creamy）。
- Fox：0 red / 1 snow，且按睡眠状态再分四张；睡眠位来自 flags，本批不接。

## 5. 未实现/限制（必须在文档里显式登记）

1. **幼年模型**：本仓库当前只渲染成年模型；变体的 `baby_asset_id` 与 Baby* 模型不接。
2. **Wolf / Fox**：分别需要 tame/angry 与 sleeping 状态，本批不做。
3. **自定义数据包新增变体**：若服务器在注册表里给出我们内置表里没有的名字（数据包/模组新增），
   纹理无法从内置资源集取到；按“接近原版 + 显式报错”处理 —— 渲染回落到该实体的默认变体，
   同时记录计数并在诊断日志里以异常形式抛出（不静默）。见 §6。
4. **马 markings**：`HorseMarkingLayer` 是独立叠加层，属于已知渲染层缺口，与变体正交。

## 6. 未知变体的处理契约（用户裁决 2026-11-03）

“跟原版差不多，但也抛出异常”：

- 渲染路径**不崩溃**：解析不到内置定义时回落到该类型的默认变体（等价于原版显示不出来的降级表现），
  并累加 `UnknownVariants` 计数；诊断开启时打印一条带类型/名字/值的错误行。
- 严格路径（工具/测试/离线分析）调用 `ResolveVariantStrict` 时**抛出** `KeyNotFoundException`，
  让缺口在自动化里立刻可见，而不是被静默吞掉。

## 7. 首批范围（用户 2026-11-03 确认）

- 基础设施：`set_entity_data` 解码（含跳过未知 serializer）、`registry_data` 变体表、`EntityRenderState.VariantId`。
- 纹理变体：Cat（11）、Horse（7）、Llama（4）。
- 冷暖变体（纹理 + 几何）：Pig（3）、Cow（3）、Chicken（3）。
