<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 渲染后端接口（后端中立契约）
summary: 能力协商、阶段模型、禁止泄漏规则；WGPU 为唯一实现，Vulkan 仅预留
status: 草稿
-->

# 渲染后端接口（后端中立契约）

依据：ADR-0018（2026-10-30 所有者裁决：**预先预留 Vulkan 接口，不实现具体功能；当前仍以 WGPU 为唯一实现**）。

## 1. 分层与禁止泄漏

| 层 | 允许 | 禁止 |
| --- | --- | --- |
| `Client/Render`、`Client/UI` | 产出中性几何与绘制命令 | 出现任何后端类型（`Device`/`Texture`/`Pipeline`）|
| `Framework/Rendering` | 持有后端句柄、管线、缓冲区 | 认识方块/实体/界面类型（既有架构门禁）|
| `Framework/Rendering/Backends`（未来） | 每个后端一份实现 | 后端类型外泄到上层 |

未来的 Vulkan 后端 MUST 只实现同一契约；**上层代码不得出现 `if (backend == Vulkan)`**，一切能力差异走 §2 的能力查询。

## 2. 能力协商（当前唯一落地的部分）

`BackendCapabilities`（`Framework/Rendering/BackendCapabilities.cs`）给出：

- `Name`：日志用后端名（当前 `wgpu`）；
- `Capabilities`：`GraphicsCapability` 位标志 —— `Compute`、`PushConstants` 现为真；`GeometryShader`、`TessellationShader`、`MeshShader`、`RayTracingPipeline`、`VariableRateShading`、`Bindless`、`SparseResources`、`MultipleQueues` 现为假；
- `MaxColorAttachments`（8）、`MaxStorageBufferBindingSize`（128 MiB）；
- `Supports(requested)` / `Missing(requested)`：后者返回缺失项名字，供**可读拒绝**使用。

用法：着色器包加载器把 `iris.features.required` 映射成 `GraphicsCapability`，调用 `Missing`；非空则拒绝加载并逐条列出缺失能力（见 `shader-pack-compatibility.md` §3.5）。

## 3. 阶段模型（为 Vulkan 预留的关键形状）

程序阶段 MUST 由**声明式列表**描述，而不是在管线里写死：

| 概念 | 现在的实现 | 未来 Vulkan |
| --- | --- | --- |
| 阶段集合 | vertex / fragment | 增加 geometry / tessellation / mesh |
| 需求声明 | 包的 `iris.features.required` → `GraphicsCapability` | 同一映射，无需改包 |
| 编译 | WGSL（naga）| WGSL→SPIR-V（naga），**着色器源码不重写** |

⇒ 包若声明 `TESSELLATION_SHADERS`：WGPU 下拒绝并说明；将来 Vulkan 后端下自动可用。**上层逻辑零改动。**

## 4. 时域放缩与厂商钩子（预留位置）

- 定义可选服务 `Iupscaler`（名称待定）：输入 jitter/运动矢量/深度/曝光，输出放缩后图像；**默认实现为空**（直接输出）。
- DLSS/XeSS 这类需要设备互操作的，只能由**未来原生后端**提供该服务；WGPU 下永远为空，不得在默认路径依赖它。
- 因此管线现在就要产出 jitter、运动矢量、可采样深度（`docs/specs/render/pipeline.md` §3 补充项）。

## 5. 明确不做

- 不实现 Vulkan 后端（无时间表，见 ADR-0018 §4 的证据门槛）；
- 不引入后端抽象层中的"最小公分母"约束：WGPU 独有的能力（如 compute 队列用法）不因未来 Vulkan 而被削弱；
- 不因为预留接口而提前写空实现类——**只有契约与能力表**，避免无人使用的死代码。
