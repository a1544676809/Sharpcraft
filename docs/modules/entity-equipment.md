<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[已评审]
基线：Minecraft 26.3
证据：`docs/analysis/render/entity-equipment.md`
最后更新：2026-11-04

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


- **Elytra（T-648 第1步）**：双翼模型层已加（WithElytra，胸槽 elytra 可见，纹理 ntity/equipment/wings/elytra）；扇动（lytraRotX/Y/Z）与独立放置为后续。
- **Elytra 翼盒与原版等价（T-649）**：`ElytraModel.createLayer` 的左右翼盒为 `texOffs(22,0).addBox(-10,0,0,10,20,2, CubeDeformation(1.0))`，右翼 `mirror()`；`ModelPart.Cube` 对 grow 做 min/max 均匀外扩，`Polygon` 每面取 u0..u4 / v0..v2 的固定矩形。`WithElytra` 以 `FromVanilla(modelHeight=bodyTop)` 复现（pivot ±5、bottom=bodyTop-20、z 含 WingsLayer +0.125 格 = +2px）。**根因（已修复）**：镜像部件（右翼）的 WEST/EAST 两个 X 面在网格里采样了另一侧的 u 段；现按 `part.Mirror` 交换两 X 面 u 段（通用 cube-meshing 修复，非镜像面/非镜像部件不受影响）。六面角点与 UV 由 `ElytraWingGeometryTests` 锁定。
- **Elytra 翼面 z-fight 根因（T-650，已定位）**：翼面条纹与几何/UV/pose 无关。`ElytraModel.createLayer` 的左右翼是**镜像同形**的 10×20×2 叠加翼板（pivot ±5；`ModelPart.Cube` 的 `mirror` 只交换绕序与 UV，不改变方块几何位置），折叠姿态下两翼大面近平行、仅在 x=0 附近相交，中心区深度差极小。原版用**反相 Z（reversed-Z）**：`Projection.getMatrix` 交换 near/far（`ref://mc-26.3-analysis/src/net/minecraft/client/renderer/Projection.java` 67–75），主深度以 `clearDepthTexture(main,0.0)` 清除（`.../GameRenderer.java` 503 行），`DepthStencilState.DEFAULT = (GREATER_THAN_OR_EQUAL, writeDepth=true, bias 0)`（`.../DepthStencilState.java`），`ARMOR_CUTOUT_NO_CULL` 继承 DEFAULT 且 `cull(false)`（`.../RenderPipelines.java` 448–456）。本引擎已迁移到反相 Z，条纹消失；但两翼仍是**共面覆盖整背**的叠加翼板，因此仍表现为"重叠、无中心缝、前后交替"。
- **Elytra 双翼改为各自半边 + 中心缝（T-650，已实施）**：根因是两翼盒按字面读为镜像同形叠加、各自覆盖整背 x[-5,5]，两片共面大面在同深度反复互覆 → 无接缝且前后交替。`WithElytra` 保持参考盒尺寸/纹理（10×20×2、tex 22,0、grow1）与 `WingsLayer` +2px z，但让每翼只占背面一半：`CentreX=∓6.5`、`PivotX=0`（脊柱）、`PivotZ=CentreZ+2`；1px 均匀外扩由 1.5px 外移吸收，两翼内缘之间留 1px 中心缝，互不重叠。旋转仍由 `ElytraAnimationState` 驱动：左 `(−xRot,−yRot,+zRot)`、右 `(−xRot,+yRot,−zRot)`（互为镜像）；静止位（xRot=π/12,zRot=−π/12）两翼呈浅 V、外尖向后下并留中心缝，滑翔位（zRot→−π/2）向两侧展开。`ElytraWingGeometryTests` 由"两翼同一 x[-6,6]"改为每翼半边角点并断言中心 1px 缝不重叠；六面 UV 不变（仍由参考纹理布局锁定）。
