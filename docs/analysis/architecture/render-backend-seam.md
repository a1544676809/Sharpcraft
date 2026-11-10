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

## 8. W5 设计：pass 录制（G8）

这是接缝里最大的一刀，也是最后一块原生残留。设计要点与风险如下。

### 8.1 为什么它比前几刀难

前四刀迁移的是**资源**（有创建/销毁，可 `Adopt*` 桥接）。录制阶段迁移的是**状态机**：

| 差异 | 资源 | 录制 |
| --- | --- | --- |
| 生命周期 | 显式创建与销毁 | 借用：`BeginRenderPass` 后必须在同一次提交内 `End` |
| 错误后果 | 句柄无效 ⇒ 单次绘制错 | pass 未结束 / 结束两次 ⇒ 整帧丢失或校验失败 |
| 状态耦合 | 无 | `SetPipeline` 决定后续 `SetVertexBuffer` 的槽位合法性 |

所以 W5 不能只做类型替换，必须同时把**使用顺序约束**表达出来，否则把"运行期校验"换成"编译期自由"，反而更难诊断。

### 8.2 中立类型

```csharp
public enum LoadOp  { Load, Clear }
public enum StoreOp { Store, Discard }
public enum IndexFormat { Uint16, Uint32 }

public readonly record struct ColorAttachmentDesc(
    TextureViewHandle View, LoadOp Load, StoreOp Store, Vector4 ClearColour);

public readonly record struct DepthStencilAttachmentDesc(
    TextureViewHandle View,
    LoadOp DepthLoad, StoreOp DepthStore, float ClearDepth,
    LoadOp StencilLoad, StoreOp StencilStore, uint ClearStencil);
```

`RenderPassHandle` 与其它句柄不同：**没有 `Destroy`**，只有 `EndRenderPass`；未结束就提交属于调用方错误。

**实现约束（2026-11-10 修正）**：本仓的句柄是 `readonly struct`，无法在 `EndRenderPass` 里把内部引用置空，因此**做不到**"结束后重复使用自动抛异常"的 fail-fast。当前的实际保障是：

| 保障 | 手段 |
| --- | --- |
| 每条路径只结束一次 | 调用方用 `try/finally`（原生 `using` 的 RAII 由它替代） |
| 结束后不误用 | 代码评审 + 每个 pass 迁移后实机截图 |
| 未结束就提交 | wgpu 自身会校验并报错，不是静默失败 |

这比原设计弱一档，已登记为受控偏差 `SIM-PASS-NO-FAILFAST`（见 `docs/traceability.md` T-786）。

### 8.3 接口面

```csharp
CommandEncoderHandle CreateCommandEncoder();
RenderPassHandle BeginRenderPass(CommandEncoderHandle encoder, ReadOnlySpan<ColorAttachmentDesc> colours, in DepthStencilAttachmentDesc? depth);
void SetPipeline(RenderPassHandle pass, PipelineHandle pipeline);
void SetBindGroup(RenderPassHandle pass, uint index, BindGroupHandle group);
void SetVertexBuffer(RenderPassHandle pass, uint slot, BufferHandle buffer);
void SetIndexBuffer(RenderPassHandle pass, BufferHandle buffer, IndexFormat format);
void Draw(RenderPassHandle pass, uint vertexCount, uint instanceCount = 1, uint firstVertex = 0, uint firstInstance = 0);
void DrawIndexed(RenderPassHandle pass, uint indexCount, uint instanceCount = 1, uint firstIndex = 0, int baseVertex = 0, uint firstInstance = 0);
void EndRenderPass(RenderPassHandle pass);
void Submit(CommandEncoderHandle encoder);
```

### 8.4 前置依赖

W5 依赖 **W4（pipeline）**：`SetPipeline` 需要 `PipelineHandle`，而目前 pipeline 只能用原生方式创建。顺序上必须 W4 → W5，或 W5 先只覆盖"不带 pipeline 的 pass"（本仓没有这种 pass，所以实际是 W4 先行）。

### 8.5 分步落地（每步可独立验证）

