<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 原版 OIT 参考管线（depth-bounds + 小波透射率）
summary: 26.3 的 OIT 三阶段行为规格，作为窗口唯一采用的透明管线
status: 已实现（2026-10-31）
updated: 2026-10-31
-->

# 原版 OIT 参考管线（depth-bounds + 小波透射率）

> 本文只描述**原版可观测行为与数据流**，据此独立实现 WGSL；不粘贴原版实现。证据以 `ref://mc-26.3-analysis/...` 指针给出。
> 目标：这是本项目窗口透明**唯一采用**的管线（原版 26.3 的 OIT）。早先的「加权混合 OIT」实测观感明显更差，
> 已**封存**（git 历史保留），不参与编译；因此不再有 `MC_TRANSPARENCY` 开关。

## 1. 与原版的关系

原版 26.3 每帧对**所有半透明内容**（地形半透明、实体、粒子、文字、云、天气、世界边界、光束…）执行同一套
**三阶段 OIT**：`DEPTH_BOUNDS → TRANSMITTANCE → ACCUMULATE`，最后一次 `COMPOSITE` 合成进主色并写深度。

- 证据：`ref://mc-26.3-analysis/src/net/minecraft/client/renderer/LevelRenderer.java`
  `executeOit`（约 L554）、`executeDepthBoundsCull`（约 L641）、`executeOitWaterMask`（约 L665），
  `OitStage` 枚举（`.../client/renderer/oit/OitStage.java`）。
- 每个半透明管线集合由 `OitPipelineSet` 生成三条管线（depth_bounds / transmittance / accumulate），
  `ref://.../client/renderer/oit/OitPipelineSet.java`。
- 常量：`OIT_WAVELET_RANK=2`、`OIT_COEFFICIENT_COUNT=8`（=2³）、`OIT_TRANSMITTANCE_TARGET_COUNT=2`（=8/4），
  `ref://.../client/renderer/LevelRenderer.java` L109-111。

## 2. 渲染目标

| 名称 | 格式 | 用途 |
| --- | --- | --- |
| `depthBounds` | RGBA16F（原版 RGBA32F；本后端 RGBA32F 不可混合，见 §10）| 每像素透明度几何的最近/最远线性深度与最近器件深度、不透明线性深度 |
| `depthBoundsCulled` | RGBA16F | 经 `depth_bounds_cull` 后：最远边界裁到不透明深度 |
| `transmittance[2]` | RGBA16F ×2 | 8 个透射率小波系数（每目标 4 个） |
| `accumulate` | RGBA16F | 加权颜色累计 `Σ (rgb·a, a)·采样透射率` |
| `oitCloudDepth` / `oitTerrainWithWaterPatchDepth` | 深度 | 云/水面贴片专用深度（本规格暂不实现，见 §6） |

证据：`LevelRenderer.java` L217-240（目标创建）、`RenderPipelines.OIT_DEPTH_BOUNDS_SNIPPET` 等
（`ref://.../client/renderer/RenderPipelines.java` L34-62）。

## 3. 三阶段片元行为

三个阶段对**同一批半透明三角形**各画一遍，只是片元着色器分支不同（`oit.glsl` 的 `executeAlphaOnlyPhase`，
`ref://.../shaders/include/oit.glsl`）。深度测试统一 `GREATER_THAN_OR_EQUAL`、**不写深度**。

### 3.1 DEPTH_BOUNDS

- 片元：`alpha < 0.01` 直接丢弃；否则写入 `(-线性深度, 线性深度, 器件深度, alpha>0.99 ? 器件深度 : 0)`，**混合 MAX**。
- 效果：`r = -最近线性深度`、`g = 最远线性深度`、`b = 最近器件深度`、`a = 不透明器件深度（无则不透明为 0）`。
- 证据：`ref://.../shaders/include/oit_depth_bounds.glsl`；snippet 混合 `BlendFunction.MAX`。

### 3.2 TRANSMITTANCE

- 片元：`absorbance = clamp(-ln(max(1-alpha,1e-4)), 0, 4)`（`toAbsorbance`）。
- 把片元深度经 `normalizeDepth` 映射为 `[0,1]` 的 `originalDepth`（在**深度边界内**归一；近相机段高精度，见 §3.5）。
- 用 **8 系数二层小波树**把 `absorbance` 沿深度编码成系数：系数 0 是"平均吸收"，其余 7 个是二叉树各层差分；
  深度 bin 数 = 系数数 = 8（`OIT_NUMBER_OF_DEPTH_BINS`）。**混合 ADDITIVE**，故多次写入自动相加、与顺序无关。
- 证据：`ref://.../shaders/include/oit_add_transmittance.glsl`、`.../oit_common.glsl`。

### 3.3 ACCUMULATE

