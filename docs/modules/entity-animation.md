<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[已评审]
基线：Minecraft 26.3
证据：`docs/analysis/render/entity-animation.md`
最后更新：2026-11-03

# 实体动画系统

在静态 bind pose 模型之上，按实体状态每帧计算部件的位置/旋转/缩放覆盖，供实体网格与装备层共同使用。

## 1. 职责范围

负责：

- 关键帧动画定义、关键帧求值与线性/Catmull-Rom 插值；
- 每个实体每帧的姿态覆盖（position/rotation/scale），以 part name 为 key；
- 动画状态/时间/循环语义；
- 动画与现有 `EntityModelPart` 层级、装备副本、可见性层的组合。

明确不负责：

- 动画资源（原版动画是 Java 行为数据，不是资产 JSON）；
- 物理/AI 状态机本身（走路速度等由实体状态输入）；
- 第一阶段的 IK、蒙皮、GPU 骨骼动画；
- 物品/方块动画（另立模块）。

## 2. 侧别归属

- 归属：`Client`（纯表现，不改变实体模拟）。
- 判定依据：`docs/analysis/render/entity-animation.md` 2、3；动画状态来自 render state，渲染器不得读 `Entity`。

## 3. 依赖关系

| 方向 | 对象 | 说明 |
| --- | --- | --- |
| 依赖 | `Client.Render.EntityModelPart` | pose 覆盖按 part name 应用 |
| 依赖 | `Client.World.EntityRenderState` | `AgeInTicks`、walk 参数、动画状态 |
| 提供 | `Client.World.EntityAnimationFrame` / `IEntityAnimationState` | 通用输入与每实体状态契约（World 侧，无渲染类型） |
| 被依赖 | `EntityModelMesher` / 实体渲染器 | 每帧取 pose |
| 被依赖 | 装备层 `WithEquipment`/`WithCopy` | 同名部件继承 pose |

## 4. 对外契约

- `AnimationDefinition`：长度、是否循环、bone → channels；
- `AnimationChannel`：target（Position/Rotation/Scale）、keyframes、interpolation；
- `KeyframeAnimation`：`Bake(rootPartNames)`、`Apply(seconds, targetScale, PoseBuffer)`；
- `AnimationState`：start tick、start/stop/animateWhen、elapsed millis；
- `EntityPose`：`TryGet(partName)` / `Add(partName, PoseOffset)`（position/rotation/scale 加性覆盖）；
- `EntityAnimationFrame`（`Client.World`）：通用动画输入，`From(EntityRenderState)` 投影；含 `PartialTick`、`HasMainHandItem`/`HasOffHandItem`（由装备槽 main/off hand 派生，对应原版 `ArmedEntityRenderState` 的手持物概念，供 Copper Golem 等持物生物选择 `walk_item`）；
- `IEntityAnimationState`（`Client.World`）：每实体可变状态的空标记，具体实现归各 provider；
- `IEntityAnimationProvider`（`Client.Render`）：`TypeId`、`Clips`、`CreateState()`、`Apply(state, frame, pose)`（热路径）、`ApplyClip(state, frame, clip, seconds, pose)`（沙盒/工具）；
- `EntityAnimationRegistry`（`Client.Render`）：`TryGet` / `For` / `CreateState` / `Clips` / `All`，按 typeId 查找 provider；
- `WalkAnimationState`（`Client.World`）：原版走路动画位置/速度的平滑与插值；
- `EntityAnimationTickContext` + `IEntityTickAnimationState`（`Client.World`）：每 tick 回调（计数递减、时钟 `animateWhen`）；
- `IEntityEventAnimationState`（`Client.World`）：`entity_event`（clientbound 34）触发的一次性 clip 时钟；
- `IEntityPoseAnimationState`（`Client.World`）：`set_entity_data` 的 `Pose`（accessor 6，serializer 20）变化时启动/切换 clip；
- `IEntitySyncedDataAnimationState`（`Client.World`）：把生物自己的整型 synced data（如 Creaking `CAN_MOVE`=16 / `IS_TEARING_DOWN`=18）交给 state 解释；
- `AnimationClipPlayer`（`Client.Render`）：从 `AnimationState` 时钟按 `(age-startTick)*50ms` 播放 baked clip。

