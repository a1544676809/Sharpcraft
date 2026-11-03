<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 实体渲染模块
summary: M5.3 P1 静态实体模型、渲染状态、部件几何与渲染/游戏逻辑解耦
status: 已评审
updated: 2026-11-02
-->

# 实体渲染模块（M5.3 P1）

## 1. 范围

本模块负责把实体渲染状态变为几何并提交渲染，不推进实体模拟、不读取网络、不修改世界。

P1 只做**静态姿态 + 朝向**，明确排除骨骼动画、物品装备渲染、皮肤叠加层的完整像素等效（装备/动画另立项）。

## 2. 解耦边界（硬约束）

原版实体渲染与游戏逻辑解耦：`EntityRenderer` 消费 `*RenderState`，不反向驱动实体。

本项目对应：

- `Common/Entities`：实体模拟、碰撞、移动；不引用渲染。
- `Client/World/EntityTracker`：把网络实体状态整理成 `EntityRenderState`（id、type、位置、yaw、pitch、UUID）；不引用 `Framework.Rendering`。
- `Client/Render`：只消费 `EntityRenderState` + `EntityModelDefinition`，产出中性几何；不读取 `ClientWorld`、不推进 tick。
- `Launcher/WindowCommand`：宿主只做状态到 draw 的装配；不得把实体逻辑写进渲染器。

未来实体渲染扩展（皮肤、装备、动画）也只能通过 `RenderState` 增加字段，不得让渲染器持有 `Entity`/`ClientWorld` 引用。

## 3. 原版模型证据

证据：`ref://mc-26.3-analysis/src/net/minecraft/client/model/HumanoidModel.java`、
`ref://.../model/player/PlayerModel.java`。

`HumanoidModel.createMesh` 的成年玩家部件（单位模型像素，1/16 block）：

| 部件 | 偏移（根坐标） | 立方体 min（相对部件） | 尺寸 |
| --- | --- | --- | --- |
| head | (0, 0, 0) | (-4, -8, -4) | 8×8×8 |
| hat（覆盖层） | 同 head | (-4, -8, -4) | 8×8×8，膨胀 0.5 |
| body | (0, 0, 0) | (-4, 0, -2) | 8×12×4 |
| right_arm | (-5, 2, 0) | (-3, -2, -2) | 4×12×4 |
| left_arm | (5, 2, 0) | (-1, -2, -2) | 4×12×4（镜像） |
| right_leg | (-1.9, 12, 0) | (-2, 0, -2) | 4×12×4 |
| left_leg | (1.9, 12, 0) | (-2, 0, -2) | 4×12×4（镜像） |

### 3.1 UV 与纹理

- 各部件使用实体皮肤图集的 `texOffs`：head `(0,0)`、body `(16,16)`、right_arm `(40,16)`、left_arm `(40,16)`（mirror）、right_leg `(0,16)`、left_leg `(0,16)`（mirror）。
- 立方体每面按 vanilla `CubeListBuilder` 的 UV 展开规则映射；UV 必须按实际皮肤纹理坐标，不可用占位色代替最终外观。
- 覆盖层（hat/jacket/sleeves/pants）在 P1 只保留模型结构，默认可见性由 `RenderState` 的部件可见性位控制；P1 先启用基础部件。
- 纹理由启动器从 jar 提取，仓库不存资产；提取失败时资源层必须给可诊断错误，不用硬编码 skin。

### 3.2 姿态

- P1 仅使用 `Yaw`/`Pitch` 做根节点朝向；`PlayerModel.setupAnim` 的骨骼动画不在 P1。
- 部件按上表偏移叠加到实体脚点；根旋转先 Yaw 后 Pitch（与现有 `LookConvention` 保持一致）。
- 名字牌锚点必须使用模型计算结果中的**头部/附着点**，不得再用占位的 `PlayerHeight + 0.5` 硬编码。

## 4. 数据与类型