- 片元：`transmittance = 1-alpha`、`accumAlpha = alpha`；
  `s = sampleTransmittance(像素, normalizeDepth(z), transmittance)`（用已累计的小波系数，求该片元之前的透射率）；
  写入 `(rgb·a, a)·s`，**混合 ADDITIVE**。
- 效果：进入相机方向被"更近的半透明层"衰减，天然顺序无关。
- 证据：`ref://.../shaders/include/oit_sample.glsl`（`sampleColorForAccumulation`、`sampleAbsorbance`）。

### 3.4 COMPOSITE

- 全屏三角形，`accum = texelFetch(accumulate)`；
  `totalT = sampleTransmittance(像素, 深度=∞(1e5), 当前=1)`；
  `coverage = 1 - (totalT < 0.02 ? 0 : totalT)`（`OIT_FULLY_OPAQUE_TOTAL_TRANSMITTANCE=0.02`）。
- `coverage<1e-5 且 rgb 全零` → `discard`。
- `normalization = accum.a>1e-5 ? coverage/accum.a : 1`；输出 `(accum.rgb·normalization, coverage)`，混合回主色；
  **写 `gl_FragDepth = 最近器件深度`**（让后续物体正确遮挡）。
- 证据：`ref://.../shaders/core/oit_composite.fsh`。

## 4. depth_bounds_cull

`DEPTH_BOUNDS` 阶段结束后、后续阶段读取前，跑一次全屏 `oit_depth_bounds_cull`：把 `depthBounds.g`（最远线性深度）
裁到 `不透明线性深度`（由 `depthBounds.a` 反线性化得到），写 `depthBoundsCulled`。作用是把透射率编码的深度范围
限制在"不透明表面之前"，避免被墙后不可见层污染。证据：`ref://.../shaders/core/oit_depth_bounds_cull.fsh`、
`LevelRenderer.executeDepthBoundsCull`。

## 5. normalizeDepth（深度边界内归一）

给定像素的最远/最近线性边界，把片元线性深度夹到 `[0, range]` 再归一；但 `bins-2` 段用于"高精度近段"（阈值 10 方块），
远段线性压缩，保证近处层次分辨。证据：`ref://.../shaders/include/oit_depth_sample.glsl`。

## 6. 本项目的范围（建议，待确认）

我们目前只渲染**地形半透明**（水/玻璃/冰），没有实体、粒子、文字、云、天气、世界边界、水面贴片。因此 reference 管线
建议**只覆盖地形半透明**，其余目标与 pass **不实现**：

- 实现：`depthBounds`、`depthBoundsCulled`、`transmittance[2]`、`accumulate` 四个目标 + 三阶段 + cull + composite。
- 不实现：`oitCloudDepth`、`oitTerrainWithWaterPatchDepth`、`water mask`、实体/云/天气的 OIT 集合（等这些内容存在后再接）。
- 深度边界取自主 pass 写出的**同一张深度纹理**（原版对地形半透明即用主深度）。

## 7. WGSL 落地（文件与 pass 顺序；独立设计）

| 文件 | 内容 |
| --- | --- |
| `Shaders/pipelines/oit_reference.wgsl` | 三个片元入口 `fs_depth_bounds` / `fs_transmittance` / `fs_accumulate` + 小波编码/采样函数；复用 `vs_main` 顶点 |
| `Shaders/pipelines/oit_composite.wgsl` | 全屏三角形 + `fs_composite`（采样 accum + 2 组系数 + depthBounds，写深度） |
| `Shaders/pipelines/oit_depth_bounds_cull.wgsl` | 全屏三角形 + `fs_cull` |

Pass 顺序（在加权 OIT 的同等位置替换/追加）：

1. 主 pass（天空/不透明/cutout，写深度与主色）
2. `DEPTH_BOUNDS`（目标 depthBounds，混合 MAX，深度 `>=` 只读）
3. `depth_bounds_cull`（→ depthBoundsCulled）
4. `TRANSMITTANCE`（2×RGBA16F，ADDITIVE）
5. `ACCUMULATE`（RGBA16F，ADDITIVE）
6. `COMPOSITE`（读 accum + 系数 + depthBoundsCulled，写主色与深度）
7. UI pass（不变）

三阶段各自把 `arenaTranslucent` / 静态半透明画一遍（同一几何，三个片元入口）。

## 8. 验证策略（纯逻辑优先）

1. **小波往返**：C# 参考实现 `encode(absorbs, depth)` 再 `sample(depth)`，对随机 8~64 层、随机深度/alpha，
   断言重建的透射率与解析值 `exp(-Σ absorb)` 的误差有界；并断言**输入顺序不影响结果**。
   这是"能靠纯逻辑量化"的核心等效性（用户方针）。
2. **不变性**：`coverage = 1 - Π(1-α)` 与累计顺序无关；单片退化为普通 alpha。
3. **运行时**：确定性水景（`MC_FRAME_WATER=1`）窗口跑满若干帧、**0 wgpu validation error**、p99 记录。
4. **像素**：本环境不可截窗口，目视由所有者本机 `dotnet run … window` 确认（现已是唯一透明路径）。

