<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[已评审]
基线：Minecraft 26.3
证据：`docs/analysis/render/entity-equipment.md`
最后更新：2026-11-06

# 实体装备与物品层

把 `set_equipment` 的装备槽状态转成实体渲染状态，并为鞍、马甲、装饰、挽具、狼甲等提供独立的装备渲染层；手持物品模型渲染后置。

## 1. 职责范围

负责：

- 解析 `SetEquipment`（id 104）中的装备槽与物品身份（含完整 `DataComponentPatch`；99 个原版组件均有解码路径）；
- 维护每个实体的装备槽状态（含解析出的组件子集），并提供给渲染层；
- 为装备资产建立“物品身份 + 层类型 → 纹理/模型”的受控映射（第一阶段静态表）；
- 在实体模型之上生成装备层，复用现有 `EntityModelDefinition` 部件副本、tint 与可见性能力；
- 提供沙盒/测试可设置装备状态的显式入口。

明确不负责：

- 物品栏、容器 UI、物品拾取与网络交互；
- 完整物品模型/方块物品渲染（Witch 手持药水后置）；
- 组件语义的**效果执行**（耐久扣除、附魔数值作用、食物回复等；客户端只解码展示所需字段）；
- 装备伤害/耐久逻辑（服务端权威，客户端只呈现状态）。

## 2. 侧别归属

- 归属：`Common` 负责 `SetEquipment` 包字节解码与槽位枚举；`Client` 负责装备状态缓存、装备资产映射与渲染层。
- 判定依据：`docs/analysis/render/entity-equipment.md` 2、3；包布局与 `Items`/`EquipmentSlot` 均为协议/共享数据，渲染层只属于客户端。

## 3. 依赖关系

| 方向 | 对象 | 说明 |
| --- | --- | --- |
| 依赖 | `Sharpcraft.Common.Network` | SetEquipment 解码、物品身份读取 |
| 依赖 | `Client.World.EntityTracker` | 把槽位状态挂到实体状态 |
| 依赖 | `Client.Render.EntityModelDefinition` / `EntityModelLayerHelpers` | 生成装备模型副本 |
| 被依赖 | `Client.Render` 实体渲染器 | 读取装备层并绘制 |
| 被依赖 | `tools/Sharpcraft.EntityModelSandbox` | 装备层可视化验收 |

依赖方向自检：`Common` 不引用 `Client`；客户端不持有网络连接或 `Entity` 对象。

## 4. 对外契约

- 接口（概念）：`IEntityEquipmentState` 只读槽位查询；`EquipmentLayerResolver` 把物品身份+层类型解析成纹理/模型描述。
- 数据：`EquipmentSlotKind`（MainHand/OffHand/Feet/Legs/Chest/Head/Body/Saddle）与 `EquipmentItemId`（第一阶段受控整数）。
- 事件/回调：无。装备状态变化由 `EntityTracker` 更新，渲染在下一帧读取。
- 明确不暴露：原始包字节、网络 reader、`ItemStack` 内部组件、WGPU 句柄。

## 5. 关键数据结构

| 结构 | 用途 | 生命周期 | 所属线程 |
| --- | --- | --- | --- |
| `EquipmentSlotKind` | 槽位枚举，对应原版 ordinal 0..7 | 常量 | 任意 |
| `EquipmentEntry` | 槽位 + 物品身份（+空/非空） | 每个实体一份，随实体移除 | 主线程 |
| `EquipmentState` | 8 槽快照，供渲染读取 | 随实体状态 | 主线程（读）/网络线程（写，通过现有同步边界） |
| `EquipmentLayerSpec` | 纹理路径、可见槽位、外扩量、颜色乘子 | 静态/模型级 | 任意（只读） |

## 6. 行为对齐与偏离

对齐点：

- SetEquipment 的字节布局、槽位 ordinal、包 id 104：见 `docs/specs/network/entity-sync.md` 与 `docs/analysis/render/entity-equipment.md` 2。
- 空栈（count ≤ 0）表示没有装备；后续条目沿用 continuation 位。
- `DataComponentPatch` 由 `ItemComponentPatchReader` 解码：覆盖全部 99 个原版组件（文本/NBT、附魔、属性、染色/纹饰、食物/药水/书/容器等）；遇到未知组件 id 时标记 opaque 并安全停止。
- 装备层在原版实体变换/动画下绘制；当前静态阶段沿用 bind pose。

已收敛（原第一阶段偏离）：

- 非空 `DataComponentPatch` 不再降级：`damage`、`charged_projectiles`、`enchantments/quick_charge` 等已作为实体表现状态输入（详见 `docs/modules/entity-animation.md` 批次 12–32）。
- 物品标签（`data/minecraft/tags/item`）由启动器提取并由 `ItemTagRegistry` 解析，支持 `piglin_loved`、`piglin_preferred_weapons` 等判定。

仍存在（后续阶段）：

- 物品/装备映射使用受控静态表，未引入完整 `EquipmentClientInfo` JSON 解析。
- Witch 手持物品不渲染，装备/鞍层优先。
- `tool`/`weapon` 等组件的**数值效果**只解析不执行（服务端权威）。

以上偏离必须登记进受控偏差清单，并在后续阶段逐项收敛。

## 7. 分阶段实施与验收

1. **E1 协议与状态**：解码 id 104，填 `EquipmentState`；单元测试覆盖单槽、多槽 continuation、空槽、完整组件 patch 与未知组件 opaque 降级。
2. **E2 装备资产映射**：受控物品 → 纹理表；单元测试覆盖马甲材质、鞍、装饰、狼甲、快乐恶魂挽具。
3. **E3 渲染层**：为 Horse/Donkey/Mule、Llama、Camel、Nautilus、Happy Ghast、Wolf 生成装备副本层；沙盒通过显式装备参数截图验收。
4. **E4 后置**：`EquipmentClientInfo` 资产化、完整物品模型渲染、Witch 手持物品模型。
5. **E5 人形护甲（已实现 2026-11-05，T-630）**：新增 `HumanoidArmor`/`HumanoidLeggings` 资产类型；`WithArmorSlot` 按槽只克隆对应部件（head / body+arms / body+legs / legs）并施加外(1.0)/内(0.5)变形；Zombie/Drowned/Husk/Skeleton/Stray/Bogged/WitherSkeleton/ZombieVillager 接四槽护甲层。**限制**：材质/槽位按护甲物品名兜底（`equippable` 组件 assetId 尚未解析，模组护甲不渲染）。

