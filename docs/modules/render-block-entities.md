<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# 方块实体 raw 源帧迁移（task-42 / #102）

> 本文件记录方块实体/静态模型族向 `RawSourceFrame` 路径的迁移。实体模型族见 `docs/modules/render.md`
> 与 task-41；两文件由不同分支维护以避免写集冲突。

## 状态

| 族 | 渲染器/网格器 | 状态 |
| --- | --- | --- |
| Conduit | `ConduitRenderer` / `ConduitModelDefinition` | 已 raw（main 基准，本批验证无退化） |
| Banner | `BannerRenderer` / `BannerModelDefinition` | 已 raw；#111 静止倾角修正 |
| Skull | `SkullRenderer` / `SkullModelDefinition` | 已 raw（mob 四型 + player head）；Piglin/Dragon 属 entity/scale bake，未改 |
| Book/Lectern/Enchant | `LecternRenderer` / `EnchantTableRenderer` / `BookModelDefinition` | 阻塞于基准样本口径（见下）；本批未迁移 |
| Statue | `CopperGolemStatueMesher` / `CopperGolemStatueModelDefinition` | 与 entity 模型跨族约束，最后处理 |
| Chest/ShulkerBox/Bell/Pot/Placeholder | 直接网格几何（`EntityPlaceholderMesher.AppendBox` 等） | N/A（不走 entity model bake，见下） |
| Shelf/Campfire/Brushable/Vault | item mesher（`BlockEntityItemMesher`） | N/A（不走 entity model bake，见下） |

## 原版源码映射表（本批相关）

| 本仓类型 | 原版类与方法（ref://） |
| --- | --- |
| `BannerModelDefinition` / `BannerRenderer` | `BannerModel.createBodyLayer` (BannerModel.java:24-35)、`BannerFlagModel.createFlagLayer`/`setupAnim` (BannerFlagModel.java:21-35)、`BannerRenderer.java:40-42,239-249` |
| `SkullModelDefinition` / `SkullRenderer` | `SkullModel.createMobHeadLayer` (SkullModel.java:19-39)、`SkullBlockRenderer.java:154-168` |
| `BookModelDefinition` / `LecternRenderer` / `EnchantTableRenderer` | `BookModel.createBodyLayer`/`setupAnim` (BookModel.java:35-73)、`LecternRenderer.java:44-53`、`EnchantTableRenderer.java:58-85` |

## 原版基准样本（reference samples）

- `tests/Sharpcraft.Client.Tests/Repro/Data/banner-vectors.golden.txt`：由原版取样器采样，含 body 与 flag 四组严格位置+UV；flag 组用 `part=flag:X:-0.45`（源字面值）。
- `tests/Sharpcraft.Client.Tests/Repro/Data/skull-raw-vectors.golden.txt`：由 `ref://mc-26.3-analysis/vectors/SkullVectorProbe.java` 采样，含 16 ground + 4 wall 共 20 组 skull head 严格位置+UV。
- 术语统一为“原版基准样本（reference samples）”，不用“金值”。

## 已知刻意的简化与阻塞

> 本批只登记与本批行为相关的项；原有 BE 简化（`SIM-BE-*`）继续见 `docs/modules/render.md` 与 `docs/risks.md`。

- **raw scaled / mirror 未覆盖**：`FromTree` 对 raw + 非单位 PartPose scale 抛 `NotSupportedException`（沿用 main 基准的登记）；raw mirror 仅按源 `CubeListBuilder.mirror` 的 U flip 实现，尚未对镜像模型逐点取样。状态：受控限制，独立审计任务 T-102-RAW-SCALE-MIRROR（待建 Issue）。
- **Statue ↔ CopperGolem entity 跨族 equality**：`CopperGolemStatueMesherTests.StandingMatchesUnposedEntityModel` 要求 statue 与 entity（`CopperGolemModelDefinition`，task-41 写集）逐字节一致；statue 迁移必须与 entity raw 在同一次集成中验收，不得单方修改测试。状态：阻塞于跨分支集成，不是本批代码缺陷。

## 实现与原版类对应

