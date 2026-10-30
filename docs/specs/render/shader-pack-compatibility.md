<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 着色器包兼容指导（移植手册）
summary: 我们能吃什么、不能吃什么，以及不支持特性如何改写；面向包作者
status: 草稿
-->

# 着色器包兼容指导

本文件面向**包作者**：说明本客户端的着色器包兼容范围，以及不受支持的部分如何改写。
契约与失败规则见 `docs/specs/render/shader-packs.md`；包的结构见 `docs/analysis/render/shader-pack-anatomy.md`。

## 1. 支持范围（P3 首批）

| 支持 | 说明 |
| --- | --- |
| `shaders/` 布局与 `worldN/` 维度目录 | 按"维度 × 阶段"解析 |
| 主干阶段 | `gbuffers_terrain`/`_textured`/`_basic`/`_block`/`_entities`/`_hand`/`_water`/`_skybasic`、`shadow`、`deferred1`、`composite*`、`final` |
| `shaders.properties` 的 `#define` 与 `profile.*` | 读取并按名注入 |
| 可命名渲染目标与采样 | 按 `shader-packs.md` §1/§4 的名单 |
| 失败回退 | 任何编译失败退回内置管线，**不崩溃** |

## 2. 不支持范围与改写建议

| 特性 | 现状 | 建议改写 |
| --- | --- | --- |
| 计算着色器 `*.csh`（如 `shadowcomp`）| 不支持 | 改为全屏片元阶段实现同类效果（多 pass 迭代），或关闭该特性开关 |
| Distant Horizons `dh_*` | 不支持（无 DH 模组）| 删除或改为由 `gbuffers_terrain` 覆盖远景；包内保留文件无妨，会被跳过 |
| Voxy `voxy_*` | 不支持 | 同上 |
| `clrwl_*`（colorwheel 扩展阶段）| 不支持 | 移除，或在原版等价阶段内实现 |
| 依赖型包（`pack.json` + `.placebo` 等前置库）| 仅识别并报错 | 提供**不含前置库依赖**的独立版本 |
| 几何/细分着色器、`textureQueryLod` 等 GLSL 专有接口 | 不支持 | 用片元阶段近似；明细见 §3 |
| 多遍反馈（同一目标既读又写）| 受限 | 拆成两个目标并显式 ping-pong |

## 3. GLSL → WGSL 常见改写

| GLSL | WGSL 写法 |
| --- | --- |
| `uniform sampler2D tex` + 采样 | `@group(0) @binding(n) var tex: texture_2d<f32>;` + `textureSample` |
| `layout(location=N) out vec4` | 片元入口的返回结构体 |
| `gl_FragCoord` | 入口参数 `@builtin(position)` |
| `#define` 开关 | `const` / `override`，或按 profile 生成管线变体 |
| `discard` | `discard`（同名） |

## 3.5 加载前先看特性声明（最重要的一条）

包可以在 `shaders.properties` 里用 `iris.features.required` / `iris.features.optional` 声明依赖的特性
（`COMPUTE_SHADERS`、`SSBO`、`CUSTOM_IMAGES`、`REVERSED_CULLING` 等）。本客户端 MUST：

1. 先解析该列表；
2. 若有 `required` 项不支持，**拒绝加载并逐条列出缺失特性**，不做半程编译；
3. `optional` 项不支持时继续加载，并记录一行"已忽略的特性"。

这比让包编译到一半失败更能帮助作者。

## 4. 失败与诊断

- 包加载失败、单阶段编译失败、采样名未知：**逐条报错并指出阶段名与文件名**；
- 任一致命错误：整体退回内置管线，客户端继续可用（原则 11）；
- 未识别阶段（DH/Voxy/clrwl）：**跳过并汇总一行日志**，不视为错误。
