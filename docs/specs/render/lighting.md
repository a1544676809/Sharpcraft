<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# 光照与 lightmap 行为规格

状态：草案（待评审）。范围：方块几何的逐顶点光照与 lightmap 乘子。
证据指针：`ref://mc-26.3-analysis/src/net/minecraft/client/renderer/Lightmap.java`、
`.../LightmapRenderStateExtractor.java`、`.../shaders/core/lightmap.fsh`、
`.../util/ARGB.java`、`.../world/attribute/EnvironmentAttributes.java`、`.../Timelines.java`、
`.../renderer/block/BlockModelLighter.java`、`.../renderer/block/ModelBlockRenderer.java`、
`.../renderer/block/CardinalLighting.java`、`.../util/LightCoordsUtil.java`。

本文件只描述**可观测行为**（给定输入产出什么颜色），不含原始实现。

## 1. 总览

一个方块顶点的最终颜色 = **贴图颜色（解码到线性）** × **lightmap 乘子（RGB，线性）** × **面朝向 shade（标量）** × **AO（标量）**，再编码回 sRGB 输出。

- **lightmap 乘子**来自该顶点四个相邻格的天空光/方块光，见 §3。
- **面朝向 shade** 与 **AO** 是标量乘子，见 §4。
- **色彩空间**：lightmap 乘子是线性值；贴图解码/编码见 §5。

## 2. 光照等级来源

每个方块位置有 `skyLight`、`blockLight`，各 0..15。来源为服务器区块包的 light nibble 数组（每个 16×16×16 光照段 2048 字节，低 nibble 为偶数 x）。
客户端读取见 `src/Sharpcraft.Common/World/ChunkLight.cs`；探针实测露天地表为 `sky=15 block=0`。

**缺省填充规则（关键）**：区块包只为部分光照段下发数组。对每个光照段：
- 在天光掩码里 → 用下发的数组；
- 不在掩码、也不在"全零掩码"里 → **默认整段 15**（露天段通常不下发，省略即满阳光）；
- 在"全零掩码"里 → 0。
方块光缺省为 0。
忽略"省略即 15"会让所有露天段读成 0，整片户外发黑、只有少数带数组的地表段偏亮（正是此前"海平面附近反而亮"的现象）。


## 3. Lightmap 乘子

对一组 `(sky, block)`（0..15），按下式得线性 RGB：

1. `block_level = block / 15`，`sky_level = sky / 15`。
2. 亮度曲线 `brightness(x) = x / (4 - 3x)`（满级 1→1，明显非线性）。
3. `block_brightness = brightness(block_level) * blockFactor`；`sky_brightness = brightness(sky_level) * skyFactor`。
4. `color = ambientColor`（叠加夜视/闪电/ Boss 暗化为 0，首版不建模）。
5. `color += skyLightColor * sky_brightness`。
6. `blockLightColor = mix(blockLightTint, 白, 0.9 * t)`，其中 `t = (2*block_level - 1)^2`。
7. `color += blockLightColor * block_brightness`。
8. `color = clamp(color, 0, 1)`。
9. 反 gamma 提升：`notGamma(c)`：`m = max(c)`，若 `m<=0` 返回 `c`，否则 `inv = 1-m`，`scaled = 1 - inv^4`，返回 `c * (scaled / m)`。
10. `color = mix(color, notGamma(color), brightnessFactor)`，再 `clamp(0,1)`。

**overworld 白天默认参数**（首版采用）：

| 参数 | 值 | 说明 |
| --- | --- | --- |
| `skyFactor` | 1.0 | 白天天空光系数 |
| `blockFactor` | 1.4 | 方块光系数（原版含 ±0.1 闪烁，首版取基值） |
| `skyLightColor` | (1,1,1) 线性 | 白天天空光颜色（白） |
| `blockLightTint` | `srgb24→linear(-10100)` ≈ (1.000, 0.687, 0.263) | 暖色方块光 |
| `ambientColor` | (0,0,0) | overworld 环境光为黑 |
| `brightnessFactor` | 0.5 | 亮度滑块默认值 |

冒烟值（手算，供测试断言）：
- `(sky=15, block=0)` → (1,1,1)。
- `(sky=0, block=0)` → (0,0,0)。
- `(sky=12, block=0)` → 约 (0.71875, 0.71875, 0.71875)。
- `(sky=0, block=8)` → 暖色，R > G > B（满方块光会 clamp 到白）。

## 4. 逐顶点平滑光照 / AO / 面朝向

- **平滑光照**：每个顶点取"该面外侧 4 个格子"（base、base-u、base-v、base-u-v，u/v 为面内两轴），分别对 sky、block 求平均，再代入 §3 得该顶点的 lightmap 乘子。
- **AO**：同一 4 格中每格贡献 `shadeBrightness = 实心碰撞方块 ? 0.2 : 1.0`，取平均作为标量乘子（纯函数 `ChunkMesher.AmbientOcclusion`），并含原版**拐角柔化**规则：当两条边格都实心时，对角格视为被它们遮蔽（用边格值），避免开放对角把半封闭凹角算亮。
- **空气永不遮挡**：`state 0`（本客户端的空气态）直接判为非遮挡。此前空气走"无布局→不透明"分支，使 AO 平均塌到 0.2 下限、整片世界变暗——这正是"关 AO 就正常"的原因。已修复（`Occludes` 短路）。
- **默认开启 AO**；宿主开关 `MC_NO_AO=1` 可关做 A/B。
  - 已知偏差：本实现"实心"判定用纹理不透明度（`Occludes`，冰/水/玻璃不算遮挡），原版用碰撞形状（冰算遮挡）。见 §7。