## 8. 未解决问题与风险

- 装备资产 `.json` 在现有资产包中缺失，需确认提取来源；否则静态映射无法长期维护。
- 组件解码已覆盖 99 个原版组件，但**部分 predicate/partial 匹配**仍走 opaque 安全停止（仅影响极端自定义物品）。
- 多装备层叠加顺序与鞍/甲冲突需要逐实体核对。
- 静态 bind pose 下装备层与动画层的关系将来需随动画系统统一。

## 9. 参考资料

- `docs/analysis/render/entity-equipment.md`
- `docs/specs/network/entity-sync.md`
- `docs/modules/entity-render.md`
## 10. Elytra 翼层（T-648..T-652 / T-662 / T-663）

### 10.1 原版 26.3 源码映射表

| 本模块概念 / 方法 | 原版类与方法路径（`ref://...`） | 精确常量 / 公式 / 树结构 | 位序 / 正负号 / 色彩空间约定 | 缺省 / 越界回退值 |
| --- | --- | --- | --- | --- |
| `WithElytra` 成人翼盒 | `ref://mc-26.3-analysis/src/net/minecraft/client/model/object/equipment/ElytraModel.java#createLayer` | 左翼 `texOffs(22,0).addBox(-10,0,0,10,20,2,new CubeDeformation(1.0F))`，`PartPose.offsetAndRotation(5,0,0, π/12, 0, −π/12)`；右翼 `mirror()`、`addBox(0,0,0,10,20,2,1.0F)`、pivot −5、zRot +π/12 | 模型 y 向下、x 向右；`FromVanilla(modelHeight=bodyTop)` 转换：`centreX=−(pivotX+minX+w/2)`、`bottom=modelHeight−(pivotY+minY+h)`、`centreZ=pivotZ+minZ+d/2` | 胸槽无 elytra 时 `VisibleWhen` 不通过 |
| `WithElytra` 婴儿翼盒 | 同上 + `ElytraModel.BABY_TRANSFORMER = MeshTransformer.scaling(0.5F)` | `MeshTransformer.scaling(0.5)` = `pose.scaled(0.5).translated(0, 24.016×(1−0.5)=12.008, 0)`，且 `MeshDefinition.transformed` **只改 root PartPose，子 CubeDefinition 保持 10×20×2**；渲染几何因此为 5×10×1，pivot ±5→±2.5，grow 1→0.5；y-down 翼盒 12.008..22.008、pivot 12.008；`WingsLayer` 的 +0.125 格平移不缩放 | 换成 y-up（modelHeight 24）：`Bottom≈2`、`PivotY≈12`；UV 采样必须仍是 `UvWidth=10, UvHeight=20, UvDepth=2`；`CentreZ=body.CentreZ+2.5`、`PivotZ=body.CentreZ+2` | `Decorate` 只为 humanoid 附加 |
| `EntityModelPart` UV 解耦 | `ref://.../MeshDefinition.java#transformed` | 几何尺寸（`Width/Height/Depth`）与 UV 采样盒尺寸分离；`UvWidth/UvHeight/UvDepth` 为 0 时回退到几何尺寸 | 一律使用 `ModelPart.Cube` 的原始 CubeDefinition 尺寸，缩放只作用于渲染变换 | 未设置时行为与旧版一致 |
| `WingsLayer.submit` 平移 | `ref://mc-26.3-analysis/src/net/minecraft/client/renderer/entity/layers/WingsLayer.java#submit` | `poseStack.translate(0, 0, 0.125F)` = +2 模型像素；`state.isBaby ? elytraBabyModel : elytraModel` | z 正方向朝背后 | 无 `EQUIPPABLE` 或 `assetId` 为空时不绘制 |
| `ElytraModel.setupAnim` 旋转 | `ref://mc-26.3-analysis/src/net/minecraft/client/model/object/equipment/ElytraModel.java#setupAnim` | 左翼 `y = crouching ? 3.0 : 0`；`xRot=state.elytraRotX`、`yRot=state.elytraRotY`、`zRot=state.elytraRotZ`；右翼 yRot/zRot 取反 | 左 `(−X,−Y,+Z)` / 右 `(−X,+Y,−Z)`（`RotationSelector`） | 无状态时默认 (π/12, 0, −π/12) |
| `ElytraAnimationState` 姿态驱动 | `ref://mc-26.3-analysis/src/net/minecraft/world/entity/ElytraAnimationState.java` | standing=(π/12,0,−π/12)；crouching=(2π/9,0.0873,−π/4)；fallflying 按 `ratio=1−(−normalizedVelocity.y)^1.5` 在 (π/12,−π/12)→(π/9,−π/2) 插值；每 tick `0.3*(target−cur)` | 弧度；x/y/z 轴 | 速度长度 < 1e−6 时 ratio=1 |
| 翼纹理 | `EquipmentClientInfo.LayerType.WINGS`；`WingsLayer` 的玩家披风/鞘翼皮肤覆盖 | 默认 `entity/equipment/wings/elytra`，64×32，tex(22,0) | sRGB，与实体纹理一致 | 玩家自定义 elytra/cape 纹理优先（T-752 已接） |

### 10.2 真值向量（Golden Vectors）

采样来源：`ElytraModel.java` / `WingsLayer.java` 的精确常量静态推导；测试落点：
`tests/Sharpcraft.Client.Tests/Render/ElytraLayerTests.cs`、`ElytraWingGeometryTests.cs`。

