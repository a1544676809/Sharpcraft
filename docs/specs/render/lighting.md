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

- **已实现（ADR-0019，顶点契约变更）**：顶点改为 14-float，携带 `sky`/`block` 光照等级（0..1 = level/15）；基础色通道存 `tint × 朝向shade × AO`。`terrain.wgsl`/`oit_reference.wgsl` 逐片元用每帧 `LightmapUniform` UBO（`skyFactor`、`blockFactor`、`brightnessFactor`、`skyLightColor`、`blockLightTint`、`ambientColor`，64 字节）精确复刻 `VanillaLightmap.Colour` 现算 lightmap，再 `litColour(texel, base × lightmap)`。离屏与窗口渲染器共用（binding 4），**时间变化只改 UBO，不重建网格**。
- **参数来源**：`LightmapParameters.OverworldAt(dayTime)` 用 `SkyAttributeTracks.SkyLightFactorAt/SkyLightColorAt`（昼 1.0/白；夜 0.24/(0.48,0.48,1) 线性），经 `FrameCommand.ToLightmapUniform` 映射为 `LightmapUniform`。
- **等价证据（纯逻辑 + 出图）**：`VanillaLightmapTests` 手算——正午全白天体 =1；夜全白天体 ≈(0.088,0.088,0.453)（R=G<B）。本地帧同一网格、仅换 UBO：地形 `top-mid` 昼 `rgba(136,136,136)` → 夜 `rgba(45,45,95)`，夜空可见星星。
- **逐片元 vs 旧烘焙**：旧实现插值“已烘焙颜色”，新实现插值等级后逐片元算非线性 lightmap——更接近原版（原版传光照等级并用 lightmap 纹理逐片元采样），平滑光照渐变处有小差异，属受控且更正确的偏差。
- 天气（雨/雷）、维度（下界/末地）环境参数未建模；`ambientColor` 恒 0。

## 7. 已知偏差与范围

- AO 的"实心"判定用纹理不透明度而非碰撞形状（§4）。
- 未实现：夜视、Darkness 效果、Boss 遮罩暗化、闪电闪光加成、**天气/维度环境参数（§6；日夜已实现）**、方块光闪烁。
- lightmap 反 gamma（notGamma）与 `brightnessFactor` 按默认 0.5 参与，未接亮度滑块。

## 8. 等效性测试计划（纯逻辑、可量化）

1. **数学等价**（`VanillaLightmap` vs §3 手算值）：`(15,0)=(1,1,1)`、`(0,0)=(0,0,0)`、`(12,0)≈0.71875`、`(0,15)` 暖色 R>G>B；`brightness(1)=1`、单调递增。
2. **网格等价**：给定世界光照与方向/AO，`ChunkMesher` 产出的顶点颜色（线性，经解码后的 `light` 通道）应等于 `VanillaLightmap(sky,block) * shade * AO`（容差 1e-4）。
3. **回归**：满天空光(`sky=15`)的开放面顶点亮通道 ≈ 1.0；`sky=0,block=0` 处 ≈ 0（原版无环境光的洞穴为黑）。

## 9. 实现落点

- `src/Sharpcraft.Client/Render/VanillaLightmap.cs`：§3 的纯函数 + `LightmapParameters`。
- `src/Sharpcraft.Client/Render/ChunkMesher.cs`：`CornerShade` 用 `VanillaLightmap` 替换旧线性 `Brightness`。
- `src/Sharpcraft.Framework/Rendering/Shaders/pipelines/terrain.wgsl` 与 `oit_reference.wgsl`：§5 的 `litColour`。

## 10. 未解缺陷：日出日落时可见区块接缝（T-835）

**现象**：日出/日落时段，远近地形与水面出现矩形明暗边界，正好沿区块边界；**正午与午夜看不见**。原版无此现象（用户实测确认）。

**为什么只有日出日落可见**：正午 `SkyFactor` 饱和为 1、午夜为 0，所有区块取到同一个值；只有中间段该值连续变化，区块之间才可能不一致。所以缺陷一定与「随时间变化的量」按区块冻结有关。

**已核查并排除**：

| 检查 | 结果 |
| --- | --- |
| `ChunkMesher` 是否烘焙时间因子 | **否**。`parameters` 在 L101/L139 赋值后从未被使用；顶点只写 `sky`/`block` 原始等级 |
| 顶点契约 | `TerrainVertexWriter` 写 8 位 `sky`、`block`，与规格 §6 一致 |
| 时间因子传递 | 经 uniform（`ToLightmapUniform`）逐帧传入着色器，与几何无关 |

### 10.1 根因（已定位，用户截图对照确认）

雾的等距面用**视空间深度**而非**径向距离**：

```wgsl
// oit_reference.wgsl / cloud_oit.wgsl
output.fogDistance = clip.w;   // 沿视线方向的深度
```

`clip.w` 的等距面是**垂直于视线的平面**，因此：

- 低头看水面时，雾边界是一条**水平直线**（原版是**圆的**）
- 转动视角时，边界跟着转（原版锚定世界空间，不转）

原版用 `length(worldPos - cameraPos)`，等距面是球面。

**这也解释了 T-835 的日落接缝**：雾色本身正确，是等距面几何错了，区块边界因此在错误的等距面上交替显形；正午/午夜因为整片雾色饱和而看不见。

**修法**：顶点着色器用世界位置与相机位置的欧氏距离，替代 `clip.w`。`cloud_oit.wgsl` 同样处理。需注意 OIT 的 `deviceDepth` 仍应使用 `clip.z / clip.w`（深度语义不变），只有 `fogDistance` 改为径向。

