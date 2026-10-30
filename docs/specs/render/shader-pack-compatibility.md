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

## 2. 兼容边界：三种不同原因，不可混为一谈

### 2.1 硬性不可支持（引擎形态决定）

| 项 | 原因 | 依据 |
| --- | --- | --- |
| `.gsh` 几何着色器 | **WebGPU 只有 vertex / fragment / compute 三阶段**，无几何阶段 | [Unity WebGPU 限制](https://discussions.unity.com/t/early-access-to-the-new-webgpu-backend/933493?page=10)、[gpuweb #1239](https://github.com/gpuweb/gpuweb/issues/1239) |
| `.tcs` / `.tes` 细分着色器 | WebGPU 无细分阶段，社区方案仍是"CPU 预展开 + compute" | [gpuweb #445](https://github.com/gpuweb/gpuweb/issues/445) |

影响与补救：这两类阶段提供的是**位移细分细节**（水面/地形波浪）与**每三角形效果**（点/线扩面、billboard、法线重算）。
缺少它们不会让整包不可用，但相关效果消失。补救路径：①位移移到 vsh（多数包本就这么做）②需要更多顶点时用 compute 预展开到 storage buffer ③扩面类效果用实例化或 compute 预生成。

实测（本机样本，`ref://` 见分析文档）：**Complementary Unbound r5.9.3 / Shaders v4.7.2 / Reimagined r5.9.3 三个包的 `.gsh`、`.tcs/.tes` 计数均为 0** ⇒ 对该族影响为零。

### 2.2 可支持但首批未实现（范围决策，不是能力缺口）

| 项 | WebGPU 具备 | 未做的原因 |
| --- | --- | --- |
| `.csh` 计算着色器 | **有** compute 阶段 | Iris 语义复杂：单 pass 最多 27 个计算、按字母序、`allowConcurrentCompute`、`imageStore` 写 colortex、`workGroups`/`indirect.pass`；且需核对设备 limits。实测 Unbound/Reimagined 各含 **3 个 `.csh`** ⇒ 这是本族**真正的**缺口 |
| SSBO | 有 storage buffer（受 `max_storage_buffer_binding_size`）| 同上，随 compute 一起排期 |
| 自定义 `image.*` | 有 storage texture | 同上 |

### 2.3 依赖我们尚不存在的模组（与渲染能力无关）

`dh_*`（Distant Horizons）、`voxy_*`（Voxy）、`clrwl_*`（Flywheel/Create）、Photonics：阶段只在对应模组存在时才有输入。
我们客户端没有这些模组，故阶段不可达；包内文件存在即可**跳过并汇总一行日志**，不是错误。

### 2.4 其余改写建议

| 特性 | 建议 |
| --- | --- |
| 多遍反馈（同一目标既读又写）| 拆成两个目标显式 ping-pong |
| 依赖型包（`pack.json` + `.placebo`）| 提供不含前置库的独立版本 |
| GLSL 专有接口（`textureQueryLod` 等）| 用片元阶段近似 |

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