| 本仓 | 原版 |
| --- | --- |
| `BannerRenderer.ModelTransform` | `BannerRenderer.createGroundTransformation`/`createWallTransformation` (BannerRenderer.java:243-249) |
| `BannerRenderer.PhaseRotation` | `BannerFlagModel.setupAnim` (BannerFlagModel.java:32-35) |
| `BannerSourceFormula`（测试辅助） | 同一源公式的独立实现，仅用于测试期望推导，不调用生产渲染器 |

## 已知刻意的简化与阻塞（续）

- **Book/Lectern/Enchant 阻塞于原版基准样本口径**：书模型带 `setupAnim` 姿态（lid/page 旋转 + page x 位移）与复合外层变换。已写独立 jar 探针 `ref://mc-26.3-analysis/vectors/BookVectorProbe.java`（应用源 setupAnim + Lectern/Enchant 外层 pose），位置与现有内联样本一致（如 lectern facing 2 的 (0.4375,0.8951,0.0958)），但 UV 与内联样本不同（内联 `u=6/64=0.09375`，探针 `u≈0.218828`）——该内联 UV 是 legacy face 校准值，需确认哪个是源字面值后再迁移，不得按实现反推。状态：**阻塞于评审**（Issue #119 / T-102-BOOK-UV）。已尝试路径：BookVectorProbe 采样、位置交叉校验；未提交任何 Book 代码改动。
- **直接网格 / item mesher 渲染器不适用 raw（N/A）**：`ChestMesher`、`ShulkerBoxMesher`、`BellMesher`、`DecoratedPotMesher`、`BlockEntityPlaceholderMesher` 用 `EntityPlaceholderMesher.AppendBox` 直接构造盒子（`grep AppendBox <file>` 可证）；`ShelfRenderer`、`CampfireRenderer`、`BrushableRenderer`、`VaultRenderer` 用 `BlockEntityItemMesher.AppendSlot` 直接构造物品几何。它们不消费 `EntityModelDefinition`/`BuildModel`，不存在 frame 烘焙补偿，故 raw 迁移不适用（非阻塞）。`ChestAtlas` 仅做纹理装载，不涉及模型帧。

## 实现说明：Skull raw 迁移

- `Definition`（mob 四型共用）与 `PlayerDefinition` 切 `RawSourceFrame: true`、`baselinePx:0`、无 legacy face transforms；删除 renderer-local `translate(0,-0.5,0)` 补偿。源 `SkullBlockRenderer` 的 `scale(-1,-1,1)` 外pose 本身完成 y-up 翻转。
- 源 `SkullModel.createMobHeadLayer` 的顶点 v1 UV 为 `(0.125,0)`；legacy face 校准表曾把它改成 `0.375`。raw 后与 jar 一致（同 #111 类别的 face 校准伪影）。
- Piglin head（`PiglinModelDefinition.Head`）与 Dragon head（非单位 scale bake）属 entity 模型族 / 专用 bake，未在本批修改。
- `SkullRawReferenceTests` 用 jar 探针样本严格比对 16 ground + 4 wall 共 20 组（每组 24 顶点位置+UV），另有 2 组 player head（`player_ground_0` / `player_wall_2`，各 48 顶点 = 8×8×8 head + 0.25-grow hat overlay）严格比对。四型 mob head 共享 8×8×8 head 几何（`SkullModel.createMobHeadLayer`），由 skeleton 组代表；player head 单独取样覆盖 hat overlay。`SkullRendererTests.cs` 的 player 内联值（含 hat overlay UV）已由该 jar 样本佐证。

## 更新记录

- 2026-11-17：Banner 切 raw；#111 修正；严格 reference sample 替换缺陷钉死测试；全量 3,335 / 三门禁通过。
- 2026-11-17：Skull（mob 四型 + player）切 raw；新增 jar 探针 skull reference sample；旧内联 face-calibration 顶点期望替换为 jar 值；全量 3,335 / 三门禁通过。
- 2026-11-17（审阅修正 task-46）：Banner 样本扩为 36 组（4 jar 锚点 + 32 源公式派生），`BannerGoldenTests` 改严格逐顶点全元组比较；新增 Skull player head 2 组 jar 样本；`render.md` 的 #111 状态统一为已解决；全量 3,369 / 三门禁通过。
