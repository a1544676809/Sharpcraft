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

### 3.5.2 物品染色（`tints` / `tintindex`）

`items/<id>.json` 的 `model` 对象可带 `tints` 数组；模型每个面用 `tintindex` 索引其中一项，`-1` 表示不染色（顶点色为纯白）。原文对照：

| 来源 | 原版 | 本仓 |
| --- | --- | --- |
| `minecraft:constant` | `value` 为 ARGB 整数，直接作为颜色 | 已实现，按 `(argb>>16/8/0)&0xFF` 拆 RGB |
| `minecraft:grass` | `GrassColor.get(temperature, downfall)` → `ColorMapColorUtil.get` | 已实现：`rain = downfall·temp`、`x = (1−temp)·255`、`y = (1−rain)·255`、`index = y<<8\|x`，色表从 `textures/colormap/grass.png` 运行时读取（256×256 校验） |
| `dye` / `potion` / `leaves` 等 | 各自 `ItemTintSource` | 未实现，面保持白色 |

**生成型物品的 tint index 是图层序号**：原版 `ItemModelGenerator.ItemLayerKey.compute` 以 `this.layerIndex` 构造 `MaterialInfo`，所以 `layer0` 的面必须带 `tintindex = 0`。留成 `-1` 会让所有 tint 来源对平面物品静默失效（症状：`short_grass` 掉在地上是灰色，而方块模型类如 `oak_leaves` 正常）。侧面挤出属于同一图层，也必须带同一 tint index。

真值：`oak_leaves` 的 `constant = -12012264`；`grass_block`/`short_grass` 的 `grass = {temperature:0.5, downfall:1.0}`，按上式得到 index `0x7F7F`。缺色表时回退原版默认值（品红 `0xFFFF00FF`），不臆造颜色。
### 3.5.1 生成型物品（`builtin/generated`）

`ItemModelGenerator.bakeExtrudedSprite` 与元素模型不是一套逻辑，MUST 单独实现：

| 项 | 原版 | 说明 |
| --- | --- | --- |
| 几何 | 盒 `(0,0,7.5)–(16,16,8.5)` | 深度仍是 1/16 |
| 正面 UV | `SOUTH_FACE_UVS = (0,0,16,16)` | |
| 背面 UV | `NORTH_FACE_UVS = (16,0,0,16)` | **U 显式反向**，令远面与近面重合 |
| 提交方式 | `addUnculledFace` ×2 | 两面都会画，**不依赖剔除** |
| 侧面 | `bakeSideFaces` 扫描 sprite 边缘像素逐个挤出面 | **已实现** `GeneratedItemGeometry`：逐像素检测四面透明邻接，`UV_SHRINK = 0.1`，深度同为 7.5–8.5；仅扫描静态首帧（原版遍历 `getUniqueFrames`，动画帧未处理） |

**踩过的坑**：背面若沿用 `defaultFaceUV`（整块元素得到 `(0,0,16,16)`）就与正面同向，于是「背面镜像精灵」与正面叠成 V 形双剑；必须显式给反向 U。

**掉落物尺寸真值**：`item/handheld` 与 `item/iron_sword` 都不声明 `ground`，只有祖先 `item/generated` 声明 `ground = { rotation:[0,0,0], translation:[0,2,0], scale:[0.5,0.5,0.5] }`。因此生成型掉落物实际是 **0.5 × 0.5 格**、厚 **0.03125 格**（`0.0625 × 0.5`），低于 `0.0625` 阈值故走单列布局，堆叠步长 `0.03125 × 1.5 = 0.046875`。

### 3.7 行向量下的合成顺序（易错点）

原版是列向量矩阵，`ItemTransform.apply` 依次调用 `translate(t)` → `rotate(rotationXYZ)` → `scale(s)` → `translate(-0.5,-0.5,-0.5)`，列向量累积为 `M = T·R·S·T(-0.5)`，因此**顶点最先被 `-0.5` 居中**，最后才平移。`LayerRenderState.applyTransform` 再 `mulPose(localTransform)`。

本仓用 `System.Numerics`（行向量、`p' = p·M`），乘法顺序必须整体反过来：

| 阶段（顶点作用顺序） | 行向量写法 |
| --- | --- |
| 居中 `-0.5` | 最左 |
| `scale` | |
| `rotateX·rotateY·rotateZ` | |
| `translate(t)` | 最右 |

掉落物整体为 `itemTransform · T_offset · R_spin · T_bob · T_entity`。**踩过的两个坑**：把 `-0.5` 居中写在外层会让物品绕角落公转而不是自转；把 `ground` 放在旋转外侧会出现同样的公转假象（先修后者，登录态复测时才发现前者）。

### 3.5.3 special 模型：两套作者约定（已查清）

原版对模型使用**两套互不相同的作者约定**，这是 special 模型路径的关键：

| 模型族 | `addBox` 的 Y 方向 | 渲染时是否翻转 | 证据 |
| --- | --- | --- | --- |
| 活体生物（`LivingEntityRenderer`） | Y 向下（头在原点上方取负值） | 是，`poseStack.scale(-1,-1,1)` | `ZombieModel` 等 `addBox(-4,-8,-4, …)` |
| 方块实体 / special（`ChestRenderer`、`ChestSpecialRenderer`） | **Y 向上**（箱子 `addBox(1,0,1, 14,10,14)` 长向正方向） | **否**，`ChestRenderer.submit` 只加朝向旋转，`ChestSpecialRenderer.submit` 连旋转都不加 | `ChestModel.createSingleBodyLayer`、`ChestSpecialRenderer.submit` |

**本仓现状**：`VanillaMeshBakery` 只实现活体那一套（`modelHeight = 24` + Y 取反），而 `SpecialItemModelDefinition` 的方块类 special 模型用的是原版 `addBox` 原始数值（Y 向上）。结果存储立方体是**作者空间的上下镜像**，已由测试钉住：

- 箱子 `bottom` 作者 y 0..10 → 存储 `Bottom = 14`、`Height = 10`（即 14..24）
- 箱子 `lid` 作者 y 9..14 → 存储 10..15
- 即**盖子存到了箱体下面**

**后果**：任何直接绘制这些存储立方体的路径都会上下颠倒并带上 24px 偏移。**尚未实测确认**手持路径是否另行补偿：先前一次「手持箱子异常」的截图无效（当时 `/summon` 用的是已废弃的 `HandItems` 键，僵尸其实没拿东西；26.3 必须用 `equipment:{mainhand:{…}}`）。

**修复方向**：为方块类 special 增加「Y 向上、无 24px 偏移」的烘焙模式，而不是沿用活体的 `(24 − y)` 变换；`ItemModelExtents.FromModel` 里的 `(24 − bakedY)` 逆变换届时也应换成直接读取。
### 3.5.4 物品渲染解析审计（L1 等效性检查层）

`tools/Sharpcraft.ItemRender.Audit` 是物品渲染等效性检查的**第一层**：纯数值、不依赖 GPU，覆盖**注册表全量**物品。它把"某个物品解析成什么、在 7 个 display 上下文中各得到什么变换和包围盒"变成可 diff 的事实，而不是靠截图目测。

用法：

```
dotnet run --project tools/Sharpcraft.ItemRender.Audit -- --pack <pack.zip> --out audit.jsonl
dotnet run --project tools/Sharpcraft.ItemRender.Audit -- --pack <pack.zip> --check tools/Sharpcraft.ItemRender.Audit/baseline.txt
```

基线（`baseline.txt`）是棘轮：解析种类计数、未解析清单、各上下文缺失 display 的数量，任何一项变化都会让 `--check` 失败。

**首轮结果**（1658 物品）：

| 种类 | 数量 |
| --- | --- |
| `elements`（模型自带 elements） | 835 |
| `generated`（生成型两层面片） | 760 |
| `special:*`（chest 11 / shulker 17 / banner 16 / statue 8 / head 6 / player_head 1 / shield 1 / trident 1 / conduit 1 / decorated_pot 1） | 63 |
| 未解析 | **0** |

审计当时发现的真实缺陷：**17 张床全部解析失败**。原因是 `composite` 的子项是"完整模型定义"（`{"type":"minecraft:model","model":"block/white_bed_head"}`），而 `ResolveFirstCase` 只接受"`model` 是对象"的选择器形态，于是整串被跳过。修复后未解析从 17 降到 0。

**已解决（掉落路径，`T-766`）**：`ItemModelResolver.ResolveAll` 解析全部子模型，每个带自己的 `transformation`（行向量 `local · display`，与原版 `LayerRenderState.applyTransform` 的 `Display · Local` 列向量语义一致），掉落渲染按子模型分段提交并用子模型 extents 的并集定位；实机掉落的红床为完整小床（白枕 + 红床面 + 木腿）。**图集预热也必须覆盖全部子模型**：`DroppedItemMesher.ResolveTextures` 原先只按第一个子模型产出贴图路径，导致床脚那一半的 `red_bed_foot_*` 从未进入 item atlas，`itemAtlas.Get` 返回 null 后这些面被整片跳过——表现就是"后半截只剩底面（`bed_down`）"。已改为遍历 `ResolveAll`，实机掉落床恢复为完整小床（白枕 + 红床面 + 木腿）。**手持路径同样已覆盖**：`EntityModelResolver.ResolveHeldItemPlacements` 让 `HeldItemMesher` 逐子模型提交（`placement.Transform · display · useItem · stab · anchor · handFrame · entityMatrix`），实机手持红床为完整小床。**顺带修复**：手持物品的贴图此前从未被预热进 item atlas（只有掉落路径预热），导致手持模型在场景内无同类掉落时整块不显示；新增 `HeldItemMesher.ResolveTextures` 并接入预热点。**仍存在的简化**：GUI/展示框路径未渲染（`SIM-ITEM-DROP-DISPLAY-GUI`）。

缺失 display 的分布同样说明问题不在解析：`thirdperson_righthand` 只缺 `minecraft:air`（空物品，正常），`head` 缺 706（原版多数物品本来就没有 head 变换），真正需要关注的是 `firstperson_righthand` 缺 10 个（`spyglass`、`heavy_core`、各类头颅），它们走 special 渲染器。
### 3.5.5 display 变换真值（L3，含一次真实 bug 修复）

`ItemTransform.apply` 的调用序列（原文 `ref://mc-26.3-analysis/src/net/minecraft/client/resources/model/cuboid/ItemTransform.java`）：

```
translate(translation)            // JSON 里已乘 1/16 并 clamp 到 ±5
rotate(new Quaternionf().rotationXYZ(rx, ry, rz))
scale(scale)                      // clamp 到 ±4
translate(-0.5, -0.5, -0.5)       // 居中
NO_TRANSFORM 是特例：只做 translate(-0.5,-0.5,-0.5)
左手指纹修复：translation.x、rotation.y、rotation.z 取反
```

**真值来源不是手推**：`tools/Sharpcraft.ItemRender.Audit/truth/DisplayTransformProbe.java` 直接跑游戏自带的 `joml-1.10.9.jar`，按上面序列构造矩阵并把一组模型点映射结果写成 `tests/Sharpcraft.Client.Tests/Data/item-display-truth.json`（6 个 case：箱子 gui/ground/fixed/第三人称、纯旋转探针、非等比缩放探针，每组 6 个点）。C# 侧 `ItemDisplayTransformGoldenTests` 逐点对照。

**由此抓到的真实 bug**：我们原来用欧拉乘积 `Rx·Ry·Rz`（`Matrix4x4.CreateRotationX * CreateRotationY * CreateRotationZ`）来复现 `rotationXYZ`，**这是错的**。金标准显示 JOML 的结果等价于四元数按 `qx * qy * qz` 相乘：

```
qx = CreateFromAxisAngle(UnitX, rx), qy = ..., qz = ...
R  = CreateFromQuaternion(qx * qy * qz)
```

修复前 `chest_gui` 的点 (1,0,0) 期望 `(0.000000, -0.049662, -0.538983)`、实际 `(-0.080881, -0.114383, -0.522823)`；修复后全部 36 个点吻合到 1e-4。也就是说**所有带旋转的 display 上下文（GUI、掉落物 ground、手持第三人称）此前朝向都是错的**，这也解释了手持物品"位置/朝向莫名"的现象。

