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
| 翼纹理 | `EquipmentClientInfo.LayerType.WINGS`；`WingsLayer` 的玩家披风/鞘翼皮肤覆盖 | 默认 `entity/equipment/wings/elytra`，64×32，tex(22,0) | sRGB，与实体纹理一致 | 玩家自定义 elytra/cape 纹理优先（后续） |

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