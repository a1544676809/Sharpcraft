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

## 9. 实现草案（侦察完成，可直接编码）

### 9.1 顶点与几何 API

| 项 | 事实 |
| --- | --- |
| 顶点写入 | `TerrainVertexWriter.Write(dest, ref offset, x, y, z, r, g, b, u, v, sprite, sky, block)` ——**与地形同一布局**（`ChunkMesher.FloatsPerVertex = 6` 个 32 位槽 / 24 字节） |
| 盒构建 | `EntityPlaceholderMesher.AppendBox(geometry, feet, width, height, red, green, blue, sprite, ref vertexFloats, ref indexCount, depth?, entityTransform?)` |
| `feet` 语义 | **底面中心点**（`minY = feet.Y`，X/Z 以 `feet` 为中心展开） |
| 光照 | 占位实现固定 `sky: 1f, block: 0f`（白天全亮）——真实实现需按方块位置采样光照 |
| 动态几何 | `DynamicGeometry`（`Clear/Reserve/Vertices/Indices/VertexFloats/IndexCount`） |

### 9.2 变换 API

`EntityModelTransform(Translation, YawDegrees, PitchDegrees, Scale, ScaleXZ, ScaleY, HasAttachRotation, AttachRotation, AttachPivot)`

`Apply` 的顺序：缩放 → Y 旋转 → X 旋转 → **绕 `AttachPivot` 的 `AttachRotation`** → 平移。

**关键**：`AttachRotation` + `AttachPivot` 就是一个通用的"绕任意枢轴旋转"，正好可用于箱盖开合——不需要新增变换类型。

### 9.3 箱子的绝对坐标（由 §1 真值向量换算，单位 = 方块）

| 部件 | 底面中心 `feet` | width | height | depth | 绕枢轴旋转 |
| --- | --- | --- | --- | --- | --- |
| `bottom` | `(8, 0, 8) / 16` | `14/16` | `10/16` | `14/16` | 无 |
| `lid` | `(8, 9, 8) / 16` | `14/16` | `5/16` | `14/16` | 绕 `(0, 9, 1)/16`，X 轴，`-(open' × π/2)` |
| `lock` | `(8, 9, 15.5) / 16` | `2/16` | `4/16` | `1/16` | 同 `lid` |

`lock` 的 `feet`：盒绝对范围 `x=7..9`（中心 8）、`y=7..11`（底面 7，高 4）、`z=15..16`（中心 15.5）。

### 9.4 类型注册表

`Sharpcraft.Common.World.VanillaBlockEntityTypes`（49 项，id 0..48）已提供：
- `Chest = 1`、`TrappedChest = 2`、`EnderChest = 3`、`ShulkerBox = 24`
- `PathOf(int)` / `IdOf(string)` / `IsChestLike(int)`

**结论：箱子渲染不需要新增注册表**，直接按 `TypeId` 分派即可。

### 9.5 光照（已查清原版做法，可行）

**原版**：`BlockEntityRenderState.extractBase` 调 `LightCoordsUtil.getLightCoords(level, pos, state)`：

- `state.emissiveRendering()` 为真 → `FULL_BRIGHT = 15728880`
- 否则取 `pack(blockLight, skyLight)`，其中 `pack(block, sky) = block << 4 | sky << 20`，两者各为 **0..15**
- 再加自发光下限：`block < state.getLightEmission()` 时抬到该值

**本仓现状**：`ClientWorld` 已提供逐方块光照查询：

```
public byte GetSkyLight(int x, int y, int z)     // 0..15，未加载区块返回 0
public byte GetBlockLight(int x, int y, int z)   // 同
```

**结论：不需要新的世界 API，也不需要扩展协议解析。** 做法：