其余随附测试：四个上下文的缩放（gui 0.625 / ground 0.25 / fixed 0.5 / 第三人称 0.375）、中心点映射到 translation、线性部分缩放、`NO_TRANSFORM` 只居中、左手修复语义。
### 3.5.6 接触表渲染（L4）

`tools/Sharpcraft.ItemRender.Sheet` 把一批物品按**固定机位 + 固定光照**渲进一张 PNG，用的就是共享物品几何链路（`ItemModelResolver` → `DroppedItemMesher.CollectQuads` → display 变换），因此图和游戏里看到的等价，只是排成网格便于比对。

```
dotnet run --project tools/Sharpcraft.ItemRender.Sheet -- --pack <pack.zip> \
    --out sheet.png --context ground [--items a,b,c] [--check sidecar-golden.txt]
```

- 渲染是**纯 CPU 软光栅**（无 GPU、无窗口），保证图片只取决于几何与贴图，可复现
- 默认 24 个物品、6 列、每格 128px；`--context` 支持 ground/gui/fixed/thirdperson_righthand，可直接与原版同上下文截图对照
- 与 PNG 同目录输出 `.txt` sidecar：每个物品解析成多少 quad、是否 special

**金标准用 sidecar 而不是像素哈希**：PNG 是原版贴图的衍生物，按原则 3 不得进仓库；sidecar 只含物品 id 与几何计数，可安全提交，且一样能抓几何/解析回归。当前 `sidecar-golden.txt` 覆盖默认 24 物品在 ground 上下文的结果。

已知可见缺口（有意保留，便于对照时一眼看出）：`chest` / `shield` / `trident` 三个格子是空的——它们走 special 渲染器，不在本几何链路内。
### 3.5.7 模型帧镜像与 `ReferenceFrame`（重要约定）

**我们的模型帧是原版模型帧的镜像**，这不是设计选择，而是把原版渲染期的翻转提前烘焙进几何的结果：

```
原版 LivingEntityRenderer：poseStack.scale(-1.0F, -1.0F, 1.0F);   // 画的时候才翻
本仓 VanillaMeshBake.FromVanilla：
    centreX = mirrorX ? -sourceCentreX : sourceCentreX     // X 取反
    bottom  = modelHeight - (pivotY + minY + height)       // Y 翻转（yDown → modelHeight − yDown）
```

于是：`本仓帧 = 原版模型帧 × diag(-1,-1,1)`（外加抬到脚底为 0 的平移）。

**这条因果关系的三个后果**：

1. 任何**不是几何**、但仍写在原版帧里的变换（物品 display、手持锚点、special 模型布局）都必须过一次镜像换算——现在统一走 `src/Sharpcraft.Client/Render/ReferenceFrame.cs`，构造时**按原版原样写**，只在边界转换一次，不再逐处手翻符号。
2. **方块类 special 模型是错的**（`T-764`）：`ChestRenderer` / `ChestSpecialRenderer` 在原版**不做**这个翻转，但我们的烘焙照样翻了，所以箱子等存储成上下镜像。
3. 手持锚点反复调不对的根因就在这里：原版数值写在未翻转帧里，直接套进已翻转帧必然错位。

**原版真值（可直接复核，探针源码见 `tools/Sharpcraft.ItemRender.Audit/truth/`）**

```
ItemInHandLayer.submitArmWithItem：
    A_ref = Rx(-90°) · Ry(180°) · T_off
    T_off = (±1, 2, -10)/16 成人；(0, 1, -4.5)/16 幼年（非 armor stand）；左手镜像 X

AnchorAxes.java（A_ref 在原版模型帧的轴像）：
    ex -> (-1.0000,  0.0000,  0.0000)
    ey -> ( 0.0000,  0.0000, -1.0000)
    ez -> ( 0.0000, -1.0000,  0.0000)
    origin -> (-0.0625, 0.6250, -0.1250)

HeldAnchorProbe3.java（右臂枢轴 (-0.3125, 0.125, 0)，y 向下的模型帧）：
    物品中心 -> (-0.3750, 0.6875, -0.3125)
    手臂末端 -> (-0.3125, 0.8750,  0.0000)
```

### 3.5.8 手持锚点的求解结果（T-765 已收口）

**帧换算**（`ReferenceFrame`）：我们的帧 = 原版帧经 `M(镜像) → S(模型比例) → T(0, 24/16·s, 0)`。实测校验：原版右臂枢轴 `(-0.3125, 0.125, 0)` 换算后为 `(0.281, 1.237, 0)`，与 `PartWorldMatrix("right_arm")` 完全一致（`HeldItemPlacementComparisonTests.TheHandFrameMatchesTheReferencePivotAfterConversion`）。

**锚点求解**：把 `MatrixDump.java` 打出的原版世界矩阵换算进我们的帧，再解 `D · anchor · H = W`，得到闭式解：

```
anchor = S(s) · Rx(-90°) · T((1, -10, -2)/16 · s)        s = 模型比例（成人 0.9，幼年 0.45）
s = 0.9 时：旋转 0.9·Rx(-90°)，平移 (0.05625, -0.56250, -0.11250)
```

`HeldItemPlacementComparisonTests.TheHeldItemLandsWhereTheReferencePutsIt` 用 `MatrixDump` 打出的真值逐点校验：

```
原版（未翻转模型帧）：物品中心 (-0.375000, 0.687500, -0.312500)，角点 (-0.650000, 0.962500, -0.037500)
我们（换算后帧）：    物品中心 ( 0.337500, 0.731250, -0.281250)，角点 ( 0.585000, 0.483750, -0.033750)  ✅
```

游戏内验证：四只僵尸分别手持铁剑、弓、苹果、矮草，位置在手上、朝向与原版一致（剑竖直、弓面竖直）。

**副手（左手）**：原版 `LayerRenderState.applyTransform` 调用 `itemTransform.apply(displayContext.leftHand(), pose)`，其中 `ItemTransform.apply` 在左手时**额外**把 `translation.x` 与 `rotation.y/z` 取反（叠加在 `thirdperson_lefthand` JSON 条目之上）。本仓 `DisplayFor` 现已传入 `applyLeftHandFix`，真值见 `BowLeftProbe.java`、测试 `LeftHandDisplayFixTests`；实机验证副手弓竖直、与主手一致。

### 3.6 图集归属（已核实，非阻塞）

26.3 把精灵分在两个图集：`atlases/items.json` 只收 `item/` 目录与盔甲纹饰置换，`atlases/blocks.json` 收 `block/` 目录与 conduit/bell/book 特例。因此方块类物品模型引用的 `block/*` 纹理在原版来自 **blocks 图集**。

本仓只维护**一个**动态打包的物品图集（`ItemAtlas`），纹理路径由 `EnsureItemTextures` 按实际引用收集后从资源管理器读取。由于图集是内部实现细节、像素来源一致，把 `block/*` 精灵收进同一图集**不改变可观测结果**，不构成简化。前提是「引用什么就收集什么」，新增解析路径（如 `elements` 面纹理）MUST 同步把新的纹理路径纳入收集。

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

### 3.5.9 掉落物布局：第一件不偏移

原版 `ItemEntityRenderer.submitMultipleFromCount` 对**第一件**绘制物品不加任何偏移：

- 厚模型（`modelDepth > 0.0625`，床/方块属于此类）：`item.submit(...)` 直接提交；只有第 2 件起才 `translate(xo, yo, zo)`，量级 `±0.15`
- 薄模型：先 `translate(0, 0, -(offsetZ * (count-1) / 2))` 沿 Z 起排，第一件同样无 X/Y 偏移；第 2 件起叠加 `±0.15*0.5` 的 X/Y 抖动

我们此前对**第一件**也套用了偏移。该偏移在行向量链中位于 `Ry(spin)` 之前：

```
world = display · T_offset · Ry(spin) · T(0, bob+minY, 0) · T(entityPos)
```

⇒ 被旋转扫成圆周，物品看起来"绕长边公转"而非自转；偏移量取 `RandomUnit(entityId, index, axis)` 哈希，因此**每次召唤随机出现**。修复为 `ItemEntityRenderer.ItemOffset(...)`：`index == 0` 时恒返回零向量，回归见 `DroppedItemLayoutTests`（5 项）。
### 3.5.17 掉落物不走实体模型路径（T-763）

原版 `ItemEntityRenderer extends EntityRenderer<ItemEntity, ItemEntityRenderState>`：`submit` 里只做 `translate(0, bob + minOffsetY, 0)`、按 `ItemEntity.getSpin` 自旋、再 `submitMultipleFromCount` 提交**物品模型**（与手持/GUI 同一套模型管线，display 上下文为 `ground`）。它不烘焙任何 `ModelPart` 模型，因此**不存在占位盒回退**。

本仓曾把 item entity 当作普通实体交给 `EntityModelMesher`，模型解析失败后落到 `EntityModelResolver.Placeholder`（14³ 盒子，按 64×64 皮肤 UV 采样实体皮肤图集），于是每个掉落物外面都出现一个"皮肤图集碎片拼贴"的立方体——即用户报告的悬浮彩色碎片。

修法：`EntityModelMesher.BuildInto` 对 `EntityTracker.ItemEntityTypeId` 直接跳过，掉落物几何只由 `DroppedItemMesher` 产出。回归：`ItemEntityModelTests`。

### 3.5.9a 手持锚点：拟合解与审计结论（T-777）

`ReferenceFrame.HeldItemAnchor` 不是逐因子推导，而是**从参考捕获求解**出来的矩阵（两组姿态 × 两种 display 共四组一致）：

```
anchor = T(T_off) · S(modelScale) · Rx(+90°)
成人：线性 0.9·Rx(+90°)，平移 (0.05625, -0.56250, -0.11250)
```

`tools/Sharpcraft.ItemRender.Audit/truth/AnchorAxes.java` 给出参考侧旋转链的基向量像：

```
A_ref = RotX(-90°) · RotY(180°) · T(1/16, 2/16, -10/16)
ex → (-1,0,0)   ey → (0,0,-1)   ez → (0,-1,0)   origin → (-0.0625, 0.6250, -0.1250)
```

**审计结论（保持）**：曾尝试把参考链共轭到烘焙系（`Frame·A_row·Frame⁻¹`）以独立复现该锚点，结果与拟合形不一致（共轭形无缩放且 X 镜像），说明共轭路径不等价于引擎实际的链接方式。完整推导需要端到端捕获参考链（含 `H` 手部矩阵与 display 的完整乘积），成本高于收益。

因此保留现锚点，依据是四组捕获一致 + 实机床/盾牌/长矛验证；`PartWorldMatrixTests` 断言的是**求解值回归钉**，不承担等效性证明。复查条件：出现持物位置偏差报告，或审计工具能输出整链矩阵时重做。

### 3.5.10 物品渲染类型：按贴图 alpha 判定（进行中）

原版判据（已核实）：

```java
// NativeImage.computeTransparency：逐像素分类
alpha == 0        -> hasTransparent
0 < alpha < 255   -> hasTranslucent

// BakedQuad：整件物品选一个渲染类型
itemRenderType = transparency.hasTranslucent()
    ? Sheets.translucentBlockItemSheet()   // RenderTypes.itemTranslucent: 混合 + ALPHA_CUTOUT 0.1 + DepthStencilState.DEFAULT
    : Sheets.cutoutBlockItemSheet();       // RenderTypes.itemCutout:    ALPHA_CUTOUT 0.1，不混合
```

26.3 资源包实测 alpha 分布（决定归类）：

| 贴图 | alpha 0 | 半透明 | 不透明 | 原版归类 |
| --- | --- | --- | --- | --- |
| `block/glass` | 191 | 0 | 65 | cutout |
| `block/black_stained_glass` | 0 | 256 | 0 | translucent |
| `block/ice` | 0 | 256 | 0 | translucent |
| `block/blue_ice` | 0 | 0 | 256 | 不透明（cutout 等价） |

已实现：`ItemAtlas` 记录每个 sprite 的半透明标记（`HasTranslucentPixel` ↔ `computeTransparency`），掉落与手持各拆 cutout / translucent 两层。

