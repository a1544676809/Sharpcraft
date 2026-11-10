<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 渲染后端接缝契约（S1 规格）
summary: 定义 IRenderBackend 与中立句柄/枚举的形状与语义，作为替换 WGPU 后端的前置契约
status: 已评审
updated: 2026-11-10
-->

# 渲染后端接缝契约

> 背景：`docs/analysis/architecture/vulkan-backend-fallback.md` §7。原则 6 要求 WGPU 调用收敛在一个抽象层内，以便替换后端；审计发现该层当前没有中立接口。本文件定义这条接缝。

## 1. 设计原则

1. **接口内零 WGPU 类型**：`IRenderBackend` 及其签名只出现本仓类型与 BCL 类型。
2. **句柄不透明**：纹理、缓冲、采样器、管线等一律用 `readonly struct` 句柄，内部仅持有 `object`/`nint`，调用方无法依赖后端细节。
3. **后端负责生命周期**：句柄由后端创建与释放，接口提供 `Destroy*`；不暴露 `IDisposable` 给调用方持有裸资源。
4. **渐进迁移**：接口先覆盖"设备 + 纹理 + 采样器 + 管线 + 提交/呈现"五类，其余（bind group、pipeline layout、compute）随后。
5. **行为零变化**：S3 完成后像素输出必须与迁移前逐像素一致。

## 2. 中立类型（先落地，供 S2 使用）

| 本仓类型 | 替换的 WGPU 类型 | 取值 |
| --- | --- | --- |
| `TextureFormat` | `WGPUTextureFormat` | `Rgba8Unorm`、`Rgba8UnormSrgb`、`Bgra8Unorm`、`Bgra8UnormSrgb`、`Depth32Float`、`R32Float` |
| `PresentMode` | `WGPUPresentMode` | `Fifo`、`Mailbox`、`Immediate` |
| `TextureUsage` | `TextureUsage` 位标志 | 与现有语义一致（`RenderAttachment`、`TextureBinding`、`CopySrc`、`CopyDst`） |
| `AddressMode` / `FilterMode` | 已中立化（T-784） | 见 `SamplerCache` |

## 3. 句柄（不透明）

```csharp
public readonly struct TextureHandle    { internal readonly object Value; }
public readonly struct TextureViewHandle{ internal readonly object Value; }
public readonly struct BufferHandle     { internal readonly object Value; }
public readonly struct SamplerHandle    { internal readonly object Value; }
public readonly struct PipelineHandle   { internal readonly object Value; }
```

调用方只能传递句柄，不能解引用；后端实现内部做 `(Brew.Wgpu.Texture)handle.Value`。

## 4. `IRenderBackend`（最小面）

| 成员 | 签名要点 | 说明 |
| --- | --- | --- |
| `Name` | `string` | 后端标识（`"wgpu"`、`"vulkan"`），用于日志与诊断 |
| `CreateTexture` | `(TextureDesc) -> TextureHandle` | 描述含尺寸、格式、用途、mip 层数 |
| `WriteTexture` | `(TextureHandle, ReadOnlySpan<byte>, uint bytesPerRow, uint width, uint height, uint mipLevel)` | 上传像素；**extent 由调用方声明**，不从行跨度反推（4 字节/像素只对 8 位 RGBA 成立） |
| `CreateTextureView` | `(TextureHandle) -> TextureViewHandle` | |
| `CreateBuffer` | `(BufferDesc) -> BufferHandle` | |
| `WriteBuffer` | `(BufferHandle, ReadOnlySpan<byte>, ulong offset)` | |
| `CreateSampler` | `(AddressMode, FilterMode, FilterMode, bool mipmaps) -> SamplerHandle` | 与 `SamplerCache` 的语义对齐；单值 address mode（两轴同值） |
| `CreateRenderPipeline` | `(PipelineDesc) -> PipelineHandle` | 描述含着色器源码、顶点布局、格式、深度设置 |
| `CreateSurface` / `Present` | 由窗口层调用 | 窗口与离屏共用同一提交路径（原则 14） |
| `Destroy*` | 每类句柄一个 | 后端拥有生命周期 |