**残留可疑点（未验证）**：`ChunkMeshCache` 在光照变化时清空整个缓存：

```csharp
if (Lighting != _appliedLighting)
{
    _meshes.Clear();
    _appliedLighting = Lighting;
}
```

既然顶点不依赖 `LightmapParameters`，这次清空**在语义上是不必要的**。但它是否与接缝有关尚未确认：清空后重建的网格内容应当完全相同，不该产生视觉差异。需要用「同一时刻、部分区块重建、部分不重建」的对照帧来判定。

**原版对照**（`AtmosphericFogEnvironment.getBaseColor`）：雾色在日落时按**视线方向**叠加 `SUNRISE_SUNSET_COLOR`——

```java
float lookingAtTheSunFactor = forwardVector.dot(sunX, 0, 0);   // 朝太阳才 > 0
if (lookingAtTheSunFactor > 0) fogColor = srgbLerp(lookingAtTheSunFactor * alpha, fogColor, color);
```

最后再与天空色 `srgbLerp(skyColorMixFactor, ...)` 混合。**这条方向性我们尚未实现**，需确认是否就是接缝的来源（若雾色不随视线变，则任何区块边界处的雾/天过渡都会显形）。

**下一步具体动作**（按顺序，每步都能单独判定）：

1. 固定时间到日落，用 `MC_AB_TOGGLE` 或强制部分区块重建，观察接缝是否随重建变化 → 判定是否与网格生命周期有关
2. 关掉 `_meshes.Clear()`（临时），同机位对比 → 若接缝消失，问题即在此
3. 若上述都不是，检查雾色是否随视线方向变化 → 对照原版补方向性
## 11. 雾的距离：双壳算法（T-835）

原版 `shaders/include/fog.glsl`：

```glsl
float linear_fog_value(float d, float start, float end) {
    if (d <= start) return 0.0;
    if (d >= end)   return 1.0;
    return (d - start) / (end - start);
}

float total_fog_value(spherical, cylindrical, envStart, envEnd, rdStart, rdEnd) {
    return max(linear_fog_value(spherical,   envStart, envEnd),
               linear_fog_value(cylindrical, rdStart,  rdEnd));
}
```

两个距离**同时**从顶点传出：

| 壳 | 距离 | 形状 | 用哪组范围 |
| --- | --- | --- | --- |
| 球形 | `length(pos)` | 球 | `environmentalStart/End`（`FOG_START/END_DISTANCE`，默认 1024 格） |
| 柱形 | `max(length(pos.xz), abs(pos.y))` | 柱 | `renderDistanceStart/End` |

柱形的意义：**垂直方向不增加距离**，抬头/低头与水平看时渐隐位置一致。

范围推导（`FogRenderer.setupFog`）：

```java
float span = Mth.clamp(renderDistanceBlocks / 10.0F, 4.0F, 64.0F);
fog.renderDistanceStart = renderDistanceBlocks - span;
fog.renderDistanceEnd   = renderDistanceBlocks;
```

视距 12 → `172.8 .. 192`。

**本仓实现状态**：柱形壳已实现；球形大气壳未接，见 `known-gaps.md`。`pos` 为相机相对坐标（`position - camera.eye`），因此 `Camera` uniform 必须在 `viewProjection` 之后**声明 `skyViewProjection` 占位**，否则 `eye` 落在字节 64 读到天空矩阵。
### 11.1 大气壳的常数值与雨雾（原版 26.3，已核实）

| 属性 | 默认值 | 来源 |
| --- | --- | --- |
| `FOG_START_DISTANCE` | **0.0** | `EnvironmentAttributes` |
| `FOG_END_DISTANCE` | **1024.0** | 同上 |
| `SKY_FOG_END_DISTANCE` | 512.0 | 同上 |

`AtmosphericFogEnvironment.setupFog`：

```text
environmentalStart = FOG_START_DISTANCE + (-160.0) * rainFogMultiplier
minRainFogEnd      = min(96.0, environmentalEnd)
environmentalEnd   = max(minRainFogEnd, environmentalEnd + (-256.0) * rainFogMultiplier)
```

`updateRainFogState`（`rainFogMultiplier` 逐刻趋近目标）：

```text
skyLightFactor = clamp((skyLight - 8) / 7, 0, 1)
target         = rainLevel * skyLightFactor * (biome.hasPrecipitation ? 1.0 : 0.5)
rainFogMultiplier += (target - rainFogMultiplier) * deltaTicks * 0.2
```

**重要修正**：即使**不下雨**（`rainFogMultiplier = 0`），大气壳仍是 `0..1024`，在 100 格处贡献 `100/1024 ≈ 0.098`、150 格处 `0.146`。由于 `total_fog_value` 取 `max(球形, 柱形)`，原版**任何天气下都有一层随距离线性增长的常驻雾**，而本仓当前只实现柱形壳，因此中距离**完全无雾**。

该修正使本项从"只影响雨天"变为"影响所有距离的画面等效性"，优先级上升。

**验证方法（已建立）**：`MC_FRAME_LOCAL=1` + `--frame` 渲染 `BuildDemoWorld`，两次哈希相同（确定性）。改动前后对拍得：不同像素 7.82%、最大通道差 10/255、差异区域恰为岛屿几何。**注意：连服务器的离线帧不适合验证雾**——该帧几乎全是天空，地形着色器覆盖像素极少，连极端值都测不出来。