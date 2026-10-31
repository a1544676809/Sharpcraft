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
- **AO**：同一 4 格中每格贡献 `shadeBrightness = 实心碰撞方块 ? 0.2 : 1.0`，取平均作为标量乘子。
  - 已知偏差：本实现"实心"判定用纹理不透明度（`Occludes`，冰/水/玻璃不算遮挡），原版用碰撞形状（冰算遮挡）。见 §7。
- **面朝向**：按 `CardinalLighting.DEFAULT` —— 上 1.0 / 下 0.5 / 南北 0.8 / 东西 0.6，作标量乘子。

最终顶点颜色（线性）= `lightmap(sky,block) * shade * AO`（RGB 逐通道乘标量）。纹理块再与线性化后的贴图逐通道相乘。

## 5. 色彩空间

- 贴图集存储为 `RGBA8Unorm`（sRGB 编码值，硬件不解码）；交换链为 `BGRA8Unorm`（无 sRGB 编码）。
- 若直接在 gamma 空间乘光照会使任何 `L<1` 的面过度压暗。故地形/半透明着色器用：**贴图 sRGB 解码(2.2 近似) → 乘线性 lightmap → 重新编码(1/2.2)**；满光时返回原贴图不变。
- `ARGB.vector3fFromRGB24` 用精确 sRGB→linear 曲线（`x>=0.04045 ? ((x+0.055)/1.055)^2.4 : x/12.92`）把环境色转为线性。

## 6. 时间 / 天气 / 维度（首版范围外）

原版 `skyFactor`、`skyLightColor`、`ambientColor`、`skyLightLevel` 由维度类型与 **时间轴（Timelines）** 驱动：白天 `skyFactor=1.0`、天空光色为白；夜晚 `skyFactor≈0.24`、天空光色约 `(0.48,0.48,1.0)`；雷暴/降水另有修正。
**首版仅实现 overworld 白天默认**（§3 表）。夜晚/天气/维度的时间轴插值列为后续工作，参数接口预留（`LightmapParameters`）。

## 7. 已知偏差与范围

- AO 的"实心"判定用纹理不透明度而非碰撞形状（§4）。
- 未实现：夜视、Darkness 效果、Boss 遮罩暗化、闪电闪光加成、时间轴日夜/天气、方块光闪烁。
- lightmap 反 gamma（notGamma）与 `brightnessFactor` 按默认 0.5 参与，未接亮度滑块。

## 8. 等效性测试计划（纯逻辑、可量化）

1. **数学等价**（`VanillaLightmap` vs §3 手算值）：`(15,0)=(1,1,1)`、`(0,0)=(0,0,0)`、`(12,0)≈0.71875`、`(0,15)` 暖色 R>G>B；`brightness(1)=1`、单调递增。
2. **网格等价**：给定世界光照与方向/AO，`ChunkMesher` 产出的顶点颜色（线性，经解码后的 `light` 通道）应等于 `VanillaLightmap(sky,block) * shade * AO`（容差 1e-4）。
3. **回归**：满天空光(`sky=15`)的开放面顶点亮通道 ≈ 1.0；`sky=0,block=0` 处 ≈ 0（原版无环境光的洞穴为黑）。

## 9. 实现落点

- `src/Sharpcraft.Client/Render/VanillaLightmap.cs`：§3 的纯函数 + `LightmapParameters`。
- `src/Sharpcraft.Client/Render/ChunkMesher.cs`：`CornerShade` 用 `VanillaLightmap` 替换旧线性 `Brightness`。
- `src/Sharpcraft.Framework/Rendering/Shaders/pipelines/terrain.wgsl` 与 `oit_reference.wgsl`：§5 的 `litColour`。
