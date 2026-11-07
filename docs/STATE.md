<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: STATE —— 跨会话进度锚点
summary: 当前里程碑、进行中、未提交改动、阻塞、下一步、总体行动大纲
status: 活文档
updated: 2026-11-06
-->

# STATE —— 跨会话进度锚点

> 本文件只记录**易变状态**，且受原则 14 与架构测试硬约束：**严格控制在 150 行以内**（历史排障记录由 Git 历史保存，禁止在此写流水账）。
> 设计结论见 `docs/modules/`，里程碑定义见 `docs/milestones/roadmap.md`，已知缺口见 `docs/specs/render/known-gaps.md`，验收矩阵见 `docs/specs/quality/vanilla-equivalence-matrix.md`。

最后更新：2026-11-06

## 当前里程碑

| 里程碑 | 状态 | 出口证据摘要 |
| --- | --- | --- |
| M1 基础逻辑地基 | **已完成** | 架构门禁、随机数/注册表/状态 ID 真值向量、真实服务器进 play |
| M2 世界数据闭环 | **已完成** | NBT、`level_chunk_with_light` 解析、WGPU 首帧出图 |
| M3 资产管线 + 启动器闭环 | **已完成** | 官方 26.3 jar 校验打包、多账号离线闸门、真实服务器带纹理窗口 |
| M4 实体系统根基 + 输入输出 | **已完成** | 20 Hz tick、移动同步（4.318 b/s）、走进去挖方块闭环、流式区块加载 |
| M5 协议深化（5.1/5.2/5.3） | **进行中** | 5.1 实体同步完成；5.2 聊天收发完成；5.3 三层地形/流体/时间天气/参考 OIT/天空盘/视距 32 完成；实体模型与动画长尾进行中 |
| M6 模组与着色器包 | **进行中** | 模组宿主与着色器包契约层就位；ALC 回收验证与包执行器接线待收口 |

## 进行中

| 任务 | 状态 / 关键点 |
| --- | --- |
| **实体模型与动画主线** | 真实部件模型 + 逐实体动画 provider 已大量落地（humanoid/baby/quadruped/aquatic/nether 等）；剩余：T-631 held-item/item-model（解锁 SPEAR/STAB）、Happy Ghast ropes、EnderDragon flight history、Parrot PARTY / Bee stinger / Guardian eye look / Piglin crossbow-hold |
| **架构：原则 14 / Phase 0 / Phase 1** | 已从 main 合入 T-654..T-661（原则 14、STATE 门禁、`PlaySession.TryHandleTransport`、`MeshingContext`、方块实体 NBT/类型注册表、`OcclusionShape`）。剩余：P0.3 帧 Pass 单轨化、P1.4 `registry_data` 动态装配、P1.5 `OcclusionShape` 完整接入 `ChunkMesher` 替换 `IsSolid` |

## 未提交改动

- 无。
- 后台任务：无。

## 已完成（近期）