**已完成**：①物品 cutout 用独立 `EntityGeometryLayerKind.ItemCutout` + `fs_item_cutout`，alpha 测试 0.1（原版 `RenderPipelines.ITEM_CUTOUT` 的 `ALPHA_CUTOUT`），实体本体仍 0.5；②半透明层沿用 `Translucent`，其状态（混合 + 深度写入开 + 不剔除 + alpha 0.1）与原版 `ITEM_TRANSLUCENT` 一致。

**已实现**：①半透明物品使用**独立的参考 OIT 集合**（`itemDepthBounds/coeff/accumulate` 四张目标 + 独立绑定组 + 独立合成），与地形互不污染，对应原版 `OitPipelineSet("item")`；②半透明物品接入**参考 OIT**——`EntityGeometryLayerKind.ItemTranslucent` 图层在主 pass 跳过，改由 OIT 三阶段（depth bounds / transmittance / accumulate）以 `DrawItemOitGeometry` 绘制，复用 `ref*` 管线（其顶点布局即实体 56 字节布局）与物品图集绑定组；②**对象式贴图项**：`{"sprite": "...", "force_translucent": true}` 现在被解析（对应原版 `Material(sprite, forceTranslucent)`），此前只认字符串导致 `#all` 无法解析、玻璃六面贴图全为 null 而完全不显示；③`force_translucent` 直接决定 translucent 归类（原版 `FaceBakery` 据此打 `FLAG_TRANSLUCENT`）。实机：冰（OIT 后无叠加加深）、玻璃、黑玻璃、手持玻璃均正常。

**遗留**：原版 `RenderTypes.ITEM_TRANSLUCENT` 挂了 `setOitPipelines(RenderPipelines.OIT_ITEM)` 且 `sortOnUpload()`，即半透明物品走 **OIT**（与方块/流体同一套参考 OIT）；我们直接在主 pass 用普通 alpha 混合，立方体多个面互相叠加 ⇒ 出现「有的面比其他面更不透明」。修法：把物品半透明图层接入现有参考 OIT pass（`oit_reference.wgsl` + `OitDepthBounds`），而不是主 pass 混合。
### 3.5.11 半透明物品接入参考 OIT（设计，待实现 T-771）

**原版依据**

```java
// RenderTypes.ITEM_TRANSLUCENT（Sheets.translucentBlockItemSheet / translucentItemSheet 的底层）
RenderSetup.builder(RenderPipelines.ITEM_TRANSLUCENT)
    .setOitPipelines(RenderPipelines.OIT_ITEM)
    .withTexture("Sampler0", texture).useLightmap().useOverlay()
    .sortOnUpload()
    .createRenderSetup();

// RenderPipelines.ITEM_TRANSLUCENT
.withShaderDefine("ALPHA_CUTOUT", 0.1F)
.withColorTargetState(new ColorTargetState(BlendFunction.TRANSLUCENT))
```

即：半透明物品**走参考 OIT**（与方块/流体同一套 depth-bounds + 透射率 + 累积），并在上传前按视距排序。我们目前在主 pass 里直接 alpha 混合，立方体多面重叠会叠加，表现为"有的面更不透明"。

**目标**

物品半透明几何与方块/流体共用同一条 OIT 主路径（原则 14 单轨），不再主 pass 混合。

**改造点（草案）**

1. **着色器**：新增 `oit_item.wgsl`（或复用 `terrain.wgsl` 的实体采样 + `oit_reference.wgsl` 的三阶段）：
   - 顶点输入沿用实体/物品 56 字节布局（`VertexLayout.Attributes()`，含 `spriteRect`）；
   - `fs_item_depth_bounds` / `fs_item_transmittance` / `fs_item_accumulate` 的出参与 `oit_reference.wgsl` 一致，仅把采样换成物品图集 + `spriteRect` → 图集 UV（同 `fs_entity_translucent`）；
   - alpha 测试 0.1（与 `ITEM_TRANSLUCENT` 一致）。
2. **绑定组**：`group0` = 物品图集（texture+sampler）+ camera + fog + lightmap（与实体物品图层相同）；`group1` = OIT 目标（与 `ref*` 一致）。参考 `CreateDepthBoundsPipeline/TransmittancePipeline/AccumulatePipeline` 的布局参数，新增物品变体。
3. **图层标记**：`EntityGeometryLayerKind` 增加 `ItemTranslucent`，主 pass 的绘制 switch 跳过它；OIT pass 新增一个 draw set（depth bounds → transmittance → accumulate），复用 `DynamicGeometry` 上传路径。
4. **Mesher**：`DroppedItemMesher` / `HeldItemMesher` 的半透明图层改用 `ItemTranslucent`（当前是 `Translucent`）。
5. **排序**：原版 `sortOnUpload()`；参考 OIT 对顺序不敏感（depth bounds + 透射率），可先不排序，实测若仍有差异再补。

**验收**：冰 / 黑玻璃 / 染色玻璃在掉落与手持下与正版同机位截图一致（面与面之间无叠加加深）。
### 3.5.12 染色玻璃竖条：sprite rect 高度错用 cell 高度（T-773，已定位待修）

**现象**：物品级染色玻璃近距离出现细竖条，且只有白 / 淡灰 / 灰 / 黑四种明显。

**证据链**

1. 源贴图与图集内容正确：`black_stained_glass` 源图与图集区域逐像素一致（`163,102×14,163`）。
2. 六面 UV 均在 [0,1]，无平铺。
3. 关掉 OIT（按 cutout 主通道绘制）后条纹**原样存在** ⇒ 与透明合成无关。
4. 顶点导出（`DynamicGeometry.Vertices`，14 float/顶点）：
   ```
   sprite = (0.5000, 0.5000, 0.6250, 0.6250)   → 128×192 图集内 (x=64, y=96, w=16, h=24)
   实时查表 itemAtlas.Get("block/black_stained_glass") = (0, 128, 16, 16)
   ```

**结论**

- 图集高 192 = 8 行 × **24** ⇒ cell 高被更高的贴图撑到 24；该 sprite 的 rect **高度取了 cell 高度（24）**，而宽度取了贴图宽度（16）。多出的 8 行采样窗口探进下一行 sprite，把邻居贴图串进来。
- 16 张染色玻璃贴图形状相同、仅颜色不同（`textures/block/*_stained_glass.png` 共 16 个，物品模型 `items/<色>_stained_glass.json` 直接指向 `block/<色>_stained_glass`）。因此串色后：彩色玻璃会串成杂色（显眼但不像"条纹"），无彩色四种串成等亮度竖条 ⇒ 只有它们看起来是竖纹。
- 另一处不一致：绘制时使用的 rect 与实时查表结果不同（`(64,96)` vs `(0,128)`），说明形状布局存在**版本不同步**（图集扩容后旧 layout 未失效）。

**修复清单**

1. `ItemAtlas` 的 sprite rect 一律使用**贴图实际宽高**；`cellWidth/cellHeight` 只用于布局，不得进入 `SpriteRect`。
2. 图集重建（新增纹理、尺寸变化）时，失效所有派生的形状/布局缓存。
3. 回归测试：构造高度不一的贴图集合，断言每个 rect 的 `Height == image.Height` 且 `V1 <= (row+1)*cellHeight/atlasHeight`。
### 3.5.13 物品图集并入统一装配器（T-774，已实现）

**动机**

当前存在两套图集装配器，违反原则 14 单轨：

| 图集 | 装配器 | padding | mip | 可变性 |
| --- | --- | --- | --- | --- |
| 方块 | `SpriteAtlasBuilder` + `SpriteAtlas` + `AtlasMipChain` | 2 texel | 有 | 不可变对象 |
| 物品 | `ItemAtlas`（自造） | 0 | 无 | 可变（`WithTexture` 原地扩容） |

物品那套的零 padding 与原地扩容直接导致 T-773（染色玻璃竖条）：几何按旧图集算 rect、旧 rect 用于新图集，且线性过滤采到相邻 sprite。

**原版依据**

- sprite 是不可变对象，UV 在创建时按该图集尺寸算死：`ref://mc-26.3-analysis/src/net/minecraft/client/renderer/texture/TextureAtlasSprite.java:24-36`（`(x+padding)/atlasWidth`，用 `contents.width()` 而非 cell）。
- 拼接留白：`ref://mc-26.3-analysis/src/net/minecraft/client/renderer/texture/Stitcher.java:28` `padding = 1 << mipLevel << clamp(anisotropy-1,0,4)`。
- 图集变化 ⇒ 全新 sprite 实例 + 模型重新烘焙，旧 UV 副本随之消失。

**迁移结果（已实现）**：`ItemAtlas` 内部改为 `SpriteAtlasBuilder.Stitch` 门面，公开 API 未变（调用点零改动）；图集从 128×192 均匀网格变为 2 的幂、每 sprite 带 padding、不可变对象。**注意**：迁移后染色玻璃竖条**仍存在**，说明该缺陷另有根因（rect 已确认正确），继续跟踪于 T-773。

1. `ItemAtlas` 内部改为持有 `SpriteAtlas`（由 `SpriteAtlasBuilder.Stitch` 产出）；公开 API `Get / GetTexture / IsTranslucent / Texture / WithTexture` 保持不变，调用点零改动。
2. 键映射：物品贴图路径（`block/glass`、`item/...`）→ `Identifier`；`SpriteRect` 与物品链路已是同一类型，无需转换。
3. `WithTexture` 改为**返回新实例**（重新 `Stitch`），不再原地扩容；客户端按实例/版本失效几何（等价原版重新烘焙）。
4. `force_translucent` 与 alpha 半透明标记在门面层保留（`IsTranslucent`），底层像素不变。
5. 回归：`T-768` 物品审计工具（1658 物品）全绿 + 掉落/手持代表性截图（床、盾牌、composite、染色玻璃、冰、玻璃）。

**风险**

- 物品图集含非 16×16 与多帧贴图：`Stitch` 已支持任意尺寸与帧数（方块图集同源路径）。
- 图集尺寸会从 128×192 变为 2 的幂方形，纹理坐标随之变化，所有 rect 必须来自新实例（不得跨实例混用）。
### 3.5.14 T-773 结论更正：竖条不在物品链路（已更正）

用户实测：**世界中的方块玻璃（染色玻璃方块）同样显示竖条纹**。由此更正 3.5.12 的结论：

| 原假设 | 现状 |
| --- | --- |
| 物品图集 sprite rect 跨版本错用 | **已排除**：T-774 统一装配器后（2 的幂 + padding 2 + 不可变对象 + 真实尺寸）条纹不变 |
| 与 OIT 无关（关 OIT 后仍出现） | **不成立**：那次实验观察到的很可能是世界里的玻璃**方块**（一直走 OIT），不是物品 |
| 物品独有缺陷 | **不成立**：方块玻璃同样出现 |

共同点：方块玻璃走地形参考 OIT，物品玻璃走物品参考 OIT。两者都经过 OIT 合成，因此竖条指向**参考 OIT 的重建误差**，而非图集或 rect。

已逐项核对**一致**的部分（CPU 参考 `OitWavelet`、WGSL、原版 GLSL 三者算法一致）：

- bin 数 8、小波阶 2、`toAbsorbance/toTransmittance`、`OIT_FULLY_OPAQUE_TOTAL_TRANSMITTANCE = 0.02`
- `normalizeDepth` 分段（含 high-precision 阈值 10）
- encode/sample 的二叉树索引、`select` 语义、`lerpAlpha`

**下一实验（待执行）**：把世界玻璃方块作为唯一变量逐层隔离
1. 临时让半透明地形走主 pass 普通混合（绕过 OIT），观察方块玻璃是否仍竖条 ⇒ 判定是否 OIT；
2. 若确认 OIT：分别把 bounds / 系数图 / accumulate 的目标以调试色输出，定位是 encode 还是 decode；
3. 用 `OitWavelet` 构造"单层平面前向"的解析用例（单层应完全精确），与 GPU 中间结果比对。
### 3.5.15 T-773 判别事实汇总（继续进行中）