## 5. 迁移顺序与归属

| 切片 | 内容 | 执行者 | 验收 |
| --- | --- | --- | --- |
| S1 | 本契约 + 中立类型 + 句柄类型（不接线） | Lead | 类型编译通过、无 WGPU 类型出现在契约签名中 |
| S2 | 把 `WGPUTextureFormat`/`WGPUPresentMode` 等替换为中立类型，并在边界做转换 | 可外包（机械） | 抽象层 public 面的 WGPU 类型计数下降；行为不变 |
| S3 | `WgpuBackend : IRenderBackend`，先接 2 条管线做样板，再照搬其余 | Lead 做样板，其余可外包 | 像素输出逐像素一致 |
| S4 | 架构门禁：`Framework/Rendering` 之外不得出现 `Brew.Wgpu` | 可外包（机械） | 自动测试 |

## 6. 不做的事

- 不为了"抽象"而复制 WGPU 的整套模型（描述符、绑定组布局等）——接口只覆盖当前真正用到的能力
- 不引入第二个后端实现；本切片只建立接缝
- 不改变现有 Pass 划分与帧流程（原则 14 单轨）

## 7. 接线连锁评估（2026-11-10）

S2 完成后签名层已中立化，但**接线**会立刻撞上接口尚未覆盖的依赖链。实测计数（`src/Sharpcraft.Framework/Rendering` public 签名中的 WGPU 类型，S2 后）：

| 类型 | 剩余出现 | 是否已被 `IRenderBackend` 覆盖 |
| --- | --- | --- |
| `Device` | 17 | 部分（`WgpuRenderBackend.Device` 逃生口） |
| `RenderPipeline` | 12 | ❌ 接口无 `CreateRenderPipeline` |
| `BindGroup` | 12 | ❌ |
| `RenderPassEncoder` | 11 | ❌ 录制阶段完全未抽象 |
| `Sampler` | 9 | ✅ `CreateSampler` |
| `TextureView` | 8 | ✅ `CreateTextureView` |
| `Buffer` | 7 | ❌ 接口定义了 `BufferHandle` 但无创建方法 |
| `VertexAttribute` | 7 | ❌ 顶点布局 |
| `CommandEncoder` | 4 | ❌ |
| `Texture` | 4 | ✅ `CreateTexture` |

### 7.1 结论：不能一刀切接线

渲染资源是**成链**的：纹理 → 视图 → bind group（需 layout）→ pipeline（需 layout）→ encoder 录制 → pass。只接前两级时，下游仍全部依赖原生 `Device`，等于在同一个类里混用两套路径——违反原则 14 的单轨要求。

### 7.2 推荐顺序（每一刀都可独立回滚、行为零变化）

| 刀 | 内容 | 连锁范围 |
| --- | --- | --- |
| **W1** | 纹理 / 视图 / 采样器接线（`EntitySkinTextureGpu` 等） | 仅本级；bind group 走 `WgpuRenderBackend.Device` 逃生口 |
| W2 | 补 `BufferHandle` 创建/写入，接线 buffer | 本级 |
| W3 | 补 `CreateBindGroupLayout` / `CreateBindGroup` | 需先定中立描述符形状（绑定槽位 + 类型） |
| W4 | 补 `CreateRenderPipeline` | 需中立 `PipelineDesc`（着色器、顶点布局、目标格式、深度状态） |
| W5 | 补 `CommandEncoder` / `RenderPass` 录制 | 最大一刀，届时才是真正"可换后端" |

**停止条件**：W3 之前不要动 pipeline 相关代码——否则会同时改描述符形状与调用点，无法逐像素验证。

### 7.3 逃生口的纪律

`WgpuRenderBackend.Device` 是**临时**逃生口，用于尚未迁移的下游（bind group 等）。规则：

- 只允许 `WgpuRenderBackend` 与尚未迁移的渲染资源使用
- 每完成一刀，其逃生口使用点必须归零
- W5 完成后该属性删除，并由门禁测试断言其不存在
