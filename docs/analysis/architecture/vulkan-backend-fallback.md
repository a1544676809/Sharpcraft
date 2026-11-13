<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 原生 Vulkan 后端作为"退路"的可行性推理
summary: 评估为摆脱 WebGPU 采样器约束而改用原生 Vulkan 的收益、代价与替代方案；结论是当前不切换，先走 WebGPU 内的分带方案
status: 草稿
updated: 2026-11-10
-->

# 原生 Vulkan 后端作为"退路"的推理

## 1. 触发点

我们为贴近原版地形采样器（`LevelRenderer:430-436`：`LINEAR/LINEAR` + anisotropy）实测受阻：

- wgpu 校验拒绝 `magFilter=Nearest` 与 `maxAnisotropy>1` 的组合，报错原文 `all filter modes must be linear`
- 这是 **WebGPU 规范**要求（[MDN](https://developer.mozilla.org/en-US/docs/Web/API/GPUDevice/createSampler)），不是 wgpu 的实现缺陷
- 原版走 OpenGL，无此限制，因此原版能同时要"最近邻放大"与"各向异性"
- 独立实现 [Horizon](https://github.com/horizon-rust/horizon)（MIT，同基线）也放弃各向异性，理由与我们实测一致

于是产生一个自然的问题：**是否改用原生 Vulkan 后端，以摆脱 WebGPU 规范约束？**

## 2. 原生 Vulkan 能解决什么

| 能力 | WebGPU | Vulkan 原生 |
| --- | --- | --- |
| 各向异性 + 最近邻放大 | ❌ 规范禁止 | ✅ 合法 |
| 采样器状态自由度 | 固定枚举 | 任意组合 |
| 描述符索引 / bindless | 受限 | 原生支持 |
| 显式同步与内存控制 | 抽象掉 | 完全可控 |
| 潜在性能上限 | 受抽象层限制 | 更高 |

## 3. 代价（按项目宪章逐条评估）

| 维度 | 说明 | 宪章关联 |
| --- | --- | --- |
| **违反硬性约束** | 原则 6 MUST 通过 WGPU 做图形后端。改用原生 Vulkan **需要所有者修订原则 6 并留 ADR**，代理不得自行决定 | 原则 6、原则 12 |
| **跨平台倒退** | Vulkan 覆盖 Windows / Linux / Android，但 **macOS 无 Vulkan**，仍需 MoltenVK 或独立 Metal 后端 —— 跨平台能力反而从"一套代码"变成"两套" | 原则 6 |
| **实现面巨大** | instance/device、swapchain、内存分配器、描述符集、管线布局、信号量/栅栏、命令缓冲、资源状态与屏障、验证层接入 —— 全部自建，且每项都要有测试 | 原则 12 |
| **与现有单轨冲突** | 原则 14 要求窗口与离屏共用同一 Pass 执行器，但**当前实际是两条平行管线**：`OffscreenRenderer` 不引用任何 `*PassRecorder`（2026-11-13 核实）。换后端要么再抽一层（等于自己写个 wgpu），要么维护**三**套管线 | 原则 14 |
| **模组与着色器包契约** | 原则 11 要求"着色器包 ↔ 引擎管线"稳定契约层；换后端会重写该契约 | 原则 11 |
| **人力与风险** | 相当于重启渲染层；当前渲染器已有大量已验证行为（遮挡剔除、间接绘制、OIT 等） | 原则 9 |

## 4. 替代方案（在 WebGPU 内解决，按代价排序）

| 方案 | 做法 | 能否解决各向异性 | 代价 |
| --- | --- | --- | --- |
| **A. 按距离分带双采样器** | 近处区块用 Nearest（无各向异性），远处区块用 Linear + anisotropy。两个采样器各自满足规范 | ✅ 远处部分解决 | 需按距离分带拆 draw / bind group，**是唯一能拿到各向异性的 WebGPU 方案** |
| **B. 精灵超采样** | 图集内 2× 最近邻放大精灵，再用 Linear 放大 | ❌ 不解决 | 图集面积 ×4；近处观感改善 |
| **C. 着色器内自定义过滤** | 手动沿各向异性轴多次采样并加权 | ✅ 完整解决 | 实现复杂、性能未知、与着色器包契约冲突 |
| **D. 保持现状 + TAA/LOD** | 与 Horizon 同路线：mipmap 级数、LOD 远景、TAA/FXAA 改善远处 | ❌ 不解决 | 零额外架构风险 |

## 5. 结论与建议

**当前不切换原生 Vulkan。** 理由：

1. 它违反原则 6 的 MUST，属于需要所有者裁决的架构变更，不能作为"技术退路"被悄悄采用
2. 唯一收益（各向异性 + 最近邻）在 WebGPU 内**存在可行替代**：方案 A 的分带双采样器
3. 跨平台能力会实际倒退（macOS 仍需第二后端）
4. 成熟同类项目（Horizon）在同样约束下选择不实现各向异性，说明这不是"必须付出巨大代价"的缺陷

**推荐的推进顺序**：

1. 先做方案 **D**（已完成大部分：mip 级数与 padding 已按原版推导）
2. 若远处观感仍不足，做方案 **A**（分带双采样器），并记录为受控偏差的替代实现
3. 只有在**方案 A 也无法满足**、且所有者明确修订原则 6 时，才评估原生 Vulkan / Metal 后端，并按原则 12 落 ADR

## 6. 复查条件

- 方案 A 实测后仍存在明显远处画质差距
- 或出现必须依赖 bindless / 描述符索引才能达成的性能目标
- 或所有者要求跨平台目标变更（例如放弃 macOS）

满足任一条时，本文件升级为 ADR 草案并提交裁决。

## 7. 抽象层审计（2026-11-10）

原则 6 要求"所有 WGPU 调用收敛在一个渲染抽象层内"，其目的就是**可替换后端**。审计当前状态：

| 层 | 直接暴露 WGPU 的位置 | 评价 |
| --- | --- | --- |
| `Sharpcraft.Client`（游戏逻辑） | 3 行：`WindowCommand.cs:309-311` 把 `MC_PRESENT_MODE` 映射到 `WGPUPresentMode` | 几乎干净，包一层枚举即可归零 |
| `Sharpcraft.Framework/Rendering`（抽象层） | 54 个 public 成员带 WGPU 类型：`Device` 14、`BindGroup` 13、`WGPUTextureFormat` 12、`RenderPipeline` 11、`Sampler` 8、`TextureView` 8、`Buffer` 6、`PipelineLayout` 3、`Texture` 3、`Instance` 2 | **这是缺口所在** |
| 后端中立接口 | **0 个** | 没有可替换的接缝 |

### 7.1 结论修正

此前把"改用 Vulkan"判为"违反原则 6"，表述不准确。准确表述是：

- **原则 6 不禁止换后端**，它禁止的是把后端调用散布到业务代码里
- 当前 `Framework/Rendering` 是 **WGPU 门面**，不是后端契约；所以"随时切换后端"的能力**尚未真正具备**
- 换后端的前置条件是先补出这条接缝，而不是先写 Vulkan

### 7.2 最小接缝设计（建议，未实施）

| 步骤 | 内容 | 验收 |
| --- | --- | --- |
| S1 | 定义 `IRenderBackend`：创建设备、创建纹理/缓冲/采样器/管线（句柄用不透明类型）、提交命令、呈现 | 接口内**零** WGPU 类型 |
| S2 | 自有枚举替换 `WGPUTextureFormat` 等（`TextureFormat`、`PresentMode`、`AddressMode`…，后者已具备） | 抽象层 public 面 WGPU 类型计数下降 |
| S3 | `WgpuBackend : IRenderBackend` 承接现有实现 | 行为零变化，测试全绿 |
| S4 | 架构测试：`Framework/Rendering` 之外的程序集不得出现 `Brew.Wgpu` | 自动门禁 |

完成 S1–S4 后，"退路"才真正可用：新增 Vulkan 后端只需实现 `IRenderBackend`，调用方不动。