三轮对照实验的结论（均以实机截图为准，非推断）：

| 实验 | 结果 | 排除了什么 |
| --- | --- | --- |
| 半透明物品强制走 cutout 主通道 | 条纹不变 | 物品侧的 OIT 集成 |
| 物品图集并入 `SpriteAtlasBuilder`（2 的幂 + padding 2 + 不可变 + 真实尺寸） | 条纹不变 | 图集 rect、padding、跨版本缓存 |
| `textureSampleLevel(..., 0.0)` 强制 mip 0 | 条纹不变 | mip 串色 |
| 单块近距：黑色染色玻璃 vs 普通玻璃 | 染色玻璃有、普通玻璃无 | 行/块聚合造成的摩尔纹（单块即可复现） |
| 世界方块玻璃 | 同样有 | "物品独有" |

**唯一相关的贴图特征**（26.3 资源包实测）：

| 贴图 | alpha 结构 | 竖条 |
| --- | --- | --- |
| white / light_gray / gray / black `_stained_glass` | 两级（边框 163 / 内部 102） | 有 |
| `glass` | 二值（0 / 255） | 无 |
| `ice` | 单一（全 102） | 无 |

结论：缺陷与"同一面内出现两种 alpha 值"相关。由于 rect / UV / 图集内容 / mip / OIT 算法均已排除，剩余解释是**该面实际采样的区域覆盖了贴图多次重复**（1 像素边框在屏幕上被复现成 8–10 根竖线）；二值 alpha（玻璃）与单一 alpha（冰）在同样采样下不呈现可辨识条纹，故只有染色玻璃暴露。

**下一步（定案实验）**：导出方块顶点缓冲里的 `uv` 与 `spriteRect`（与物品链路同一手法）：
- `uv` 落在 0..1 且 rect 正常 ⇒ 平铺发生在着色器 UV 展开
- `uv` 落在 0..16 等像素空间 ⇒ 方块网格器 UV 生成错误
### 3.5.16 T-773 根因与修复：PNG 灰度+alpha 解码错位（已解决）

**根因**

26.3 资源包把**接近无彩色的染色玻璃**存成 PNG **colour type 4（灰度+alpha，每像素 2 字节）**，其余贴图是灰度(0)、调色板(3) 或真彩(6)：

| 贴图 | PNG colour type | 现象 |
| --- | --- | --- |
| white / light_gray / gray / black `_stained_glass` | **4（灰度+alpha）** | 竖条纹 |
| `glass` | 6（RGBA） | 正常 |
| `ice` | 3（调色板） | 正常 |
| `stone` | 0（灰度） | 正常 |

`PngDecoder.ReadSample` 计算样本序号时，第一通道用了 `x` 而不是 `x * channelsPerPixel`：

```
index = secondChannel ? (x * 2) + 1 : x        // 修复前
index = (x * channelsPerPixel) + (second ? 1 : 0)   // 修复后
```

于是灰度值隔一半取到了**相邻像素的 alpha**，图集里该 sprite 变成 RGB 与 alpha 交错的条纹。实测证据（图集原始数据，行 8）：

```
修复前: (25,25,25,163) (163,163,163,102) (25,25,25,102) (102,102,102,102) ...
修复后: (25,25,25,163) (25,25,25,102) ×14 (25,25,25,163)
```

**为什么只有这四种**：只有它们被资源包以灰度+alpha 存储；红/蓝等彩色玻璃是调色板或真彩，解码路径正确。冰与普通玻璃同理不受影响。方块与物品共用同一图集来源，所以两者同时出现竖条。

**修复与回归**（并新增资源格式审计：`Sharpcraft.ItemRender.Audit --textures` 统计包内 3964 张 PNG 的 11 种 colourType×bitDepth 组合并逐文件校验解码，其中 type 4 共 **401 张**，即此前全部解码错）

- `PngDecoder.ReadSample` 增加 `channelsPerPixel`，灰度+alpha 两通道都按 `x * 2` 取样。
- 回归测试 `PngDecoderTests.GrayAlphaKeepsTheGrayOfItsOwnPixel`：合成 4×1 灰度+alpha PNG，断言 `(25,25,25,163)(25,25,25,102)(25,25,25,102)(25,25,25,163)`。
- 实机验证：黑/白/红染色玻璃方块与物品均为均匀半透明，无竖条。

**排查过程中被排除的方向**（保留以免重复走弯路）：物品图集 rect 跨版本缓存、图集 padding/mip 串色、参考 OIT 重建误差、子通道重复绘制、greedy 合并平铺。它们各自与本缺陷无关，但 T-774（图集统一）与 T-771/T-772（物品 OIT）本身仍是正确改进。
## 7. 风险与限制

#### 3.5.9 掉落物布局：第一件不偏移

原版 `ItemEntityRenderer.submitMultipleFromCount` 对**第一件**绘制物品不加任何偏移：

- 厚模型（`modelDepth > 0.0625`，床/方块属于此类）：`item.submit(...)` 直接提交；只有第 2 件起才 `translate(xo, yo, zo)`，量级 `±0.15`
- 薄模型：先 `translate(0, 0, -(offsetZ * (count-1) / 2))` 沿 Z 起排，第一件同样无 X/Y 偏移；第 2 件起叠加 `±0.15*0.5` 的 X/Y 抖动

我们此前对**第一件**也套用了偏移。该偏移在行向量链中位于 `Ry(spin)` 之前：

```
world = display · T_offset · Ry(spin) · T(0, bob+minY, 0) · T(entityPos)
```

⇒ 被旋转扫成圆周，物品看起来"绕长边公转"而非自转；偏移量取 `RandomUnit(entityId, index, axis)` 哈希，因此**每次召唤随机出现**。修复为 `ItemEntityRenderer.ItemOffset(...)`：`index == 0` 时恒返回零向量，回归见 `DroppedItemLayoutTests`（5 项）。
### 3.5.17 掉落物不走实体模型路径（T-763）

原版 `ItemEntityRenderer extends EntityRenderer<ItemEntity, ItemEntityRenderState>`：`submit` 里只做 `translate(0, bob + minOffsetY, 0)`、按 `ItemEntity.getSpin` 自旋、再 `submitMultipleFromCount` 提交**物品模型**（与手持/GUI 同一套模型管线，display 上下文为 `ground`）。它不烘焙任何 `ModelPart` 模型，因此**不存在占位盒回退**。

本仓曾把 item entity 当作普通实体交给 `EntityModelMesher`，模型解析失败后落到 `EntityModelResolver.Placeholder`（14³ 盒子，按 64×64 皮肤 UV 采样实体皮肤图集），于是每个掉落物外面都出现一个"皮肤图集碎片拼贴"的立方体——即用户报告的悬浮彩色碎片。

修法：`EntityModelMesher.BuildInto` 对 `EntityTracker.ItemEntityTypeId` 直接跳过，掉落物几何只由 `DroppedItemMesher` 产出。回归：`ItemEntityModelTests`。

### 3.5.9a 手持锚点：拟合解与审计结论（T-777）

`ReferenceFrame.HeldItemAnchor` 不是逐因子推导，而是**从参考捕获求解**出来的矩阵（两组姿态 × 两种 display 共四组一致）：

```
anchor = T(T_off) · S(modelScale) · Rx(+90°)
成人：线性 0.9·Rx(+90°)，平移 (0.05625, -0.56250, -0.11250)
```

`tools/Sharpcraft.ItemRender.Audit/truth/AnchorAxes.java` 给出参考侧旋转链的基向量像：

```
A_ref = RotX(-90°) · RotY(180°) · T(1/16, 2/16, -10/16)
ex → (-1,0,0)   ey → (0,0,-1)   ez → (0,-1,0)   origin → (-0.0625, 0.6250, -0.1250)
```

**审计结论（保持）**：曾尝试把参考链共轭到烘焙系（`Frame·A_row·Frame⁻¹`）以独立复现该锚点，结果与拟合形不一致（共轭形无缩放且 X 镜像），说明共轭路径不等价于引擎实际的链接方式。完整推导需要端到端捕获参考链（含 `H` 手部矩阵与 display 的完整乘积），成本高于收益。

因此保留现锚点，依据是四组捕获一致 + 实机床/盾牌/长矛验证；`PartWorldMatrixTests` 断言的是**求解值回归钉**，不承担等效性证明。复查条件：出现持物位置偏差报告，或审计工具能输出整链矩阵时重做。

### 3.5.10 物品渲染类型：按贴图 alpha 判定（进行中）

原版判据（已核实）：

```java
// NativeImage.computeTransparency：逐像素分类
alpha == 0        -> hasTransparent
0 < alpha < 255   -> hasTranslucent

// BakedQuad：整件物品选一个渲染类型
itemRenderType = transparency.hasTranslucent()
    ? Sheets.translucentBlockItemSheet()   // RenderTypes.itemTranslucent: 混合 + ALPHA_CUTOUT 0.1 + DepthStencilState.DEFAULT
    : Sheets.cutoutBlockItemSheet();       // RenderTypes.itemCutout:    ALPHA_CUTOUT 0.1，不混合
```

26.3 资源包实测 alpha 分布（决定归类）：

| 贴图 | alpha 0 | 半透明 | 不透明 | 原版归类 |
| --- | --- | --- | --- | --- |
| `block/glass` | 191 | 0 | 65 | cutout |
| `block/black_stained_glass` | 0 | 256 | 0 | translucent |
| `block/ice` | 0 | 256 | 0 | translucent |
| `block/blue_ice` | 0 | 0 | 256 | 不透明（cutout 等价） |

已实现：`ItemAtlas` 记录每个 sprite 的半透明标记（`HasTranslucentPixel` ↔ `computeTransparency`），掉落与手持各拆 cutout / translucent 两层。

**已完成**：①物品 cutout 用独立 `EntityGeometryLayerKind.ItemCutout` + `fs_item_cutout`，alpha 测试 0.1（原版 `RenderPipelines.ITEM_CUTOUT` 的 `ALPHA_CUTOUT`），实体本体仍 0.5；②半透明层沿用 `Translucent`，其状态（混合 + 深度写入开 + 不剔除 + alpha 0.1）与原版 `ITEM_TRANSLUCENT` 一致。

**已实现**：①半透明物品使用**独立的参考 OIT 集合**（`itemDepthBounds/coeff/accumulate` 四张目标 + 独立绑定组 + 独立合成），与地形互不污染，对应原版 `OitPipelineSet("item")`；②半透明物品接入**参考 OIT**——`EntityGeometryLayerKind.ItemTranslucent` 图层在主 pass 跳过，改由 OIT 三阶段（depth bounds / transmittance / accumulate）以 `DrawItemOitGeometry` 绘制，复用 `ref*` 管线（其顶点布局即实体 56 字节布局）与物品图集绑定组；②**对象式贴图项**：`{"sprite": "...", "force_translucent": true}` 现在被解析（对应原版 `Material(sprite, forceTranslucent)`），此前只认字符串导致 `#all` 无法解析、玻璃六面贴图全为 null 而完全不显示；③`force_translucent` 直接决定 translucent 归类（原版 `FaceBakery` 据此打 `FLAG_TRANSLUCENT`）。实机：冰（OIT 后无叠加加深）、玻璃、黑玻璃、手持玻璃均正常。

**遗留**：原版 `RenderTypes.ITEM_TRANSLUCENT` 挂了 `setOitPipelines(RenderPipelines.OIT_ITEM)` 且 `sortOnUpload()`，即半透明物品走 **OIT**（与方块/流体同一套参考 OIT）；我们直接在主 pass 用普通 alpha 混合，立方体多个面互相叠加 ⇒ 出现「有的面比其他面更不透明」。修法：把物品半透明图层接入现有参考 OIT pass（`oit_reference.wgsl` + `OitDepthBounds`），而不是主 pass 混合。
### 3.5.11 半透明物品接入参考 OIT（设计，待实现 T-771）

**原版依据**

