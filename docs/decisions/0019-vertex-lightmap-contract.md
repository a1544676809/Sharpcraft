<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# ADR-0019: 顶点携带光照等级，lightmap 在着色器每帧现算

- 状态：已接受（所有者 2026-11-01 “可以修改” 授权）
- 影响面：地形/流体/方块实体/实体顶点格式、`terrain.wgsl`/`oit_reference.wgsl`、离屏与窗口渲染器、`FrameRequest`
- 相关：`docs/specs/render/lighting.md §6`、原则 12（跨模块契约变更需所有者确认 + ADR）

## 背景

光照乘子（lightmap）目前在网格构建时按 `(sky, block)` 烘焙进顶点色，见 `VanillaLightmap`。昼夜由 `SkyAttributeTracks` 的 `SKY_LIGHT_FACTOR`/`SKY_LIGHT_COLOR` 驱动（夜 0.24 / 蓝）。烘焙方式下，时间一变就得重网格化：离屏路径每帧重建尚可，实时窗口路径只能用“参数变化→整体重建”的临时办法（T-261），会有一帧尖峰。

原版模型是：顶点带 `(sky, block)` 光照等级，lightmap 在着色器里用每帧 uniform（`skyFactor`、`skyLightColor`、`blockFactor`、`ambient`、`brightness`）现算，逐片元采样（非线性），因此时间变化零重建。

## 决策

把 12-float 顶点 `pos3 + colour3 + uv2 + spriteRect4` 扩展为 **14-float**：

```
pos.x, pos.y, pos.z,          // 世界坐标
base.r, base.g, base.b,       // 基础色 = 生物群系 tint × 面朝向 shade × AO（线性）
uv.u, uv.v,                   // 精灵内重复坐标
rect.u0, rect.v0, rect.u1, rect.v1,  // 图集精灵矩形
sky, block                    // 光照等级（各 0..1 = level/15）
```

`terrain.wgsl` / `oit_reference.wgsl` 新增：

- 顶点属性 `@location(4) light: vec2<f32>`；
- 每帧 `Lightmap` uniform（`skyFactor`、`blockFactor`、`brightnessFactor`、`skyLightColor`、`blockLightTint`、`ambientColor`，64 字节）；
- 片元里 `lightmap = lightmapColour(light.x, light.y)`（精确复刻 `VanillaLightmap.Colour` 的公式：brightness 曲线、parabola 混合方块光色、clamp、notGamma、brightnessFactor 混合），再 `litColour(texel, base * lightmap)`。

这样光照逐片元计算（等价于原版“顶点传光照等级 + lightmap 纹理采样”），且昼夜只改 UBO，**不重建网格**。

`LightmapParameters`（Client）映射为 Framework 的 `LightmapUniform` 后放入 `FrameRequest`，由渲染器上传；Framework 不反向依赖 Client。

## 影响与验证

- 顶点步长 48 → 56 字节；方块实体/实体占位网格补 `light=(1,0)`（全天空，随昼夜变化）。
- `ClipSpaceTransform`（CPU 基准工具）保持输出裁剪空间 13-float，仅从 14-float 输入取其前 12 项，无需语义变更；相关测试的输入改为 14-float。
- 视觉较“烘进顶点色”的旧实现**逐片元更接近原版**：平滑光照渐变处会有小差异（旧实现是插值已烘焙颜色，新实现是插值等级后逐片元算非线性 lightmap），属**受控、更正确的偏差**，由离屏出图与昼夜 UBO 差异佐证。
- 移除 T-261 的“参数变化整体重建”临时逻辑（不再需要）。

## 备选与取舍

- 保留烘焙、缓存按参数重建（T-261）：无契约变更，但每次昼夜变化重网格化、有尖峰，且非原版模型。已作为临时方案验收。
- 把光照压进 1 个 float（packed）：省 4 字节，但解码复杂、可读性差；不采用。

## 后果

- 契约变更：任何自建顶点缓冲的模块（占位网格、未来实体模型）必须写 14-float。
- 回退成本：改回 12-float 并恢复烘焙；可控。