1. **T-662 / T-663 Elytra baby 收尾（2026-11-06）**：`ElytraModel.BABY_TRANSFORMER = MeshTransformer.scaling(0.5F)` 的 scale 后 y 偏移 `24.016×(1−0.5)=12.008` 已计入（`Bottom=2`、`PivotY=12`）；**T-663 修复 UV**：`MeshDefinition.transformed` 只改 root pose，子 CubeDefinition 仍 10×20×2，`EntityModelPart.UvWidth/UvHeight/UvDepth=10/20/2` 保持与成人相同采样；补 `YOffsetSelector` crouch 位移（婴儿 1.5px / 成人 3px）。15 项 Elytra 测试通过。
2. **T-664 VecDelta.Stepped（2026-11-06）**：`EntityMove` 携带逐步 fixed-point delta，`EntityTracker.ApplyMove` 逐 step 解码并按其 ticks 入队，不再丢失多步轨迹。
3. **T-665 模型 UV 不变量门禁（2026-11-06）**：`ModelUvInvariantTests` 全注册表 UV 边界/面积扫描 + 成人/幼年鞘翅 48 顶点 UV 逐值一致 + `UvWidth/UvHeight/UvDepth` 同键一致性；4 项通过。
4. **T-666 协议分发覆盖门禁（2026-11-06）**：`ProtocolDispatchGateTests` 反射枚举 63 个 PlayClientbound 常量，强制 `PlaySession` 的解析包都有 live consumer（5 个 transport/preload-owned 白名单 + Transfer 显式未处理）；3 项通过。
5. **T-667 MeshTransformer scaling 集中化（2026-11-06）**：新增 `VanillaMeshBake.Scale`，统一 `MeshTransformer.scaling` 的 `yOffset=24.016×(1−factor)`、几何/pivot/grow 与 UV 原始盒；`WithElytra` baby 分支已迁移，6 项 bake 测试 + Elytra 测试通过。
6. **T-668 LivePlaySession 有界队列（2026-11-06）**：64 MiB `BoundedReceiveQueue` + 原子字节计数；`PumpAsync` 的异常统一写入 `_failure`，队列溢出直接判定会话失效；5 项队列/格式化测试通过。
7. **T-669 根/子骨骼坐标烘焙集中化（2026-11-06）**：`VanillaMeshBake.BakeRoot/BakeChild` 收纳 `FromVanilla/FromVanillaChild` 转换，`PlayerModelDefinition` 改为薄委托；3 项/4 cases 转换等价测试通过。
8. **T-670 Vanilla MeshDefinition DSL 核心（2026-11-06）**：新增 `VanillaPartPose/VanillaCubeDefinition/VanillaPartDefinition/VanillaMeshDefinition` + `VanillaMeshBake.Bake`；pre-order 展平、纹理继承/覆盖、重复名/空 root 校验；4 项测试通过。
9. **T-671 首个真实模型 DSL 迁移（2026-11-06）**：`PlayerModelDefinition.AdultParts` 改为 `VanillaMeshDefinition` 树 + `VanillaMeshBake.Bake`；`FromVanilla/FromVanillaChild` 保留；新增结构化值比较器，`OverlayLayers` 按值比较；3 项测试通过。
10. **T-672 Zombie/Skeleton 人形 DSL 迁移（2026-11-06）**：两套六部件模型改走 DSL；抽出共享 `ModelPartAssertions`；WitherSkeleton/Stray/Bogged 兼容性测试通过；3 项新测试。
11. **T-673 Enderman/Drowned/Husk DSL 迁移（2026-11-06）**：三套六部件模型改走 DSL，保留 Enderman modelHeight 25、DrownedOuter overlay、Husk scale/mirror；新增 1 项 legacy 等价测试。
12. **T-674 BabyModelDefinitionsHumanoid DSL 迁移（2026-11-06）**：Zombie/ZombieVillager/Villager 改为嵌套 DSL 树，Drowned 保留 outer_* 复制；6 项值等价/顺序/Parent/变体测试通过。
13. **T-675 WithClothing DSL 迁移（2026-11-06）**：`VanillaCubeDefinition.PoseSource` 透传；Stray/Bogged 六个 outer_* 部件改 DSL；Humanoid 全家族迁移完成；5 项 humanoid 测试通过。
14. **T-676 Pig/Cow/Chicken DSL 迁移（2026-11-06）**：三套 Normal/Warm/Cold 四足模型改走 DSL，保留 cold overlays/snout UV/horn rotations/zero-width fin；4 项测试通过。
15. **T-677 Sheep/Rabbit/Frog/Tadpole DSL 迁移（2026-11-06）**：12/13/18/2 部件（含深层 child 树与 zero-size pivot）全部改走 DSL；7 项值/顺序/Parent/变体测试通过。
16. **T-678 Allay/Bat/Bee DSL 迁移（2026-11-06）**：8/9/10 部件树改走 DSL，Allay Translucent、Bee textureSelector 保留；7 项测试通过；Breeze 留待 DSL 增加 EmissiveOnly/per-part texture 字段。
17. **T-679 Breeze DSL 迁移（2026-11-06）**：DSL 增加 `IncludeInEmissive/EmissiveOnly/TextureWidth/TextureHeight` per-cube 字段；Breeze 17 部件（EmissiveOnly eyes + 7 个 wind shell）迁移完成；4 项测试通过。
18. **T-680 Aquatic 家族 DSL 迁移（2026-11-06）**：Cod/Salmon/Pufferfish/TropicalFish/Dolphin 全部迁移；Salmon scale/顺序、Pufferfish 三态、TropicalFish pattern copies 保持；8 项测试通过。
19. **T-681 Nether 模型 DSL 迁移（2026-11-06）**：Hoglin/Strider/Blaze/Ghast/HappyGhast 五套模型迁移，保留 Zoglin 共享、cold/shooting selector、程序化 rods/tentacles 与 scales；7 项测试通过。
20. **T-682 Piglin DSL 迁移（2026-11-06）**：复用 AdultParts 的 body/arms/legs 前缀 + 6 部件 piglin head DSL 树；Definition/Brute/Zombified 元数据不变；3 项测试通过。
21. **T-683 Villager/Illager 系列 DSL 迁移（2026-11-06）**：DSL 增加 MirrorX/VisibleWhen；Villager/ZombieVillager/Witch/Illager 迁移完成；变体/VariantMap/scale 保持；7 项测试通过。
22. **T-684 Cat/Wolf/Fox/Panda DSL 迁移（2026-11-06）**：四套动物模型迁移，保留 Cat/Wolf collar copylayer、Fox sleeping selector、Panda variants；7 项测试通过。
23. **T-685 Camel/Horse/Llama/Goat DSL 迁移（2026-11-07）**：四套大型四足模型迁移；Horse `WithHorseMarkings`/5 个 equine 定义、Llama 4 毛色保持；7 项测试通过。
24. **T-654..T-661 main 合并（2026-11-06）**：原则 14 + 单轨收敛 + 方块实体/遮挡地基并入本分支；`PlaySession` 冲突取 main 的“未处理包全部延迟”版本；traceability 编号顺延（原 main T-344..T-351 → T-654..T-661）。
25. **T-648..T-650 / T-651..T-653 渲染与解耦（2026-11-05~06）**：Elytra 翼盒与 UV 原版等价、双翼镜像全宽；流体面剔除修复（玻璃后水可见）；云 OIT depth-bounds 写深度；`Sharpcraft.Assets` 分层 + 启动器/客户端解耦（ADR-0026/0027）。
26. **实体模型/动画批量落地（2026-11-02~05）**：全部 baby 模型族、villager/zombie/horse 变体、cat/wolf 项圈、panda 基因、大量动画 provider 与测试。
27. **协议与渲染长尾（2026-11-01~05）**：Play Clientbound 覆盖审计 + P0/P1 包批次（T-642..T-647）；反相 Z 迁移；参考 OIT 云/流体；天空盘逐像素近似。

