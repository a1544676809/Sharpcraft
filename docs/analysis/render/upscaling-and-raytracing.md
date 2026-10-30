<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 硬件光追与时域放缩在 WGPU 上的可行性
summary: 逐项核实 DLSS / XeSS / FSR / 硬件光追的可用性、依据，以及无论选谁都必须先具备的管线输入
status: 草稿
-->

# 硬件光追与时域放缩在 WGPU 上的可行性（2026-10-30 核实）

## 1. 结论一览

| 能力 | WGPU 可行？ | 依据 |
| --- | --- | --- |
| 硬件光追 | **部分**：原生后端有实验性 ray query + 加速结构；WebGPU 规范尚无 | [wgpu 光追跟踪 issue](https://github.com/gfx-rs/wgpu/issues/6762)、[gpuweb #535](https://github.com/gpuweb/gpuweb/issues/535)；Bevy 0.17 已用 wgpu 做实时光追（Solari）|
| DLSS | **不可行** | 专有 NGX SDK，需要直接持有 D3D12/Vulkan 设备与交换链、驱动侧 hook 并提供运动矢量；wgpu 不暴露设备句柄，也无 vendor interop |
| XeSS | **XMX 路径不可行**（同 DLSS 类）；**DP4a/着色器路径理论可行**（compute），但 SDK 为专有二进制，需自行重写 | XeSS 的双路径设计 |
| FSR | **最可行**：FSR1 纯空间、FSR2 用运动矢量 + 深度，均为 compute；AMD 以 MIT 提供 HLSL/GLSL | [FSR 硬件覆盖最广的说明](https://www.strayspark.studio/blog/dlss-fsr-xess-super-resolution-ue57-guide) |
| FSR3 帧生成 | **不可行** | 需要 present 时序、异步计算与驱动侧插帧，WebGPU 的呈现模型不提供 |

## 2. 关键前提：所有时域放缩器都要同一组输入

无论最终接 FSR、DLSS 还是 XeSS，都需要渲染管线先提供：

1. **jittered projection**（每帧亚像素抖动，投影矩阵随之变化）；
2. **逐像素运动矢量**（物体/相机的屏幕空间位移）；
3. **深度**（当前帧，可选历史帧）；
4. **reactive / transparency mask**（几何与非几何分界，避免鬼影）；
5. **曝光与色调映射的分离时机**（在 tonemap 之前放缩）。

⇒ 这属于**原则 11 的接入点**：`docs/specs/render/pipeline.md` §3 的 pass 表必须把"运动矢量输出""jitter""深度可采样"列为管线能力，
否则将来接任何放缩器都要返工。**这比选哪家更重要**。

## 3. 与跨平台原则的冲突

原则 6 要求 Windows / macOS / Linux 原生一致。DLSS 绑定 NVIDIA、XeSS-XMX 绑定 Intel/DX12-Vulkan 扩展，
与"跨平台一致"直接冲突。建议策略：

- **内置**一套自有的时域放缩（FSR 风格，MIT 兼容，可自己用 WGSL 实现）作为默认路径；
- **外部原生插件**（DLSS/XeSS）作为可选加速，接口在 M6 之后另议，不进默认路径；
- 硬件光追只在原生后端以**实验特性开关**形式做，默认关闭（API 不稳定，且 Metal 不支持）。

## 4. 待核实

- 我们使用的 `Brew.Wgpu` 绑定版本是否已暴露 ray query / 加速结构的 feature；未暴露则近期不排期。
- 自研时域放缩的质量门槛（鬼影、闪烁）需与"逐像素对照"手段一起验收（见 `docs/specs/quality-attributes.md`）。

## 5. WGPU 是否等于告别现代图形特性（判断）

**不是。丢的是"厂商锁定类"特性，其余绝大多数现代观感都由 compute 算法实现，而 compute 是可用的。**

| 现代特性 | WGPU 可用性 | 替代做法 |
| --- | --- | --- |
| 计算着色器 | **可用** | 体积光、SSAO、SSR、云层 raymarch、TAA/锐化、GPU 剔除、cluster 光照全走它 |
| 间接绘制 / GPU 驱动管线 | 可用（indirect draw + storage buffer）| 现代渲染器的主流做法，正是 WebGPU 的强项 |
| 存储缓冲 / 存储纹理 | 可用（受 limits）| 光照列表、粒子、自定义图像 |
| 多渲染目标 | 可用（4–8 目标）| gbuffer 延迟管线 |
| 阴影（PCF/PCSS）、软阴影 | 可用 | 阴影图 + compute 滤波 |
| 软件光追 / SSGI / DDGI | 可用（compute 求交）| 观感接近而性能可调；不需要硬件 RT |
| **硬件光追管线（BLAS/TLAS）** | 仅实验性 ray query，原生后端 | 默认不做；需要时开实验开关 |
| **几何/细分着色器** | **不可用**（规范无此阶段）| 位移移到 vsh / compute 预展开 |
| **Mesh Shader** | 不可用 | compute 做 cluster 剔除 + indirect draw（业界同法）|
| **DLSS / XeSS-XMX** | 不可用（厂商 SDK + 设备互操作）| 自有 FSR 风格时域放缩 |
| 可变速率着色、采样器反馈、稀疏资源 | 不可用/受限 | 无等价需求时不必关心 |

### 关键判断

1. **高端包的实际需求是被测出来的**：Complementary 三个样本 `.gsh`/`.tes` 计数为 0，观感主要来自**阴影图 + SSAO + 体积光 + SSR + 时域抗锯齿**——全部可在 compute/fragment 内实现。缺的是 compute，不是光追。
2. **跨平台一致性反过来支持这个选择**：Metal 本来就没有几何/细分阶段；DX12/Vulkan 有但我们三平台一致才是原则 6 的要求。
3. **真正的纪律要求**是：管线按 **GPU 驱动 + 时域可放缩** 设计（indirect draw、运动矢量、jitter、深度可采样），而不是按"哪家 API 特性最全"设计。
4. **留后门**：原则 6 已要求所有 GPU 调用收敛在一个抽象层，因此将来若出现必须在某平台用原生特性的场景，可以**新增一个原生后端实现**而不动游戏逻辑——这是接口隔离带来的期权，不是现在的负担。
