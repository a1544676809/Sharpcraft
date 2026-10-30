<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# 受控偏差清单

> 原则 9：任何改变**可观测行为**的改动都必须在此登记并评审。默认行为必须与原版一致；偏差必须可关闭、可回退。

| 编号 | 偏差 | 默认状态 | 开关 | 影响面 | 依据 |
| --- | --- | --- | --- | --- | --- |
| D-001 | 已加载的**目录资源包**文件变化后实时重载（原版只有整体重载）| **关闭** | `MC_RESOURCE_PACK_WATCH=1` / `--watch-resource-packs` | 仅开启时；关闭时零偏差 | ADR-0016 |

## D-002 几何与细分着色器不可用

- **偏差**：包内 `.gsh` 几何着色器与 `.tcs/.tes` 细分着色器不执行，相关效果（位移细分、每三角形扩面）不可得。
- **原因**：渲染后端为 WGPU/WebGPU，规范只有 vertex / fragment / compute 三阶段，无几何与细分阶段；非实现取舍。
- **影响面**：实测 Complementary Unbound r5.9.3、Shaders v4.7.2、Reimagined r5.9.3 三个样本中该两类文件计数均为 0，族内无影响；其他包含此类阶段的包，效果缺失但可加载（除非其 `iris.features.required` 声明 `TESSELLATION_SHADERS`，则拒绝加载并报明原因）。
- **复查条件**：若 WebGPU 引入细分/几何阶段，或出现大量依赖该能力的目标包。
