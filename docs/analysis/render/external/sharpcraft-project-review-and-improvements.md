<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: Sharpcraft 全量项目审查与改进方案
summary: 外部审查稿：专项缺陷诊断、全库隐藏缺陷、架构分叉与改进优先级
status: 外部审查稿（原始存档）
updated: 2026-11-06
-->

# Sharpcraft 全量项目深度审查、缺陷剖析与工程化改进方案

**审查基准**：`Sharpcraft-clean-20261106.zip`（对标 Minecraft Java Edition `26.3` / 协议号 `775`）

**文档定位**：面向架构师与研发 Agents 的技术审查与修复交接指南（涵盖专项 Bug 诊断、全库隐藏缺陷、架构分叉治理与实现保证体系）。

## 一、 专项诊断：为什么小僵尸（Baby Zombie）的鞘翅工作不正常？

在 `docs/STATE.md` 中，`T-662`（`WingsLayer` 幼年分支 `elytra_baby`）已被标记为“已完成”，且上轮针对成年鞘翅的 Z-fighting 与中缝修复（`T-666` / `RotationZyx` 矩阵乘法顺序修正）已合入代码库。但在实际渲染中，**小僵尸（及所有幼年人形生物）佩戴鞘翅时依然会出现严重的贴图错乱、半边透明缺失、正反面贴图串位以及头部穿模**。