1. **成人翼部件**：`Width=10, Height=20, Depth=2, Grow=1, CentreX=0, Bottom=bodyTop−20, PivotY=bodyTop, PivotZ=body.CentreZ+2, CentreZ=body.CentreZ+3, tex(22,0)/64×32`。
2. **婴儿翼部件**：`Width=5, Height=10, Depth=1, Grow=0.5, CentreX=0, Bottom=2, PivotY=12, PivotX=∓2.5, PivotZ=body.CentreZ+2, CentreZ=body.CentreZ+2.5`（由 `MeshTransformer.scaling(0.5)` 的 12.008 y 偏移换算，不再是 `bodyTop−10`）；`UvWidth=10, UvHeight=20, UvDepth=2`，六个面 UV 与成人完全一致。
3. **成人网格角点**：两翼各为同一 `x[−6,6]`、`y[bodyTop−21, bodyTop+1]`、`z[body.CentreZ+1, body.CentreZ+5]` 的 10×20×2 外扩盒（测试以 bodyTop=24 断言 y[3,25]、z[1,5]），每翼 6 面 ×4 角点。
4. **UV 表**：六面按 `ModelPart.Polygon` 取 `u0=22,u1=24,u2=34,u3=36,u4=46,v0=0,v1=2,v2=22`；右翼 WEST/EAST 的 u 段按镜像交换为 22..24 / 34..36。
5. **婴儿网格**：胸槽 elytra 的 baby zombie 产出 2 翼 ×6 面 ×4 角点 = 48 顶点（`VertexFloats/14=48`）；六个面的 UV 必须与成人逐值相同（`BabyWingsSampleTheSameReferenceTexelsAsTheAdultModel`）。
6. **潜行偏移**：`ElytraModel.setupAnim` 的 `leftWing.y += 3` 在婴儿 root 0.5 缩放下实际下移 1.5 模型像素、成人下移 3 像素（`CrouchingDropsTheBabyWingsByTheRootScaledReferenceOffset`）。
6. **姿态**：standing `elytraRot=(π/12,0,−π/12)` 时两翼形成在 x=0 相交的浅 V，左右大面法线 X 分量相反且非零（非共面）；crouch 时 y=3px。

### 10.3 已知偏差

- 右翼镜像的 WEST/EAST 面 u 段修复（T-649）为通用 cube-meshing 行为，已由几何测试锁定。
- 两翼在参考模型中为**全宽重叠**的镜像同形翼板；曾按“各占半边”实现，属于对参考的误读，已于 T-650/T-662 收敛回参考几何。
- **T-663 修正**：初版 T-662 把几何尺寸和 UV 采样盒一起缩小（5×10×1），导致 `u4/v2` 从 46/22 缩到 34/11、采样到透明区；现在 `EntityModelPart` 的 `UvWidth/UvHeight/UvDepth` 保持 10/20/2，几何仍用 5×10×1，并补上潜行 1.5px/3px 位移。
- 婴儿翼 `PivotY=12` 来自 `MeshTransformer.scaling` 的 12.008 y 偏移；`BabyZombieModel` 肩线在 9px，翼上缘因此会进入头部后侧 3px（参考同一变换亦然）。如果实机确认原版没有该叠压，再单独登记受控偏差调整。

## 11. `use_player_texture`（玩家披风/鞘翅纹理替换）

### 11.1 原版 26.3 源码映射表

| 本模块概念 / 方法 | 原版类与方法路径（`ref://...`） | 精确常量 / 公式 / 树结构 | 位序 / 正负号 / 色彩空间约定 | 缺省 / 越界回退值 |
| --- | --- | --- | --- | --- |
| 玩家纹理覆盖入口 | `ref://mc-26.3-analysis/src/net/minecraft/client/renderer/entity/layers/WingsLayer.java#submit` | 仅 `AvatarRenderState` 传入 `playerElytraTexture`；胸槽 `EQUIPPABLE.assetId` 为空时不绘制 | — | 无玩家皮肤时传 `null` |
| 玩家纹理优先级 | `WingsLayer.getPlayerElytraTexture` | `skin.elytra()?.texturePath()` 优先；否则 `skin.cape()?.texturePath()`，且仅当 `showCape == true` | — | 两者皆空返回 `null` |
| 装备层替换条件 | `ref://mc-26.3-analysis/src/net/minecraft/client/renderer/entity/layers/EquipmentLayerRenderer.java#renderLayers` | `layer.usePlayerTexture() && playerTextureOverride != null ? playerTextureOverride : layerTextureLookup.apply(...)` | 只替换第一层；后续层不继承 | 覆盖为 `null` 时使用层纹理 |
| 玩家皮肤纹理路径 | `SkinManager.TextureCache.getTextureLocation` | `skins/<sha1>` / `capes/<sha1>` / `elytra/<sha1>`；`<sha1>` = SHA-1(unencodedChars(URL 最后一段 hash)) | 十六进制小写 | 下载失败回退默认皮肤 |
| 档案纹理属性 | `ref://mc-26.3-analysis/src/net/minecraft/network/protocol/game/ClientboundPlayerInfoUpdatePacket.java` 的 `ADD_PLAYER` | `GAME_PROFILE_PROPERTIES` 中名为 `textures` 的属性值是 base64 JSON；`textures.SKIN/CAPE/ELYTRA.url` | JSON 键大写；URL 只接受 http/https | 无属性/解析失败回退默认皮肤 |

### 11.2 真值向量（Golden Vectors）

采样来源：上述 `WingsLayer`/`EquipmentLayerRenderer` 的精确条件与 `SkinManager` 的路径公式；测试落点：
`tests/Sharpcraft.Client.Tests/Render/EntityLayerTests.cs`、`tests/Sharpcraft.Client.Tests/World/PlayerSkinTexturesTests.cs`、
`tests/Sharpcraft.Client.Tests/Render/PlayerSkinCacheTests.cs`。