| 步 | 内容 | 验收 |
| --- | --- | --- |
| W5a | `CreateCommandEncoder` / `Submit` / `EndRenderPass`：只做 encoder 生命周期 | 帧仍正常，录制代码仍用原生 pass |
| W5b | `BeginRenderPass` + 附件描述 | 一帧的颜色/深度清除行为不变（对比截图） |
| W5c | `SetPipeline`/`SetBindGroup`/`SetVertexBuffer`/`SetIndexBuffer`/`Draw*` 逐组迁移 | 每迁移一组跑一次截图 |
| W5d | 删除 `RenderBackendInterop`、`Device` 逃生口与全部 `As*`，加门禁断言其不存在 | 架构测试：`Framework/Rendering` 之外零 `Brew.Wgpu`，且层内 `As*` 数为 0 |

### 8.6 已知风险

| 风险 | 缓解 |
| --- | --- |
| pass 未结束导致整帧丢失 | `EndRenderPass` 后置空内部引用；重复使用抛异常；每步迁移后实机截图 |
| 状态顺序错误（pipeline 与 vertex layout 不匹配） | 逐组迁移（W5c），每次只改一类调用 |
| 性能回退（每次绘制多一层间接） | 迁移后用 `FrameDiagnostics` 对比帧时间，超过 5% 回退则重新评估 |
| 大量调用点（`RenderPassEncoder` 11 处 public + 更多内部） | 按文件分批，每批一个提交，可独立回滚 |

## 9. 收口时的两项阻塞（2026-11-10 记录）

接缝已覆盖全部 19 个渲染 pass 与全部渲染管线的创建。仍不在接缝内的有两类，各自原因不同：

| 项 | 位置 | 阻塞在哪 | 解除条件 |
| --- | --- | --- | --- |
| 3 处 `cloudOit` 管线布局 | `WindowedRenderer` `Run` | **不是 API 表达力问题**：`backend.CreatePipelineLayout` 已能表达，阻塞在**句柄的释放方式**。现有代码是 `using var x = cloudOit is null ? default : device.CreatePipelineLayout([...])`，而 `PipelineLayoutHandle` 是 `readonly struct`，没有 `Dispose`，无法放进 `using` | **已裁决（2026-11-10，项目所有者）**：保持显式 `Destroy`，把 `using` 改写为 `try/finally`。**不改句柄语义**——句柄仍是纯标识，不持有后端引用。实施步骤：①三处改为 `var x = cloudOit is null ? default : backend.CreatePipelineLayout([wgpu.AdoptBindGroupLayout(...)])` ②`Create*Pipeline` helper 的 `PipelineLayout` 形参改为 `PipelineLayoutHandle`（这些 helper 内部已有 `AdoptPipelineLayout`，改后直接透传）③在 `Run` 的相应作用域末尾 `finally { if (x.IsValid) backend.Destroy(x); }` |
| 2 个 compute 管线 | `OffscreenRenderer` 的深度金字塔与运动向量 | **API 表达力问题**：接缝没有 `CreateComputePipeline` / `BeginComputePass` / `DispatchWorkgroups` | 新增 compute 切片（见 §8 之后的 W6） |

此外 `CopyTextureToBuffer` / `CopyBufferToBuffer` 仍为原生，原因同上：它们不在"渲染 pass 录制"的语义内，属于独立能力。

这三类都不影响"渲染后端可替换"这一主目标：一个原生 Vulkan 后端只要实现 `IRenderBackend` 的现有方法，全部渲染路径即可迁移；只有 compute 与回读需要额外实现。
### 9.1 离屏路径的正确调用形式（验证用）

```powershell
Sharpcraft.Client.exe --version 26.3 --gameDir <dir> --pack <pack.zip> --offlineDeveloperMode `
  --frame --quickPlayMultiplayer 127.0.0.1:25565 --width 1280 --height 720
