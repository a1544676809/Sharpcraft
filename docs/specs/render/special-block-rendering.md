<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# 特殊渲染方块（T-041）规格骨架

> 状态：`规格不足`（骨架已建，清单与逐族细节待补）
> 更新：2026-11-14

## 1. 本项要解决什么

原版里有一批方块**不能用一个立方体模型表示**，或需要额外的几何/动画/朝向处理：火把、箱子、活塞、作物，以及告示牌、旗帜、床、潜影盒等。当前实现只覆盖其中的立方体近似或占位，因此这些方块的外观与原版不同。

流体已由 `_still` 回退覆盖，**不在本项范围内**。

## 1.1 已核实的事实（2026-11-14，`ref://mc-26.3-analysis`）

| 事实 | 数值 | 证据 |
| --- | --- | --- |
| `RenderShape != MODEL` 的方块 | **9 个，全部是 `INVISIBLE`** | `src/net/minecraft/world/level/block/**` 中 `return RenderShape.INVISIBLE` 的重写：air、barrier、bubble_column、end_gateway、end_portal、light 等 |
| `ENTITYBLOCK_ANIMATED` | **0 处** | 全 `src/net` 搜索无命中——26.3 已不再用这个分类 |
| 方块实体渲染器实现 | **27 个** | `src/net/minecraft/client/renderer/blockentity/*Renderer.java` |
| 方块模型 JSON | **2883 个** | `src/assets/minecraft/models/block/` |

**结论：「265 个特殊渲染方块」不指 `RenderShape`。** 9 个 INVISIBLE 方块无需建模（只需不绘制），真正的"特殊渲染"来源是 **27 个方块实体渲染器** + 若干非立方体模型。后者绝大多数由**通用模型管线**处理（台阶、楼梯、栅栏、墙、火把、作物都是模型 JSON），不属于"特殊渲染器"。

因此 265 这个数字最可能是"模型非完整立方体的方块数"，其中需要**专门渲染器**的只有 27 类。真实工作量应按 27 类方块实体渲染器估算，而不是 265。

## 2. 清单必须来自原版，不得凭印象

任务描述里的"265 个"目前**没有可追溯来源**。规格第一步是把它变成可核查的清单：

1. 从原版方块注册表导出全部方块，记录每个的 `RenderShape`／`BlockRenderType`。
2. 筛出 **非 `MODEL`** 的（`ENTITYBLOCK_ANIMATED`／`INVISIBLE` 等），这一批是"需要特殊渲染"的**下界**。
3. 追加**模型非立方体**的方块（火把、作物、活塞臂、栅栏、台阶等）——它们的几何由模型 JSON 或代码构建，不是单个 cube。
4. 交叉核对：与渲染层里现有的 `SpecialModelRenderer`／`BlockEntityRenderer` 注册表比对，确保没有遗漏。

**产出格式**（每条一行，可直接转成子任务）：

| 方块／家族 | 原版渲染类（`ref://`） | 几何来源（模型 JSON / 代码构建） | 需要动态数据？（方块实体／状态） |
| --- | --- | --- | --- |

## 3. 分组（提案，待清单确认后细化）

按**渲染机制**分组，而不是按方块名——同组可共用一套实现与判据：

| 组 | 代表 | 机制要点 |
| --- | --- | --- |
| A 静态非立方体模型 | 火把、栅栏、台阶、楼梯、墙 | 纯模型 JSON，无动态数据；应已由现有模型加载覆盖，需先核实 |
| B 方块实体 · 静态 | 箱子、熔炉、告示牌 | 需方块实体数据（朝向、开启状态）驱动变换 |
| C 方块实体 · 动画 | 活塞臂、箱子开合、旗帜摆动 | 需逐帧插值 |
| D 作物／植被 | 小麦、胡萝卜、花 | 生长阶段换模型或换 UV |
| E 需要特殊着色 | 附魔台、信标光柱、末影箱 | 额外 pass／混合模式 |

**A 组优先核实**：如果模型加载已经覆盖，真实工作量会远小于 265。

## 4. 每族规格必须记录什么

- 原版类与方法路径（`ref://`），以及**精确常量**（枢轴、偏移、缩放、UV 布局）
- 几何构建方式：模型 JSON 还是代码；若是代码，记录其数学（盒定义、旋转、镜像）
- 动态输入：哪些方块实体字段／方块状态位影响外观
- 光照与色彩空间（本仓已统一为 gamma 空间混合，见 `docs/decisions/0013-gamma-space-ui-blending.md`）

## 5. 验证