1. **无皮肤属性**：玩家穿 elytra，`PlayerElytraOverride == null` → 使用 `entity/equipment/wings/elytra`。
2. **仅披风且 showCape=true**：`CAPE` 存在、`ELYTRA` 缺失 → 使用 `capes/<sha1>`。
3. **鞘翅优先**：`CAPE` 与 `ELYTRA` 同时存在 → 使用 `elytra/<sha1>`，披风不参与。
4. **showCape=false**：仅 `CAPE` 存在 → 回退 `entity/equipment/wings/elytra`。
5. **非玩家 humanoid**：Zombie 穿 elytra，即使 state 带 `PlayerElytraTexture` → 仍使用 `entity/equipment/wings/elytra`；`WingsLayer` 只为 `AvatarRenderState` 提供覆盖。
6. **路径公式**：URL hash `abc123` → `capes/689307d2fc53af0fb941bc1bb42737ce4f3ef540`；hash `deadbeef` → `elytra/8c00f528eca4aa2abd8617baad2a9fb47e0a2097`（SHA-1 over UTF-16LE code units）。
7. **下载失败**：某纹理下载/解码失败时该纹理路径置空，不把缺失路径交给 atlas；回退层纹理。

### 11.3 当前实现与已知限制

- `PlayerSkinTexturesParser` 解析 `textures` 属性；`PlayerSkinCache` 按 UUID 缓存下载结果并把 body/cape/elytra 纹理注册进 `EntitySkinLibrary` atlas；玩家基础模型通过 `PlayerBodyTexture` selector 使用 body 路径。
- `EntityRenderState.PlayerElytraOverride` 实现 `elytra > cape(showCape)` 的优先级；`EntityModelResolver.ResolveEquipmentTexture` 在 `UsePlayerTexture` 为真时替换第一层纹理。
- `PlayerSkinCache` 的下载线程只做 IO/PNG 解码，atlas 注册在宿主帧线程；`WindowedRenderer` 的 entity skin 纹理源支持运行期重建。
- `PlayerModelType.SLIM` 的独立皮肤模型已由 §12 接入；纹理属性签名校验已由 §14 的 `Sharpcraft.AuthLib` + `PlayerSkinCache` 接入。

## 12. SLIM 玩家模型（T-752 补全）

### 12.1 原版 26.3 源码映射表

| 本模块概念 / 方法 | 原版类与方法路径（`ref://...`） | 精确常量 / 公式 / 树结构 | 位序 / 正负号 / 色彩空间约定 | 缺省 / 越界回退值 |
| --- | --- | --- | --- | --- |
| 玩家模型选择 | `ref://mc-26.3-analysis/src/net/minecraft/client/renderer/entity/player/AvatarRenderer.java#AvatarRenderer` | 构造参数 `slimSteve` 决定 bake `ModelLayers.PLAYER_SLIM`/`PLAYER`，护甲为 `PLAYER_SLIM_ARMOR`/`PLAYER_ARMOR` | `slim=true` 为 3px 手臂 | 无档案属性时由 `DefaultPlayerSkin.get(profileId)` 选取 |
| 宽/窄手臂网格 | `ref://mc-26.3-analysis/src/net/minecraft/client/model/player/PlayerModel.java#createMesh` | slim：左臂 tex(32,48) 盒 `(-1,-2,-2,3,12,4)`、右臂 tex(40,16) 盒 `(-2,-2,-2,3,12,4)`；wide：左 `(-1,-2,-2,4,12,4)`、右 `(-3,-2,-2,4,12,4)`；二者 pivot 均 `(±5,2,0)`；袖层 UV 不变、宽随臂宽、外扩 0.25 | 原版 y-down；DSL 经集中 bake 转换，`CentreX=−(pivotX+minX+w/2)`；纹理 64×64，sRGB | 未知/非玩家模型按 wide |
| 腿/裤/外套层 | 同上 | 两模型逐值相同：左腿 tex(16,48) `(-2,0,-2,4,12,4)` pivot `(1.9,12,0)`、左裤 tex(0,48)、右裤 tex(0,32)、外套 tex(16,32) `(-4,0,-2,8,12,4)`；层外扩 0.25 | 同上 | — |
| 档案 model 字段 | `ref://mc-26.3-analysis/src/net/minecraft/client/resources/SkinManager.java#registerTextures` | `PlayerModelType.byLegacyServicesName(skinInfo.getMetadata("model"))`；无 SKIN 条目时取 `DefaultPlayerSkin.get(profileId)` | 字符串 `slim` 区分大小写语义之外的其它值按 wide | 缺字段按 wide |
| 玩家护甲模型层 | `ref://mc-26.3-analysis/src/net/minecraft/client/model/geom/LayerDefinitions.java#createRoots`、`ref://mc-26.3-analysis/src/net/minecraft/client/renderer/entity/ArmorModelSet.java#putFrom`、`ref://mc-26.3-analysis/src/net/minecraft/client/renderer/entity/player/AvatarRenderer.java#AvatarRenderer` | `playerArmor = PlayerModel.createArmorMeshSet(INNER_ARMOR_DEFORMATION, OUTER_ARMOR_DEFORMATION)`；`PLAYER_ARMOR.putFrom(playerArmor)` 与 `PLAYER_SLIM_ARMOR.putFrom(playerArmor)` 注册的是**同一个** ArmorModelSet；`PlayerModel.createArmorMeshSet` 没有 slim 参数，委托 `HumanoidModel.createArmorMeshSet` 的 wide 基础 mesh 并补空 sleeve/pants/jacket 组 | `putFrom` 只按 head/chest/legs/feet put，不做几何变换；护甲纹理 64×32；`new PlayerModel(part, slimSteve)` 的 slim 只用于 `translateToHand` 的 0.5F 偏移 | slim 与 wide 的烘焙护甲树完全相同（4px 手臂） |
| 默认皮肤表 | `ref://mc-26.3-analysis/src/net/minecraft/client/resources/DefaultPlayerSkin.java` | 18 项：索引 0..8 为 `entity/player/slim/<name>` + SLIM，9..17 为 `entity/player/wide/<name>` + WIDE；`get(UUID)` = `DEFAULT_SKINS[Math.floorMod(uuid.hashCode(), 18)]`；`getDefaultSkin()` 固定索引 6 = `entity/player/slim/steve` | Java `UUID.hashCode()` 位序；`Math.floorMod` 结果非负 | 无档案属性时按 UUID 索引 |
| UUID 哈希与 Guid 位序 | `java.util.UUID.hashCode()`（JDK 公开契约）+ `ref://mc-26.3-analysis/src/net/minecraft/client/resources/DefaultPlayerSkin.java#get` | `hilo = mostSigBits ^ leastSigBits`；`hashCode = (int)(hilo >> 32) ^ (int)hilo`，等价于低 32 位的 `most ^ (most >>> 32) ^ least ^ (least >>> 32)`；`index = Math.floorMod(hashCode, 18)` | `most`/`least` 是 canonical UUID 字符串的前/后 16 个十六进制位（big-endian long）；.NET `Guid.GetHashCode()` 与 Java 无关，**禁止**使用；`Guid.ToString("N")` 或 `Guid.TryWriteBytes(..., bigEndian: true)` 才是正确来源 | `Guid.Empty` → hash 0 → 索引 0 → `entity/player/slim/alex` |
| 名称表顺序 | 同上 `DEFAULT_SKINS` | `alex, ari, efe, kai, makena, noor, steve, sunny, zuri`；`index % 9` 取名；`index < 9` 为 slim 目录，否则 wide 目录 | 目录名小写；sRGB 纹理 64×64 | — |