1. `BlockEntityRenderState` 增加两个字段 `Sky` / `Block`（byte，0..15）
2. `FrameCommand` 构造状态时用 `world.GetSkyLight(...)` / `GetBlockLight(...)` 填充（位置取方块实体所在方块）
3. mesher 传给 `TerrainVertexWriter.Write` 时归一化：`sky = Sky / 15f`、`block = Block / 15f`

箱子自发光为 0，`emissiveRendering()` 为假，因此不需要自发光分支。

### 9.6 剩余待确认

1. `DynamicGeometry` 的容量语义（`Reserve` 参数单位）
2. `FrameCommand` 里 `BlockEntityPlaceholderMesher` 的调用点如何替换（按 `TypeId` 分派 vs 新建 `BlockEntityMesher` 统一入口）

## 10. 已实现与阻塞（2026-11-14）

### 已实现

- `src/Sharpcraft.Client/Render/ChestMesher.cs`：按 §1 真值向量生成三个盒（箱体 / 箱盖 / 锁扣），箱盖与锁扣通过 `EntityModelTransform.AttachRotation` 绕 `(0, 9, 1)` 枢轴摆动；`OpenAngle` 实现三次方缓出。
- `EntityPlaceholderMesher.AppendBox` 增加可选 `sky` / `block` 参数（默认 `1f` / `0f`，**现有调用行为不变**）。
- `BlockEntityRenderState` 增加 `Sky` / `Block` / `Open`（默认 `15` / `0` / `0`）。
- `tests/Sharpcraft.Client.Tests/ChestMesherGoldenTests.cs`：6 条真值断言（缓出角度、模型常量、盒数、非箱子跳过）。全套 **2724** 测试通过。

### 阻塞：箱子贴图不在方块图集

`SpecialModelDefinitions.ChestTexture = "entity/chest/normal"` 被装入 **`ItemAtlas`**（`WindowCommand` 的 `ItemAtlas.Load([... ChestTexture])`），而方块实体的占位几何是**合并进地形顶点数组**、用**方块图集**绘制的。

因此 `ChestMesher` 的几何虽然正确，但**无法在当前合并路径上用正确贴图绘制**——它需要走**物品/实体图集**的独立绘制段。

### 已按原版确认（2026-11-14）

查 `ref://mc-26.3-analysis/src/net/minecraft/client/renderer/Sheets.java`：**原版为每类方块实体建专属图集**，既不是方块图集也不是物品图集。

| 常量 | 图集路径 |
| --- | --- |
| `CHEST_SHEET` | `textures/atlas/chest.png` |
| `SHULKER_SHEET` | `textures/atlas/shulker_boxes.png` |
| `BANNER_SHEET` | `textures/atlas/banner_patterns.png` |
| `SHIELD_SHEET` | `textures/atlas/shield_patterns.png` |
| `DECORATED_POT_SHEET` | `textures/atlas/decorated_pot.png` |

箱子图集由 **22 张**贴图拼接而成（`textures/entity/chest/`）：`normal` / `normal_left` / `normal_right`、`trapped*`、`ender`、`copper*`（4 个氧化阶段 × 单/左/右）、`christmas*`。

**结论：我先前提的两个方案都不符合原版。**

- 方案 1（绑定物品图集）——原版不这样
- 方案 2（并入方块图集）——原版也不这样

**正确做法**：方块实体走**独立绘制段**，并为每类方块实体建**专属图集**（`textures/atlas/<family>.png`），用与本仓 `ItemAtlas.Load(resources, [路径...])` 相同的拼接机制从 `textures/entity/chest/*.png` 生成箱子图集。

本仓现状偏差：`SpecialModelDefinitions.ChestTexture = "entity/chest/normal"` 被放进 `ItemAtlas`——那只适用于**物品**形态的箱子模型；方块实体渲染应改用专属图集。

### 实现顺序（按原版）

1. `ChestAtlas`：拼接 22 张箱子贴图（复用 `ItemAtlas.Load` 机制）
2. 方块实体独立绘制段：绑定该图集、独立绘制范围
3. `FrameCommand` 按 `TypeId` 分派到 `ChestMesher`