## 9. 风险与未决

- 精度：8 系数 × RGBA16F 对多层堆叠有近似；原版接受该近似（`OIT_COEFF_COUNT=8` 是设计值）。
- 成本：三阶段 = 半透明几何画 3 遍 + 3 个全屏 pass；这是现在唯一的透明管线（加权 OIT 已封存）。
- 深度约定：`deviceToLinearDepth` 依赖投影矩阵项与深度范围 `[0,1]` 或 `[-1,1]`；须与我们 `CameraMath` 投影一致核对。
- 开关落地须遵守原则 13：`MC_TRANSPARENCY` 只在宿主层解析，游戏程序集只收显式选项；失败回落 weighted。

## 10. 实现约束：标准 Z 适配（关键，勿照抄符号）

原版是**反向 Z**：`GREATER_THAN_OR_EQUAL` 深度比较 + 深度边界用 `MAX` 混合即可同时得到"最近线性/最远线性/最近器件"。
我们是**标准 Z**（`CameraMath.BuildProjection` 产出 WebGPU `[0,1]` 深度、近 0 远 1，深度比较 `LessEqual`，清 `1.0`），
所以所有"最近/最远"的取法都要翻转，否则会得到一个看着像、其实深度方向反了的画面。落地约定：

| 事项 | 原版（反向 Z）| 我们（标准 Z）|
| --- | --- | --- |
| 深度比较（OIT 三阶段）| `GREATER_THAN_OR_EQUAL` | `LESS_EQUAL`；仍不写深度 |
| 深度边界编码到 RGBA16F（混合 `MAX`）| `(-linear, linear, device, opaqueDevice)` | `(-linear, +linear, -device, alpha>0.99 ? -linear : -65504)` |
| 还原 | 直接读 | `closestLinear = -r`、`furthestLinear = g`、`closestDevice = -b`、`opaqueLinear = -a` |
| composite 的 `frag_depth` | `= b`（最近器件）| `= -b` |

理由：四个通道**都取 `MAX`** 仍是自洽的——`r:MAX(-linear)=-最近`、`g:MAX(+linear)=最远`、`b:MAX(-device)=-最近器件`、
`a:MAX(-linear_opaque | -65504)=-最近不透明线性`（无则不透明为 `-65504`⇒还原 65504⇒`min(g,65504)=g`，等价"无上限"）。
注意：目标格式用 **RGBA16F**，因为本后端 RGBA32F 不支持混合；16F 在远处（~2000 方块）线性深度约有 ±1–2 方块量化，reference 可接受。

线性深度**直接复用顶点着色器已输出的 `fogDistance = clip.w`（= 视距，单位方块）**，不引入 `ProjMat`；器件深度用
`clip.z/clip.w`（NDC z）作为插值传递。`depth_bounds_cull` 与 `composite` 在此约定下翻转比较与符号。

## 11. 未实现清单（本轮只做地形半透明，其余形成文档不做）

以下属于原版 OIT 的一部分，但当前客户端**没有对应内容**，故本规格**不实现**，待内容存在后再接：

- `oitCloudDepth` / `oitTerrainWithWaterPatchDepth` 两个额外深度目标与 `water mask` 阶段（水面贴片/云）。
- 实体、粒子、世界文字、云、天气、世界边界、光束、物品光效等的 OIT 管线集合（`RenderPipelines.OIT_ENTITY` 等）。
- `OIT_ADDITIVE` 特例（自发光面只贡献颜色、不遮挡）。
- 原版按维度的 `OitPipelineSet` 变体（`OIT_TERRAIN_MULTIDRAW` 等）。

## 12. 参考指针

- `ref://mc-26.3-analysis/src/net/minecraft/client/renderer/oit/OitStage.java`
- `ref://mc-26.3-analysis/src/net/minecraft/client/renderer/oit/OitPipelineSet.java`
- `ref://mc-26.3-analysis/src/net/minecraft/client/renderer/oit/OitRenderPassProvider.java`
- `ref://mc-26.3-analysis/src/net/minecraft/client/renderer/LevelRenderer.java`（executeOit / executeDepthBoundsCull / executeOitWaterMask）
- `ref://mc-26.3-analysis/src/net/minecraft/client/renderer/RenderPipelines.java`（OIT_*_SNIPPET、OIT_COMPOSITE、OIT_DEPTH_BOUNDS_CULL）
- `ref://mc-26.3-analysis/src/assets/minecraft/shaders/include/oit.glsl` / `oit_common.glsl` / `oit_depth_bounds.glsl` / `oit_add_transmittance.glsl` / `oit_depth_sample.glsl` / `oit_sample.glsl`
- `ref://mc-26.3-analysis/src/assets/minecraft/shaders/core/oit_composite.fsh` / `oit_depth_bounds_cull.fsh`