### 12.2 真值向量（Golden Vectors）

采样来源：上述 `PlayerModel.createMesh` / `DefaultPlayerSkin` 的精确常量静态推导；测试落点：
`tests/Sharpcraft.Client.Tests/Render/PlayerSlimModelTests.cs`。

1. **臂宽**：wide 左右臂 `Width=4`；slim 左右臂 `Width=3`；`Height=12`、`Depth=4`、基础 `Grow=0` 两者相同。
2. **UV 起点**：两模型左臂 tex(32,48)、右臂 tex(40,16)、左袖 tex(48,48)、右袖 tex(40,32) 完全一致；差异只在展开宽度（`u4−texX = 2·(d+w)`：wide `2·(4+4)=16`、slim `2·(4+3)=14`）。
3. **右臂偏移**：wide 右臂盒 minX=−3、slim minX=−2，转换后表现为不同 `CentreX`；左臂 minX 恒为 −1。
4. **非臂部件不变**：head/body/双腿/裤/外套的盒尺寸、UV 与层外扩在两模型逐值相同。
5. **默认纹理与模型一致**：`Slim=true` 且无自定义 body 纹理时回退 `entity/player/slim/steve`；`Slim=false` 回退 `entity/player/wide/steve`；不得交叉回退；自定义 body 纹理优先。
6. **wide 路径不回归**：未带 slim 标记的玩家仍解析到原 `Definition`（臂宽 4）。

**Java 采样真值向量（UUID → 默认皮肤）**：用 GraalVM JDK 25 的 `jshell` 运行 `java.util.UUID.hashCode()` + `Math.floorMod(h, 18)` + `DefaultPlayerSkin` 的 18 项名称/目录表采样得到，脚本与输出只在仓库外核对、不入库；测试落点 `tests/Sharpcraft.Client.Tests/Render/DefaultPlayerSkinTests.cs`。`hash` 列同时由 JDK 与公式独立计算并逐值一致，不是按 C# 实现反推。

| # | UUID | hashCode | index | 默认 body path | slim |
| --- | --- | --- | --- | --- | --- |
| 1 | `00000000-0000-0000-0000-000000000000` | 0 | 0 | `entity/player/slim/alex` | true |
| 2 | `00000000-0000-0000-0000-000000000001` | 1 | 1 | `entity/player/slim/ari` | true |
| 3 | `069a79f4-44e9-4726-a5be-fca90e38aaf5` | -369792882 | 0 | `entity/player/slim/alex` | true |
| 4 | `853c80ef-3c37-49fd-aa49-938b674adae6` | 1946714239 | 1 | `entity/player/slim/ari` | true |
| 5 | `61699b2e-d327-4a01-9f1e-0ea8c3f06bc6` | -291457983 | 15 | `entity/player/wide/steve` | false |
| 6 | `ffffffff-ffff-ffff-ffff-ffffffffffff` | 0 | 0 | `entity/player/slim/alex` | true |
| 7 | `80000000-0000-0000-0000-000000000000` | -2147483648 | 16 | `entity/player/wide/sunny` | false |
| 8 | `a1b2c3d4-e5f6-4a5b-8c9d-0e1f2a3b4c5d` | -488453171 | 13 | `entity/player/wide/makena` | false |
| 9 | `deadbeef-0000-0000-0000-00000000cafe` | -559057903 | 11 | `entity/player/wide/efe` | false |
| 10 | `12345678-1234-5678-9abc-def012345678` | -2004318072 | 0 | `entity/player/slim/alex` | true |

`getDefaultSkin()` 固定索引 6 → `entity/player/slim/steve`（slim=true），不随 UUID 变化。

### 12.3 当前实现与已知限制