建议新增（名字可在实现评审时调整）：

- `EntityModelDefinition`：`Parts[]`，每个 `Part` 有 `Name`、`Parent`、`Offset`、`Cubes[]`、`VisibleByDefault`。
- `EntityModelCube`：`Min`, `Size`, `TexOffset`, `Mirror`, `Inflate`。
- `EntityModelResolver`：按 `TypeId` 返回模型定义（P1 至少 player；其它 type 回退占位）。
- `EntityModelMesher`：`Build(ReadOnlySpan<EntityRenderState>, EntityModelDefinition, TextureRegion skin, pose)` → `DynamicGeometry`/中性顶点。

所有类型位于 `Client/Render`，不得位于 `Common/Entities`；`Framework` 只接受中性几何/纹理。

## 5. 渲染与纹理

- 实体纹理单独图集（skin atlas）或按材质分图集；P1 先接入一张 skin 纹理，图集容量不足时再评估纹理数组（pipeline §7.3）。
- 使用现有实体/方块兼容的顶点布局（位置 3 + 颜色 3 + UV 2 + 光照 2/共享顶点格式），保证可复用现有地形管线。
- P1 输出静态部件一次构建、按实体 draw；不引入动画顶点重算。

## 6. 测试与验收

1. 模型定义数值测试：每个部件的 offset/cube/UV 与 `HumanoidModel.createMesh` 逐值一致；
2. 网格测试：每盒 6 面、24 顶点/36 索引、镜像 UV 正确；
3. 渲染状态测试：`EntityRenderState` 只读消费，渲染层不得引用 world/entity 模拟类型（架构测试）；
4. 固定机位双客户端：两个玩家模型可见，名字牌锚点来自真实头部高度；与占位盒体对照记录偏差；
5. 纹理缺失/损坏有可诊断回退，不崩溃。

## 7. 当前状态

- [x] 渲染状态输入 `EntityRenderState` 与视锥剔除（T-344）
- [x] 世界空间名字牌（T-351）
- [x] 玩家静态模型定义/网格/纹理接入
- [x] 变体（variant）系统：`set_entity_data` 解码 + 注册表驱动的纹理/模型选择（T-467）
- [ ] 名称牌高度改为真实模型 `nameTagAttachment`
- [ ] 其它实体类型模型（P1 后）

## 8. 变体（variant）

行为规格见 `docs/analysis/render/entity-variants.md`；本节只记模块契约。

- **入口**：`EntityRenderState.VariantId` 是线上原始整型值（注册表下标或枚举 id），`EntityTracker.ApplyEntityData`
  从 `set_entity_data`（clientbound 101）写入；字段缺失即默认变体（原版只发非默认值）。
- **解析**：`EntityVariantSpecs`（`Client/World`）给出每类实体的访问器索引、注册表路径、名字表与默认名；
  `EntityVariantTable`（`Client/Render`）把值解析成名字再查 `EntityModelResolver` 的变体定义。
- **注册表优先**：configuration 阶段的 `registry_data` 到位时以服务器的条目顺序为准（位置即 id）；没有注册表数据时
  回落到原版 bootstrap 顺序，因此离线/沙盒可测，但数据包新增或重排变体不会被识别。
- **纹理与几何**：变体定义各自带 `TexturePath`，并随 `EntityModelResolver.Definitions` 一起进 skin atlas；
  Pig/Cow/Chicken 的 warm/cold 是**几何变体**（ColdPig/WarmCow/ColdCow/ColdChicken），不是换图。
- **未知变体**：渲染路径回落默认变体并记录 `EntityVariantTable.UnknownVariants`，不崩溃；
  `ResolveVariantStrict` / `ResolveStrict` 抛出 `KeyNotFoundException`，供工具与自动化把缺口变成失败。
- **不覆盖**：幼年模型与 baby 纹理、Wolf（tame/angry 三态）、Fox（睡眠态）——见分析文档 §5。