```java
// RenderTypes.ITEM_TRANSLUCENT（Sheets.translucentBlockItemSheet / translucentItemSheet 的底层）
RenderSetup.builder(RenderPipelines.ITEM_TRANSLUCENT)
    .setOitPipelines(RenderPipelines.OIT_ITEM)
    .withTexture("Sampler0", texture).useLightmap().useOverlay()
    .sortOnUpload()
    .createRenderSetup();

// RenderPipelines.ITEM_TRANSLUCENT
.withShaderDefine("ALPHA_CUTOUT", 0.1F)
.withColorTargetState(new ColorTargetState(BlendFunction.TRANSLUCENT))
```

即：半透明物品**走参考 OIT**（与方块/流体同一套 depth-bounds + 透射率 + 累积），并在上传前按视距排序。我们目前在主 pass 里直接 alpha 混合，立方体多面重叠会叠加，表现为"有的面更不透明"。

**目标**

物品半透明几何与方块/流体共用同一条 OIT 主路径（原则 14 单轨），不再主 pass 混合。

**改造点（草案）**

1. **着色器**：新增 `oit_item.wgsl`（或复用 `terrain.wgsl` 的实体采样 + `oit_reference.wgsl` 的三阶段）：
   - 顶点输入沿用实体/物品 56 字节布局（`VertexLayout.Attributes()`，含 `spriteRect`）；
   - `fs_item_depth_bounds` / `fs_item_transmittance` / `fs_item_accumulate` 的出参与 `oit_reference.wgsl` 一致，仅把采样换成物品图集 + `spriteRect` → 图集 UV（同 `fs_entity_translucent`）；
   - alpha 测试 0.1（与 `ITEM_TRANSLUCENT` 一致）。
2. **绑定组**：`group0` = 物品图集（texture+sampler）+ camera + fog + lightmap（与实体物品图层相同）；`group1` = OIT 目标（与 `ref*` 一致）。参考 `CreateDepthBoundsPipeline/TransmittancePipeline/AccumulatePipeline` 的布局参数，新增物品变体。
3. **图层标记**：`EntityGeometryLayerKind` 增加 `ItemTranslucent`，主 pass 的绘制 switch 跳过它；OIT pass 新增一个 draw set（depth bounds → transmittance → accumulate），复用 `DynamicGeometry` 上传路径。
4. **Mesher**：`DroppedItemMesher` / `HeldItemMesher` 的半透明图层改用 `ItemTranslucent`（当前是 `Translucent`）。
5. **排序**：原版 `sortOnUpload()`；参考 OIT 对顺序不敏感（depth bounds + 透射率），可先不排序，实测若仍有差异再补。

**验收**：冰 / 黑玻璃 / 染色玻璃在掉落与手持下与正版同机位截图一致（面与面之间无叠加加深）。
### 3.5.12 染色玻璃竖条：sprite rect 高度错用 cell 高度（T-773，已定位待修）

**现象**：物品级染色玻璃近距离出现细竖条，且只有白 / 淡灰 / 灰 / 黑四种明显。

**证据链**

1. 源贴图与图集内容正确：`black_stained_glass` 源图与图集区域逐像素一致（`163,102×14,163`）。
2. 六面 UV 均在 [0,1]，无平铺。
3. 关掉 OIT（按 cutout 主通道绘制）后条纹**原样存在** ⇒ 与透明合成无关。
4. 顶点导出（`DynamicGeometry.Vertices`，14 float/顶点）：
   ```
   sprite = (0.5000, 0.5000, 0.6250, 0.6250)   → 128×192 图集内 (x=64, y=96, w=16, h=24)
   实时查表 itemAtlas.Get("block/black_stained_glass") = (0, 128, 16, 16)
   ```

**结论**

- 图集高 192 = 8 行 × **24** ⇒ cell 高被更高的贴图撑到 24；该 sprite 的 rect **高度取了 cell 高度（24）**，而宽度取了贴图宽度（16）。多出的 8 行采样窗口探进下一行 sprite，把邻居贴图串进来。
- 16 张染色玻璃贴图形状相同、仅颜色不同（`textures/block/*_stained_glass.png` 共 16 个，物品模型 `items/<色>_stained_glass.json` 直接指向 `block/<色>_stained_glass`）。因此串色后：彩色玻璃会串成杂色（显眼但不像"条纹"），无彩色四种串成等亮度竖条 ⇒ 只有它们看起来是竖纹。
- 另一处不一致：绘制时使用的 rect 与实时查表结果不同（`(64,96)` vs `(0,128)`），说明形状布局存在**版本不同步**（图集扩容后旧 layout 未失效）。

**修复清单**

1. `ItemAtlas` 的 sprite rect 一律使用**贴图实际宽高**；`cellWidth/cellHeight` 只用于布局，不得进入 `SpriteRect`。
2. 图集重建（新增纹理、尺寸变化）时，失效所有派生的形状/布局缓存。
3. 回归测试：构造高度不一的贴图集合，断言每个 rect 的 `Height == image.Height` 且 `V1 <= (row+1)*cellHeight/atlasHeight`。
### 3.5.13 物品图集并入统一装配器（T-774，已实现）

**动机**

当前存在两套图集装配器，违反原则 14 单轨：

| 图集 | 装配器 | padding | mip | 可变性 |
| --- | --- | --- | --- | --- |
| 方块 | `SpriteAtlasBuilder` + `SpriteAtlas` + `AtlasMipChain` | 2 texel | 有 | 不可变对象 |
| 物品 | `ItemAtlas`（自造） | 0 | 无 | 可变（`WithTexture` 原地扩容） |

物品那套的零 padding 与原地扩容直接导致 T-773（染色玻璃竖条）：几何按旧图集算 rect、旧 rect 用于新图集，且线性过滤采到相邻 sprite。

**原版依据**

- sprite 是不可变对象，UV 在创建时按该图集尺寸算死：`ref://mc-26.3-analysis/src/net/minecraft/client/renderer/texture/TextureAtlasSprite.java:24-36`（`(x+padding)/atlasWidth`，用 `contents.width()` 而非 cell）。
- 拼接留白：`ref://mc-26.3-analysis/src/net/minecraft/client/renderer/texture/Stitcher.java:28` `padding = 1 << mipLevel << clamp(anisotropy-1,0,4)`。
- 图集变化 ⇒ 全新 sprite 实例 + 模型重新烘焙，旧 UV 副本随之消失。

**迁移结果（已实现）**：`ItemAtlas` 内部改为 `SpriteAtlasBuilder.Stitch` 门面，公开 API 未变（调用点零改动）；图集从 128×192 均匀网格变为 2 的幂、每 sprite 带 padding、不可变对象。**注意**：迁移后染色玻璃竖条**仍存在**，说明该缺陷另有根因（rect 已确认正确），继续跟踪于 T-773。

1. `ItemAtlas` 内部改为持有 `SpriteAtlas`（由 `SpriteAtlasBuilder.Stitch` 产出）；公开 API `Get / GetTexture / IsTranslucent / Texture / WithTexture` 保持不变，调用点零改动。
2. 键映射：物品贴图路径（`block/glass`、`item/...`）→ `Identifier`；`SpriteRect` 与物品链路已是同一类型，无需转换。
3. `WithTexture` 改为**返回新实例**（重新 `Stitch`），不再原地扩容；客户端按实例/版本失效几何（等价原版重新烘焙）。
4. `force_translucent` 与 alpha 半透明标记在门面层保留（`IsTranslucent`），底层像素不变。
5. 回归：`T-768` 物品审计工具（1658 物品）全绿 + 掉落/手持代表性截图（床、盾牌、composite、染色玻璃、冰、玻璃）。

**风险**

- 物品图集含非 16×16 与多帧贴图：`Stitch` 已支持任意尺寸与帧数（方块图集同源路径）。
- 图集尺寸会从 128×192 变为 2 的幂方形，纹理坐标随之变化，所有 rect 必须来自新实例（不得跨实例混用）。
### 3.5.14 T-773 结论更正：竖条不在物品链路（已更正）

用户实测：**世界中的方块玻璃（染色玻璃方块）同样显示竖条纹**。由此更正 3.5.12 的结论：

| 原假设 | 现状 |
| --- | --- |
| 物品图集 sprite rect 跨版本错用 | **已排除**：T-774 统一装配器后（2 的幂 + padding 2 + 不可变对象 + 真实尺寸）条纹不变 |
| 与 OIT 无关（关 OIT 后仍出现） | **不成立**：那次实验观察到的很可能是世界里的玻璃**方块**（一直走 OIT），不是物品 |
| 物品独有缺陷 | **不成立**：方块玻璃同样出现 |

共同点：方块玻璃走地形参考 OIT，物品玻璃走物品参考 OIT。两者都经过 OIT 合成，因此竖条指向**参考 OIT 的重建误差**，而非图集或 rect。

已逐项核对**一致**的部分（CPU 参考 `OitWavelet`、WGSL、原版 GLSL 三者算法一致）：

- bin 数 8、小波阶 2、`toAbsorbance/toTransmittance`、`OIT_FULLY_OPAQUE_TOTAL_TRANSMITTANCE = 0.02`
- `normalizeDepth` 分段（含 high-precision 阈值 10）
- encode/sample 的二叉树索引、`select` 语义、`lerpAlpha`

**下一实验（待执行）**：把世界玻璃方块作为唯一变量逐层隔离
1. 临时让半透明地形走主 pass 普通混合（绕过 OIT），观察方块玻璃是否仍竖条 ⇒ 判定是否 OIT；
2. 若确认 OIT：分别把 bounds / 系数图 / accumulate 的目标以调试色输出，定位是 encode 还是 decode；
3. 用 `OitWavelet` 构造"单层平面前向"的解析用例（单层应完全精确），与 GPU 中间结果比对。
### 3.5.15 T-773 判别事实汇总（继续进行中）

三轮对照实验的结论（均以实机截图为准，非推断）：

| 实验 | 结果 | 排除了什么 |
| --- | --- | --- |
| 半透明物品强制走 cutout 主通道 | 条纹不变 | 物品侧的 OIT 集成 |
| 物品图集并入 `SpriteAtlasBuilder`（2 的幂 + padding 2 + 不可变 + 真实尺寸） | 条纹不变 | 图集 rect、padding、跨版本缓存 |
| `textureSampleLevel(..., 0.0)` 强制 mip 0 | 条纹不变 | mip 串色 |
| 单块近距：黑色染色玻璃 vs 普通玻璃 | 染色玻璃有、普通玻璃无 | 行/块聚合造成的摩尔纹（单块即可复现） |
| 世界方块玻璃 | 同样有 | "物品独有" |

**唯一相关的贴图特征**（26.3 资源包实测）：

| 贴图 | alpha 结构 | 竖条 |
| --- | --- | --- |
| white / light_gray / gray / black `_stained_glass` | 两级（边框 163 / 内部 102） | 有 |
| `glass` | 二值（0 / 255） | 无 |
| `ice` | 单一（全 102） | 无 |

结论：缺陷与"同一面内出现两种 alpha 值"相关。由于 rect / UV / 图集内容 / mip / OIT 算法均已排除，剩余解释是**该面实际采样的区域覆盖了贴图多次重复**（1 像素边框在屏幕上被复现成 8–10 根竖线）；二值 alpha（玻璃）与单一 alpha（冰）在同样采样下不呈现可辨识条纹，故只有染色玻璃暴露。

**下一步（定案实验）**：导出方块顶点缓冲里的 `uv` 与 `spriteRect`（与物品链路同一手法）：
- `uv` 落在 0..1 且 rect 正常 ⇒ 平铺发生在着色器 UV 展开
- `uv` 落在 0..16 等像素空间 ⇒ 方块网格器 UV 生成错误
### 3.5.16 T-773 根因与修复：PNG 灰度+alpha 解码错位（已解决）

**根因**

26.3 资源包把**接近无彩色的染色玻璃**存成 PNG **colour type 4（灰度+alpha，每像素 2 字节）**，其余贴图是灰度(0)、调色板(3) 或真彩(6)：

| 贴图 | PNG colour type | 现象 |
| --- | --- | --- |
| white / light_gray / gray / black `_stained_glass` | **4（灰度+alpha）** | 竖条纹 |
| `glass` | 6（RGBA） | 正常 |
| `ice` | 3（调色板） | 正常 |
| `stone` | 0（灰度） | 正常 |