### 已登记的实现简化（原则 15）

| 简化 | 影响 | 计划 |
| --- | --- | --- |
| 箱子一律按**关闭**状态绘制 | 看不到开合动画 | `Open` 字段已预留；需方块实体 NBT/动画数据源 |
| 无朝向旋转 | 所有箱子朝向一致 | 需方块状态的 `facing`；当前 `BlockEntityRecord` 未携带 |
| 未接入 `FrameCommand` 绘制路径 | 不影响现有渲染 | 阻塞于上述图集问题 |

## 11. 绘制段侦察（2026-11-14）

### 现状

`FrameExecutor.Record` 的不透明场景 pass 中：

- L368：`backend.SetBindGroup(pass, 0, bindGroup)` —— **整帧只有一个 group 0 绑定**（方块图集）
- `DrawScenePass(WorldRenderPassKind kind)` 按 pass 类型分派（Sky / TerrainOpaque / …）

地形顶点与当前占位方块实体几何**合并在同一组顶点数组**里，因此共用同一个绑定。

### 结论：可以在同一 pass 内切换绑定

现有结构支持在 opaque pass 内：

```
SetBindGroup(pass, 0, terrainBindGroup)
  → 绘制地形范围
SetBindGroup(pass, 0, chestBindGroup)      // 切到箱子专属图集
  → 绘制方块实体范围
SetBindGroup(pass, 0, terrainBindGroup)    // 还原（若后续还有地形绘制）
```

前提：箱子 bind group 与地形**共用同一 `BindGroupLayout`**（uniform + texture + sampler）。若布局一致，**可复用现有地形管线**，不需要新管线。

### 改动清单（预估 3–4 个文件）

| 文件 | 改动 |
| --- | --- |
| `FrameResources.cs` | `Options` 增加可选 `BlockEntityBindGroup` |
| `FrameExecutor.cs` | opaque pass 末尾按范围切换绑定并绘制方块实体段 |
| `FrameCommand.cs` | 建 `ChestAtlas` + bind group，独立索引范围（不再合并进地形数组） |
| `FramePipelines.cs` | 仅当绑定布局不一致时才需要新增管线变体 |

### 待确认项已解决（2026-11-14）

**`EntitySkinTextureGpu` 就是现成的"纹理 → 世界布局 bind group"helper。**

| 事实 | 位置 |
| --- | --- |
| `public void Upload(backend, bindGroupLayout, TextureData, cameraBuffer, fogBuffer, lightmapBuffer, …)` | `EntitySkinTextureGpu.cs` L26 |
| `public BindGroupHandle BindGroup { get; private set; }` | 同文件 L21 |
| 世界 bind group 布局 | `WindowedRenderer.cs` L165–176：Texture(0) + Sampler(1) + Camera(2) + Fog(3) + Lightmap + Sprite storage |

现有 `itemAtlasGpu.Upload(backend, bindGroupLayout, itemData, …)`（L222）走的就是这条路。因此箱子图集**完全复用同一模式**：

```csharp
using var chestAtlasGpu = new EntitySkinTextureGpu();
chestAtlasGpu.Upload(backend, bindGroupLayout, chestAtlas.Texture, cameraBuffer, fogBuffer, lightmapBuffer, …);
// chestAtlasGpu.BindGroup 即为世界布局的绑定
```

**结论：不需要新的纹理类、不需要新的布局、不需要新的管线。** 箱子绘制段 = `ChestAtlas` + `EntitySkinTextureGpu` + 在 opaque pass 内切换绑定。

### 风险

`FrameExecutor` 是**窗口与离屏共用**的单一执行路径（原则 14 单轨要求）。改动它会影响两条路径，必须同时验证：

- 沙盒帧 `9CA9E034…` / 34897 B 逐字节不变（无方块实体时不受影响）
- 窗口冒烟帧率正常