经交叉比对 Sharpcraft 源码与原版 26.3 反编译源码（[MeshTransformer.java](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/client/model/geom/builders/meshtransformer/ "null")、[MeshDefinition.java](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/client/model/geom/builders/meshdefinition/ "null")、[ElytraModel.java](https://www.google.com/search?q=https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/client/model/elytramodel/ "null")、[WingsLayer.java](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/client/renderer/entity/layers/wingslayer/ "null")），定位到以下 **3 个叠加的根本原因**：

### 1. 致命根因 A：几何尺寸（Geometry Box）与 UV 采样盒尺寸（UV Box）强耦合，导致幼年鞘翅 UV 区域被错误砍半

#### （1）原版 26.3 的真实机制

在原版 [ElytraModel.java](https://www.google.com/search?q=https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/client/model/elytramodel/) 中：

> [外部审查稿中的反编译/实现代码片段已按 AGENTS.md 原则 3 移除；仅保留行为描述、结论与验收建议。]

查看原版 [MeshTransformer.java](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/client/model/geom/builders/meshtransformer/) 与 [MeshDefinition.java](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/client/model/geom/builders/meshdefinition/) 的实现：

> [外部审查稿中的反编译/实现代码片段已按 AGENTS.md 原则 3 移除；仅保留行为描述、结论与验收建议。]

**关键事实**：`MeshTransformer.scaling(0.5F)` **仅仅修改了根节点 `root` 的 `PartPose`（施加 `0.5x` 缩放和 `Y + 12.008` 平移）**，而 `left_wing` 和 `right_wing` 的 `CubeDefinition` 尺寸**仍然是完整的 `(10, 20, 2)`**，其 `Grow`（`CubeDeformation(1.0F)`）在局部空间中也依然是 `1.0F`（经 `root` 0.5 倍缩放后在世界空间等效为尺寸 `5×10×1`、`Grow = 0.5`）。 因此，在原版中，幼年鞘翅与成年鞘翅在 `64×32` 的 `elytra.png` 上采样的是**完全相同的 `10×20×2` 像素区域**：

* `u0 = 22, u1 = 24, u2 = 34, u3 = 36, u4 = 46`

* `v0 = 0,  v1 = 2,  v2 = 22`

#### （2）Sharpcraft 当前的错误实现

在 `src/Sharpcraft.Client/Render/EntityModelLayerHelpers.cs:258-298` 中，由于 `EntityModelPart` 没有独立的局部 `Scale` 属性或独立的 `UvWidth / UvHeight / UvDepth` 字段，`WithElytra(baby: true)` 直接把 `EntityModelPart` 的 `Width / Height / Depth` 手动除以 2 改成了 `(5f, 10f, 1f)`：

> [外部审查稿中的反编译/实现代码片段已按 AGENTS.md 原则 3 移除；仅保留行为描述、结论与验收建议。]

然而，在 `src/Sharpcraft.Client/Render/EntityModelMesher.cs:291-305` 中，`AddPart` **直接使用 `part.Width, part.Height, part.Depth` 去计算六个面的 UV 展开坐标**：

> [外部审查稿中的反编译/实现代码片段已按 AGENTS.md 原则 3 移除；仅保留行为描述、结论与验收建议。]

这直接引发了灾难性的贴图采样错位：

| 立方体面 | 成年鞘翅 UV 区间 `(u, v)` | 当前幼年鞘翅错误 UV 区间 `(u, v)` | 在 `elytra.png` 上的实际后果 | 
| ----- | ----- | ----- | ----- | 
| **NORTH（背面主可见面）** | `[24..34] × [2..22]`（完整翅面） | `[23..28] × [1..11]`（左上角 1/4） | 采到了内侧领口全透明缺口（`u ∈ [24..28], v ∈ [2..7]` 含大量 `alpha=0` 像素），且下半截羽毛（`v ∈ [11..22]`）被完全截断！ | 
| **SOUTH（贴紧身体内侧面）** | `[36..46] × [2..22]`（全透明区） | `[29..34] × [1..11]`（原本属于 NORTH 右半边的实心贴图！） | 原版全透明的背面，在幼年鞘翅上错误画出了本该属于正面右半侧的灰色翅膀纹理！ | 
| **WEST（外侧厚度边）** | `[22..24] × [2..22]`（2px 脊边） | `[22..23] × [1..11]`（仅上半 1px） | 侧边纹理宽度减半且下半截丢失。 | 
| **EAST（内侧中缝边）** | `[34..36] × [2..22]`（全透明区） | `[28..29] × [1..11]`（NORTH 面中央实心列！） | 内侧中缝出现一条突兀的实心竖线。 | 

### 2. 致命根因 B：幼年人形生物背部挂点（PivotY / CentreZ）与 26.3 新版幼年模型严重失配（穿入后脑勺）

在 `src/Sharpcraft.Client/Render/BabyModelDefinitionsHumanoid.cs:13-15` 中，26.3 新版小僵尸（`BabyZombie`）的身体比例已经完全重做（不再是旧版简单缩放）：

* **`body`**：`Bottom = 4f, Height = 5f, Depth = 2f`（即身体 Y 范围为 `[4, 9]`，肩膀线在 **`Y = 9f`**，背部表面在 `Z = +1f`）。

* **`head`**：`Bottom = 9f, Height = 6f, Depth = 6f`（即头部 Y 范围为 **`[9, 15]`**，加上 `head_overlay` 的 `grow = 0.25f` 后，后脑勺向后延伸到 **`Z = +3.25f`**）。

然而，在 `src/Sharpcraft.Client/Render/EntityModelLayerHelpers.cs:261-271` 中，`WithElytra(baby: true)` 却硬编码了：

> [外部审查稿中的反编译/实现代码片段已按 AGENTS.md 原则 3 移除；仅保留行为描述、结论与验收建议。]

算一下几何空间就会发现严重冲突：

1. **高度高出肩膀 3 个像素**：小僵尸的肩膀在 `Y = 9f`，而幼年鞘翅的 `PivotY = 12f`（算上 `grow = 0.5f` 顶部达到 `Y = 12.5f`），整整高出肩膀 **3.5 个模型像素**，直接挂在了小僵尸的**后脑勺正中间**！

2. **深度与后脑勺穿模**：小僵尸头部（含 `head_overlay`）占据 `Y ∈ [8.75, 15.25]`、`Z ∈ [-3.25, +3.25]`；而幼年鞘翅在 `Y ∈ [1.5, 12.5]`、`Z ∈ [1.5, 3.5]`。这意味着鞘翅上半部 `[9.0, 12.5]` 的区间**完全插进了小僵尸的后脑勺内部**（`Z = 1.5..3.25` 重叠）！

3. **原因追溯**：原版 `ElytraModel.BABY_TRANSFORMER` 的 `y = 12.008F`（换算到 Sharpcraft 24px 基准即 `24 - 12 = 12f`）是在**原版 `HumanoidModel.BABY_TRANSFORMER` 坐标系**下工作的——原版 `WingsLayer` 挂在 `HumanoidRenderer` 下，或者在 26.3 中配合 `WingsLayer.submit` 的 `poseStack.translate(0.0F, 0.0F, 0.125F)` 使用。而在 Sharpcraft 中，`BabyZombie` 采用的是独立烘培在 `Y ∈ [0, 15]` 的 `BabyModelDefinitionsHumanoid`（身高 15px，肩膀 `Y = 9f`），如果直接把 `PivotY` 硬编码为 `12f` 而不是对齐 `body` 顶部（`bodyTop = body.Bottom + body.Height = 9f`）并避开后脑勺，必然穿模。

### 3. 次要根因 C：幼年潜行（Crouching）位移未按 0.5 倍缩放

在 `src/Sharpcraft.Client/Render/EntityModelMesher.cs:215-225` 的 `ResolveEquipmentWingPose` 中：

> [外部审查稿中的反编译/实现代码片段已按 AGENTS.md 原则 3 移除；仅保留行为描述、结论与验收建议。]

无论成年还是幼年，潜行时翅膀都固定下移 `-3f` 模型像素。而在原版 [ElytraModel.java](https://www.google.com/search?q=https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/client/model/elytramodel/) 中，`leftWing.y = 3.0F` 位于 `root`（`scale = 0.5F`）内部，经父节点缩放后实际只下移 `1.5F` 模型像素。

### 4. 修复方案（可直接交由 Agent 实施）

1. **在 `EntityModelPart` 中解耦几何尺寸与 UV 采样尺寸（或支持 `UvWidth / UvHeight / UvDepth` 覆盖）**：

   * 在 `src/Sharpcraft.Client/Render/PlayerModelDefinition.cs` 的 `EntityModelPart` 记录结构体中新增可选属性：

     > [外部审查稿中的反编译/实现代码片段已按 AGENTS.md 原则 3 移除；仅保留行为描述、结论与验收建议。]

   * 在 `src/Sharpcraft.Client/Render/EntityModelMesher.cs:291-293` 中改为：

     > [外部审查稿中的反编译/实现代码片段已按 AGENTS.md 原则 3 移除；仅保留行为描述、结论与验收建议。]

   * 在 `EntityModelLayerHelpers.WithElytra` 中，当 `baby == true` 时，显式传入 `UvWidth: 10, UvHeight: 20, UvDepth: 2`（保持与成年鞘翅完全一致的 `10×20×2` UV 采样盒）。

2. **修正幼年鞘翅的挂点坐标**：

   * 将幼年鞘翅的 `pivotY` 从硬编码 `12f` 改为基于宿主 `body` 顶部对齐（例如 `var bodyTop = body.Bottom + body.Height; var pivotY = baby ? bodyTop : 24f;`，对应 `bottom = pivotY - height`），或根据原版幼年身体比例锚定在肩线 `Y = 9f` 处，使翅膀自然垂落在 `Y ∈ [-1, 9]`，既不穿入 `Y ∈ [9, 15]` 的大头，也与身体背部贴合。

3. **补齐单元测试 UV 断言**：

   * 在 `tests/Sharpcraft.Client.Tests/Render/ElytraWingGeometryTests.cs` 的 `BabyWingsAreMeshedWhenChestHasElytra` 中，直接复用 `BothWingSlabsSampleTheReferenceTexelsOnAllSixFaces` 的六面 UV 断言，确保幼年鞘翅六个面的 `(u, v)` 与原版 `10×20×2` 完全一致。

## 二、 渲染与实体模型/动画系统的明显缺陷与潜在隐患

除小僵尸鞘翅外，通读 `src/Sharpcraft.Client/Render/` 与 `src/Sharpcraft.Framework/Rendering/` 全量代码后，发现以下 **6 处系统性缺陷**：

### 1. `FromVanillaChild` 的子骨骼静态旋转共轭在多轴旋转下失真（违反线性代数相似变换）

* **位置**：`src/Sharpcraft.Client/Render/PlayerModelDefinition.cs:202-204`、`src/Sharpcraft.Client/Render/EntityModelMesher.cs:544-595`

* **问题分析**：

  * Sharpcraft 在将原版（Y 轴向下、X 轴翻转）转换到烘焙模型空间（Y 轴向上、X 轴镜像）时，引入了基变换矩阵 $S = \text{diag}(-1, -1, 1)$（行列式 $\det(S) = +1$，本质是绕 Z 轴旋转 $180^\circ$）。

  * 对于任意原版旋转矩阵 $R_{\text{vanilla}} = R_z(\gamma) R_y(\beta) R_x(\alpha)$，在 Sharpcraft 坐标系下的精确共轭矩阵满足： 

    $$
    S \cdot R_z(\gamma) R_y(\beta) R_x(\alpha) \cdot S^{-1} = \big(S R_z(\gamma) S\big)\big(S R_y(\beta) S\big)\big(S R_x(\alpha) S\big) = R_z(+\gamma) R_y(-\beta) R_x(-\alpha)
    $$

  * 因为 `EntityModelMesher.RotationZyx`（行向量右乘）已经修复为 $R_x(\text{rx}) \cdot R_y(\text{ry}) \cdot R_z(\text{rz})$，所以**无论根骨骼还是子骨骼，精确的共轭符号都恒为 `(-rotationX, -rotationY, +rotationZ)`**！

  * 然而，虽然 `FromVanillaChild` 写了 `RotationX: -rotationX, RotationY: -rotationY, RotationZ: rotationZ`，但在 **40+ 个实体模型定义文件**（如 `PlayerModelDefinition.FromVanilla` 默认原样存储 `rotationX, rotationY, rotationZ`，以及诸多动物/怪物根部件手工调整符号）中，**根部件（Root Parts）与子部件（Child Parts）使用了两套不一致的旋转符号约定**：

    * `FromVanilla`（第 143–145 行）**没有**对 `rotationX, rotationY` 取反，导致调用方在各个 `*ModelDefinition.cs` 里有的手工取了反、有的没取反（例如 `SpiderModelDefinition` 设置了 `mirrorX: false` 特判）。

    * 在 `EntityModelMesher.PartMatrix`（第 579–587 行）中，动态动画增量 `pose.Rx, pose.Ry, pose.Rz` 直接与 `part.RotationX, part.RotationY, part.RotationZ` 相加，而 `EntityPartPose.Build` 中的动态姿态符号又是按根部件手调的，一旦作用在通过 `FromVanillaChild` 定义的子部件上，静态角（已取反）与动态角（未统一符号）就会发生方向冲突。

* **改进建议**：统一所有 `FromVanilla` 与 `FromVanillaChild` 的角度转换约定为 $S R S^{-1} \implies (-rx, -ry, +rz)$，并用一套针对全部已注册实体模型的 **原版骨骼世界坐标 Golden Vector 测试** 一次性校验所有包含非零初始旋转的模型。

### 2. `EntityModelMesher` 在渲染 `OverlayLayers` 时丢弃了 `GrowXyz`（非均匀膨胀失效）

* **位置**：`src/Sharpcraft.Client/Render/EntityModelMesher.cs:111-125`

* **问题分析**：

  > [外部审查稿中的反编译/实现代码片段已按 AGENTS.md 原则 3 移除；仅保留行为描述、结论与验收建议。]

  `with` 表达式只覆盖了标量 `Grow = layer.Grow`，却**保留了父部件原本的 `GrowXyz`**！而在 `AddPart`（第 273–284 行）中：

  > [外部审查稿中的反编译/实现代码片段已按 AGENTS.md 原则 3 移除；仅保留行为描述、结论与验收建议。]

  `part.GrowXyz` 的优先级高于 `part.Grow`！这意味着如果任何基础部件设置了 `GrowXyz`，它的所有 `OverlayLayers` 的 `layer.Grow` 都会被完全忽略；反之，`EntityModelLayer` 结构体自身也不支持非均匀的 `GrowXyz`。

* **改进建议**：在构造 `layerPart` 时显式重置 `GrowXyz = null`（或在 `EntityModelLayer` 中支持 `Vector3? GrowXyz = null` 并同步传递）。

### 3. `EntityModelMesher.Build` 热路径每帧每实体分配大量托管堆对象（GC 压力）

* **位置**：`src/Sharpcraft.Client/Render/EntityModelMesher.cs:40-154`

* **问题分析**：

  * 在 `InteractiveSession` 的每一帧中，所有可见实体都会调用 `EntityModelMesher.Build` 重新构建网格。

  * 在 `Build` 内部，每处理一个实体都会执行：

    * `model.Parts.ToDictionary(part => part.Name, StringComparer.Ordinal)`（第 77 行：每个实体分配一个 `Dictionary<string, EntityModelPart>`）；

    * `new Dictionary<string, (Vector3 Pivot, Matrix4x4 Matrix)>(partsByName.Count, StringComparer.Ordinal)`（第 78 行：每个实体又分配一个字典）；

    * `EntityPartPose.Build(entity)`（内部创建 `Dictionary<string, PartPose>`）；

    * `var vertices = new List<float>(...)` 和 `var indices = new List<uint>(...)`，最后再 `.ToArray()`。

  * 当同屏有 50–100 个实体时，每帧（60 FPS）会产生数千个短命 `Dictionary` 与数组拷贝，直接触发高频 Gen0/Gen1 GC 停顿，违反了 `AGENTS.md` 第 7 条“渲染热路径零分配（Zero Allocation on Hot Paths）”原则。

* **改进建议**：

  1. 将 `partsByName` 字典（甚至拓扑排序后的父子索引数组 `int ParentIndex`）在 `EntityModelDefinition` 构造时一次性预计算缓存；

  2. 用栈上 `Span<(Vector3 Pivot, Matrix4x4 Matrix)>`（按部件下标索引）替代字符串字典 `frames`；

  3. 像 `ChunkMesher` 使用 `MeshBuffer` 一样，为实体网格器引入可复用的缓冲区。

### 4. `WindowedRenderer` 与 `OffscreenRenderer` 严重的代码复制与双轨维护风险（违反原则 14 与 ADR-0004）

* **位置**：`src/Sharpcraft.Framework/Rendering/WindowedRenderer.cs`（2,304 行）与 `src/Sharpcraft.Framework/Rendering/OffscreenRenderer.cs`（2,020+ 行）

* **问题分析**：

  * 两个文件各自独立维护了超过 **2,000 行** 高度重复的 WebGPU 管线创建、BindGroupLayout 声明、Uniform 缓冲区写入、OIT（Order-Independent Transparency）Pass、天气/天体/实体/UI 渲染 Pass 代码。

  * 这导致任何一处渲染修复（例如深度状态、混合模式、Uniform 布局、Entity Pass 顺序）都必须在两个 2,000 行的静态巨型方法里手动同步粘贴，极易产生“离屏截图测试通过（`OffscreenRenderer`），但实机窗口（`WindowedRenderer`）表现不一致”的隐蔽缺陷，直接违背了 `AGENTS.md` 原则 14（禁止维护两套并行的渲染管线逻辑）。

* **改进建议**：将 Pipeline 构建、BindGroup 管理与 RenderPass 录制提取为共享的 `WorldRenderPipeline` / `FramePassRecorder`，让 `OffscreenRenderer` 与 `WindowedRenderer` 仅负责提供目标 `TextureView`（离屏纹理 vs Swapchain Surface）与生命周期驱动。

### 5. `ChunkMesher` 贪心合并（Greedy Meshing）的 `RectangleIsUniformlyLit` 重复跨区块查询开销

* **位置**：`src/Sharpcraft.Client/Render/ChunkMesher.cs:311-383`

* **问题分析**：

  * 在 `MergeSectionFaces` 中，每当贪心扩展出一个 `width > 1 || height > 1` 的候选矩形时，都会调用 `RectangleIsUniformlyLit` 对 `(width + 2) × (height + 2)` 范围内的所有邻居方块重新执行 `neighbourhood.GetBlockState`、`Occludes` 和 `neighbourhood.TryGetLight`。

  * 如果发现某处不满足均匀光照，代码直接把 `width = 1; height = 1;` 退回单格（第 314–315 行），然后在下一个 `(u + 1, v)` 格再次尝试扩展并重复扫描同一批格子！在光照变化的复杂地形（如火把旁、树荫下、洞穴中），这会退化为 $O(N^2)$ 的重复查询。

* **改进建议**：在生成 `keys[256]` 平面表（第 238–247 行）时，顺带在一个 `stackalloc bool[256]` 掩码中标记该格是否允许参与合并（自身及四邻光照一致且无遮挡），在 `while (u + width < 16)` 扩展阶段直接判断该掩码，无需事后回退重扫。

### 6. `WindowedRenderer` 纹理热更新与动态缓冲区的 GPU 资源泄漏

* **位置**：`src/Sharpcraft.Framework/Rendering/WindowedRenderer.cs:81-94` 及帧循环内动态纹理/缓冲区重建处

* **问题分析**：

  * 虽然退出时不销毁 `Surface/Device` 有注释说明是为了规避 `wgpu-hal` 交换链信号量 panic，但在窗口运行期间（如窗口频繁缩放重建 Depth Texture / OIT Texture、或动态更新实体皮肤/精灵纹理时），部分旧的 `TextureView` / `BindGroup` / `Buffer` 未在替换前显式 `Dispose()`，长时间运行会导致显存与句柄持续上涨。

## 三、 网络协议、实体同步、世界管理与并发安全隐患

通读 `src/Sharpcraft.Common/Network/`、`src/Sharpcraft.Client/World/` 与 `src/Sharpcraft.Client/Game/` 后，发现以下 **4 处高优先级逻辑与并发缺陷**：

### 1. 实体同步（Entity Sync）残余的 3 个边界缺陷

虽然 `T-660` 已经补上了 `entity_position_sync`（Play Clientbound ID `0x23` / `35`），但仔细审查 `EntityTracker.cs` 与 `EntityPackets.cs` 可以发现还有 **3 个隐藏问题**：

1. **`StepInterpolation` 在消费完最后一步的同一个 Tick 额外多走了一步（`EntityTracker.cs:252-288`）**：

   > [外部审查稿中的反编译/实现代码片段已按 AGENTS.md 原则 3 移除；仅保留行为描述、结论与验收建议。]

   看第 273–288 行：当 `_currentStepTicks >= _currentStepDuration` 时，代码从 `_steps` 队列 Dequeue 下一个 step，并在第 284 行将 `_currentStepTicks = 1f`（注释写着 *"Thereference's PopStep immediately runs the first tick of the newly popped step"*，且在第 285 行已经执行了 `LerpPosition(1f / _currentStepDuration)`），但紧接着第 288 行**没有 `return`，而是继续往下执行了 `Advance(1f, EntityTracker.InterpolationSteps)`**！这导致新弹出的 step 在第一个 Tick 被推进了 **2 次**（`_currentStepTicks` 直接变成 `2f`），插值速度翻倍、提前一帧耗尽！

2. **`EntityPackets.TryReadVecDelta` 将多步 `VecDelta.Stepped` 累加为单个 `double`，导致正负往返移动相互抵消为 `0`（`EntityPackets.cs:478-494`）**：

   * 在 `TryReadVecDelta` 中，当 `stepCount > 0` 时，循环把所有 step 的 `lx / 4096.0` 累加到单个 `deltaX, deltaY, deltaZ` 上。

   * 而在 `EntityTracker.ApplyMove`（`EntityTracker.cs:603-605`）中：

     > [外部审查稿中的反编译/实现代码片段已按 AGENTS.md 原则 3 移除；仅保留行为描述、结论与验收建议。]

     利用了 `move.DeltaY == 0d` 表示“该轴未移动，保持 `entity.Y`”。如果一个多步包里实体先起跳 `+0.2` 再下落 `-0.2`（总和 `DeltaY == 0d`），或者由于浮点误差未能还原逐个 step 的轨迹，不仅中间跳起轨迹丢失，还会因为 `DeltaY == 0d` 误判为“该轴无变化”！

   * 同时，`entity.CodecBaseY + move.DeltaY` 直接做浮点数加法，没有遵循原版 `VecDeltaCodec` 的 `Decode(Encode(base) + delta)` `1/4096` 定点舍入规则，长距离连续微移后会产生累积浮点漂移。

3. **`ApplyPositionSync` 处理 `PositionPath.Stepped` 时覆盖了 `CodecBase` 却未按每步 `TickOffset` 差值推进（`EntityTracker.cs:659-684`）**：

   * 在 `EntityPackets.TryReadEntityPositionSync`（`EntityPackets.cs:412-426`）中，`PositionPathStep` 读取的是每一步的 `tickOffset`，而在 `EntityTracker.ApplyPositionSync`（第 672–679 行）中，直接把 `step.TickOffset` 当作 `durationTicks` 传给了 `EnqueueInterpolation`，如果原版发来的是累积 Tick 偏移（如 `1, 2, 3`），插值时长就会被错误拉长。

### 2. `LivePlaySession` 接收队列无界且连接异常状态存在竞态

* **位置**：`src/Sharpcraft.Client/Game/LivePlaySession.cs:18-21, 151-169`

* **问题分析**：

  * `_incoming` 是一个无界的 `ConcurrentQueue<byte[]>`。当主线程正在进行耗时的区块网格初始化、资源包重载或窗口拖拽阻塞消息循环时，后台 `PumpAsync` 任务会无限制地将服务端发来的区块包与实体包读入内存，极端情况下造成数百 MB 的瞬时内存暴涨。

  * `PumpAsync`（第 165 行）仅捕获 `IOException / SocketException / ObjectDisposedException`，如果 `ReceiveAsync` 抛出协议解码异常（如压缩包损坏导致的 `InvalidDataException` 或 `InvalidOperationException`），后台 Task 会静默 Faulted 退出，而 `_failure` 仍为空字符串，`IsAlive` 永远返回 `true`，客户端表现为“假死断流却不报错”。

### 3. `InteractiveSession` 巨石类（God Object）职责过载

* **位置**：`src/Sharpcraft.Client/Game/InteractiveSession.cs`

* **问题分析**：

  * `InteractiveSession.cs` 单个文件集中了网络包分发、玩家物理移动、方块交互、背包/容器状态、聊天命令、日夜天气计算、实体提取、UI 布局与渲染回调拼装等全部逻辑。

  * 这使得任何网络协议或渲染参数的改动都必须修改 `InteractiveSession.cs`，不仅极易在合并时产生冲突，也让单元测试难以在不启动完整会话的情况下独立测试子状态机。

### 4. `ModHost` 可回收程序集上下文（`R-019` Collectible ALC）无法被 GC 回收的真正原因

* **位置**：`src/Sharpcraft.Modding/ModHost.cs:56-116`、`tests/Sharpcraft.Modding.UnloadProbe/Program.cs:79-102`

* **问题分析**： 在 `docs/risks.md` 中，`R-019` 记录了“加载了程序集的 `ModLoadContext` 在卸载后无法被 GC 回收，原因未知”。经审查 `ModHost.cs` 与 `UnloadProbe/Program.cs`，根本原因有 **3 点**：

  1. **`ModHost.Unload` 中的 Lambda 闭包在托管堆上捕获了强引用 `ModLoadContext context`（`ModHost.cs:107-113`）**：

     > [外部审查稿中的反编译/实现代码片段已按 AGENTS.md 原则 3 移除；仅保留行为描述、结论与验收建议。]

     C# 编译器为 `RemoveAll` 的谓词生成了一个闭包对象 `<>c__DisplayClass8_0`，其字段 `public ModLoadContext context` **持有了对 `context` 的强引用**！同时栈帧局部变量 `instance`、`mod`、`context` 在方法内联或 Tier-0 JIT 下直到外层方法返回前都不会被清零。

  2. **`UnloadProbe/Program.cs:98` 中 `context.LoadFromAssemblyPath(payload)` 的返回值 `Assembly` 残留在栈槽/寄存器，且 `LoadFromAssemblyPath` 锁死底层 PE 文件句柄**：

     * 在 .NET CoreCLR 中，测试 Collectible `AssemblyLoadContext` 卸载时，任何触碰 `Assembly`、`Type`、`MethodInfo` 或模组实例的代码**必须完全封装在一个带 `[MethodImpl(MethodImplOptions.NoInlining)]` 的子方法中，且该子方法绝对不能在最后一行附近保留 `Assembly` 引用，也不能让 `ModHost.Load/Unload` 被内联到调用 `GC.Collect()` 的帧中**。

     * 更重要的是，在 `UnloadProbe/Program.cs:95` 中，探针把 **`typeof(ModHost).Assembly.Location`（即宿主自身的 `Sharpcraft.Modding.dll`）** 拷贝成了 `Payload.dll` 并加载进了 `ModLoadContext`！由于 `ModLoadContext.Load`（`ModLoadContext.cs:38`）把同名程序集解析回默认上下文（`Default` ALC），在自定义 ALC 中通过 `LoadFromAssemblyPath` 强行加载与宿主同身份（Same Assembly Identity `Sharpcraft.Modding`）的程序集会在 CLR 内部类型系统与宿主 `Default` ALC 建立跨上下文绑定表引用，从而阻止该 ALC 被卸载！如果改用独立的测试模组程序集（不同 Assembly Name）并用 `LoadFromStream` 加载，同时给 `ModHost.Load` / `ModHost.Unload` 加上 `[MethodImpl(MethodImplOptions.NoInlining)]` 并消除 `RemoveAll` 闭包捕获，ALC 即可被 100% 确定性回收。

## 四、 研发流程优化与“实现保证”体系建议（为什么类似问题反复出现？）

回顾近期的三次典型问题——**实体垂直位置不同步（漏掉 ID 35 `entity_position_sync`）**、**成年鞘翅无中缝与深度穿模（`RotationZyx` 矩阵顺序写反）**、以及本次的**小僵尸鞘翅贴图错乱与穿模（`T-662` 把几何缩放当成 UV 缩放）**，可以发现一个非常清晰的共性模式：

> **文档与门禁非常严密（有 `AGENTS.md` 14 条原则、有 `ClassMap`、有 `STATE.md`），但每个 Bug 在交付时都“顺利通过了所有单元测试和沙盒自检”！**

为什么会这样？因为现有的实现与验证流程存在 **4 个结构性漏洞**：

### 漏洞 1：“自证式单元测试（Self-Confirming Tests）”陷阱

* **现状复盘**：

  * 在 `RotationZyx` 写反时，编写该代码的 Agent 用同一个写反的公式去手算测试期望值，写出了“通过”的测试；

  * 在 `T-662` 小僵尸鞘翅中，Agent 在 `ElytraWingGeometryTests.cs:168-195` 里只断言了 `VertexFloats / 14 == 48`（顶点数是 48 个）和 `maxY < 16px`（比成年矮），**完全没有检查哪怕一个顶点的 UV 坐标，也没有检查它与小僵尸头部的 Z/Y 包围盒是否相交**！

* **改进保证措施（强制测试契约）**：

  1. **禁止“只测顶点数/非空”的伪几何测试**：在 `Sharpcraft.Architecture.Tests` 或代码审查清单中规定，任何新增模型或渲染层的测试必须包含：

     * **六面 UV 精确断言**（验证 `u0..u4, v0..v2` 与原版 `CubeDefinition` 完全一致，特别是缩放模型不得改变 UV 跨度）；

     * **与宿主身体的无穿模包围盒断言（Disjoint / Clearance Invariant）**：例如外层装备（盔甲、鞘翅）在静止姿态下不得穿入宿主实体的头部或躯干内部。

### 漏洞 2：Golden Vector 提取依赖 Agent “手工心算”，缺乏自动化提取脚本

* **现状复盘**：

  * `AGENTS.md` 原则 14 要求对比原版 Golden Vectors，但目前许多测试里的注释写着 *"hand-composed in the baked y-up space"*（手工在烘焙空间里心算）。一旦 Agent 心算时把左乘/右乘、Y 轴翻转或 `MeshTransformer` 理解错，算出来的“Golden Vector”本身就是错的。

* **改进保证措施（工具化 Golden Vector 生成器）**：

  * 在 `tools/` 下新增一个轻量级参考几何计算器或解析工具（例如 `tools/Sharpcraft.ReferenceVerifier`）：直接以原版 26.3 的原始参数（`PartPose.offsetAndRotation`、`CubeListBuilder.texOffs().addBox()`、`MeshTransformer.scaling()`）作为输入，严格按照原版 `ModelPart.compile` + `PoseStack` 矩阵栈算出原版空间下的 8 个顶点世界坐标与 6 个面 UV，再通过唯一的标准坐标变换函数映射到 Sharpcraft 空间，直接生成测试断言数据。**严禁 Agent 手工心算三维旋转和 UV 坐标**。

### 漏洞 3：沙盒视觉验证（Sandbox Visual Verification）视角单一且贴图对比度不足

* **现状复盘**：

  * 原版 `elytra.png` 整体呈暗灰色，在默认光照和小尺寸离屏渲染下，幼年鞘翅即使半边透明、正反面贴图串位，缩略图看起来也像“背上有两块灰色的东西”，导致 Agent 肉眼或简单像素存在性检查放过了严重的 UV 错乱。

* **改进保证措施（棋盘格 UV 诊断贴图 + 多角度强制同屏对比）**：

  1. **内置标准 UV 象限诊断贴图（UV Checkerboard / Quadrant Reference Texture）**：在测试夹具中提供一张把每个面（`NORTH/SOUTH/EAST/WEST/UP/DOWN`）涂成高饱和度纯色（如红、绿、蓝、黄、品红、青）且透明区全透的测试贴图。一旦 `SOUTH`（全透）串位到了 `NORTH`（红色），或者 UV 砍半，像素级自动断言能瞬間捕获！

  2. **三视角强制回归矩阵**：所有实体与装备渲染任务完成前，沙盒场景必须同时输出 **正背面（`Yaw = 0°`）、斜后方 45°（`Yaw = 35°`）、正侧面（`Yaw = 90°`）** 三个机位，并将**成年与幼年、站立与潜行**同屏并排渲染。

### 漏洞 4：架构测试（`Sharpcraft.Architecture.Tests`）只测了“文档格式与程序集引用”，未覆盖“领域不变量”

* **现状复盘**：

  * 目前 `Sharpcraft.Architecture.Tests` 里的 6 个测试类全部在测 SPDX 头、Markdown 链接可达性、环境变量调用和 `.csproj` 引用边界，**没有任何一条架构测试去扫描已注册的 `EntityModelDefinition` 或网络协议包表**。

* **改进保证措施（新增领域不变量门禁 `ModelAndProtocolInvariantTests`）**： 在 `tests/Sharpcraft.Architecture.Tests/` 中新增自动化全表扫描测试：

  1. **全实体模型 UV 越界与退化扫描**：遍历 `EntityModelResolver` 中注册的所有成年与幼年模型（含全部变体和装备层），验证每个 `EntityModelPart` 的 `u4 <= TextureWidth`、`v2 <= TextureHeight`、非空部件的 UV 采样宽高必须匹配原版纹理比例。

  2. **父子骨骼引用完整性扫描**：自动验证所有模型的 `Parent` 与 `PoseSource` 指向的目标部件必然存在、无环，且 `OverlayLayers` 不会意外继承非空的 `GrowXyz`。

  3. **协议包覆盖率门禁**：扫描 `PlayPacketIds` 中声明的所有实体与区块相关 Clientbound Packet ID，确保每一个声明的 ID 在 `PlaySession` 和 `InteractiveSession` 中都有对应的解码分支（防止再次出现声明了常量却未接入会话的空洞）。

## 五、 改进优先级路线图（供团队与 Agents 排期参考）

| 优先级 | 任务编号建议 | 模块 | 核心修复内容 | 涉及核心文件 | 
| ----- | ----- | ----- | ----- | ----- | 
| **P0（立即修复）** | `T-667` | 幼年鞘翅渲染 | 解耦 `EntityModelPart` 的几何尺寸与 UV 尺寸（`UvWidth/UvHeight/UvDepth`），修复幼年鞘翅 UV 砍半、挂点 `PivotY=12f` 穿头及潜行偏移未缩放问题，补齐六面 UV 测试 | `PlayerModelDefinition.cs`, `EntityModelMesher.cs`, `EntityModelLayerHelpers.cs`, `ElytraWingGeometryTests.cs` | 
| **P0（立即修复）** | `T-668` | 实体移动插值 | 修复 `EntityTracker.StepInterpolation` 弹出新步时同帧双重 `Advance` Bug、`TryReadVecDelta` 多步正负抵消 Bug，以及 `PositionPath.Stepped` 时长计算 | `EntityTracker.cs`, `EntityPackets.cs`, `EntityTrackerTests.cs` | 
| **P1（近期治理）** | `T-669` | 实体模型系统 | 修复 `OverlayLayers` 继承父部件 `GrowXyz` 的缺陷；统一 `FromVanilla` 与 `FromVanillaChild` 的旋转角共轭约定 `(-rx, -ry, +rz)`；消除 `EntityModelMesher.Build` 每帧字典分配 | `EntityModelMesher.cs`, `PlayerModelDefinition.cs` | 
| **P1（近期治理）** | `T-670` | 模组宿主 (`R-019`) | 消除 `ModHost.Unload` 的 `RemoveAll` 闭包捕获，标注 `NoInlining`，将 `UnloadProbe` 的测试负载改为非同名程序集并使用流式加载，彻底闭环 `R-019` | `ModHost.cs`, `ModLoadContext.cs`, `UnloadProbe/Program.cs` | 
| **P2（架构演进）** | `T-671` | 渲染单轨化 & 门禁 | 抽离 `WindowedRenderer` 与 `OffscreenRenderer` 的共享 RenderPass 录制管线；拆分 `InteractiveSession`；在 `Architecture.Tests` 中落地全模型 UV/包围盒不变量扫描 | `WindowedRenderer.cs`, `OffscreenRenderer.cs`, `InteractiveSession.cs`, `Sharpcraft.Architecture.Tests` | 