`EntityTracker` 通过注入的 `AnimationStateFactory` 为每个 `TrackedEntity` 创建一次 state，`EntityRenderState.Animation` 携带该引用；`EntityModelMesher` 每帧为每个实体准备一份复用 `EntityPose` 并调用一次 provider `Apply`。原 `EntityAnimationController` 的静态 switch 已删除。

**通用状态提取（2026-11-04）**：
- `EntityTracker.Tick()` 用每 tick 实际水平位移驱动 `TrackedEntity.WalkAnimation`（原版 `calculateEntityAnimation` → `updateWalkAnimation`：默认 `min(dist*4,1)`/factor 0.4/幼年位置 ×3，Creaking 覆写 `min(dist*25,3)`），`Interpolate` 输出 `WalkAnimationPos/Speed`，因此走路动画在真机生效（此前恒为 0）。
- `entity_event`（id 34）解码为 `entityId + eventId`，路由到 `IEntityEventAnimationState.OnEntityEvent`；Warden 以此启动 attack(4)/sonic_charge(62)/tendril(61)，Creaking 启动 attack(4)/invulnerability(66)。
- `Tick()` 每 tick 调用 `IEntityTickAnimationState.OnTick`，让 Creaking 的攻击/无敌计数递减并 `animateWhen`，Warden 的触须倒计时递减；`EntityAnimationFrame.PartialTick` 提供帧内插值，触须按原版双帧 `lerp` 取值。
- `set_entity_data` 的 `Pose`（index 6 / serializer 20）路由到 `IEntityPoseAnimationState`：Warden `EMERGING(13)/DIGGING(14)/ROARING(11)/SNIFFING(12)` 启动对应 clip；`IEntitySyncedDataAnimationState` 把整型字段交给 state，Creaking 用 `CAN_MOVE(16)` 门控走路、`IS_TEARING_DOWN(18)` 驱动死亡 clip。

## 5. 关键数据结构

| 结构 | 用途 | 生命周期 | 所属线程 |
| --- | --- | --- | --- |
| `AnimationDefinition` | 静态动画表 | 进程级只读 | 任意 |
| `AnimationChannel` / `Keyframe` | 关键帧与插值 | 进程级只读 | 任意 |
| `IEntityAnimationProvider` | 每生物控制器（单例） | 注册期创建、进程级 | 任意 |
| `IEntityAnimationState` | 每实体可变状态 | 随 `TrackedEntity`，首次出现创建 | 主线程 |
| `WalkAnimationState` | 走路位置/速度平滑 | 每实体、随 `TrackedEntity` | 主线程 |
| `EntityAnimationFrame` | 每帧通用输入（值类型） | 每帧、栈上 | 主线程 |
| `AnimationState` | 动画开始 tick（状态实现内） | 每实体 | 主线程 |
| `EntityPose` | 每实体部件覆盖（Mesher 复用缓冲） | 每实体、跨帧复用 | 主线程 |
| `PoseOffset` | 位置/旋转/缩放增量 | 值类型 | 主线程 |

## 6. 行为对齐与偏离

对齐点：

- 每帧先回到 `initialPose`，再叠加动画增量（与 `Model.setupAnim → resetPose` 一致）；
- `posVec` 的 Y 取反、`scaleVec` 是 `x-1` 的加性缩放；
- 时间秒数、循环取模、二分找关键帧、`targetScale = min(walkSpeed*scale,1)`；
- 走路位置/速度来自真实位移（`WalkAnimationState`，含幼年 ×3 与 Creaking 覆写），不再由沙盒伪造；
- 一次性 clip 由 `entity_event` 与每 tick 计数驱动 `AnimationState`，再经 `AnimationClipPlayer` 按 `(age-startTick)*50ms` 采样；
- 父子部件层级继承。

偏离/限制：

- 第一版只覆盖 idle/walk 和少量实体，不一次性移植全部实体动画；
- 动画状态中的交互/攻击状态先用客户端可观测的简化输入，后续补 AI 状态同步。

## 7. 分阶段与验收