```

`--frame` 是诊断模式，地址取自 `--quickPlayMultiplayer`（不接受位置参数，也没有 `--output`：帧写到 `%TEMP%\sharpcraft-frame.png`）。stdout 出现 `frame written to <path>` 即成功。这条路径会跑 compute（深度金字塔与运动向量），是验证 compute 切片的手段。
### 9.2 尚未迁移的文件（2026-11-10 核查）

此前"全部渲染管线走接缝"的结论只覆盖了 `WindowedRenderer` 与 `OffscreenRenderer` 两个文件（那是执行代理的白名单）。按目录全量核查后，仍有以下文件直接在设备上创建管线：

| 文件 | 原生创建 | 内容 |
| --- | --- | --- |
| `UiRenderer.cs` | 3 个 render pipeline | 界面三套材质管线 |
| ~~`SkyDiscResources.cs`~~ | ~~1 个~~ **已迁（2026-11-10）** | 天体圆盘 |
| ~~`WorldTextResources.cs`~~ | ~~1 个~~ **已迁（2026-11-10）** | 世界文本 |
| ~~`FramePresentation.cs`~~ | ~~1 个~~ **已迁（2026-11-10）** | 呈现 blit |
| `OffscreenRenderer.cs`（`CelestialResources` 嵌套类） | 1 个 render pipeline | 天体颜色 |
| `ChunkCuller.cs` | 1 个 compute pipeline | 剔除 |

另有 15 处 `device.CreatePipelineLayout` 分布在这些文件中。

**教训**：迁移范围的结论必须来自**目录级全量核查**，而不是"我改过的两个文件"。这条已作为下一步的输入：把上述 6 个文件纳入范围后，才能说"所有管线都走接缝"。
### 9.3 降低逃生口的正确做法（已验证）

迁移"创建点"**不会**降低逃生口：只要资源字段仍是原生类型，调用点就要 `Adopt*`/`As*`，数字反而上升。

真正降低的做法是**把资源持有者的字段改成句柄**：

| 做法 | 逃生口 |
| --- | --- |
| 只迁创建（R13/R14） | 上升 219 → 225 |
| 字段改句柄（`SkyDiscResources`，R15） | 调用点的 `AdoptPipeline` / `AdoptPipelineLayout` / `AdoptBindGroupLayout` 消失 |

**已完成的样板**：`SkyDiscResources`（持有 `BindGroupLayoutHandle` / `PipelineLayoutHandle` / `PipelineHandle`，`Draw(backend, pass)` 直接传句柄，`Dispose` 改 `backend.Destroy`）。

**已知级联**：`WorldTextResources` 的字段相互依赖（texture → view → sampler → bind group → layout → pipeline layout → pipeline），一次全改会出现 5 处以上级联错误。**应分两步**：先把 `_pipeline` / `_bindGroup` 改句柄（Draw 侧即减 2 个 adopt），再把 buffer 与纹理链改掉。
### 9.4 compute 管线的迁移没有收益（除非先补 pass 录制）

接缝已有 `CreateComputePipeline`（`ChunkCuller` 与离屏的深度金字塔/运动向量都已走它），但**compute pass 的录制仍是原生**（`encoder.BeginComputePass` / `ComputePass.SetPipeline` / `DispatchWorkgroups`）。

后果：把 `ChunkCuller._pipeline` 从 `ComputePipeline?` 改成句柄，pass 处就必须 `backend.AsComputePipeline(...)` 才能用——**逃生口反而 +1**。

所以 compute 剩余的迁移顺序必须是：**先补 `BeginComputePass` / `SetPipeline` / `DispatchWorkgroups` / `EndComputePass` 到接缝，再改字段类型**。单独改字段没有意义。
### 9.5 compute 侧整体转换配方（`ChunkCuller` 为例）

compute 录制能力已就绪（§"compute pass 录制"），接缝也已具备 bind group 的 buffer 入口：

```csharp
BindGroupEntryDesc.Uniform(uint binding, BufferHandle buffer, ulong offset, ulong size)
```

因此 `ChunkCuller` 可以整体转换，字段对应关系：

| 原字段 | 改为 | 创建方式 |
| --- | --- | --- |
| `BindGroupLayout? _layout` | `BindGroupLayoutHandle?` | `backend.CreateBindGroupLayout([BindGroupLayoutEntryDesc(...)])` |
| `PipelineLayout? _pipelineLayout` | `PipelineLayoutHandle?` | `backend.CreatePipelineLayout([_layout.Value])` |
| `ShaderModule? _shader` | **删除** | 接缝从源码编译，不再需要显式模块 |
| `ComputePipeline? _pipeline` | `ComputePipelineHandle?` | `backend.CreateComputePipeline(new ComputePipelineDesc(source, "cs_main", _pipelineLayout.Value))` |
| `BindGroup? _bindGroup` | `BindGroupHandle?` | `backend.CreateBindGroup(_layout.Value, [BindGroupEntryDesc.Uniform(0, bounds, 0, size), ...])` |
| `WgpuBuffer? _bounds` 等 4 个 | `BufferHandle?` | `backend.CreateBuffer(...)`；每帧更新用 `backend.WriteBuffer(...)` |
| — | 新增 `_backend` | 构造时传入，而不是 `new WgpuRenderBackend(device)` |

`Record` 方法签名改为 `Record(IRenderBackend backend, CommandEncoder encoder, ...)`（或直接收 `CommandEncoderHandle`），dispatch 改：

```csharp
var pass = backend.BeginComputePass(encoder);
try
{
    backend.SetPipeline(pass, _pipeline.Value);
    backend.SetBindGroup(pass, 0, _bindGroup.Value);
    backend.DispatchWorkgroups(pass, (uint)((slotCount + 63) / 64), 1, 1);
}
finally
{
    backend.EndComputePass(pass);
}
```

**注意**：`WriteBuffer` 是泛型方法，调用形式为 `backend.WriteBuffer<byte>(buffer, offset, data)`，参数顺序与原生 `queue.WriteBuffer(buffer, offset, data)` 一致但类型是句柄。
### 9.6 真正的根阻塞：帧的 encoder 仍是原生

把 §9.5 的配方代入 `ChunkCuller` 逐条核算后，得到一个反直觉的结论：**转换完成后逃生口是 +1 而不是 -5**。

原因是最后剩下的那一处：`ChunkCuller.Dispatch` 需要拿一个 `CommandEncoderHandle` 才能开始 compute pass，而帧的 encoder 是 `device.CreateCommandEncoder()` 出来的原生对象，必须 `AdoptCommandEncoder` —— **+1**。

于是所有"资源持有者句柄化"的努力都会在这一点上打平或倒退。

**根阻塞因此是**：帧的 encoder 不在接缝里。要让它在接缝里，必须回答三个问题（此前都被刻意推后）：

| 问题 | 现状 | 为什么难 |
| --- | --- | --- |
| `Finish` 与 `Submit` 的所有权 | 渲染器 `encoder.Finish()` + `Queue.Submit(commands)` + 显式 `Dispose` | 接缝的 `Submit` 内部 Finish + Dispose（G25），两者语义冲突 |
| `CommandBuffer` 的释放 | 渲染器显式 `commands.Dispose()` | 接缝的 `Submit` 不返回也不释放它 |
| copy / compute 之外的 encoder 操作 | `CopyTextureToBuffer`、`CopyBufferToBuffer` | 接缝没有这些方法 |

**结论**：`W5d` 的"逃生口归零"不能靠继续转换资源持有者达成，必须先做**encoder 生命周期切片**（W5e）。在此之前，725→0 这个数字是不可达的；能达成的只是把 adopt 从 N 处集中到少数几处。

因此 `T-786` 应拆成两个可独立收口的任务：
- `T-786a`：渲染 pass 录制进接缝（**已完成**）
- `T-786b`：encoder 生命周期与 compute/copy 记录进接缝，之后逃生口归零
### 9.7 `ChunkCuller` 转换的两个新缺口（2026-11-10 实测）

按 §9.5 配方执行时遇到两个 §9.5 未覆盖的问题，均已确认是**接缝能力缺口**而非笔误：

| 缺口 | 现象 | 影响 | 解除方式 |
| --- | --- | --- | --- |
| `BindingKind` 只有 `UniformBuffer` / `Texture` / `Sampler` | 剔除的 bind group 需要 `ReadOnlyStorageBuffer`(0,1) 与 `StorageBuffer`(2) | 无法用 `BindGroupLayoutEntryDesc` 表达，编译报 CS0117 | 扩充 `BindingKind`（加 `StorageBuffer` / `ReadOnlyStorageBuffer`），并在 `ToWgpu(BindingKind)` 与 `BindGroupEntry` 的 switch 里补分支 |
| `ChunkCuller.Args` 是公开属性、类型为 `WgpuBuffer?` | 渲染器用它做间接绘制的参数缓冲 | 内部 `_args` 改 `BufferHandle` 会破坏公开签名（CS0029） | 先把 `Args` 改为 `BufferHandle`，同步改调用点（需接缝支持间接绘制绑定） |

**结论**：`ChunkCuller` 的转换要先做这两项接缝扩展，不是纯替换。顺序：`BindingKind` 扩充 → `Args` 签名 → §9.5 配方。

本轮已回退该文件，工作区保持干净；§9.5 的配方仍然有效，只是缺这两项前置。
### 9.8 `ChunkCuller` 未被接线（2026-11-10 核查）

全仓核查（`src` + `tests`）：`ChunkCuller` **没有任何引用、没有 `new ChunkCuller(...)`、没有 `Dispatch` 调用**。它是 `gpu-culling-indirect` 工作树里尚未接线的特性。

因此：

| 判断 | 结论 |
| --- | --- |
| 转换它是否降低逃生口 | **否**。它当前全是原生调用（0 处逃生口），转换后反而 +1（`AdoptCommandEncoder` 已消除，但 `Args` 公开属性等仍需处理） |
| 是否值得现在转换 | **否**。未接线的代码转换后无法实机验证，违反"每步实机截图"的要求 |
| 正确处理 | **留到该特性接线时一并做**。届时 §9.5 + §9.7 的配方与能力都已就绪 |

**教训**：R20–R22 三轮尝试转换这个类，直到实测编译错误才注意到它没被使用。**动工前应先查引用面**，而不是先看文件本身难不难。
### 9.9 定论：孤点转换必然抬高逃生口，只有整图转换才降低

三次独立测量得到同一结论：

| 转换对象 | 转换前 | 转换后 | 结果 |
| --- | --- | --- | --- |
| `SkyDiscResources`（持有者整体改句柄，**进出都是句柄**） | 225 | — | 使用点 3 个 adopt 消失 |
| `FramePresentation` / `SkyDiscResources` / `WorldTextResources` 的**创建点**（输入仍是原生） | 219 | 225 | **+6** |
| `UiRenderer.CreateTextPipeline` 单个静态方法（输入 layout 仍是原生、产出被原生消费者接收） | 217 | 219 | **+2** |

**规律**：一处代码同时满足下面两条时才净减：

1. 它的**输入**是句柄（不需要 `Adopt*`）
2. 它的**输出**被句柄消费者接收（不需要 `As*`）

只满足一条就会净增或打平。因此"逐个文件迁移"这条路在数学上不可达零——**必须按依赖图整片迁移**：一个资源持有者 + 它的创建者 + 它的消费者一起改。

**已证实的整片**：`WindowedRenderer.Run` 的 encoder（R21，−8）。
**按此规律的下一片**：`UiRenderer` 三方法 + `WindowedRenderer` 里持有它们返回值的字段 + `Run` 里的 layout，一次性改为句柄。单独改 `UiRenderer` 已验证会 +2。
### 9.10 为何 `CelestialColorResources` 也不是有界切片（2026-11-10 实测）

它看起来像 `SkyDiscResources`（同一类持有 + 使用），但 `Draw` 内部循环使用三类**数组**：`_bindGroups[]`、`_vertexBuffers[]`、`_indexBuffers[]`。

只把 3 个管线改句柄的话：

| 变化 | 逃生口 |
| --- | --- |
| `CreatePositionColorPipeline` 去掉 3 个 `AsPipeline` | −3 |
| 两个调用点去掉 `AsRenderPass` | −2 |
| `Draw` 里 `SetBindGroup` / `SetVertexBuffer` / `SetIndexBuffer` 需要 `Adopt*`（每个元素各一次，元素数 = 天体颜色绘制数） | **+3N** |

`N` 是天体颜色绘制数（日出扇形与星星，通常个位数但非 1），因此**仍然净增**。

**结论**：这类"数组化的绘制集合"必须先整体改为 `BindGroupHandle[]` / `BufferHandle[]`，否则任何部分转换都是净增。这与 §9.9 的规律一致——切片边界由**数据结构的整体性**决定，不由文件边界决定。