## 12. 绘制段：实测超出微切片，需拆三步（2026-11-14）

尝试直接实现后确认：**不是 3–4 个文件的小改动**。实际障碍是**几何的归属**。

### 障碍

`TerrainPassRecorder.RecordOpaque` 通过 `_arena.Draw(pass, …)` 绘制，而 `_arena` 是 `ChunkGeometryStore`——**它自己拥有顶点/索引缓冲与绘制范围**。

而方块实体几何目前是**合并进静态世界网格**（`FrameCommand` 的 `mesh`），走 `StaticWorldBuffers`，**不在 arena 里**。因此「在 opaque pass 里切绑定再画方块实体范围」缺少一个前提：**那段几何没有自己的缓冲与绘制入口**。

### 必须拆成三步（原则 14：≤3 文件 / ≤150 行）

**第 1 步：几何独立**
- `FrameCommand` 不再把方块实体顶点并入地形 `mesh`
- 改为写入 `ChunkGeometryStore`（新建一个方块实体 arena）或独立缓冲
- 改动：`FrameCommand` + `ChunkGeometryStore`（或新增一个轻量缓冲类）

**第 2 步：绘制入口**
- `TerrainPassRecorder` 增加「用指定 bind group 绘制方块实体段」的方法
- `FrameExecutor` 在 opaque pass 末尾调用
- 改动：`TerrainPassRecorder` + `FrameExecutor`

**第 3 步：图集与分派**
- `WindowCommand` / `FrameCommand` 用 `EntitySkinTextureGpu` 上传 `ChestAtlas`
- `FrameResources.Options.BlockEntityBindGroup` 传入
- 按 `TypeId` 分派 `ChestMesher`
- 改动：`WindowCommand` + `FrameCommand` + `FrameResources`

### 本次尝试的处理

已加的两处字段（`FrameRequest.BlockEntityVertices/Indices/Atlas`、`FrameResources.Options.BlockEntityBindGroup`）**已回退**——没有消费方的字段属于债务，不应留在树里。

### 建议

第 1 步是关键决策点（方块实体几何放 arena 还是独立缓冲），建议单独评审后再动手。前两步可并行评审，第 3 步依赖前两步。

## 13. 原版的几何归属（2026-11-14 确认）

### 事实

| 环节 | 原版做法 | 证据 |
| --- | --- | --- |
| 地形 | `SectionRenderDispatcher` → 每个 section 独立编译的网格 | — |
| **方块实体** | **`submitBlockEntities` → `SubmitNodeCollector`** | `LevelRenderer.java` L326–330、L1008–1021 |
| 收集 | `SubmitNodeCollector` 按 **render type** 收集绘制提交，稍后分批成 GPU 缓冲 | `SubmitNodeCollector.java`、`SubmitNodeStorage` |
| 坐标 | `poseStack.translate(blockPos - cameraPos)`——**相机相对坐标** | `LevelRenderer.java` L1017 |

### 结论

**原版把方块实体从地形几何中完全剥离**：走 `SubmitNodeCollector` 的独立提交路径，按 render type 分批，每批自带图集与管线。

因此我前面列的方案 A（放进 `ChunkGeometryStore` 的 arena）**不符合原版**——arena 是区块地形的编译产物，方块实体从不进入它。

**正确做法 = 方案 B 的强化版**：方块实体有自己的提交与缓冲，独立于地形 arena。

### 对三步拆分的影响

第 1 步改为：**为方块实体建立独立的顶点/索引提交**（对应原版的 `SubmitNodeCollector` 分批），而不是复用 `ChunkGeometryStore`。

顺带记录一条原版细节：**方块实体变换用相机相对坐标**（`blockPos - cameraPos`），不是世界绝对坐标。我们的 `BlockEntityRenderState` 目前用世界坐标——接入时应改为相机相对，避免远处浮点精度抖动。