0. **A0 provider 基座（已完成 2026-11-03）**：`IEntityAnimationProvider` + 每生物 `IEntityAnimationState` + `EntityAnimationRegistry` + `EntityAnimationFrame` + `EntityRenderState.Animation` 接线；现有 controller 全部迁入 provider，行为与旧测试不变；沙盒改从 registry 取 clip；每 provider 稳态零分配测试。
1. **A1 求值内核**：`AnimationDefinition`/`KeyframeAnimation`/`AnimationState`/`EntityPose`；单元测试 Camel walk 和 Armadillo roll 的时间/循环/插值。
2. **A2 网格接入**：`EntityModelMesher` 支持 pose buffer，装备副本同名继承；固定帧 screenshot/数值测试。
3. **A3 四足样板**：Pig/Camel 的 idle+walk，接入真实 render state。
4. **A4 两足样板**：Zombie/Villager 手臂与头部。
5. **A5 实体扩展**：现有已移植者已在 A0 迁入 provider；其余按 backlog 逐类新增 provider，每类独立验收。批次 1（Warden / Creaking / Breeze）已完成数据 + provider + 沙盒 clip，热路径仅通用帧可表达部分；one-shot 与 Breeze 六 clip 待 render-state 动画状态接线（T-568）。批次 2（Sniffer / Copper Golem / Nautilus）已完成：Sniffer 经 `SNIFFER_STATE(35)`、Copper Golem 经 `COPPER_GOLEM_STATE(37)` 驱动状态 clip（`walk_item` 已热路径，见 T-572），Nautilus swimming 纯热路径（T-571）。批次 3（Ravager / Hoglin+Zoglin / Nautilus→Zombie Nautilus）已完成：Ravager 程序化 attack/stun/roar（entity event 4/39/69 + tick 计数），Hoglin 程序化 headbutt（event 4），Zoglin 复用 Hoglin provider，Zombie Nautilus(156) 复用 Nautilus swim（T-573）。批次 4（Witch / Vex）已完成：Witch 程序化（nose 抖动用 `EntityId%10`、持物覆盖），Vex 程序化（`isCharging` 来自 flags byte，`setArmsCharging` 用持物 flag）；新增通用 `EntityAnimationFrame.EntityId`（T-574）。批次 5（Parrot / Strider）经客户端表现状态层完成 Parrot 的 FLYING/STANDING/ON_SHOULDER 与 flap、Strider 的 isRidden 与 bristle（T-578）；批次 6（Tadpole / Ghast）用 `IsInWater`/`AgeInTicks` 完成尾巴与触手（T-579）。批次 7（Allay / Parrot SITTING）完成 Allay 的 dancing/spinning/holding 客户端派生与 setupAnim（T-580），Parrot 增加由 TamableAnimal synced flags 派生的 SITTING 姿势（PARTY 仍缺点唱机状态）。批次 8（Guardian / Elder Guardian）用客户端 tail/spikes 随机时钟派生（eye look 因需 lookAt/相机目标而阻塞）（T-581）。批次 9（Spider+Cave Spider / Silverfish / Blaze）以纯 age/walk 程序化复刻（T-582）。批次 10（Phantom / Cod / Salmon）以 `EntityId+AgeInTicks` 与 `IsInWater` 复刻拍翅/尾鳍摆动（T-583）。批次 11（Snow Golem / Happy Ghast / Iron Golem）以纯 look、tentacle 与事件计数（attack/offer flower）复刻（T-584）。批次 12（Endermite / Dolphin）+ 复用（Creeper / Giant）：Endermite 四段 age 摆动、Dolphin 按 `IsMoving` 摆尾；Creeper/Giant 的 setupAnim 恰为共享四足/人形走路，直接复用 provider（T-585）。批次 13（Llama+TraderLlama / Turtle / WanderingTrader 复用）：Llama 头组（head/neck/ears 同 pivot）与四腿；Turtle 陆/水两套腿姿（产蛋子支阻塞）；WanderingTrader 复用 Villager provider（T-586）。批次 14（Shulker / Bee）：Shulker peek 时钟 + lid/head（attach-face 属渲染变换阻塞），Bee 拍翅/上下浮动/翻滚（roll 由 flags bit 派生；stinger 可见性无 pose 通道，阻塞）（T-587）。批次 15（Illager 组）：经 `ArmPose` 可见性通道（交叉臂 vs 单臂）与事件/同步派生，复刻 Vindicator/Evoker/Pillager/Illusioner 的 armPose（十字弩蓄力进度因缺计时字段仅作 draw-start，阻塞）（T-589）。批次 16（Piglin 组）：DANCING 与 CROSSBOW_CHARGE（含 used-item 蓄力进度）可等效；ADMIRING/MELEE_ATTACK/CROSSBOW_HOLD 因缺物品标签/充能状态阻塞（T-591）。批次 17（物品标签基础能力）：提取 `data/minecraft/tags/item` 并解析为 `ItemTagRegistry`，Piglin 的 ADMIRING（`piglin_loved`）与 MELEE_ATTACK（`piglin_preferred_weapons`）解除阻塞；CROSSBOW_HOLD 仍待充能状态（T-592）。批次 18（装备组件解析）：新增 `DataComponentTypes` 注册表快照与 `ItemComponentPatchReader`（damage/max_damage/repair_cost/unbreakable/enchantments/charged_projectiles，未知组件安全 opaque 停止），`set_equipment` 不再在任意组件处停止；Piglin CROSSBOW_HOLD 由 `charged_projectiles` 派生（T-593）。批次 19（快速装填）：`EnchantmentTypes` 快照映射 `quick_charge`，`ItemComponentInfo.QuickChargeLevel` → `EntityAnimationFrame.MainHandQuickChargeLevel`；Illager/Piglin 蓄力时长按 `25 - 5*level` 缩短（T-594）。批次 20（组件表扩展）：新增通用 `NbtTagReader` 跳过匿名 NBT，支持 `custom_name`/`item_name`/`lore`（文本组件）与 `rarity`/`item_model`/`enchantment_glint_override`，提高装备包完整解析率（T-595）。批次 21（复杂组件）：支持 `attribute_modifiers`（条目 + display）与 `custom_model_data`（float/bool/string/int 四列表），进一步减少 opaque 停止（T-596）。批次 22（其余常用组件）：支持 `tooltip_display`、`use_effects`、`enchantable`、`attack_animation`/`interact_animation`、`damage_type`（T-597）。批次 23（mainArm）：`Mob` flags bit1 → `IsLeftHanded` → `EntityAnimationFrame.IsLeftHanded`；Illager/Piglin 的 armPose（admiring/hold/charge/melee swing）按主手镜像（T-598）。批次 24（Slime/MagmaCube squish）：`AbstractCubeMob` 的 squish 时钟进入表现层，`EntityModelTransform` 支持非均匀（size × squish）缩放；MagmaCube provider 按 `(i-4)*max(0,squish)*1.7` 推开八段（T-599）。批次 25（Shulker attach-face）：synced `DATA_ATTACH_FACE_ID`（index16 direction）→ 渲染期 `EntityModelTransform` 按 `Direction.getRotation()` 绕 (0,0.5,0) 旋转（T-600）。批次 26（组件表补全）：补齐 food / stored_enchantments / dye / dyed_color / trim / minimum_attack_charge / creative_slot_lock / intangible_projectile / damage_resistant / glider / additional_trade_cost / potion_duration_scale / instrument / ominous_bottle_amplifier / waxed（T-601）。批次 27（复杂组件）：支持 tool（规则 holderSet + 可选 speed/correct）、weapon、equippable（slot/sound/可选 asset/overlay/holderSet + 5 bool）、repairable（holderSet）、bundle_contents（item stack 模板列表）（T-602）。批次 28（特定物品组件）：支持 `blocks_attacks`、`consumable`（含 ConsumeEffect 五类）、`potion_contents`（含 MobEffectInstance 递归 hidden）、`fireworks`/`firework_explosion`、`banner_patterns`（T-603）。批次 29（其余物品组件）：支持 `custom_data`/`sign_text_front`/`sign_text_back`/`bucket_entity_data`(NBT)、`use_remainder`、`map_id`/`map_post_processing`/`block_state`/`jukebox_playable`、`pot_decorations`、`container`、`suspicious_stew_effects`、`mob_visibility`、`writable_book_content`/`written_book_content`、`note_block_sound`/`lock`（T-604）。批次 30（变体/颜色组件）：`base_color`、`wolf_collar`、`fox_variant`、`salmon_size`、`parrot_variant`、`tropical_fish_*`、`mooshroom_variant`、`rabbit_variant`、`horse_variant`、`llama_variant`、`axolotl_variant`、`cat_collar`、`sheep_color`、`shulker_color`、`cushion_color`（均 VarInt id）（T-605）。批次 31（最后三项）：`can_place_on`/`can_break`（BlockPredicate：可选 holderSet/state/NBT + 精确组件匹配）、`bees`（Occupant：entity type + NBT + 两 VarInt）、`profile`（GameProfile/Partial + skin patch）（T-606）。批次 32（零散组件收尾）：`use_cooldown`、`attack_range`、`tooltip_style`、`death_protection`、`villager_food`、`map_decorations`/`debug_stick_state`/`container_loot`(NBT)、`lodestone_tracker`、`sulfur_cube_content`、`compostable`/`cooking_fuel`/`brewing_fuel`（ResolvableInt/Float）、`piercing_weapon`、`kinetic_weapon`（T-607）。至此 99 个数据组件全部有解析覆盖。批次 16（2026-11-04，T-618）：Skeleton / Stray / Bogged / WitherSkeleton（118/131/16/150）复用 humanoid 走/头/腿，并补 `SkeletonModel.setupAnim` 的 `isAggressive && !isHoldingBow` 近战抬臂分支（含 `AnimationUtils.bobArms`）；Stray/Bogged 服装 overlay 部件用 `PoseSource` 跟随对应基础部件，避免身体动而服装静止。批次 17（2026-11-04，T-619）：把 humanoid 的 `HumanoidModel.setupAnim` **整条链路**补齐（不再只做走路子集）——头 yaw（相对 body yaw）/pitch、fall-flying 头、`/speedValue` 与腿 ∓0.005、乘客姿态、arm pose（EMPTY/ITEM/BLOCK/BOW_AND_ARROW/SPYGLASS/TOOT_HORN/BRUSH/THROW_TRIDENT）、`setupAttackAnimation`（WHACK）、潜行位移、`bobModelPart`、游泳 `setupSwimAnimation`；zombie 族在 `setupAttackAnimation` 末尾追加 `animateZombieArms`，skeleton 族在 super 之后追加激进抬臂。frame 新增 `SpeedValue/SwimAmount/IsVisuallySwimming/IsFallFlying/UseItemHand/SwingHand/SwingType`；`SwingAnimationState` 保留 hand/type。**阻塞**：SPEAR 姿态与 STAB 挥击依赖 `SpearAnimations`（长矛模型几何），先标阻塞、不近似；CROSSBOW_* 姿态对 humanoid mob 渲染器不可达。批次 18（2026-11-05，T-625）：新增 `FelineAnimationProvider`，复刻 `AdultFelineModel.setupAnim`（Cat 21 / Ocelot 92）——head pitch + 相对 yaw 施加到 `head_main/nose/ear1/ear2` 四部件、走路四腿相位、crouch/sit 的 body/tail/leg 偏移，以及 body(π/2)、tail1(0.9) 等**绝对赋值→增量**换算；Cat 额外支持坐姿（TamableAnimal flags）。**阻塞（记录不近似）**：Cat `lieDown/relax`（synced IS_LYING/RELAX_STATE_ONE 未解码）与 sprint 尾巴（shared flag bit3 未解码）。ZombieVillager(157) 因与僵尸族同构，直接复用 `HumanoidAnimationProvider`。

## 8. 未解决问题与风险

- 动画定义是行为数据，移植工作量大且容易把角度/Y 轴符号写反；每类必须有数值测试。
- 动画状态与 AI 交互同步未完成，第一版 walk 参数需从客户端移动速度近似。
- 装备/透明/可见性组合需要多帧回归。
- 性能：每帧每实体每通道求值必须避免分配；bake 表缓存，pose 缓冲复用。

## 9. 参考资料

- `docs/analysis/render/entity-animation.md`
- `docs/analysis/render/entity-render-backlog.md`
- `docs/modules/entity-render.md`

- docs/analysis/render/entity-animation-architecture.md
