<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 MinecraftCSharp contributors -->

状态：[已接受]
日期：2026-10-28

# ADR-0004：窗口层选用 Silk.NET.Windowing，并抽象出原生句柄

## 背景

原则 6 要求窗口创建、输入等平台能力走抽象接口并给出各平台实现；AGENTS §5 把"WGPU 绑定与窗口层选型"列为待决项。ADR-0003 已定 WGPU 绑定（`Brew.Wgpu`），本 ADR 补齐窗口层，使渲染能真正上屏（M2 第 4 步）。

## 决策

1. **窗口库**：`Silk.NET.Windowing` 2.23.0（GLFW/SDL 后端由包依赖决定，Windows 上为 GLFW）。
2. **抽象边界**：`MinecraftCSharp.Framework.Platform.WindowHost` 是唯一接触窗口 API 的类，对外只暴露与平台无关的 `NativeSurfaceSource`（Windows / X11 / Wayland / MetalLayer 四种句柄形态）。渲染器只消费 `NativeSurfaceSource`，不了解窗口库。
3. **呈现**：`WindowedRenderer` 用交换链呈现；**截图仍走离屏路径**（`OffscreenRenderer`），因为交换链纹理不保证可拷贝。
4. **尺寸**：交换链按 **framebuffer 物理像素**配置，而非逻辑尺寸（DPI 缩放下列两者不同，按逻辑尺寸配置会导致每帧 `Outdated`）。

## 备选方案

| 方案 | 优点 | 缺点 | 结论 |
| --- | --- | --- | --- |
| **Silk.NET.Windowing** | 成熟、跨平台（GLFW/SDL）、与 Silk.NET 生态一致、句柄类型齐全 | 依赖较重；版本需与 Silk.NET.Core 对齐 | **采纳** |
| 直接 P/Invoke Win32 窗口 | 依赖最少 | 只覆盖 Windows，违反跨平台原则；需自写消息泵 | 否决 |
| SDL3 绑定 | 跨平台、输入/音频一体 | 额外原生依赖，且与现有 Silk.NET 生态重复 | 暂不采纳 |

## 实施中发现的坑（记录以备复查）

| 现象 | 原因 | 处理 |
| --- | --- | --- |
| 交换链配置后每帧都判定"需要重建"，画面永不上屏 | `Brew.Wgpu` 的 `SurfaceAcquireResult.NeedsReconfigure` 用 `(int)(Status - 4) <= 1` 判断，而状态枚举是 `uint`：`SuccessOptimal(1)` 相减后回绕为负，被误判为需要重建 | 改为显式比较 `Outdated` / `Lost`；该辅助属性视为不可用（已登记 R-012） |
| `wgpuSurfaceConfigure: SurfaceOutput must be dropped before a new Surface is made` | 拿到交换链纹理后立即重建配置，纹理仍存活 | 重建前先释放纹理 |
| 按逻辑尺寸配置导致持续 `Outdated` | DPI 缩放：逻辑 960×540，物理可能是 1440×810 | 一律使用 framebuffer 物理像素 |

## 后果

- `MinecraftCSharp.Framework` 新增 `Silk.NET.Windowing` 依赖；渲染代码仍是唯一持有 WGPU 句柄的地方。
- 目前只在 Windows 上实测；X11/Wayland/macOS 分支代码存在但**未验证**（登记 R-011）。
- 窗口只有呈现能力：无输入、无 resize 事件处理（尺寸变化时重建配置的逻辑已就位，但未接入事件）。
- 帧循环是"呈现 N 帧后关闭"的临时形态，正式的主循环（输入 → 更新 → 渲染）留到后续里程碑。

## 复查条件

- 需要在 Linux/macOS 上验收窗口时（触发 R-011 复查）。
- 引入输入系统时（窗口层将扩展为事件源）。
- `Brew.Wgpu` 修复 `NeedsReconfigure` 后，可简化状态判断。

## 修订记录

### 2026-10-28 — resize 与宽高比缺口补齐

首次落地时窗口只跟随初始尺寸，投影矩阵用的是请求的逻辑尺寸而非 framebuffer 尺寸。补正内容：

- 相机投影改用像素重载 `CameraMath.BuildViewProjection(eye, target, fov, width, height)`，与交换链共用同一尺寸来源；
- 每帧检测 framebuffer 尺寸变化，变化时**同时**重建交换链配置、深度纹理与顶点缓冲（顶点烘焙了宽高比）；
- 新增确定性验证开关 `MC_WINDOW_SELFRESIZE=WxH`：第 30 帧触发生成 resize，实测 `960x540 -> 640x360` 后继续正常呈现。

结论未变：窗口层仍为 Silk.NET.Windowing + 原生句柄抽象。