- `PlayerModelDefinition` 新增 `SlimDefinition`（同一 DSL 树，仅手臂盒按 12.2 替换），`EntityRenderState.Slim` 由 `PlayerSkinTextures.Slim` 透传；`EntityModelMesher.ResolveModel` 对玩家类型按该标记选择经过 `Decorate` 的 slim 变体，wide 定义与纹理 selector 保持原样。
- `DefaultPlayerSkin.get(UUID)` 已按 12.2 的 Java 真值向量实现：`DefaultPlayerSkin.Get(Guid)` 返回 `(Path, Slim)`，使用 `Guid.TryWriteBytes(bigEndian: true)` 取 most/least 两个 long、`JavaUuidHashCode` + `Math.floorMod` 语义，18 条路径预生成、稳态零分配；`EntityRenderState.Uuid` 由 `EntityTracker` 透传，`PlayerModelDefinition` 两个定义的无皮肤回退改为按 UUID 取默认路径，`EntityTracker`/`PlayerSkinCache` 在无可用 body 时按同一默认皮肤的 `Slim` 设置模型标记，纹理与模型始终一致；wide 路径不回归。
- **26.3 事实核对（T-752a-2，原「slim 护甲偏差」描述已更正）**：`LayerDefinitions.createRoots`（`ref://mc-26.3-analysis/src/net/minecraft/client/model/geom/LayerDefinitions.java#createRoots`）用同一个 `playerArmor` ArmorModelSet 注册 `PLAYER_ARMOR` 与 `PLAYER_SLIM_ARMOR`；`ArmorModelSet.putFrom`（`ref://mc-26.3-analysis/src/net/minecraft/client/renderer/entity/ArmorModelSet.java#putFrom`）只按四个槽 put，不变换几何；`PlayerModel.createArmorMeshSet`（`ref://mc-26.3-analysis/src/net/minecraft/client/model/player/PlayerModel.java#createArmorMeshSet`）无 slim 参数，使用 wide 基础 mesh；`AvatarRenderer`（`ref://mc-26.3-analysis/src/net/minecraft/client/renderer/entity/player/AvatarRenderer.java#AvatarRenderer`）的 `slimSteve ? PLAYER_SLIM_ARMOR : PLAYER_ARMOR` 只是不同 ModelLayerLocation，`new PlayerModel(part, slimSteve)` 的 slim 仅影响 `translateToHand` 的 0.5F 偏移。因此 **26.3 中 slim/wide 玩家护甲烘焙树完全相同（4px 手臂）**，现有「slim 护甲复用 wide 玩家四槽树」已与原版一致，不是偏差。按 AGENTS 4.1（证据与需求冲突）不新增 slim 护甲树，避免凭旧版印象引入 26.3 可观测偏差；如基线升级且有新事实再单独评审。
- 纹理属性签名校验已由 `Sharpcraft.AuthLib` 实现（§14）：公钥发现、`SHA1withRSA` 验签和纹理 URL 策略均在该中性共享库；`PlayerSkinCache` 按本地 UUID 区分 `requireSecure`。
- `PlayerSkinCache` 的档案下载仍按原版 `SkinManager` 的 body/cape/elytra 组合语义：任一请求的纹理失败则整体回退默认皮肤；atlas 注册路径不变。

## 13. 旧版 64×32 皮肤转换（T-752 补全）

### 13.1 原版 26.3 源码映射表

| 本模块概念 / 方法 | 原版类与方法路径（`ref://...`） | 精确常量 / 公式 / 树结构 | 位序 / 正负号 / 色彩空间约定 | 缺省 / 越界回退值 |
| --- | --- | --- | --- | --- |
| 转换入口 | `ref://mc-26.3-analysis/src/net/minecraft/client/renderer/texture/SkinTextureDownloader.java#downloadAndRegisterSkin` | `processLegacySkin` 仅当调用方传入的 `processLegacySkin` 为真时执行；下载器只对 SKIN/body 传 true | — | cape/elytra 不转换；其它尺寸丢弃 |
| 尺寸判定 | 同上 `#processLegacySkin` | `width == 64 && (height == 32 || height == 64)`；`isLegacy = height == 32` | — | 不满足则丢弃纹理并报错 |
| 扩图 / 清空 | 同上，`ref://mc-26.3-analysis/src/com/mojang/blaze3d/platform/NativeImage.java#copyFrom` / `#fillRect` | 新建 64×64 清零图；`copyFrom` 在宽度相同时复制源前 `64*32*4` 字节（上半 32 行）；`fillRect(0,32,64,32,0)` 清空下半 32 行 | 行主序 RGBA8；仓库 `RgbaImage.Pixels` 每像素 4 字节、alpha 为第 4 字节 | — |
| 区域复制 | `#copyRect(startX,startY,offsetX,offsetY,sizeX,sizeY,swapX,swapY)` | 目标原点 = `(startX+offsetX, startY+offsetY)`；`swapX` 时 `dx=sizeX-1-x`；12 组依次为 `(4,16,16,32,4,4)`、`(8,16,16,32,4,4)`、`(0,20,24,32,4,12)`、`(4,20,16,32,4,12)`、`(8,20,8,32,4,12)`、`(12,20,16,32,4,12)`、`(44,16,-8,32,4,4)`、`(48,16,-8,32,4,4)`、`(40,20,0,32,4,12)`、`(44,20,-8,32,4,12)`、`(48,20,-16,32,4,12)`、`(52,20,-8,32,4,12)`，全部 `swapX=true, swapY=false` | 源 y=16/20，目标 y=48/52；复制的是原始 4 字节，不换通道 | — |
| alpha 修正 | 同上 `#setNoAlpha` / `#doNotchTransparencyHack`，`ref://mc-26.3-analysis/src/net/minecraft/util/ARGB.java#opaque` / `#transparent` | `setNoAlpha` = alpha 置 255，依次作用于 `(0,0,32,16)`、`(0,16,64,32)`、`(16,48,48,64)`；`doNotchTransparencyHack(32,0,64,32)` 仅 legacy：若该区所有像素 alpha≥128，则整区 alpha 置 0 | `ARGB.alpha = color >>> 24`（最高字节），`opaque = color|0xFF000000`，`transparent = color&0xFFFFFF` | 区内任一 alpha<128 则整区不改 |

### 13.2 真值向量（Golden Vectors）

采样来源：上述 `SkinTextureDownloader` / `NativeImage` / `ARGB` 的精确常量静态推导；测试落点：
`tests/Sharpcraft.Client.Tests/Render/PlayerSkinCacheTests.cs`（合成 64×32 PNG，离线）。

1. **尺寸**：64×32 输入 → 64×64 输出；上半 32 行的 RGB 逐值保持，alpha 会按 13.1 的 `setNoAlpha`/Notch hack 规则修正（不保证逐字节不变）。
2. **下半部**：y∈[32,48) 在复制前为全 0（透明）；12 组 `copyRect` 把源 y∈[16,32) 的腿/臂区域写到 y∈[48,64)，y∈[32,48) 保持透明。
3. **首两组复制**：`(4,16,16,32,4,4,true,false)` 目标 `(20,48)` 水平镜像；`(8,16,16,32,4,4,true,false)` 目标 `(24,48)`；源像素在目标处左右翻转后逐值相同。
4. **不透明区**：`(0,0,32,16)`、`(0,16,64,32)`、`(16,48,48,64)` 三区所有像素 alpha=255。
5. **Notch hack**：当 `(32,0,64,32)` 全不透明时该区 alpha 全为 0；只要有一个 alpha<128 则该区保持原样。
6. **64×64**：不扩图、不复制，但仍应用 3 组 `setNoAlpha`；cape/elytra 的下载结果完全不经过转换器。
7. **错误尺寸**：非 64 宽或高度既非 32 也非 64 时转换失败并回退（不注册路径），与参考丢弃行为一致。