## 9. 父子层级（Parent + 局部 PartPose）

- `EntityModelPart.Parent` 指向同一定义里的父部件；子部件的 `Pivot/CentreX/Bottom/CentreZ` 与旋转都按
  **相对父原点**解释，由 `PlayerModelDefinition.FromVanillaChild` 生成（只做全局 X 镜像与 Y 翻转，不加 modelHeight 偏移）。
- `EntityModelMesher` 为每个部件算一个 `Matrix4x4`：`PartFrame(part) = R(part) · T(pivot) · PartFrame(parent)`；
  根部件的 cube 仍是绝对坐标，所以根的存储矩阵再左乘 `T(-pivot)`（等价于绕 pivot 旋转）。
- 空 group 节点（宽高深全 0）只承载 pivot，不发射几何；Wolf 的 `head`/`tail` 就是这种节点。
- 验收：`EntityModelHierarchyTests` 用 head_parts→head→right_ear 三层链与独立手算包围盒逐轴比对；其余根旋转部件由
  `TheColdCowHornsSitAtTheVanillaPositionAfterTheRotationSignConversion` 钉住。
- 已迁移：Horse / Fox / Wolf / Rabbit / Camel（Rabbit、Camel 为本轮新增注册；Camel type id 19 由 `EntityTypes` 注册序推导，
  与既有 id 交叉验证）。



## 5.1 纹理坐标证据（避免再次猜测）

- `PngDecoder` 明确输出 **row-major RGBA8**，首行是图像顶部（`docs/.../PngDecoder.cs` 的 `RgbaImage` 注释）；
- `SpriteAtlasBuilder` 明确约定 `V0` 是 sprite 的顶行、mesh 的 `v=0` 是面的顶边（`src/Sharpcraft.Client/Assets/SpriteAtlas.cs` 注释）；
- 地形 `terrain.wgsl` 使用同一 `TextureData`/`SpriteRect`/`textureSample` 路径，块纹理显示正常，说明 WGPU 纹理坐标 `(0,0)` 顶部与本地图像行序一致；
- 原版 `ModelPart.Polygon` 的 UV 赋值是：
  `vertices[0]=(u1,v0)`、`vertices[1]=(u0,v0)`、`vertices[2]=(u0,v1)`、`vertices[3]=(u1,v1)`；
- 因此 skin 不应做全局 `1-v` 翻转；之前该做法把面采样到透明区域，导致玩家消失。正确做法是保持 `SpriteRect` 顶部约定，按 `ModelPart.Polygon` 的顶点顺序生成 UV。

## 10. RenderLayer 提交

- 基础模型与 clothing → `Cutout`；Player overlays / Drowned outer / Slime outer → `Translucent`；眼睛 → `Emissive`。
- 契约与状态见 `docs/decisions/0021-entity-render-layers.md` 与 `docs/analysis/render/entity-render-layers.md` §6。
- 眼睛：`EntityModelDefinition.EmissiveTexturePath` 提供纹理（Enderman `enderman_eyes`、Spider `spider_eyes`），
  与基础几何共用，走 emissive 管线（无光照、深度 `>=`、不写、半透明混合）。

## 11. 部件可见性（`VisibleWhen`）

远端实体有些状态会**切换整组部件**，典型是犰狳：缩壳时 `body`（连同 head/tail）与四条腿隐藏，只显示 `cube`；出壳时相反。

- `EntityModelPart` 新增 `Func<EntityRenderState, bool>? VisibleWhen`，`EntityModelMesher` 在发射部件前求值，false 则整部件（含其几何与 overlay）跳过。
- 状态布尔放在 `EntityRenderState`（如 `IsHidingInShell`），由 `EntityTracker` 从同步数据计算；渲染层不读取同步字段。
- 犰狳状态来源：`ARMADILLO_STATE` 是枚举序列化器（serializer id 36），访问器 18，值 0 IDLE / 1 ROLLING / 2 SCARED / 3 UNROLLING。隐藏条件按原版 `ArmadilloState.shouldHideInShell`：
  - IDLE：永不隐藏；
  - ROLLING：进入状态 **>5 tick** 后隐藏；
  - SCARED：立即隐藏；
  - UNROLLING：进入状态 **<26 tick** 时仍隐藏，之后展开。