- **单方块沙盒**：用 `MC_DEMO_STATE` 强制某个方块状态，渲一帧与参考截图逐像素对比（T-841/T-842 已建立该流程）。
- 每族至少一条真值向量（原版输入 → 期望几何/像素），不得只写结构自检。

## 6. 下一步

1. 完成 §2 的导出与筛清单（这是本项的第一件实事）
2. 按清单修正 §3 的分组
3. 每组写独立子任务，各自带验证判据

## 7. 子任务切分（22 个具体渲染器）

27 个 `*Renderer.java` 中，`BlockEntityRenderer`、`AbstractEndPortalRenderer`、`AbstractSignRenderer`、`BlockEntityWithBoundingBoxRenderer` 是基类，`TestInstanceRenderer` 仅测试用——**实际要实现的 22 个**，按可见度分三档：

### 第一档（高频可见，建议先做）

| # | 渲染器 | 对应方块 |
| --- | --- | --- |
| 1 | `ChestRenderer` | 箱子、陷阱箱、末影箱 |
| 2 | `StandingSignRenderer` + `HangingSignRenderer` | 告示牌、悬挂告示牌 |
| 3 | `ShulkerBoxRenderer` | 潜影盒（16 色） |
| 4 | `SkullBlockRenderer` | 头颅（骷髅/凋灵/玩家/生物头） |
| 5 | `BannerRenderer` | 旗帜（16 色 + 图案层） |
| 6 | `DecoratedPotRenderer` | 饰纹陶罐 |

### 第二档（功能性方块）

| # | 渲染器 | 对应方块 |
| --- | --- | --- |
| 7 | `BeaconRenderer` | 信标光柱 |
| 8 | `BellRenderer` | 钟 |
| 9 | `EnchantTableRenderer` | 附魔台（书页动画） |
| 10 | `LecternRenderer` | 讲台（书） |
| 11 | `CampfireRenderer` | 营火（食物 + 火焰） |
| 12 | `PistonHeadRenderer` | 活塞头 |
| 13 | `SpawnerRenderer` | 刷怪笼（旋转实体） |
| 14 | `TrialSpawnerRenderer` | 试炼刷怪笼 |
| 15 | `VaultRenderer` | 宝库 |
| 16 | `ConduitRenderer` | 潮涌核心 |

### 第三档（低频）

| # | 渲染器 | 对应方块 |
| --- | --- | --- |
| 17 | `BrushableBlockRenderer` | 可疑的沙/砾石 |
| 18 | `ShelfRenderer` | 书架 |
| 19 | `CopperGolemStatueBlockRenderer` | 铜傀儡雕像 |
| 20 | `TheEndGatewayRenderer` | 末地折跃门 |
| 21 | `TheEndPortalRenderer` | 末地传送门 |

### 每档的验收方式

- **真值向量**：从分析工作区提取该方块的原始几何参数（枢轴、偏移、缩放、UV）落盘为测试基准（原则 14）
- **单方块沙盒**：用 `MC_DEMO_STATE` 强制方块状态，渲一帧与参考截图逐像素对比
- **不得**用自写期望值当等效性证据

### 建议顺序

第一档 1–6 优先：出现频率最高、玩家最容易注意到差异；且箱子/告示牌/潜影盒共享"轴对齐盒 + 纹理"的实现骨架，可复用。

## 8. 当前集成点（2026-11-14 核实）

仓库里方块实体渲染的现状：

| 环节 | 位置 | 说明 |
| --- | --- | --- |
| 数据 | `Sharpcraft.Common.World.BlockEntityRecord`（`LocalX/Y/LocalZ/TypeId`） | 由 `ChunkPacketParser` 解析 |
| 渲染状态 | `Sharpcraft.Client.Render.BlockEntityRenderState`（世界坐标 + `TypeId`） | `FrameCommand` 组装 |
| **几何** | **`BlockEntityPlaceholderMesher`** | **占位实现——当前所有方块实体都画成占位几何** |
| 合并 | `FrameCommand` 把方块实体顶点追加到地形顶点数组 | 共用不透明 stage |

**因此 T-041 的实现路径是：新增 `ChestMesher`（按 §1 的真值向量），在 `FrameCommand` 按 `TypeId` 分派，替代 `BlockEntityPlaceholderMesher` 对箱子的处理。**

已有可复用素材：`SpecialModelDefinitions.ChestTexture = "entity/chest/normal"`、`ChestTextureWidth/Height = 64/64`。

**尚未确认**：`BlockEntityPlaceholderMesher` 的顶点布局（必须与地形同一 `VertexLayout`），以及 `TypeId` 到方块类型的映射表在哪定义。这两项是动手前的最后侦察。