### 13.3 当前实现与已知限制

- 转换在 `PlayerSkinCache.LoadTexture` 的 body/SKIN 分支内完成（下载线程做 IO/PNG 解码与像素处理）；`EntitySkinLibrary` 仍按 64×64 消费，cape/elytra 不调用转换。
- `TextureData` 与 atlas 注册仍使用 `RgbaImage` 的 RGBA 字节数组；不引入第三方图像库、不访问网络。
- `processLegacySkin` 的 `url` 参数只用于原版报错信息，本实现不做 URL 过滤；不复制任何反编译代码或资产进仓库。

## 14. `textures` 属性签名校验（T-752c，已实现）

### 14.1 调查结论（只记行为/架构）

| 项 | 结论 |
| --- | --- |
| 原版入口 | `SkinManager` 的档案纹理缓存把验签整体委托给会话服务的 `unpackTextures(Property)`；`net.minecraft` 侧只消费 `SignatureState` |
| `INVALID` | 仅记录警告，纹理仍进入后续注册，不直接丢弃 |
| `SIGNED` | `PlayerSkin.secure()` 等价于 `signatureState() == SIGNED` |
| `requireSecure` | `PlayerInfo.createSkinLookup` 取 `!isLocalPlayer(profileId)`；社交/举报界面显式传 true。只有 requireSecure 为真时未签名/无效签名才回退 `DefaultPlayerSkin.get(profile)`；本地玩家即使未签名也使用档案纹理 |
| 实现前现状（历史） | `PlayerListPackets.TryReadProfileProperties` 读取 `signed` 标志并跳过（上限 4096 字节）签名串但不保留；`PlayerSkinTextures` 无签名状态字段 |
| 验签设施（历史调查） | 当时 `src/Sharpcraft.Auth` 及全部 `src` 无 RSA/公钥/验签 API；分析工作区无 `com.mojang.authlib` 源、无 textures 属性公钥 |
| 当前实现 | `Sharpcraft.AuthLib`（中性共享库）提供 `ProfileProperty`/`ProfileSignatureState`/`ProfileSignatureKeySet`/`MinecraftServicesKeyClient`；`PlayerSkinTextures` 携带 `SignatureState`；`PlayerSkinCache` 按本地 UUID 判定 `requireSecure` |

### 14.2 结构化上报（AGENTS.md 4.1）

1. **已尝试路径**：定位 `SkinManager`/`PlayerInfo`/社交与举报界面的签名消费点；全仓检索 `SignatureState`、`unpackTextures`、RSA/公钥/Verify；确认 `PlayerListPackets` 只保留 textures 值；核查外部分析工作区路径与内容。
2. **具体卡点**：缺少 (a) Mojang 为 `textures` 属性签名的 RSA 公钥（位于 authlib，而非 26.3 客户端分析工作区），(b) authlib `unpackTextures` 的签名数据格式、算法与 `SignatureState` 完整枚举语义，(c) 本仓任何公钥/验签基础设施。另外「校验失败即回退默认皮肤」并非原版无条件行为，只在 `requireSecure=true` 时成立。
3. **需要提供的输入**：authlib 源或官方公开验签规格与公钥；或项目所有者明确「本阶段只记录签名存在性、不做密码学校验」的书面决策；以及是否按 `requireSecure`（本地/远程）区分回退策略。
4. **可选方案与影响面**：A) 提供凭据后实现完整验签 + `SignatureState` 字段 + 有效/无效签名测试（影响 `PlayerSkinTextures`、`PlayerSkinCache`、`PlayerListPackets`、`EntityTracker`）；B) 仅解析并透传 `signed`/签名字节、显式标注「未验签」，不做密码学判断（不把未验签写成已验签）；C) 保持现状并在追踪矩阵登记阻塞。**倾向 C**：在缺少可验证凭据前不伪造校验，符合原则 3/14 与 4.1；若选 B 需明确书面授权。

### 14.3 当前状态

已实现：

- `Sharpcraft.AuthLib` 从 discovery 文档加载 `authentication.endpoints.getPublicKeys` 与 `profiles.endpoints.getTexture.validUris`，再读取 `profilePropertyKeys[].publicKey`；
- `ProfileSignatureKeySet` 使用 `SHA1withRSA` / `PKCS#1` 验证 `ProfileProperty`，产出 `Unsigned` / `Invalid` / `Signed`；
- `PlayerSkinTextures` 携带 `SignatureState`，`PlayerSkinCache` 对远程玩家要求 `Signed`，本地玩家允许 `Unsigned` / `Invalid`（与原版 `requireSecure` 一致）；
- 纹理 URL 仅接受 discovery 允许的 `textures.minecraft.net/texture/` 前缀；任一请求纹理失败时 body/cape/elytra 组合整体回退默认皮肤。

验证：`tests/Sharpcraft.AuthLib.Tests/ProfileSignatureTests.cs`（3 项）、`tests/Sharpcraft.Client.Tests/World/PlayerSkinTexturesTests.cs`、`PlayerSkinCacheTests` 覆盖签名状态、URL 策略与本地/远程差异。

## 15. Trim 纹理 / palette（T-754）

### 15.1 原版 26.3 源码映射表