## 阻塞项

- 无硬阻塞。
- **R-017**：XSTS 端点偶发网络抖动，联网全链路验收脚本含重试策略。
- **R-019**：模组 `AssemblyLoadContext` 热卸载后的 GC 回收探针尚未稳定转绿（排入 Phase 4 P4.1）。

## 下一步具体动作

1. **T-631 held-item/item-model**：先按原则 14 补 `docs/modules/entity-equipment.md` 的原版源码映射表与 ≥5 组 Golden Vectors（`ItemInHandRenderer`/`ItemModelResolver`），再实现 SPEAR/STAB。
2. **Happy Ghast ropes / EnderDragon flight history**：按实体渲染 backlog 顺序推进。
3. **P1.5（`Client/Render/ChunkMesher`）**：用 `OcclusionShape.IsFaceOccludedByState/Self` 替换 `IsSolid` 近似；配 `FluidMesherTests` 真值回归。
4. **P1.4（T-042）**：提取 26.3 `registry_data` 真值向量，把服务器下发注册表动态装配进 `BlockStateLayout`。
5. **P0.3（`Framework/Rendering`）**：`OffscreenRenderer` 与 `WindowedRenderer` 收敛为共用世界 Pass 序列。

## 最近提交

- `bd424b3` fix(render): decouple baby elytra UVs and preserve stepped move deltas (T-663/T-664)
- `5a5a9c9` docs: record the latest commits in STATE
- `7b68fe3` feat(render): finish the baby elytra variant (ELYTRA_BABY) (T-662)
- `b1b65df` merge: main (principle 14 guardrails, Phase 0/1 foundation)
- `b4d32e2` docs: reconcile the equivalence matrix with the branch state