`PngDecoder.ReadSample` 计算样本序号时，第一通道用了 `x` 而不是 `x * channelsPerPixel`：

```
index = secondChannel ? (x * 2) + 1 : x        // 修复前
index = (x * channelsPerPixel) + (second ? 1 : 0)   // 修复后
```

于是灰度值隔一半取到了**相邻像素的 alpha**，图集里该 sprite 变成 RGB 与 alpha 交错的条纹。实测证据（图集原始数据，行 8）：

```
修复前: (25,25,25,163) (163,163,163,102) (25,25,25,102) (102,102,102,102) ...
修复后: (25,25,25,163) (25,25,25,102) ×14 (25,25,25,163)
```

**为什么只有这四种**：只有它们被资源包以灰度+alpha 存储；红/蓝等彩色玻璃是调色板或真彩，解码路径正确。冰与普通玻璃同理不受影响。方块与物品共用同一图集来源，所以两者同时出现竖条。

**修复与回归**（并新增资源格式审计：`Sharpcraft.ItemRender.Audit --textures` 统计包内 3964 张 PNG 的 11 种 colourType×bitDepth 组合并逐文件校验解码，其中 type 4 共 **401 张**，即此前全部解码错）

- `PngDecoder.ReadSample` 增加 `channelsPerPixel`，灰度+alpha 两通道都按 `x * 2` 取样。
- 回归测试 `PngDecoderTests.GrayAlphaKeepsTheGrayOfItsOwnPixel`：合成 4×1 灰度+alpha PNG，断言 `(25,25,25,163)(25,25,25,102)(25,25,25,102)(25,25,25,163)`。
- 实机验证：黑/白/红染色玻璃方块与物品均为均匀半透明，无竖条。

**排查过程中被排除的方向**（保留以免重复走弯路）：物品图集 rect 跨版本缓存、图集 padding/mip 串色、参考 OIT 重建误差、子通道重复绘制、greedy 合并平铺。它们各自与本缺陷无关，但 T-774（图集统一）与 T-771/T-772（物品 OIT）本身仍是正确改进。
## 7.1 已知刻意的简化（必须后续解决）

对照对象为 26.3 原版（`ref://mc-26.3-analysis/src/...`）。逐项登记：原版行为 → 当前实现 → 差异 → 状态。

| ID | 简化项 | 原版对照（`ref://mc-26.3-analysis/`） | 原版行为 | 当前实现 | 差异 / 等价性缺口 | 状态 | 计划 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| SIM-ITEM-MODEL-STATIC-FALLBACK | item model 分支求值 | `src/net/minecraft/client/renderer/item/ItemModelResolver.java`、`.../item/` 下 `select`/`condition`/`range_dispatch` 各 `ItemModel` 实现 | 每个 item model 按当前 `ItemStack`/组件/实体/关卡状态**求值**分支，光这条路径就分 `list`/`select`/`condition`/`range_dispatch`/`composite`/`special` 多级 | `ItemModelResolver.ResolveElement` 只做**静态**分支：优先 `fallback`、否则首个 `cases`/`entries` 子模型 | `minecraft:using_item`、`use_duration`、`charge_type`、`crossbow/pull`、`local_time`（`MM-dd`）、`has_component`、`display_context`、`block_state` 等 property 全部未求值；弓/弩/时钟/指南针/蜂巢等持有状态永远渲染默认姿态。**举盾（部分已解决）**：`condition(minecraft:using_item)` 现已求值——`ItemModelResolver.Resolve(itemId, usingItem)` 按状态选 `on_true`/`on_false`，`EntityRenderState.IsUsingItem` 已由 `Presentation.IsUsingItem`（metadata index 8 bit 0）提供，`ResolveHeldItemModel` 会传入；测试 `ItemModelUsingItemConditionTests` 覆盖（含"其他 property 仍走 false 分支"的守卫）。**举盾已接入**：special 盾牌图层的常量变换改为按状态解析（`EntityModelDefinition.LocalTransformSelector`），用 `using_item` 选择 `shield` / `shield_blocking` 的 display 变换，再叠加 `scale(1,-1,-1)` 与锚点；`BLOCK` 手臂姿态本就已实现，因此持有者进入使用状态时盾会随姿态盖到身前 | 规格已就绪 | `T-631e` |
| SIM-ITEM-RENDER-STATE | item 渲染状态 | `src/net/minecraft/client/renderer/item/ItemStackRenderState.java` | 持有 `displayContext`、`LayerRenderState[]`（每层含 quads / itemTransform / foilType / tintLayers / specialRenderer / extents / usesBlockLight / particleMaterial）、`animated`、`oversizedInGui`、模型包围盒 | 仅有 `ItemModelResolution(ModelId, Textures, Display, SpecialRendererType, SpecialProperties)` 描述符 | 没有层列表、`FoilType`（NONE/STANDARD/SPECIAL）、染色层、粒子材质、`visitExtents`/`getModelBoundingBox`、`oversizedInGui`；无法表达双层物品（如 potion 的 `layer0` + `potion_overlay`）与 glint | 规格已就绪 | `T-631e` |
| SIM-ITEM-HELD-GEOMETRY | 手持物品锚点与几何 | `src/net/minecraft/client/renderer/entity/layers/ItemInHandLayer.java`、`.../model/effects/SpearAnimations.java` | `translateToHand(state, arm)` → `rotateDegrees(XP, -90)` → `rotateDegrees(YP, 180)` → 平移 `(±1, 2, -10)/16`（成人）或 `(0, 1, -4.5)/16`（baby 且非 armor stand）；STAB 挥击再叠加 `thirdPersonAttackItem` | `HeldItemMesher` 复用**与掉落物同一套**物品几何（`ItemModelResolver` 解析 → `DroppedItemMesher.CollectQuads` → `EmitQuads`），世界矩阵 = `anchor × PartWorldMatrix(arm) × EntityModelTransform`；每只手的 `thirdperson_righthand` / `thirdperson_lefthand` display 变换由模型 JSON 提供（箱子实测 scale 0.375）。原先的固定 8×8 quad 与 `HeldItemModelDefinition` 已删除 | ①**已解决**：STAB 的 `thirdPersonAttackItem` 偏移已按原版顺序 `D · STAB · anchor · H` 接入（`SpearAnimations.ThirdPersonAttackItemMatrix`，真值见 `StabProbe.java`、测试 `SpearStabOffsetTests`）；②`animateUseItem` 经核对**只有 SPEAR 姿态重写**（其余为空实现，拉弓/举盾/进食由 arm pose 完成），故仅长矛使用姿态待补；③special 模型（trident/shield/chest…）仍走各自的手持图层，与本路径分离；④**已解决**：锚点现由原版世界矩阵反解为 `T(T_off)·S(s)·Rx(−90°)` 的等价形式（`ReferenceFrame.HeldItemAnchor`），并用**两姿态 × 两 display** 共四组真值验证同一锚点全部命中（`HeldItemPlacementComparisonTests`），不再依赖具体 display | 进行中 | `T-765` |
| SIM-ITEM-SPECIAL-MODELS | special 模型覆盖 | `src/net/minecraft/client/renderer/special/SpecialModelRenderers.java`（注册 13 种：bell/banner/book/conduit/chest/copper_golem_statue/head/player_head/shulker_box/shield/trident/decorated_pot/end_cube） | 13 种 `SpecialModelRenderer` 各自烘焙模型并提交几何 | 已移植 10 种：trident/shield/head/player_head/chest/shulker_box/banner（本体+旗面）/decorated_pot（base+sides）/conduit/end_cube | 缺 `bell`/`book`/`copper_golem_statue` 三种；`banner` 未做 `attachment`（ground/wall）与摆动相位，`decorated_pot` 未读 `POT_DECORATIONS` 选 side 图案，`end_cube` 用 `entity/end_portal/end_portal` 贴图近似原版 `endPortal`/`endGateway` shader | 规格已就绪 | `T-631e` |
| SIM-ITEM-SPECIAL-STATE | special 参数与组合 | `.../special/ChestSpecialRenderer.java`、`.../special/ShulkerBoxSpecialRenderer.java`、`.../special/ShieldSpecialRenderer.java`、`.../blockentity/BannerRenderer.java` | `ChestSpecialRenderer.Unbaked(texture, openness, chestType)` 按 `chest_type` 烘焙单体/左/右模型并 `setupAnim(openness)`；`ShulkerBoxSpecialRenderer` 用 `openness` 抬升 lid；`ShieldSpecialRenderer` 依 `BannerPatternLayers`+`BASE_COLOR` 选基准 sprite，`BannerRenderer.submitPatterns` 逐层以 `bannerPattern` 渲染类型 + dye 色再提交同一模型 | `banner_patterns`/`base_color` 已解码并透传到 `EntityRenderState`；盾牌按 `hasPatterns` 选 sprite；**图案叠加层已实现**：banner 旗面/shield 各烘焙 6 份共面副本，逐层用 `entity/banner/<asset>`、`entity/shield/<asset>` sprite + `DyeColors` tint，按层索引 `VisibleWhen` 裁剪；`GreaterEqual` 深度模式已验证支持共面 | **已核实（仅限物品形态）**：26.3 全部 `assets/minecraft/items/*.json` 中 `openness` / `attachment` / `chest_type` 出现次数均为 **0**（对 `items/*.json` 逐文件 grep），因此**物品 special 渲染器**恒取缺省 `0.0F` / `ground` / `single`，当前静态烘焙对物品形态等效。**世界内的箱子/潜影盒是另一条路径**：`ChestRenderer` 用 `ChestBlock.opennessCombiner(blockEntity).get(partialTicks)`、`ShulkerBoxRenderer` 用 `blockEntity.getProgress(partialTicks)` 在运行时取开合度，本仓方块实体目前只有 `BlockEntityPlaceholderMesher` 占位盒（T-241），**没有任何真实箱子/潜影盒几何与开盖动画**，属于独立的方块实体建模任务（T-759）而非 `openness` 细节；届时还需为网格 DSL 补 `visibleFaces` 逐面隐藏能力（原版双箱 `allOfEnumExcept(EAST/WEST)`、饰纹陶罐侧面 `EnumSet.of(NORTH)` 依赖它，见 T-760） | 规格已就绪 | `T-631e` |
| SIM-ITEM-ATLAS-VALIDATION | item atlas 验收 | `src/net/minecraft/client/renderer/texture/ItemTextureAtlas.java`（原版 item 纹理走独立 atlas 与 sprite 系统） | 原版 item sprite 由 atlas 打包 + sprite 元数据决定 UV | 自研 `ItemAtlas` 网格打包，按可见实体主/副手纹理**按需增量**加入 | 无真实资源包下的 UV/尺寸/missing sprite 回归；`WithTexture` 每次重建整张图，未做与原版 atlas 的逐 sprite 对照 | 规格已就绪 | `T-631e` |
| SIM-ITEM-DROP-DISPLAY-GUI | 掉落物 / 展示框 / GUI | `.../item/ItemDisplayContext.java`（10 个上下文）、`.../entity/item/ItemEntityRenderer.java`、`.../item/ItemEntityRenderState.java`、`.../resources/model/cuboid/ItemModelGenerator.java`、`.../entity/ItemFrameRenderer.java` | 同一套 item model 在 10 个 display context 下复用；掉落物用 `getRenderedAmount(count)`（1/2/3/4/5 分桶）、`bob = sin(age/10 + bobOffs)·0.1 + 0.1`、`minOffsetY = -bbox.minY + 0.0625`、`getSpin = age/20 + bobOffs`、厚薄两种堆叠布局；`bobOffs` 是 `random.nextFloat()·2π`（本地随机、不同步）；生成型物品两面均 `addUnculledFace` 且背面 UV 反向，另有 `bakeSideFaces` 按 sprite 边缘挤出侧面 | **掉落物已接**：`DroppedItemMesher` 解析模型 → `ItemModelGeometry` 出 quad → `ItemModelExtents.ForDisplay` 取包围盒 → `ItemEntityRenderer` 的出 Bob/Spin/接地/堆叠 → `ItemDisplayTransform.ToMatrix()` 消费 `ground` 变换，作为 `dropped_item` 图层追加。`getRenderedAmount` 分桶与生成型物品的前后两面 UV 已逐项对上，**真实服务器截图确认**自转与单剑外观正确 | ① **special 模型（trident/shield/chest 等）在掉落物路径不绘制**，只画 `elements` 与生成型精灵；② 元素 `rotation` 未施加；③ `tintindex` 染色**仅掉落物路径**生效，且只支持 `minecraft:constant` 与 `minecraft:grass`（`dye`/`potion`/`leaves` 等来源未实现）；手持路径已接 tint（`HeldItemModelDefinition` 的手持立方体带 `TintSelector`，按主/副手解析 `tintindex 0`）；④ 抖动随机数用「实体 id 派生」的稳定值（原版用渲染器 `random`，逐次可能不同）——分布一致但取值不同；⑤ 展示框与 GUI 仍未实现；⑥ `bakeSideFaces` 侧面已实现，但只扫描静态首帧（原版遍历 `getUniqueFrames`，动画 sprite 的其余帧未挤出面） | 规格已就绪 | `T-761` |