| 本模块概念 / 方法 | 原版类与方法路径（`ref://...`） | 精确常量 / 公式 / 结构 | 位序 / 正负号 / 色彩空间约定 | 缺省 / 越界回退值 |
| --- | --- | --- | --- | --- |
| trim 组件解码 | `ArmorTrim.STREAM_CODEC` / `TrimMaterial.STREAM_CODEC` / `TrimPattern.STREAM_CODEC` | `ArmorTrim(material, pattern)` 两个 registry holder id，依次 VarInt | — | 未知 id 保留 null，不破坏包边界 |
| trim material 注册顺序 | `ref://mc-26.3-analysis/src/net/minecraft/world/item/equipment/trim/TrimMaterials.java` | quartz, iron, netherite, redstone, copper, gold, emerald, diamond, lapis, amethyst, resin（11） | `palette_id = minecraft:trim/<id>` | 无匹配 → 不绘制 trim |
| trim pattern 注册顺序 | `ref://mc-26.3-analysis/src/net/minecraft/world/item/equipment/trim/TrimPatterns.java` | sentry, dune, coast, wild, ward, eye, vex, tide, snout, rib, spire, wayfinder, shaper, silence, raiser, host, flow, bolt（18） | `asset_id = minecraft:<id>`；26.3 全部 `decal=false` | 无匹配 → 不绘制 trim |
| palette 映射 | `ref://mc-26.3-analysis/src/net/minecraft/client/resources/palette/PaletteMapping.java` | `base`/`target` 必须同尺寸；opaque base 颜色映射到 target；输出 `alpha = baseAlpha * targetAlpha / 255` | base `trim_base.png` 为 8×1 灰度；target 为 8×1 RGB | 尺寸不等或缺失 → `PaletteMapping.NONE` |
| trim 纹理路径 | `EquipmentClientInfo.LayerType.trimAssetPrefix()` | `trims/entity/<layer>/<pattern asset id>`；humanoid 与 humanoid_leggings；baby 不绘制 trim | `textures/` 前缀由资源层补 | 缺纹理 → 不绘制 |
| trim 渲染层 | `EquipmentLayerRenderer.renderLayers` / `RenderTypes.armorTrim` | 同一 armor 模型；trim 在 armor 层之后、glint 之前；`armorTrim(texture, decal)`，decal 使用 `ARMOR_DECAL_CUTOUT_NO_CULL`（`EQUAL`, no depth write） | 当前全部 decal=false，等价使用 `armor_cutout_no_cull`（`Elytra` 管线） | `hasTrim=false`（HUMANOID_BABY）不绘制 |
| trim override | `EquipmentClientInfo.TrimOverride` / `ResolveTrim` | 第一条 material/pattern predicate 匹配的 override 可覆盖 texture/palette | — | 未匹配使用 pattern/material 默认路径 |

### 15.2 真值向量（Golden Vectors）

采样来源：`TrimMaterials.java` / `TrimPatterns.java` 的 bootstrap 顺序、`PaletteMapping.java` 的精确公式、`HumanoidArmorLayer`/`EquipmentLayerRenderer` 的层顺序；测试落点：
`tests/Sharpcraft.Client.Tests/Render/TrimTextureTests.cs`。

1. **material 顺序**：索引 0 = `minecraft:quartz`，索引 1 = `minecraft:iron`，索引 10 = `minecraft:resin`；`iron.palette_id = minecraft:trim/iron`。
2. **pattern 顺序**：索引 0 = `minecraft:sentry`，索引 2 = `minecraft:coast`，索引 17 = `minecraft:bolt`；`coast.asset_id = minecraft:coast`。
3. **palette 像素**：base `[224,192,160,128,96,64,32,0]`；target `iron` 的前三项为 `[197,210,212]`、`[191,201,200]`、`[157,170,170]`；trim 的 224 像素映射为 197/210/212，透明 alpha=0 保持透明。
4. **纹理尺寸**：trim humanoid/humanoid_leggings 为 64×32；`trim_base` 与 material palette 为 8×1。
5. **路径**：`Path(Humanoid, "minecraft:coast", "minecraft:iron") = trims/entity/humanoid/coast/iron`；leggings 对应 `humanoid_leggings/coast/iron`。
6. **渲染顺序**：armor 层 order 10 → trim 层 11 → glint 层 12（同一槽位）；无 trim 时 trim 层不可见。
7. **组件解码**：`trim` 组件 material=1 / pattern=2 → `minecraft:iron` / `minecraft:coast`（按 bootstrap 表）；越界 id → null。
8. **palette override**：equipment asset 的 `trim_overrides` 匹配 `material=minecraft:iron` 且 `palette=minecraft:trim/iron_darker` 时，纹理 key 为 `trims/entity/humanoid/coast/iron/trim/iron_darker`。

### 15.3 当前实现与已知限制

- `ItemComponentInfo.TrimMaterialId` / `TrimPatternId` 由 `ItemComponentPatchReader` 解码；`TrimMaterialRegistry` / `TrimPatternRegistry` 给出 26.3 bootstrap 顺序。
- `TrimTextureCatalog` 加载 `textures/palettes/trim_base.png`、`textures/palettes/trim/<material>.png` 和 `textures/trims/entity/humanoid[_leggings]/<pattern>.png`，按 `PaletteMapping` 做 CPU 映射，生成 `TextureData` 并加入 `EntitySkinLibrary` atlas。
- `EntityModelResolver` 为 humanoid armor 槽位添加 `trim_<kind>_<slot>` 层，复用对应 armor 树；无 trim 组件时不可见。
- `trim_overrides` 的 **palette** 与 **texture** 替换已按 equipment asset 接入（例如 iron/gold 的 `*_darker` 变体；texture override key 带 `/tex/<path>` 后缀）。
- Glint 层已按原版 `glint.fsh` / `TextureTransform.ARMOR_ENTITY_GLINT_TEXTURING` 实现：0.16 scale、10° 旋转、110000/30000 时间偏移、`SRC_COLOR/ONE` 颜色混合、`EQUAL` 深度、fog fade 与 `glintStrength=0.75`；trim/no-trim 共用同一个等效 GLINT pass。
- 动态 `registry_data` 的 trim material/pattern holder 顺序已由 `TrimRegistryTable.Configure` 按当前连接装配；26.3 bootstrap 仍是缺省回退。自定义 entry 的 `palette_id`/`asset_id` NBT 尚未解析，未知自定义材质/图案会回退为不绘制。
