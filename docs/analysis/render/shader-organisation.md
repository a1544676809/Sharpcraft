<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 原版与本项目的着色器组织对比
summary: 原版 93 个着色器条目的组织规范；我们当前的内联状态与改造建议
status: 草稿
-->

# 原版与本项目的着色器组织对比（2026-10-30）

证据：本机原版 jar 内 `assets/minecraft/shaders/` 实测 **93 个条目**（只读文件名，未复制任何内容）。

## 1. 原版的组织规范（实测）

| 目录 | 条目 | 规矩 |
| --- | --- | --- |
| `core/` | **64** | **每条管线一对 `.vsh/.fsh`**：`terrain`、`block`、`entity`、`item`、`particle`、`clouds`、`gui`、`position_color`、`glint`、`panorama`、`animate_sprite`、`blit_*`、`integrate_depth`、`lightmap`、`oit_*` |
| `include/` | **18** | 共享片段：`fog.glsl`、`light.glsl`、`matrix.glsl`、`projection.glsl`、`globals.glsl`、`terrainglobals.glsl`、`chunksection.glsl`、`texture_sampling.glsl`、`oit_*.glsl` |
| `post/` | 10 | 后处理效果链（含 JSON 声明）|

两个结构性事实值得注意：

1. **文件即管线**：命名与渲染管线一一对应，`include/` 只放被多管线复用的代码（雾、光照、矩阵、投影）。
2. **原版 26.3 自带 OIT 着色器**（`oit_*`、`oit_composite`、`oit_depth_bounds_cull`）⇒ 半透明不只有"距离排序"一种做法。
   我们的 `pipeline.md` §7.1 已裁决"先按距离排序"，与此**不冲突**，但应记录"原版有 OIT 路径"这一事实，供后续复查。

## 2. 我们当前的状态（实测）

| 位置 | WGSL 数量 | 问题 |
| --- | --- | --- |
| `OffscreenRenderer.cs`（523 行）| 2 套（地形 + 天空）| 内联字符串；`Camera`/`Fog` 结构体在**每个**着色器里重复声明 |
| `UiRenderer.cs`（273 行）| 2 套以上（面板 + 文字）| 内联字符串，风格与上面不同 |
| `WindowedRenderer.cs`（1026 行）| 0（复用离屏的地形着色器）| **共享了着色器却不共享绑定契约** ⇒ 本轮之前已经因此把窗口路径改坏过一次 |

**结论：不够好。** 具体三条：

1. **没有"文件即管线"**：着色器是散落在三个 C# 文件里的字符串，无法一眼看出一共有几条管线；
2. **没有 include 机制**：`struct Camera`、`Fog` 在两处重复，改一处忘一处就会错位（已发生过）；
3. **没有绑定契约的单一来源**：binding 0/1/2/3 的语义只存在于各处的 `CreateBindGroupLayout` 里，与 WGSL 声明靠人工对齐——这正是"窗口路径缺 binding 3"事故的根因。

## 3. 改造建议（进入计划）

| 步 | 内容 |
| --- | --- |
| G1 | 建 `Framework/Rendering/Shaders/`：`terrain.wgsl`、`sky.wgsl`、`ui.wgsl`、`ui_text.wgsl` 各一文件（嵌入资源）|
| G2 | 加 `include` 组合器：源文件里写 `//#include "camera.wgsl"`、`//#include "fog.wgsl"`，编译期拼接（WGSL 无预处理器）|
| G3 | **绑定契约单一来源**：一个 `Bindings` 常量表（binding 0=图集、1=采样器、2=相机、3=雾），布局与 WGSL 都从它生成/校验 |
| G4 | 启动期**自检**：解析每个 WGSL 的 `@binding(n)`，与布局声明比对，不一致即失败并指出管线名 |
| G5 | 管线条目表：`PipelineDescriptor { name, shader, vertexEntry, fragmentEntry, depth, blend }`，与 `core/` 一一对应 |

G4 是防再犯的关键：把"靠人眼对齐"变成"启动即报错"。

## 4. 与本轮目标的关系

接下来还要新增 3–4 个着色器（运动矢量、深度金字塔 compute、半透明、方块实体）。**在 G1–G5 之前继续加，就是继续复制那三类缺陷。**
因此建议：**先做着色器组织改造（G1–G5）**，再继续 O2 与其余阶段。