<!-- simplification:SIM-ITEM-MODEL-STATIC-FALLBACK task:T-631e -->
<!-- simplification:SIM-ITEM-RENDER-STATE task:T-631e -->
<!-- simplification:SIM-ITEM-COMPOSITE-FIRST task:T-766 -->
<!-- simplification:SIM-ITEM-HELD-GEOMETRY task:T-765 -->
<!-- simplification:SIM-ITEM-SPECIAL-MODELS task:T-631e -->
<!-- simplification:SIM-ITEM-SPECIAL-STATE task:T-631e -->
<!-- simplification:SIM-ITEM-ATLAS-VALIDATION task:T-631e -->
<!-- simplification:SIM-ITEM-DROP-DISPLAY-GUI task:T-761 -->

这些简化不得以“已完成”收口；在视觉/真值验收前对应进度保持 `进行中`。解决后从代码移除 `[Simplification]` 标记，但本节的记录与上面的机器标记 MUST 保留。

#### 3.5.9 掉落物布局：第一件不偏移

原版 `ItemEntityRenderer.submitMultipleFromCount` 对**第一件**绘制物品不加任何偏移：

- 厚模型（`modelDepth > 0.0625`，床/方块属于此类）：`item.submit(...)` 直接提交；只有第 2 件起才 `translate(xo, yo, zo)`，量级 `±0.15`
- 薄模型：先 `translate(0, 0, -(offsetZ * (count-1) / 2))` 沿 Z 起排，第一件同样无 X/Y 偏移；第 2 件起叠加 `±0.15*0.5` 的 X/Y 抖动

我们此前对**第一件**也套用了偏移。该偏移在行向量链中位于 `Ry(spin)` 之前：

```
world = display · T_offset · Ry(spin) · T(0, bob+minY, 0) · T(entityPos)
```

⇒ 被旋转扫成圆周，物品看起来"绕长边公转"而非自转；偏移量取 `RandomUnit(entityId, index, axis)` 哈希，因此**每次召唤随机出现**。修复为 `ItemEntityRenderer.ItemOffset(...)`：`index == 0` 时恒返回零向量，回归见 `DroppedItemLayoutTests`（5 项）。
### 3.5.17 掉落物不走实体模型路径（T-763）

原版 `ItemEntityRenderer extends EntityRenderer<ItemEntity, ItemEntityRenderState>`：`submit` 里只做 `translate(0, bob + minOffsetY, 0)`、按 `ItemEntity.getSpin` 自旋、再 `submitMultipleFromCount` 提交**物品模型**（与手持/GUI 同一套模型管线，display 上下文为 `ground`）。它不烘焙任何 `ModelPart` 模型，因此**不存在占位盒回退**。

本仓曾把 item entity 当作普通实体交给 `EntityModelMesher`，模型解析失败后落到 `EntityModelResolver.Placeholder`（14³ 盒子，按 64×64 皮肤 UV 采样实体皮肤图集），于是每个掉落物外面都出现一个"皮肤图集碎片拼贴"的立方体——即用户报告的悬浮彩色碎片。

修法：`EntityModelMesher.BuildInto` 对 `EntityTracker.ItemEntityTypeId` 直接跳过，掉落物几何只由 `DroppedItemMesher` 产出。回归：`ItemEntityModelTests`。

### 3.5.9a 手持锚点：拟合解与审计结论（T-777）

`ReferenceFrame.HeldItemAnchor` 不是逐因子推导，而是**从参考捕获求解**出来的矩阵（两组姿态 × 两种 display 共四组一致）：

```
anchor = T(T_off) · S(modelScale) · Rx(+90°)
成人：线性 0.9·Rx(+90°)，平移 (0.05625, -0.56250, -0.11250)
```

`tools/Sharpcraft.ItemRender.Audit/truth/AnchorAxes.java` 给出参考侧旋转链的基向量像：

```
A_ref = RotX(-90°) · RotY(180°) · T(1/16, 2/16, -10/16)
ex → (-1,0,0)   ey → (0,0,-1)   ez → (0,-1,0)   origin → (-0.0625, 0.6250, -0.1250)
```

**审计结论（保持）**：曾尝试把参考链共轭到烘焙系（`Frame·A_row·Frame⁻¹`）以独立复现该锚点，结果与拟合形不一致（共轭形无缩放且 X 镜像），说明共轭路径不等价于引擎实际的链接方式。完整推导需要端到端捕获参考链（含 `H` 手部矩阵与 display 的完整乘积），成本高于收益。

因此保留现锚点，依据是四组捕获一致 + 实机床/盾牌/长矛验证；`PartWorldMatrixTests` 断言的是**求解值回归钉**，不承担等效性证明。复查条件：出现持物位置偏差报告，或审计工具能输出整链矩阵时重做。

### 3.5.10 物品渲染类型：按贴图 alpha 判定（进行中）

原版判据（已核实）：

```java
// NativeImage.computeTransparency：逐像素分类
alpha == 0        -> hasTransparent
0 < alpha < 255   -> hasTranslucent

// BakedQuad：整件物品选一个渲染类型
itemRenderType = transparency.hasTranslucent()
    ? Sheets.translucentBlockItemSheet()   // RenderTypes.itemTranslucent: 混合 + ALPHA_CUTOUT 0.1 + DepthStencilState.DEFAULT
    : Sheets.cutoutBlockItemSheet();       // RenderTypes.itemCutout:    ALPHA_CUTOUT 0.1，不混合
```

26.3 资源包实测 alpha 分布（决定归类）：

| 贴图 | alpha 0 | 半透明 | 不透明 | 原版归类 |
| --- | --- | --- | --- | --- |
| `block/glass` | 191 | 0 | 65 | cutout |
| `block/black_stained_glass` | 0 | 256 | 0 | translucent |
| `block/ice` | 0 | 256 | 0 | translucent |
| `block/blue_ice` | 0 | 0 | 256 | 不透明（cutout 等价） |

已实现：`ItemAtlas` 记录每个 sprite 的半透明标记（`HasTranslucentPixel` ↔ `computeTransparency`），掉落与手持各拆 cutout / translucent 两层。

**已完成**：①物品 cutout 用独立 `EntityGeometryLayerKind.ItemCutout` + `fs_item_cutout`，alpha 测试 0.1（原版 `RenderPipelines.ITEM_CUTOUT` 的 `ALPHA_CUTOUT`），实体本体仍 0.5；②半透明层沿用 `Translucent`，其状态（混合 + 深度写入开 + 不剔除 + alpha 0.1）与原版 `ITEM_TRANSLUCENT` 一致。

**已实现**：①半透明物品使用**独立的参考 OIT 集合**（`itemDepthBounds/coeff/accumulate` 四张目标 + 独立绑定组 + 独立合成），与地形互不污染，对应原版 `OitPipelineSet("item")`；②半透明物品接入**参考 OIT**——`EntityGeometryLayerKind.ItemTranslucent` 图层在主 pass 跳过，改由 OIT 三阶段（depth bounds / transmittance / accumulate）以 `DrawItemOitGeometry` 绘制，复用 `ref*` 管线（其顶点布局即实体 56 字节布局）与物品图集绑定组；②**对象式贴图项**：`{"sprite": "...", "force_translucent": true}` 现在被解析（对应原版 `Material(sprite, forceTranslucent)`），此前只认字符串导致 `#all` 无法解析、玻璃六面贴图全为 null 而完全不显示；③`force_translucent` 直接决定 translucent 归类（原版 `FaceBakery` 据此打 `FLAG_TRANSLUCENT`）。实机：冰（OIT 后无叠加加深）、玻璃、黑玻璃、手持玻璃均正常。

**遗留**：原版 `RenderTypes.ITEM_TRANSLUCENT` 挂了 `setOitPipelines(RenderPipelines.OIT_ITEM)` 且 `sortOnUpload()`，即半透明物品走 **OIT**（与方块/流体同一套参考 OIT）；我们直接在主 pass 用普通 alpha 混合，立方体多个面互相叠加 ⇒ 出现「有的面比其他面更不透明」。修法：把物品半透明图层接入现有参考 OIT pass（`oit_reference.wgsl` + `OitDepthBounds`），而不是主 pass 混合。
### 3.5.11 半透明物品接入参考 OIT（设计，待实现 T-771）

**原版依据**

```java
// RenderTypes.ITEM_TRANSLUCENT（Sheets.translucentBlockItemSheet / translucentItemSheet 的底层）
RenderSetup.builder(RenderPipelines.ITEM_TRANSLUCENT)
    .setOitPipelines(RenderPipelines.OIT_ITEM)
    .withTexture("Sampler0", texture).useLightmap().useOverlay()
    .sortOnUpload()
    .createRenderSetup();

// RenderPipelines.ITEM_TRANSLUCENT
.withShaderDefine("ALPHA_CUTOUT", 0.1F)
.withColorTargetState(new ColorTargetState(BlendFunction.TRANSLUCENT))
```

即：半透明物品**走参考 OIT**（与方块/流体同一套 depth-bounds + 透射率 + 累积），并在上传前按视距排序。我们目前在主 pass 里直接 alpha 混合，立方体多面重叠会叠加，表现为"有的面更不透明"。

**目标**

物品半透明几何与方块/流体共用同一条 OIT 主路径（原则 14 单轨），不再主 pass 混合。

**改造点（草案）**

1. **着色器**：新增 `oit_item.wgsl`（或复用 `terrain.wgsl` 的实体采样 + `oit_reference.wgsl` 的三阶段）：
   - 顶点输入沿用实体/物品 56 字节布局（`VertexLayout.Attributes()`，含 `spriteRect`）；
   - `fs_item_depth_bounds` / `fs_item_transmittance` / `fs_item_accumulate` 的出参与 `oit_reference.wgsl` 一致，仅把采样换成物品图集 + `spriteRect` → 图集 UV（同 `fs_entity_translucent`）；
   - alpha 测试 0.1（与 `ITEM_TRANSLUCENT` 一致）。
2. **绑定组**：`group0` = 物品图集（texture+sampler）+ camera + fog + lightmap（与实体物品图层相同）；`group1` = OIT 目标（与 `ref*` 一致）。参考 `CreateDepthBoundsPipeline/TransmittancePipeline/AccumulatePipeline` 的布局参数，新增物品变体。
3. **图层标记**：`EntityGeometryLayerKind` 增加 `ItemTranslucent`，主 pass 的绘制 switch 跳过它；OIT pass 新增一个 draw set（depth bounds → transmittance → accumulate），复用 `DynamicGeometry` 上传路径。
4. **Mesher**：`DroppedItemMesher` / `HeldItemMesher` 的半透明图层改用 `ItemTranslucent`（当前是 `Translucent`）。
5. **排序**：原版 `sortOnUpload()`；参考 OIT 对顺序不敏感（depth bounds + 透射率），可先不排序，实测若仍有差异再补。

**验收**：冰 / 黑玻璃 / 染色玻璃在掉落与手持下与正版同机位截图一致（面与面之间无叠加加深）。
### 3.5.12 染色玻璃竖条：sprite rect 高度错用 cell 高度（T-773，已定位待修）