- **实机对比**：此前"整体偏暗"实为 AO 把空气误判为遮挡（已修，见上）。修好后 AO 观感已接近原版（2026-10-31 所有者确认）。
- **面朝向**：按 `CardinalLighting.DEFAULT` —— 上 1.0 / 下 0.5 / 南北 0.8 / 东西 0.6，作标量乘子。

最终顶点颜色（线性）= `lightmap(sky,block) * shade * AO`（RGB 逐通道乘标量）。纹理块再与线性化后的贴图逐通道相乘。

## 5. 色彩空间

- 贴图集存储为 `RGBA8Unorm`（sRGB 编码值，硬件不解码）；交换链为 `BGRA8Unorm`（无 sRGB 编码）。
- 若直接在 gamma 空间乘光照会使任何 `L<1` 的面过度压暗。故地形/半透明着色器用：**贴图 sRGB 解码(2.2 近似) → 乘线性 lightmap → 重新编码(1/2.2)**；满光时返回原贴图不变。
- `ARGB.vector3fFromRGB24` 用精确 sRGB→linear 曲线（`x>=0.04045 ? ((x+0.055)/1.055)^2.4 : x/12.92`）把环境色转为线性。

## 6. 时间 / 天气 / 维度

- **已实现（离屏路径，2026-11-01）**：`LightmapParameters.OverworldAt(dayTime)` 用 `SkyAttributeTracks.SkyLightFactorAt/SkyLightColorAt` 取当前 `skyFactor`/`skyLightColor`（昼 1.0 / 白；夜 0.24 / (0.48,0.48,1) 线性），其余参数保持昼默认；`ChunkMesher`（`Mesh`/`MeshChunk`/`MergeSectionFaces`/`EmitMergedQuad`/`CornerShade`/`EmitSectionFluids`）贯穿 `LightmapParameters`，在 UV/AO lightmap 计算处用其求值。离屏路径每帧重建网格，因此顶点色随 `DayTime` 更新，**无需改顶点格式或着色器**。
- **等价证据（纯逻辑 + 出图）**：`VanillaLightmapTests` 手算——正午 `OverworldAt(6000)` 全白天体 =1；夜 `OverworldAt(18000)` 全白天体 ≈(0.088,0.088,0.453)（R=G<B，暗且偏蓝）；本地帧地形 `top-mid` 昼 `rgba(136,136,136)` → 夜 `rgba(45,45,95)`。
- **已实现（实时窗口路径，分片式）**：网格缓存（`ChunkMeshCache.Lighting`）与后台网格池（`ChunkWorkerPool.Lighting`）记录所用 `LightmapParameters`；`WindowCommand` 每帧比较当前 `OverworldAt(DayTime)`，一旦变化即 `Clear` 全部缓存并按新参数重建（`QueueMissing`，逐个分片重传）。因为光图烘进顶点色，这必然重网格化一次；`ChunkMeshCacheTests.ChangingTheLightmapParametersRemeshesEveryChunk` 证明“同参不重建、变参全重建”。**代价**：每次显著日夜变化会有一帧重网格化尖峰（按参数变化触发，非每帧）。
- **真正零重建的正确做法（未来，需 ADR）**：顶点只存 `sky`/`block` 等级 + `AO×朝向` 标量，lightmap 在着色器用**每帧更新的 UBO**（skyFactor、skyLightColor、blockFactor、ambient、brightness）现算。这要改 12-float 顶点契约（terrain/fluid/方块实体/实体 + 两渲染器 + 着色器），属**跨模块契约变更**，按原则 12 须所有者确认并落 ADR 后再实施。
- 天气（雨/雷）、维度（下界/末地）环境参数未建模；`ambientColor` 恒 0。

## 7. 已知偏差与范围

- AO 的"实心"判定用纹理不透明度而非碰撞形状（§4）。
- 未实现：夜视、Darkness 效果、Boss 遮罩暗化、闪电闪光加成、**天气/维度环境参数（§6；日夜已在离屏路径实现）**、方块光闪烁。
- lightmap 反 gamma（notGamma）与 `brightnessFactor` 按默认 0.5 参与，未接亮度滑块。

## 8. 等效性测试计划（纯逻辑、可量化）

1. **数学等价**（`VanillaLightmap` vs §3 手算值）：`(15,0)=(1,1,1)`、`(0,0)=(0,0,0)`、`(12,0)≈0.71875`、`(0,15)` 暖色 R>G>B；`brightness(1)=1`、单调递增。
2. **网格等价**：给定世界光照与方向/AO，`ChunkMesher` 产出的顶点颜色（线性，经解码后的 `light` 通道）应等于 `VanillaLightmap(sky,block) * shade * AO`（容差 1e-4）。
3. **回归**：满天空光(`sky=15`)的开放面顶点亮通道 ≈ 1.0；`sky=0,block=0` 处 ≈ 0（原版无环境光的洞穴为黑）。

## 9. 实现落点

- `src/Sharpcraft.Client/Render/VanillaLightmap.cs`：§3 的纯函数 + `LightmapParameters`。
- `src/Sharpcraft.Client/Render/ChunkMesher.cs`：`CornerShade` 用 `VanillaLightmap` 替换旧线性 `Brightness`。
- `src/Sharpcraft.Framework/Rendering/Shaders/pipelines/terrain.wgsl` 与 `oit_reference.wgsl`：§5 的 `litColour`。