- 因此 `TrackedEntity` 记录 `StateStartTick`（状态值变化时更新为当前 `GameTime`），供上述时序判定使用。

> 可见性只决定“是否发射几何”，不改变碰撞/音效等其他逻辑；与 RenderLayer 正交。

## 12. 部件级纹理尺寸与 Emissive-only

- `EntityModelPart` 新增 `TextureWidth/Height`：UV 归一化按**该部件所属 layer 声明的纹理尺寸**（0 表示沿用模型默认）。Breeze 的 body 是 32×32、wind 是 128×128，必须按部件区分。
- `IncludeInEmissive`（默认 true）控制该部件是否参与 emissive 眼睛 pass；wind 层设 false，避免眼睛贴图落到风壳上。
- `EmissiveOnly`（默认 false）表示该部件**只在 emissive pass 发射**（Breeze 的 `eyes` 复制了 head 几何，只用于眼睛层）。
- 这些字段只影响几何/纹理发射，不改变同步状态契约。

## 13. 部件级 emissive 纹理（多发光层）

- `EntityModelPart` 新增 `EmissiveTexturePath`：该部件在 emissive pass 使用自己的发光贴图；为 null 时回退到模型的 `EmissiveTexturePath`。
- `EntityModelDefinition.TexturePaths` 自动收集部件级 `TexturePath` 与 `EmissiveTexturePath`，skin atlas 会一并加载。
- `EmissiveOnly = true` 的部件只在 emissive pass 发射，因此可以“复制一份基础几何”挂在同一父级下，专门承载某一层发光贴图，不参与普通绘制。
- Warden 用此机制表达四层：bioluminescent、pulsating spots（本实现固定 spots_1）、heart、tendrils（纹理复用 `warden`）。真正的逐层 alpha/脉冲动画仍需动画系统。

## 14. 顶点 tint 与热带鱼

- `EntityModelPart.TintSelector` 返回每部件颜色乘子；未设置时使用 `EntityRenderState.Tint`（default 视为白色）。
- 顶点色（vertex colour）由 mesher 写入 tint，`fs_entity_cutout` 用 `input.colour` 与 lightmap 相乘，等价于原版模型 tint。
- 热带鱼用两个乘子：`Tint`（baseColor，作用于基础层）和 `PatternTint`（patternColor，作用于 pattern 复制层）。pattern 复制层与基础几何同顶点、同 draw 顺序，`LESS_EQUAL` 深度让后绘制者覆盖。
- 变体键不按线性索引：`EntityVariantSpec.KeySelector` 可把原始 packed 值映射成名称；热带鱼 packed 值低 16 位是 pattern（含 base id），16..23 是 baseColor id，24..31 是 patternColor id。颜色表来自 `DyeColor.getTextureDiffuseColor()`。

## 15. 幼年模型（IsBaby）

- `EntityRenderState.IsBaby` 由 `EntityTracker` 从同步数据 index 16 的 `AgeableMob` baby 布尔解析（只对 `AgeableTypes` 白名单类型生效，避免把 Ghast charging 等 index 16 误判）。
- `EntityModelResolver` 增加幼年字典：`Resolve(typeId, baby)`、`TryResolveVariant(typeId, key, baby)`；`EntityVariantTable.Resolve(..., baby)` 据此选择。
- 幼年几何多数是独立 `Baby*Model`（不是简单缩放）；幼年纹理使用 `*_baby` / `*_*_baby`，与成年变体键一一对应。
- 已铺：baby pig / cow / chicken（含 warm/cold 幼年纹理）。其余 Baby* 逐批补。