**现象**：物品级染色玻璃近距离出现细竖条，且只有白 / 淡灰 / 灰 / 黑四种明显。

**证据链**

1. 源贴图与图集内容正确：`black_stained_glass` 源图与图集区域逐像素一致（`163,102×14,163`）。
2. 六面 UV 均在 [0,1]，无平铺。
3. 关掉 OIT（按 cutout 主通道绘制）后条纹**原样存在** ⇒ 与透明合成无关。
4. 顶点导出（`DynamicGeometry.Vertices`，14 float/顶点）：
   ```
   sprite = (0.5000, 0.5000, 0.6250, 0.6250)   → 128×192 图集内 (x=64, y=96, w=16, h=24)
   实时查表 itemAtlas.Get("block/black_stained_glass") = (0, 128, 16, 16)
   ```

**结论**

- 图集高 192 = 8 行 × **24** ⇒ cell 高被更高的贴图撑到 24；该 sprite 的 rect **高度取了 cell 高度（24）**，而宽度取了贴图宽度（16）。多出的 8 行采样窗口探进下一行 sprite，把邻居贴图串进来。
- 16 张染色玻璃贴图形状相同、仅颜色不同（`textures/block/*_stained_glass.png` 共 16 个，物品模型 `items/<色>_stained_glass.json` 直接指向 `block/<色>_stained_glass`）。因此串色后：彩色玻璃会串成杂色（显眼但不像"条纹"），无彩色四种串成等亮度竖条 ⇒ 只有它们看起来是竖纹。
- 另一处不一致：绘制时使用的 rect 与实时查表结果不同（`(64,96)` vs `(0,128)`），说明形状布局存在**版本不同步**（图集扩容后旧 layout 未失效）。

**修复清单**

1. `ItemAtlas` 的 sprite rect 一律使用**贴图实际宽高**；`cellWidth/cellHeight` 只用于布局，不得进入 `SpriteRect`。
2. 图集重建（新增纹理、尺寸变化）时，失效所有派生的形状/布局缓存。
3. 回归测试：构造高度不一的贴图集合，断言每个 rect 的 `Height == image.Height` 且 `V1 <= (row+1)*cellHeight/atlasHeight`。
### 3.5.13 物品图集并入统一装配器（T-774，已实现）

**动机**

当前存在两套图集装配器，违反原则 14 单轨：

| 图集 | 装配器 | padding | mip | 可变性 |
| --- | --- | --- | --- | --- |
| 方块 | `SpriteAtlasBuilder` + `SpriteAtlas` + `AtlasMipChain` | 2 texel | 有 | 不可变对象 |
| 物品 | `ItemAtlas`（自造） | 0 | 无 | 可变（`WithTexture` 原地扩容） |

物品那套的零 padding 与原地扩容直接导致 T-773（染色玻璃竖条）：几何按旧图集算 rect、旧 rect 用于新图集，且线性过滤采到相邻 sprite。

**原版依据**

- sprite 是不可变对象，UV 在创建时按该图集尺寸算死：`ref://mc-26.3-analysis/src/net/minecraft/client/renderer/texture/TextureAtlasSprite.java:24-36`（`(x+padding)/atlasWidth`，用 `contents.width()` 而非 cell）。
- 拼接留白：`ref://mc-26.3-analysis/src/net/minecraft/client/renderer/texture/Stitcher.java:28` `padding = 1 << mipLevel << clamp(anisotropy-1,0,4)`。
- 图集变化 ⇒ 全新 sprite 实例 + 模型重新烘焙，旧 UV 副本随之消失。

**迁移结果（已实现）**：`ItemAtlas` 内部改为 `SpriteAtlasBuilder.Stitch` 门面，公开 API 未变（调用点零改动）；图集从 128×192 均匀网格变为 2 的幂、每 sprite 带 padding、不可变对象。**注意**：迁移后染色玻璃竖条**仍存在**，说明该缺陷另有根因（rect 已确认正确），继续跟踪于 T-773。

1. `ItemAtlas` 内部改为持有 `SpriteAtlas`（由 `SpriteAtlasBuilder.Stitch` 产出）；公开 API `Get / GetTexture / IsTranslucent / Texture / WithTexture` 保持不变，调用点零改动。
2. 键映射：物品贴图路径（`block/glass`、`item/...`）→ `Identifier`；`SpriteRect` 与物品链路已是同一类型，无需转换。
3. `WithTexture` 改为**返回新实例**（重新 `Stitch`），不再原地扩容；客户端按实例/版本失效几何（等价原版重新烘焙）。
4. `force_translucent` 与 alpha 半透明标记在门面层保留（`IsTranslucent`），底层像素不变。
5. 回归：`T-768` 物品审计工具（1658 物品）全绿 + 掉落/手持代表性截图（床、盾牌、composite、染色玻璃、冰、玻璃）。

**风险**

- 物品图集含非 16×16 与多帧贴图：`Stitch` 已支持任意尺寸与帧数（方块图集同源路径）。
- 图集尺寸会从 128×192 变为 2 的幂方形，纹理坐标随之变化，所有 rect 必须来自新实例（不得跨实例混用）。
### 3.5.14 T-773 结论更正：竖条不在物品链路（已更正）

用户实测：**世界中的方块玻璃（染色玻璃方块）同样显示竖条纹**。由此更正 3.5.12 的结论：

| 原假设 | 现状 |
| --- | --- |
| 物品图集 sprite rect 跨版本错用 | **已排除**：T-774 统一装配器后（2 的幂 + padding 2 + 不可变对象 + 真实尺寸）条纹不变 |
| 与 OIT 无关（关 OIT 后仍出现） | **不成立**：那次实验观察到的很可能是世界里的玻璃**方块**（一直走 OIT），不是物品 |
| 物品独有缺陷 | **不成立**：方块玻璃同样出现 |

共同点：方块玻璃走地形参考 OIT，物品玻璃走物品参考 OIT。两者都经过 OIT 合成，因此竖条指向**参考 OIT 的重建误差**，而非图集或 rect。

已逐项核对**一致**的部分（CPU 参考 `OitWavelet`、WGSL、原版 GLSL 三者算法一致）：

- bin 数 8、小波阶 2、`toAbsorbance/toTransmittance`、`OIT_FULLY_OPAQUE_TOTAL_TRANSMITTANCE = 0.02`
- `normalizeDepth` 分段（含 high-precision 阈值 10）
- encode/sample 的二叉树索引、`select` 语义、`lerpAlpha`

**下一实验（待执行）**：把世界玻璃方块作为唯一变量逐层隔离
1. 临时让半透明地形走主 pass 普通混合（绕过 OIT），观察方块玻璃是否仍竖条 ⇒ 判定是否 OIT；
2. 若确认 OIT：分别把 bounds / 系数图 / accumulate 的目标以调试色输出，定位是 encode 还是 decode；
3. 用 `OitWavelet` 构造"单层平面前向"的解析用例（单层应完全精确），与 GPU 中间结果比对。
### 3.5.15 T-773 判别事实汇总（继续进行中）

三轮对照实验的结论（均以实机截图为准，非推断）：

| 实验 | 结果 | 排除了什么 |
| --- | --- | --- |
| 半透明物品强制走 cutout 主通道 | 条纹不变 | 物品侧的 OIT 集成 |
| 物品图集并入 `SpriteAtlasBuilder`（2 的幂 + padding 2 + 不可变 + 真实尺寸） | 条纹不变 | 图集 rect、padding、跨版本缓存 |
| `textureSampleLevel(..., 0.0)` 强制 mip 0 | 条纹不变 | mip 串色 |
| 单块近距：黑色染色玻璃 vs 普通玻璃 | 染色玻璃有、普通玻璃无 | 行/块聚合造成的摩尔纹（单块即可复现） |
| 世界方块玻璃 | 同样有 | "物品独有" |

**唯一相关的贴图特征**（26.3 资源包实测）：

| 贴图 | alpha 结构 | 竖条 |
| --- | --- | --- |
| white / light_gray / gray / black `_stained_glass` | 两级（边框 163 / 内部 102） | 有 |
| `glass` | 二值（0 / 255） | 无 |
| `ice` | 单一（全 102） | 无 |

结论：缺陷与"同一面内出现两种 alpha 值"相关。由于 rect / UV / 图集内容 / mip / OIT 算法均已排除，剩余解释是**该面实际采样的区域覆盖了贴图多次重复**（1 像素边框在屏幕上被复现成 8–10 根竖线）；二值 alpha（玻璃）与单一 alpha（冰）在同样采样下不呈现可辨识条纹，故只有染色玻璃暴露。

**下一步（定案实验）**：导出方块顶点缓冲里的 `uv` 与 `spriteRect`（与物品链路同一手法）：
- `uv` 落在 0..1 且 rect 正常 ⇒ 平铺发生在着色器 UV 展开
- `uv` 落在 0..16 等像素空间 ⇒ 方块网格器 UV 生成错误
### 3.5.16 T-773 根因与修复：PNG 灰度+alpha 解码错位（已解决）

**根因**

26.3 资源包把**接近无彩色的染色玻璃**存成 PNG **colour type 4（灰度+alpha，每像素 2 字节）**，其余贴图是灰度(0)、调色板(3) 或真彩(6)：

| 贴图 | PNG colour type | 现象 |
| --- | --- | --- |
| white / light_gray / gray / black `_stained_glass` | **4（灰度+alpha）** | 竖条纹 |
| `glass` | 6（RGBA） | 正常 |
| `ice` | 3（调色板） | 正常 |
| `stone` | 0（灰度） | 正常 |

`PngDecoder.ReadSample` 计算样本序号时，第一通道用了 `x` 而不是 `x * channelsPerPixel`：

```
index = secondChannel ? (x * 2) + 1 : x        // 修复前
index = (x * channelsPerPixel) + (second ? 1 : 0)   // 修复后
```

于是灰度值隔一半取到了**相邻像素的 alpha**，图集里该 sprite 变成 RGB 与 alpha 交错的条纹。实测证据（图集原始数据，行 8）：

```
修复前: (25,25,25,163) (163,163,163,102) (25,25,25,102) (102,102,102,102) ...
修复后: (25,25,25,163) (25,25,25,102) ×14 (25,25,25,163)
```

**为什么只有这四种**：只有它们被资源包以灰度+alpha 存储；红/蓝等彩色玻璃是调色板或真彩，解码路径正确。冰与普通玻璃同理不受影响。方块与物品共用同一图集来源，所以两者同时出现竖条。

**修复与回归**（并新增资源格式审计：`Sharpcraft.ItemRender.Audit --textures` 统计包内 3964 张 PNG 的 11 种 colourType×bitDepth 组合并逐文件校验解码，其中 type 4 共 **401 张**，即此前全部解码错）

- `PngDecoder.ReadSample` 增加 `channelsPerPixel`，灰度+alpha 两通道都按 `x * 2` 取样。
- 回归测试 `PngDecoderTests.GrayAlphaKeepsTheGrayOfItsOwnPixel`：合成 4×1 灰度+alpha PNG，断言 `(25,25,25,163)(25,25,25,102)(25,25,25,102)(25,25,25,163)`。
- 实机验证：黑/白/红染色玻璃方块与物品均为均匀半透明，无竖条。

**排查过程中被排除的方向**（保留以免重复走弯路）：物品图集 rect 跨版本缓存、图集 padding/mip 串色、参考 OIT 重建误差、子通道重复绘制、greedy 合并平铺。它们各自与本缺陷无关，但 T-774（图集统一）与 T-771/T-772（物品 OIT）本身仍是正确改进。
## 7.2 其他限制

- 26.3 的 `items/*.json` 与旧版 `models/item` 结构不同；必须按 26.3 的 item model 类型分发，不能只读旧 parent 字段。
- `ItemStackRenderState` 的层/foil/tint 结构未在仓库实现；T-631a 只建立解析数据，不提前造渲染状态。
- 物品 atlas 与 block atlas 分离；不能复用 block atlas 的 sprite 假设。
- 完整物品模型、特殊模型（弓、盾、三叉戟、地图等）不在 T-631a 范围。
