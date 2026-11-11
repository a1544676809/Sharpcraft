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
### 9.11 一次成功的整片：管线布局句柄化（217 → 184）

按 §9.9 的规则做成的一片，可作为后续切片的标准范例：

| 改动 | 消除 | 新增 |
| --- | --- | --- |
| 15 个 `Create*Pipeline` 形参 `PipelineLayout` → `PipelineLayoutHandle` | `AdoptPipelineLayout` ×14 | 0 |
| 其中 10 个返回值 `RenderPipeline` → `PipelineHandle`（消费者全是 `SetPipeline`） | `AdoptPipeline` ×8 | 0 |
| `pipelineLayout` / `blitDepthLayout` / 5 个 ref layout / 3 个 cloud layout 改句柄 | `AsBindGroupLayout` ×4 | 0 |
| 帧循环退出处补 17 个 `backend.Destroy` | — | 生命周期显式化 |

**为什么这次能降而单方法转换不能**：这一片里创建者、持有者、消费者**同时**换了类型，因此没有任何一处需要 `As*` 把句柄换回原生。

**仍然留在原生侧的**（都有明确原因，不是遗漏）：
- 5 个 OIT helper（`CreateDepthBounds`/`Transmittance`/`Accumulate`/`Cull`/`Composite`）返回值仍是 `RenderPipeline`：消费者是 `ReferenceOitPasses.Parameters` 的原生字段，改句柄会新增 8 处 `AsPipeline`。**下一片要把 `Parameters` 一起改。**
- 4 个 OIT 纹理布局：见 §9.12。

### 9.12 `TextureSampleType` 缺 `UnfilterableFloat`（已修）

`oitTargetFormat` 默认是 `Rgba32Float`，这类纹理**可采样但不可过滤**。接缝原先只有 `Float`（filterable），于是：

- 用 `Float` 描述 OIT 布局 → 创建 bind group 时 wgpu 直接校验失败：
  `Texture binding 0 expects sample type Float { filterable: true }, but was given a view with format Rgba32Float (sample type Float { filterable: false })`
- 这不是"近似"，是**必然的运行期错误**

已补 `TextureSampleType.UnfilterableFloat` 及映射。这条也解释了为什么有些布局此前只能留在原生侧——**不是懒，是接缝表达不了**。

**教训**：接缝的枚举必须覆盖它要替换的原生枚举中所有实际用到的值，否则迁移会在运行期而非编译期失败。
### 9.13 第二个成功整片：OIT 目标与绑定（184 → 137）

同一形状再次验证有效——**持有者连同创建者与消费者一起换类型**：

| 改动 | 消除 |
| --- | --- |
| `ItemOitSet` 完全改为接缝持有者（4 张目标纹理 + 视图 + 3 个 bind group 全走句柄；`Create` 不再需要 `Device`） | `AdoptBindGroup` ×6、`AdoptTextureView` ×8、`AdoptPipeline` ×4 |
| `ReferenceOitPasses.Parameters` 的视图/深度/管线/自有绑定全部句柄化 | `AdoptTextureView` ×12、`AdoptPipeline` ×8、`AdoptBindGroup` ×6、`AdoptBindGroupLayout` ×6 |
| 4 个 OIT 纹理布局经接缝创建（用新增的 `UnfilterableFloat`） | `AdoptBindGroupLayout` ×4（但 WindowedRenderer 侧新增 9 处 peek） |

**净 −47**。

**剩下的 6 处 `AdoptBindGroup` 全部指向两个仍原生创建的对象**：主图集 bind group（在 `Run` 里 `device.CreateBindGroup`）与 `clouds.BindGroup`（`CloudOitResources` 持有）。9 处 `AsRenderPass` 则指向 `GeometryArena.Draw*` 与 `CloudOitResources.DrawGeometry` 仍收原生 pass。这两组就是下一片的全部内容。

**累计轨迹**：225 → 217（encoder 整片）→ 184（管线布局整片）→ **137**（OIT 整片）。每次下降都对应一次"创建者+持有者+消费者同时换类型"，无例外。
### 9.14 第三片：云集合、图集绑定、几何入口与呈现（137 → 98）

| 步 | 改成接缝的东西 | 消除 |
| --- | --- | --- |
| 1 | `CloudOitResources` 的布局/绑定/缓冲整体句柄化，`DrawGeometry` 收 `(IRenderBackend, RenderPassHandle)` | 13 |
| 2 | 主图集 bind group 经接缝创建，消费者直传句柄（含 `ReferenceOitPasses.TerrainAtlas`） | 9 |
| 3 | `ChunkGeometryStore` 的 `Draw*` 收 `RenderPassHandle`，`DrawEntry` 走接缝 | 9 |
| 4 | `FramePresentation` 全句柄化，`Ensure` 返回 `TextureViewHandle` | 8 |

**四步互为前提**：不做步 1 的缓冲句柄化，步 2 的绑定就仍需 adopt；不做步 4 的 `Ensure`，帧目标的每个 pass 就各要一次 `AdoptTextureView`。这是"依赖图整片迁移"的又一例。

**累计**：225 → 217 → 184 → 137 → **98**（−127，−56%）。
### 9.15 第四片：实体层、两个小组与离屏外壳（98 → 70）

| 批 | 内容 | 消除 |
| --- | --- | --- |
| 1 | 实体层缓冲改 `BufferHandle`；`DrawItems` 回调改 `Action<RenderPassHandle>` | 6 |
| 2 | `SkyDiscResources` / `WorldTextResources` 全句柄 | 5 |
| 3 | `OffscreenRenderer` 的布局、相机/雾/光照贴图缓冲、主绑定、4 个管线、6 个几何缓冲 | 17 |

批 3 的收获最大，因为离屏外壳此前几乎全原生；它的清理路径也第一次被 `--frame` 完整走到。

**首次双路径验证**：窗口截图确认场景，`--frame` 确认离屏写出非黑帧并干净退出。两者相机不同，**不能逐像素比对**，这是记录在案的验证局限。

**跳过项（均为净 0，不是遗漏）**：compute 两处（其绑定与 `CopyBufferToBuffer` 交错）、`AdoptCommandEncoder`（encoder 必须原生给拷贝用）、目标/深度视图的 `AdoptTextureView`（纹理本体要给回读用）。

**累计**：225 → 217 → 184 → 137 → 98 → **70**（−155，−69%）。剩余集中在两个天体绘制集合（数组化，§9.10）与上述净 0 项。
### 9.16 第五片：两个天体绘制集合（70 → 61），§9.10 的数组化集合已收口

`CelestialColorResources` 与 `CelestialResources` 整片句柄化：纹理、视图、采样器、绑定、缓冲、管线全部改接缝对象，`Draw` 收 `(IRenderBackend, RenderPassHandle)`。`WindowedRenderer` 现在 **0 处 `AsRenderPass`**。

**证据形式（重要，适用于看不见的画面元素）**：天体在窗口机位视野外，无法用截图直接证明。可用的证据只有三类：

1. 全部资源创建成功且整轮**无 wgpu validation / panic**（布局、采样类型、混合状态或顶点布局与 shader 不符时会在创建或首次 draw 立刻报错）
2. 逐项代码等价（混合分量、深度状态、剔除、stride 与属性、采样器参数与改动前一一对应）
3. **离屏帧与改动前逐字节相同**（`sha256 fcc9f831…`，99256 B）——离屏路径会构造并绘制 `CelestialResources`

这三类证据可以支撑"无退化"，但**不能**支撑"天体绘制正确性已通过视觉验证"。文档必须保持这个区分。

**累计**：225 → 217 → 184 → 137 → 98 → 70 → **61**（−164，−73%）。
### 9.17 第六片：窗口外壳整体入缝（61 → 16）

五批，全部净减：

| 批 | 内容 | 消除 |
| --- | --- | --- |
| A | 内联 peek 改用已有共享句柄；`depthView` 复用；`cloudDepthView` 提为句柄 | 13 |
| B1 | glyph/sprite sheet、UI 变换与顶点/索引缓冲、三个 UI 绑定组（新增 `CreateUiBindGroup` 工厂，含两条重传路径） | 11 |
| B2 | 主图集、深度（含 resize）、5 个 OIT 目标纹理全部经接缝创建；blit 深度绑定入缝 | 11 |
| C | 相机/雾/光照贴图、动态与主几何、windowCutout、退役队列全部改 `BufferHandle` | 9 |
| D | `WorldTextResources` 去掉自建 `SamplerCache` + peek，改用 `backend.CreateSampler` | 1 |

**顺带修掉一处既有泄漏**：帧循环退出时从未释放 cloud depth 目标。这是把销毁调用改对称时才暴露出来的。

**验证**：离屏帧与基线 `sha256 fcc9f831…` / 99256 B **逐字节一致**，证明这一轮所有 UI/纹理/缓冲/深度的句柄化零像素退化。

**累计**：225 → 217 → 184 → 137 → 98 → 70 → 61 → **16**（−209，−93%）。
### 9.18 最终边界：剩余 7 处与它们指向的 3 个缺失能力（16 → 7）

`UiRenderer` 三条管线走 `PipelineDesc`、`SamplerCache` 改发句柄（句柄指向缓存仍持有的对象，缓存继续负责释放）。窗口 HUD 区域（560×128，覆盖面板与文字两条管线）与改造前**逐像素相同（0/71680，最大通道差 0）**。

| # | 位置 | 为什么净 0 | 消除条件 |
| --- | --- | --- | --- |
| 1 | `WindowedRenderer` 交换链视图 | 换句柄需 `AdoptTexture` + `CreateTextureView`：+1 −1 = 0 | surface 获取入缝 |
| 2 | `FramePresentation` 呈现目标视图 | 纹理必须原生给 `FrameCapture.Capture` 回读 | 接缝提供 `CopyTextureToBuffer` |
| 3 | `OffscreenRenderer` encoder | 同帧末尾还有原生 `CopyTextureToBuffer` ×2 与 `CopyBufferToBuffer` | 同上 |
| 4/5 | `OffscreenRenderer` 彩色/深度目标视图 | 纹理是帧回读的拷贝源 | 同上 |
| 6/7 | `OffscreenRenderer` 两处 compute 布局 | compute 链全原生；只入布局需反向 `AsBindGroupLayout`（+1），连 bind group 一并入缝则每级 3 个 `AdoptBuffer`（净增） | 接缝提供 compute pass + storage/map-read 缓冲形状 |

**这 7 处不是"还没轮到"，而是接缝本身缺三块能力**：① 纹理↔缓冲的拷贝/回读 API（消掉 #2–#5）；② surface 获取入缝（#1）；③ compute pass 与 storage/map-read 缓冲形状（#6/#7）。

**继续下去的正确做法是扩 `IRenderBackend`，而不是继续搬调用点。**

**累计轨迹**：225 → 217（encoder）→ 184（管线布局）→ 137（OIT）→ 98（云/图集/几何/呈现）→ 70（实体/小组/离屏）→ 61（天体）→ 16（窗口外壳）→ **7**（UI/采样器/离屏 atlas）。**−218，−97%**，全程离屏帧哈希不变。
### 9.19 收口：7 → 1，以及最终唯一保留项（2026-11-10）

补上三块缺失能力后，离屏 compute 与回读整片入缝：

| 新增能力 | 消除 |
| --- | --- |
| `BindGroupEntryDesc.Storage` / `ReadOnlyStorage` | compute 绑定可表达，两处 compute 链解锁 |
| `byte[] ReadBuffer(Instance, BufferHandle, ulong, nuint)` | 截图回读与 compute 读回不再需要原生缓冲 |
| `CopyTextureToBuffer` / `CopyBufferToBuffer` | 帧回读与级间拷贝入缝 |

`FrameCapture` 现在**零原生对象**；深度金字塔与运动向量走 `BeginComputePass/SetPipeline/SetBindGroup/DispatchWorkgroups/EndComputePass`。

**最终唯一保留项**：`WindowedRenderer` 的 `AdoptTextureView(surfaceView)` —— 交换链视图。换成 `AdoptTexture` + `CreateTextureView` 仍是 1 换 1（净 0）。**消除条件**：把 surface 获取本身入缝（`IRenderBackend` 增 `AcquireSurfaceImage()` / `ConfigureSurface(...)`），需同时改 `RenderBackend.cs` 与窗口层 surface 契约（`Sharpcraft.Framework.Platform`）。这是**跨层设计变更**，不是调用点搬运。**审计结论（2026-11-10，项目所有者裁决）：保持**，登记为受控偏差 `SIM-SURFACE-NOT-IN-SEAM`（T-788）。理由：这一处的存在恰好标记出"呈现层不属于渲染后端"这条边界，为凑数字抹掉它反而丢失信息；且它不改变任何可观测行为，而 surface 入缝会让 `IRenderBackend` 同时承担渲染与呈现两种职责，并使这块改动无法被离屏路径或截图覆盖。复查条件：需要第二个渲染后端时。

**累计**：225 → 217 → 184 → 137 → 98 → 70 → 61 → 16 → 7 → **1**（−224，−99.6%）。

### 9.20 本轮发现的两个既有缺陷（与本目标无关，但需登记）

1. **`--frame` 输出不是比特稳定的**。同一构建连跑两次可得到不同哈希；两个不同只在于一小片云的取样（537 px，44×29 区域，R/B/A 通道差、G 不变）。根因：云网格取样跟随**实时世界时钟**，`MC_FORCE_DAYTIME` / `MC_FORCE_PARTIAL` 覆盖不到它。**影响**：任何把 `--frame` 哈希当作回归门禁的做法都会随机失败。**建议**：要么让云取样接受强制时间，要么在 `--frame` 里固定世界时钟，之后该哈希才可作为门禁。当前正确的比较方式是"同世界状态下新旧构建输出对比"。
2. **`MC_FRAME_DEPTH` / `MC_FRAME_PYRAMID` / `MC_FRAME_MOTION` 诊断开关未传到渲染器**。设置后没有任何 `depth:` / `pyramid:` / `motion:` 输出。**影响**：深度金字塔与运动向量换算后**无法获得运行时真值证据**，目前只有编译通过 + 逐项语义等价。应单开任务修复 host→GameOptions 的 flag 通道，届时 `mismatches` 计数才能作为等价性证据。
### 9.21 compute 链的运行时真值（2026-11-10）

**此前认为"`MC_FRAME_*` 诊断开关未传到渲染器"是缺陷，实为用法误解**。按原则 13，游戏代码只读宿主传入的快照（`--debugOptionsFile` 的 JSON），故意不读环境变量；用 env 设置因此静默无效。

正确用法：

```powershell
'{"MC_FRAME_PYRAMID":"1","MC_FRAME_MOTION":"1","MC_FRAME_DEPTH":"1","MC_FRAME_MOTION_DX":"1.5"}' |
  Set-Content $env:TEMP\diag.json -Encoding UTF8

Sharpcraft.Client.exe ... --frame --quickPlayMultiplayer 127.0.0.1:25565 --debugOptionsFile $env:TEMP\diag.json
```

输出（实跑）：

```
depth:   640x360 nearest 410.8 blocks (raw 0.0002) farthest 2000.0 blocks centre 554.7 blocks
pyramid: 9 levels, total mismatches 0, worst delta 0.000000; 1:320x180 mismatches 0, ... 9 levels
motion:  2 samples, mismatches 0, worst delta 0.0000 px
```

**意义**：这两条 compute 链是接缝化改造的对象，`mismatches 0` 说明接缝版本与 CPU 参考**逐级一致**。这是它们第一次获得运行时等效性证据——此前只有编译通过与逐项语义等价。

**顺带发现并修复一个真 bug**：存储绑定在**布局** switch 里加了，但 **bind group entry** 的 switch 漏了，导致首次创建 compute 绑定组时抛 `no entry counterpart: ReadOnlyStorageBuffer`。这个错误编译期与单测都发现不了（没有任何单测创建 compute 绑定组），**只有实际跑一次 compute 才会暴露**——这正是缺诊断时被掩盖的那类问题。

**教训**：`As*`/`Adopt*` 一类的 switch 分支扩充必须成对检查（layout ↔ entry），且必须有一次运行期覆盖，否则是"编译通过但会抛"的形态。
### 9.22 `--frame` 可以作为像素回归门禁（前提：钉住世界状态）

同一结论：`MC_FORCE_DAYTIME` / `MC_FORCE_PARTIAL` **有效**，但必须经 `--debugOptionsFile` 传入。实测：

```json
{"MC_FORCE_DAYTIME":"6000","MC_FORCE_PARTIAL":"0.5"}
```

两次连续 `--frame`（640×360）输出 **`sha256 963467906ED32C82` / 35579 B，逐字节相同**。**但这是间歇性的**：随后的重复测试中出现过一次不一致（35579 vs 35565 B，14 字节差），再跑又一致。

**因此可行的回归门禁**（目前未自动化，属建议）：

```powershell
# 固定世界状态后连跑两次，必须逐字节相同；改动前后也必须相同
--frame --quickPlayMultiplayer <addr> --debugOptionsFile <pinned.json>
```

**必须诚实标注的局限**：钉住时间**不足以**保证逐字节可重复。`--frame` 会在约 8 秒内从**运行中的服务器**采集世界数据，服务器在两次运行之间仍在 tick（生物位置、方块更新、区块到达顺序都可能不同），因此偶发不一致。已实测：连续三次运行的结果为「一致 / 不一致（14 字节） / 一致」。**所以它现在还不是硬门禁，而是一个有用的信号**：不一致既可能是渲染变化，也可能是世界漂移，需要人工区分。若要变成硬门禁，需要更彻底地隔离世界（例如快照式世界源，或把 `MC_FRAME_SECONDS` 压到极短）。

**这一对发现的意义**：项目此前只有"窗口截图目视"这一种渲染验证手段；现在有了可重复、可逐字节比较的离屏基准，且深度金字塔与运动向量已有 `mismatches 0` 的运行时真值。
### 9.23 OIT 半透明几何的首次实机覆盖（2026-11-10）

此前本目标所有验证的场景都是 `translucent 0 tri`——即**参考 OIT 序列从未处理过半透明地形/物品几何**，只走到目标创建、云子 pass 与合成。那次覆盖是缺的，不是通过。

本轮在服务器侧放置玻璃与水（`fill ... minecraft:glass` / `minecraft:water`），首次把半透明几何喂进 OIT 链路：

```
layers: cutout 0 tri, translucent 20 tri
```

stderr 中 `Validation` / `panic` / `uncaptured` 全为 0。画面核对（项目所有者目视确认"看起来是正确的"）：玻璃透明且边框正确、可透视后方水体；水为蓝色半透明、可见水下石头；玻璃与水的遮挡顺序正确；HUD 未被半透明层污染。

**因此这条缺口关闭**：参考 OIT 序列（bounds → cull → cloud bounds → transmittance → cloud transmittance → accumulate → cloud accumulate → composite）已被实际绘制到半透明几何并且输出正确。

**注意**：这次覆盖是"看起来正确"的目视确认，不是像素级与原版比对。原版真值仍缺（见 §9.22 与 T-786 的剩余缺口）。
### 9.24 缺口 ① 复核：原版没有这两条 compute 链（结案）

先前把"compute 的 `mismatches 0` 是对本项目自写 CPU 参考、非原版真值"列为待补缺口，**这个框定本身是错的**。核查 26.3 jar 的着色器资产：

| 核查项 | 结果 |
| --- | --- |
| `shaders/**/*.comp`（compute shader） | **0 个** |
| 文件名匹配 `motion` / `velocity` 的着色器 | **无** |
| `integrate_depth.fsh` | 仅 297 字符：`texelFetch` 取深度、`depth == 0.0` 时 `discard`、否则写 `gl_FragDepth`。**不是金字塔**，是一次深度搬运 |
| 全仓 `DepthPyramid` 的消费者 | `FrameCommand`（宿主侧诊断）+ `OffscreenRenderer`（对照用的 CPU 参考） |
| GPU 计算链的运行条件 | `request.Diagnostics?.BuildPyramid == true` / `MotionVectors == true` —— **只在诊断开关下运行** |

**结论**：`depth_pyramid` 与 `motion_vectors` 是**本项目自有的诊断/预研特性**，原版 26.3 没有对应物，因此**不存在可提取的原版真值向量**。它们不在生产渲染路径上（诊断开关关闭时不运行），也就不参与"与原版等效"这件事。

它们的正确性判据是：GPU 路径复现一个**独立的 CPU 实现对同一条规则的实现**——`mismatches 0`（9 级金字塔 + 运动样本）已证明这一点。

**局限（如实记录）**：本次核查覆盖的是**着色器资产**（jar 内 `shaders/` 目录）。我没有追查 Java 侧是否用多次片段着色器 pass 组合出等价的金字塔——`integrate_depth.fsh` 的存在暗示可能有逐级搬运的做法。若将来这两条链要进生产路径，需要先做那次追查。

**缺口 ① 关闭（不适用）。**
**裁决（2026-11-10，项目所有者）**：**停放，不接线**（T-791）。理由见 T-791：无帧时间数据证明 CPU 剔除是热点、量级估算可忽略、接线会引入无法自证的可见集合变化、且原版无 compute 着色器使本项纯属性能实验。**不删除**，因为它是已下令特性的一部分，接线所需的顺序已记录（测量 → `DrawIndexedIndirect` → 对照门禁 → 接线）。
### 9.25 窗口路径只渲染 7–9 个区块（T-792，2026-11-10 实测）

排查 GPU 剔除基准时发现的**功能性缺陷**，比剔除优化本身重要得多。

**现象**：窗口模式 `cull: chunks=7`，80 秒不增长；同一服务器离屏路径可加载 637 个。

**决定性实验**：

| 运行 | 结果 |
| --- | --- |
| 默认（`MC_START_CHUNKS=9`） | `chunks: 9 ok, 0 failed`，之后一直是 7 个在场景里 |
| `MC_START_CHUNKS=400` | `chunks: 400 ok, 0 failed` —— **网络流式收区块完全正常** |
| 同上，继续观察 | 卡在 `chunk workers: 8` 之后 **300 秒未出现窗口** —— 一次性预载 400 个会被网格化阻塞 |

**根因（修复时实测更正）**：`InteractiveSession` 的增量收区块链路一直是通的，**它从来没被触发过——因为连接在预载结束后就死了**。证据：①Fabric 服务器日志里每次 `logged in` 之后**恰好 30 秒**出现 `lost connection: Timed out`；②客户端 socket 处于 `CloseWait`（服务器先关）。原因是预载到 9 个返回后，`ChunkWorkerPool` 创建 + 网格预热 + `BuildRequest` 合计 **~29 秒没有一帧在跑**，没人 `DrainIncoming`、没人回 keep-alive 与 `ChunkBatchReceived`，服务器 30 秒判死。连接一死，`cull: chunks` 永远 7、`pool: parsed` 永远 0。**另更正本节的另一个推测**：资源加载其实只有 ~0.4 秒（`sprites: 2048x2048 atlas ... 106 ms`），不是"数秒"。

**原版规格**（`ref://mc-26.3-analysis/src/net/minecraft/client/`）：资源重载成功回调 → `onResourceLoadFinished` → `onGameLoadFinished` → `Gui.buildInitialScreens` 链尾的 `QuickPlay.connect` → `ConnectScreen.startConnecting`。即**先加载完资源，再连接**；连接后由 `ClientChunkCache` 持续接收并按批次确认（`ChunkBatchReceived`）。我们的顺序相反：先连接预载，再花数秒加载资源，期间既不上报批次确认也不消费区块包。

**这同时解释了之前观察到的服务器日志 `Timed out`**：连接建立后客户端长时间阻塞在资源加载，不响应服务器。

**修复（已完成）**：①世界选择链移到资源/精灵之后、`ChunkWorkerPool` 之前（对齐原版顺序）；②`LivePlaySession` 的收发泵用**共享的** `PlaySession.TryHandleTransport` 就地应答 keep-alive / ping / `ChunkBatchFinished` 并丢弃（这三类只回包、不含游戏状态），区块包仍只经 `InteractiveSession` 在游戏线程进世界——**没有第二套分发**（原则 14）；③连接写入加锁，泵线程与游戏线程都会写流。**实测**：`cull: chunks` 7 → 200（45 s）→ 366（150 s），`loaded=637` 与离屏路径一致；帧时间 2.0–5.6 ms；服务器结束原因变为 `Disconnected`，不再 `Timed out`；截图已是完整地形（树、花、裸岩坡、远山）。

### 9.26 GPU 剔除的实测结论：不值得（T-791 最终裁决）

固定机位（`-74.098 / 121 / 37.303`，朝北，yaw 180 / pitch 1.5）、放大化世界、视距 32、`MC_PRESENT_MODE=immediate`（解除帧率上限）：

| 配置 | 驻留区块 | 剔除 | 帧时间（60 帧滚动均值） |
| --- | --- | --- | --- |
| 剔除 ON（现状） | 646 | 444（69%） | **≈ 6.0 ms** |
| 剔除 OFF（`MC_CULL_DISABLE=1`） | 674 | 0 | **6.4 – 8.7 ms**（随区块流入上升） |

**总收益**：剔除节省约 0.4–2.7 ms，约合 1 µs / 每次避免的绘制调用。

**CPU 剔除循环成本**：646 槽位 × 6 平面点积 ≈ 3,900 次点积，量级 **< 0.05 ms**（不到帧时间的 1%）。

**裁决：否决 GPU 剔除方案。** 把它搬到 GPU 最多省下这 < 0.05 ms，却要新增 `DrawIndexedIndirect`、间接参数缓冲、GPU/CPU 可见集合一致性门禁。收益比代价低两个量级。`ChunkCuller` 保留为停放代码（T-791 行已记录复查条件）。

**顺带测得的关键对照**：同一机位下**原版 652 fps（约 1.5 ms），本实现约 165 fps（6.0 ms）——慢约 4 倍**。这才是性能工作的正确入口，且它可归因、可测量；把剔除搬到 GPU 属于在 1% 的量级上做优化。

### 9.27 帧时间归因结论：每区块独立缓冲导致的提交量爆炸（T-798）

**测量方法**：VS「CPU 使用率」性能探查器（附加进程，符号正常解析），固定机位、858×527、视距 12、RTX 3060、`MC_PRESENT_MODE=immediate`。

**自 CPU 排名（真正烧 CPU 的函数）**：

| 函数 | 自 CPU |
| --- | --- |
| `Brew.Wgpu.CommandEncoder.Finish()` | **34.37%** |
| `Brew.Wgpu.RenderPassEncoder.SetVertexBuffer(...)` | **7.45%** |
| `Brew.Wgpu.RenderPassEncoder.SetIndexBuffer(...)` | **5.59%** |
| `Brew.Wgpu.Queue.Submit(...)` | 5.43% |
| `Brew.Wgpu.Surface.Present()` | 5.22% |
| `ChunkGeometryStore.DrawFarthestFirst` | 1.76% |
| `ChunkGeometryStore.Draw` | 1.19% |

**根因**：`ChunkGeometryStore.DrawEntry` 对**每个区块**做：

```csharp
_backend.SetVertexBuffer(pass, 0, entry.Vertex);      // 每区块独立缓冲
_backend.SetIndexBuffer(pass, entry.Index, ...);      // 每区块独立缓冲
_backend.DrawIndexed(pass, entry.IndexCount, 1, 0, 0, 0);   // 永远从偏移 0 画
```

每个区块持有**独立的顶点与索引缓冲**（不是共享 arena），于是每帧产生数百次缓冲绑定 + 数百次绘制。`SetVertexBuffer` + `SetIndexBuffer` 合计 **13% 自 CPU** 是直接的绑定开销，而 `CommandEncoder.Finish()` 的 **34.37%** 是它的下游后果——命令条目越多，最终化时逐条序列化的成本越高。

**优化方向**：把区块几何合并进**少量共享大缓冲**，绘制时用 `firstIndex` / `baseVertex` 偏移区分。收益是双重的：直接消除数百次缓冲绑定（13%），并大幅削减 `Finish()` 的序列化量（34%）。

**这与已停放的 `ChunkCuller`（T-791）直接相关**：该特性的设计前提正是"共享 arena + 每槽位一条间接绘制记录"。当前每区块独立缓冲的结构让间接绘制无法落地。因此**合并缓冲是启用 GPU 剔除的前置条件**，不再是两件独立的事。

**测量方法教训（重要）**：初期的"CPU 占用 44%、9 个线程满载"是**区块加载期**的读数，不是稳态——性能探查器的 CPU 曲线显示前高后低，加载完成后其余线程即空闲。**任何 CPU 测量必须先确认处于稳态**，否则会把正常工作误判为缺陷。本轮曾据此连续误判三类根因（网格化空转、present 驱动自旋、收包循环空转），全部被后续证据否证。

### 9.28 分页共享 arena 的缓冲布局与绘制约定（T-798 实现规格）

原则 3.5：先写规格再改代码。本节定义 `ChunkGeometryStore` 的新缓冲组织方式；它替代 ADR-0020 的"每区块独立缓冲"，
采用 ADR-0020 自己列出的后续选项（分页 arena），决策记录见 ADR-0029。

#### 9.28.1 布局

- 每层（solid / cutout / translucent）各持有一组**固定大小页**，`pageBytes` 默认 4 MiB。
- 一"页" = 1 个顶点缓冲（容量 `pageBytes`）+ 1 个索引缓冲（容量 `pageBytes`）。
- 页内几何按区间分配：
  - 顶点区间 `[vByte, vByte + vBytes)`，**起点按 `bytesPerVertex` 对齐**（保证 `baseVertex` 是整数）。
  - 索引区间 `[iIndex, iIndex + iCount)`，起点即索引序号（4 字节对齐天然成立）。
- 顶点区间与索引区间**互不相关**（分别在自己的缓冲里），因此不需要把一块区块的顶点与索引放在同一偏移。
- 单块几何大于一页时（放大化世界可能出现）：为它单独开一页，容量取 `roundUp(需求, 2×)`，不参与常规分配。

#### 9.28.2 绘制约定与推导

网格化产物中，索引是**相对本区块顶点**的 0 基值。共享后顶点被放到页内偏移 `vByte`，于是：

```
firstIndex = iIndex                                  // 该区块自己的索引区间起点（单位：索引）
baseVertex = vByte / bytesPerVertex                  // 顶点槽位偏移（单位：顶点）
DrawIndexed(pass, indexCount, 1, firstIndex, baseVertex, 0)
```

- 索引值 = `index + baseVertex`，正是"本区块第 index 个顶点"在共享顶点缓冲里的位置，故**索引数据可以逐字节原样上传**，无需重写。
- `firstIndex` 与 `baseVertex` 都落在 `uint`/`int` 范围内（单页 4 MiB，`baseVertex ≤ 4 MiB / 56 ≈ 75k`）。
- 几何等价性因此是构造性的：上传字节 = 网格化字节（与每区块独立缓冲时完全一致），绘制语义 = 同一批次。

#### 9.28.3 增长与卸载

- **增长**：某页放不下时先在其余页找（first-fit），都不行就**新建一页**。不重建、不重传已有区块，故增长是"每页一次性"成本，分摊后接近 O(1)/字节。
- **就地更新**（方块编辑最常见）：顶点字节数与索引数都不变时，直接 `WriteBuffer` 到原偏移。
- **尺寸变化**：释放旧的两段区间、按新尺寸重新分配；区间释放会与相邻空区合并（coalesce）。
- **区块卸载**：只释放它的两段区间并合并；页变空则销毁该页（保留最后一页以防抖动）。**不做整块重建**，卸载是 O(log n)，不会产生 O(n) 重传。
- 碎片：first-fit + 合并足以覆盖"区块尺寸相近"的负载；若某页长期无法满足大请求，大请求会落到新页，属预期行为。

#### 9.28.4 绑定次数

- 不透明与切边层按**页优先**顺序绘制：同一页的区块连续发出，于是每层每 pass 的绑定次数 = 该层**可见页数**（视距 12 时 1–8），而不是可见区块数（数百）。
- 半透明层保持**全局从远到近**顺序（参考 OIT 的累积与深度界都是顺序无关的，但保持原顺序可确保画面逐像素不变）：绑定次数 = 该顺序下的**页切换次数**，通常远小于区块数。
- 新增诊断 `BindCallsTotal`（`SetVertexBuffer` + `SetIndexBuffer` 计数）与 `PageCount`，由 `WindowedRenderer` 打印"每帧绑定次数"，用来证明绑定从数百降到常数级。

#### 9.28.5 诊断口径变化

- `UploadedBytes` 语义不变（写入的字节数），因此 `frame #0 uploaded +N KiB` 应与改动前**完全一致**。
- `MemoryBytes` 改报**页容量之和**（含页内碎片），会变大，属预期；实际驻留几何字节另由 `LiveBytes` 报出。
- `Presized` / `FullRebuilds` 保留（分别为"是否预分配页"与"是否整块重建"，分页方案下恒为 false / 0）。

### 9.29 T-798 之后：瓶颈已从 CPU 转到 GPU（2026-11-11）

**T-798（共享几何页）的效果**：帧时间 5.13–5.70 → 2.5–3.6 ms；绑定次数 2668 → 278 次/帧。VS 探查器在新构build上显示**所有函数自 CPU 均为 0.00%**（最高 `RecordScenePasses` 23.15% 是调用树总计值，不是自身耗时），说明 CPU 侧已无热点。

**新的瓶颈是 GPU**：游戏内覆盖层实测 `GPU₁ 60 °C, 96% 利用率, 122.1 W`，`361 FPS / 2.5 ms`。独显接近满载。

| | GPU 时间 |
| --- | --- |
| 原版 26.3（616 fps） | **≈1.6 ms** |
| 本实现（361 fps） | **≈2.5 ms** |
| 差距 | **≈0.9 ms** |

**关键矛盾**：A/B 实测 OIT 单独占 **1.32 ms**，比整个 0.9 ms 的差距还大。而原版 26.3 **同样有 OIT**（jar 内含 `oit_composite.fsh`、`oit_depth_bounds_cull.fsh`），却在 1.6 ms 内完成了全部绘制。**因此本实现的参考 OIT 比原版贵约 3–4 倍。**

**AB 归因（同机位、视距 12、858×527、区块 680–700）**：

| 配置 | 帧时间 | 相对基线 |
| --- | --- | --- |
| 基线 | ≈3.95 ms | — |
| 关 OIT | ≈2.63 ms | −1.32 ms（33%） |
| 关半透明 | ≈3.01 ms | −0.94 ms（24%） |
| 关云 | ≈3.30 ms | −0.65 ms（16%） |

（**不可相加**：关 OIT 与关半透明重叠，都是去掉半透明几何绘制。）

拆解基线：不透明 + UI + 上屏 ≈ 1.5–1.7 ms，OIT ≈ 1.32 ms，云 ≈ 0.65 ms。**仅不透明部分就已接近原版的总预算**，故不能靠删除透明或云来对齐——必须让它们更便宜。

**调查清单（下一轮，只做 OIT 的 GPU 开销）**：

| 问题 | 关注点 |
| --- | --- |
| pass 数量 | 本实现 9 个（bounds → cull → cloud bounds → transmittance → cloud transmittance → accumulate → cloud accumulate → composite）；原版几个？每个全屏 pass 都是一次读写带宽 |
| 累积缓冲格式与分辨率 | `Rgba32Float` 为 16 B/像素；原版是否用更省的格式或半分辨率 |
| 深度边界剔除是否生效 | 原版有 `oit_depth_bounds_cull.fsh`，把半透明处理限制在深度窄带内从而避免全屏开销。本实现有对应 pass，但**未确认剔除是否真的在缩小工作集** |
| 半透明几何的光栅化次数 | 此前测得"重画三遍"；需确认在当前实现下是否仍然如此 |

**优先做"该省没省"的那一条**——若深度边界剔除形同虚设，其收益可能是零风险的。

**测量纪律（再次强调）**：GPU 利用率与帧时间必须在区块稳定后读取。加载期 8 个网格化 worker 满载是正常行为，此前曾据此误判四次。

### 9.30 OIT 的 GPU 开销归因（T-799，2026-11-11）

#### 9.30.1 每个 pass 的读写量（全屏 RGBA32Float 16 B/像素；858×527 = 452 166 px）

本实现每帧的参考 OIT 序列（`ReferenceOitPasses.Record`），五个目标**全部全分辨率 `Rgba32Float`**（`MC_OIT_LOW_PRECISION=1` 时降为 `Rgba8Unorm`）：

| # | pass | 颜色目标（清 + 写） | 读 | 几何 | 每像素目标流量（估） |
| --- | --- | --- | --- | --- | --- |
| 1 | 深度边界 | `Bounds` 清 −65504 + MAX 写 16 B | — | **半透明地形第 1 次光栅化** | 32 B |
| 1b | 不透明深度剔除 | `BoundsCulled` 清 + 写 16 B | `Bounds` 16 B | 全屏三角 | 48 B |
| 1c | 云深度边界 | `BoundsCulled` 保留 + MAX 写 | — | 云几何 | 32 B |
| 2 | 波let 透射率 | `Transmittance0/1` 清 + 加写 32 B | `BoundsCulled` 16 B | **半透明地形第 2 次**（+ 云） | 48 B |
| 2c | 云透射率 | 两目标保留 + 加写 | 同上 | 云几何 | 32 B |
| 3 | 颜色累积 | `Accumulate` 清 + 加写 16 B | `BoundsCulled` + 2 系数 = 48 B | **半透明地形第 3 次**（+ 云） | 64 B |
| 3c | 云累积 | `Accumulate` 保留 | 同上 | 云几何 | 48 B |
| 4 | 合成 | `Frame`（BGRA8）读改写 16 B | `Accumulate`+2 系数+`BoundsCulled` = 64 B | 全屏三角 + 写深度 | 80 B |
| 5 | 半透明物品集 | **4 个额外全屏 pass**（bounds / 透射率 / 累积 / 合成），4 个自有 `Rgba32Float` 目标 | 同 1–4 结构 | 物品（常为空） | ≈ 176 B |

合计约 **330 B/像素（地形+云）+ 176 B/像素（物品集）** ≈ **150 MB/帧 + 80 MB/帧**；按 2.5–3.9 ms/帧折算，OIT 目标带宽需求约 **40–60 GB/s**。**最大项是第 3 阶段（累积）与第 2 阶段（透射率）的“清 + 写 + 读”三趟全分辨率浮点目标**，其次是物品集那 4 个通常为空的 pass。

#### 9.30.2 ① 深度边界剔除：**部分生效**

- **生效的部分**：`oit_depth_bounds_cull.wgsl` 真的计算了“最近不透明面”并把最远边界夹住（`min(bounds.g, closestOpaqueLinear)`），结果 `BoundsCulled` 被后续两阶段读取，用来把片元深度归一化到窄带内（`normalizeDepth`），与原版 `oit_depth_sample.glsl` 行为一致。
- **不生效的部分（关键）**：**没有任何一处用窄带去缩小工作集**。后续阶段既不按带宽 `discard`，也没有用 scissor / 硬件 depth-bounds（WebGPU 未暴露）限制光栅区域；因此半透明几何**仍然全屏光栅化三次**，全屏的合成也逐像素执行。剔除只改善“深度分箱的归一化质量”，不省 GPU 时间。
- 与原版对照：原版三个阶段同样**不做带宽丢弃**（`oit_depth_sample.glsl` 只做归一化），区别是原版剔除 pass 会把不透明深度写进深度缓冲（`gl_FragDepth`），让后续阶段靠**硬件深度测试**提前杀掉被不透明面遮住的片元。本实现的后续阶段同样挂在主不透明深度上（`DepthAttachmentDesc.Keep`），所以这一点**等价**。
- **结论**：①不是“形同虚设”，但也**没有可回收的零风险收益**——它没有省下任何工作集。

#### 9.30.3 与原版的行为对照（只描述行为，证据为 jar 内资产结构）

原版资产里只有两个 OIT 专用 shader（`oit_composite.fsh` 与 `oit_depth_bounds_cull.fsh`），半透明几何的写入由 `terrain.fsh` 按编译期定义 `OIT_DEPTH_BOUNDS` / `OIT_TRANSMITTANCE` / `OIT_ACCUMULATE` 分成**三个变体**完成，深度边界由 `oit_depth_bounds.glsl` 直接写进 `fragColor`。即：

| 维度 | 原版 | 本实现 | 差异 |
| --- | --- | --- | --- |
| 半透明几何光栅化 | **3 次**（三个变体） | **3 次** | 相同，④的“重画三遍”属于原版设计，不是我们的额外开销 |
| 全屏 pass | 2 个（cull + composite） | 2 个（cull + composite） | 相同 |
| OIT 目标 | 由 PostChain 定义（资产内无 JSON，无法读出格式/分辨率） | 5 个全分辨率 `Rgba32Float` | **本实现把 bounds / 裁剪后 bounds 分成两张全分辨率目标**，且系数与累积也用 16 B/像素 |
| 物品集 | 独立 `OitPipelineSet("item")` | 独立 `ItemOitSet`（4 个 pass + 4 目标） | 结构相同；本实现**每帧无条件执行**（已修，见 9.30.4） |
| 云 | 云参与 OIT | 云另有 3 个 pass | 结构相同 |

**我们多出来的**：①更宽的目标格式（若原版用 16 位/半分辨率，我们就是 2–4 倍带宽）；②bounds 与裁剪后 bounds 各占一张全分辨率目标；③物品集空跑（已消除）。

#### 9.30.4 本轮已做的两件事

1. **新增永久诊断 `MC_OIT_STAGES=1..5`**：只记录前 N 个 OIT 阶段（0 = 全部），用于按阶段做 A/B。经 `FrameDiagnostics.OitStages` 传入，默认不生效。
2. **物品集空跑消除**：`ReferenceOitPasses` 只在“本帧存在半透明物品几何”时执行物品 OIT 集，并新增退出诊断 `window: item oit set ran on N/M frames`。合成 shader 对空输入逐像素 `discard`，故跳过**逐像素等价**。实测本机位 `0/30434` 帧有物品几何 → 之前每帧白跑 4 个全分辨率 pass + 4 次全屏清；但**省下的量在噪声内**（见 9.30.5），说明它不是大头。

#### 9.30.5 测量结论与噪声边界（重要）

用 `MC_OIT_STAGES` 做阶段前缀 A/B（同一机位、钉死白天 6000 / partial 0.5、静置 85 s、每配置一个新进程）：

| 阶段前缀 | 帧时间均值 | 样本 |
| --- | --- | --- |
| 0（无 OIT） | 2.295 ms | 2.113–2.686 |
| 1 深度边界 | 2.551 ms | 2.321–3.053 |
| 2 + 剔除 | 3.231 ms | 2.562–4.766 |
| 3 + 透射率 | 2.475 ms | 2.299–2.795 |
| 4 + 累积 | 3.224 ms | 2.906–3.560 |
| 5 + 合成（完整） | 3.374 ms | 3.166–3.608 |

**“阶段 3 比阶段 2 更快”在物理上不可能**，说明单 pass 的差值（0.2–0.7 ms）**小于跨进程测量噪声（约 ±0.5 ms）**。可用的稳健结论只有两条：

- **OIT 整体约 1.1 ms**（阶段 0 的 2.295 → 完整 3.374），与 T-798 之后的 A/B（3.95 → 2.63，即 1.32 ms）一致。
- **单 pass 的拆分不可信**，要拿到可信的分阶段数字必须用 **GPU timestamp query**（wgpu 有 `QuerySet` + `writeTimestamp` + `resolveQuerySet`，但接缝目前没有暴露，需要先批准新增接缝方法）；或者在同一进程内切换阶段（当前诊断是按帧读取 `currentRequest.Diagnostics`，但宿主每进程只构造一次请求，需要宿主侧支持）。

#### 9.30.6 优化建议（按收益 ÷ 风险排序，均未实施）

| 建议 | 预期收益 | 风险与影响面 | 结论 |
| --- | --- | --- | --- |
| 1. OIT 五个目标从 `Rgba32Float` 降到 `Rgba16Float` | OIT 带宽减半（约 0.5 ms 量级） | 深度边界里的设备深度精度下降（半精度在 1.0 附近约 1e-3），可能出现深度分箱的阶梯/条带；需同机位像素 A/B | 先做 A/B 再定 |
| 2. 系数 / 累积目标改半分辨率 | 带宽降到 1/4 | 半透明边缘变软、薄面断层；与原版视觉可能有可见差异 | 需像素对照，暂不动 |
| 3. `bounds` 与 `boundsCulled` 合并为一张目标（剔除 pass 原地读改写） | 少一张全分辨率目标的清与读（约 16–32 B/px） | 需同目标读写（wgpu 允许不同 pass 内同一纹理读写，但单 pass 内不允许 aliasing）；改动小、视觉无影响 | 推荐先做 |
| 4. 物品集空跑消除（**已实施**） | 4 个全屏 pass + 4 次清 | 逐像素等价；本机位无物品几何 | 已完成 |
| 5. GPU timestamp query 接缝 | 得到可信的分阶段 GPU 时间 | 需新增接缝方法（须先批准） | 建议下一步先批准这一项，否则后续优化都在噪声里做决定 |

### 9.31 GPU 时间戳：尝试过，此绑定不可用，代码已回退

**结论（2026-11-11）**：`wgpuCommandEncoderWriteTimestamp` 除设备级 `TIMESTAMP_QUERY` 外，还要求**实例级** `TIMESTAMP_QUERY_INSIDE_ENCODERS`；`Brew.Wgpu 29.0.0.5` 的 `WGPUInstanceFeatureName` 枚举不含该值，托管 `InstanceDescriptor` 也无特性字段，故无法请求。缺失时该调用产生 uncaptured error 并 abort 进程（不可捕获）。

已实现的接缝扩展（`QuerySetHandle` / `CreateTimestampQuerySet` / `WriteTimestamp` / `ResolveQuerySet` / `BufferUsages.QueryResolve`）、开关、11 个时间标记与读回逻辑在验证通过后**整体回退**，理由：一份"永远返回 unavailable"的诊断比没有更容易误导后来者，且接缝多 5 个无人调用的方法。重新加回的成本约 20 分钟。

**改用**：同进程交替 A/B（每 60 帧切换配置，分别累加均值，噪声可降到 ±0.05 ms 量级）与分辨率缩放实验（像素数腰斩看 OIT 成本是否同步腰斩，用于判定"几何光栅化主导"还是"目标带宽主导"）。两者都不碰接缝。

**顺带得出的一条可复用推算**（同机位同会话）：

| 配置 | 帧时间差 |
| --- | --- |
| 关 OIT（连几何带 5 个 pass 全省） | −1.32 ms |
| 关半透明（只省几何，pass 照跑） | −0.94 ms |
| **diff** | **0.38 ms = pass 本身的开销；0.94 ms（≈70%）= 半透明几何的三次光栅化** |

该推算与按目标格式算出的流量估算（≈330 B/px ≈150 MB/帧 → 约 0.3–0.4 ms @ 40–60 GB/s）**互相印证**，因此"OIT 的大头在几何侧而非目标带宽侧"有两条独立证据。

**已否决**：`Rgba32Float` → `Rgba16Float`（用户裁决 2026-11-11：不做，后续提案不得再提）。
### 9.32 「不透明部分」的归因（T-801，2026-11-11）

#### 9.32.1 方法：同进程交替 A/B（本轮新增的永久诊断）

T-799 的教训是"跨进程 A/B 的噪声（±0.5 ms，实测可达 1.2 ms）大于要测的差值"。本轮新增 `MC_AB_TOGGLE=N`（`FrameDiagnostics.AbToggleFrames`）：**在同一个进程内每 N 帧切换一组开关**，每个报告窗口同时累计 A 组（全画）与 B 组（跳过宿主指定的项）的帧时间均值，漂移在同一窗口内互相抵消，报告行带 `abA=…/n abB=…/n`。

实测同一次运行里，跨窗口的 A−B 差值稳定（例：`diff=0.14 / 0.24 / 0.32 / 0.61`），说明该方法可用；**后续所有逐项归因都应使用它，而不是重跑进程。**

#### 9.32.2 归因表（858×527、视距 12、钉死白天 6000 / partial 0.5、静置 95–150 s、每组 ≥1.2 万帧）

| 被跳过项 | A 组（全画） | B 组（跳过该项） | 该项成本 | 占全帧 |
| --- | --- | --- | --- | --- |
| 不透明 + 切边地形 | 3.260 ms | 2.815 ms | **0.446 ms** | ≈14 % |
| 界面（UI pass） | 3.169 ms | 3.067 ms | **0.101 ms** | ≈3 % |
| 实体 | 3.041 ms | 3.017 ms | **0.024 ms** | <1 %（噪声级） |
| 三者合计（同上一次运行） | 3.306 ms | 2.886 ms | **0.420 ms** | ≈13 % |
| OIT（参照 T-799 的跨进程 A/B） | — | — | 1.32 ms | 33 % |
| 云（同上） | — | — | 0.65 ms | 16 % |

**关键结论：几何和 UI 加起来只有 0.45–0.57 ms，剩下约 0.8–0.9 ms 是全帧的固定成本**——主 pass 的清屏与天空、深度清屏、上屏拷贝、`Submit`/`Acquire`、驱动每帧开销。这部分**没有任何现有开关能测到**。

> 修正：T-799 里"不透明 + UI + 上屏 ≈1.5–1.7 ms"是把不可比的 A/B 相减得到的残差，**实际几何/UI 成本只有 0.45–0.57 ms**。

#### 9.32.3 几何量（精确计数，`cull:` 行新增 `drawn=` / `tri=`）

| 指标 | 数值 |
| --- | --- |
| 已加载列 | 637 |
| 视锥剔除 | 451 列 |
| **实际绘制** | **186 列 / 186 次 draw / 1 227 928 三角形** |
| 平均每列 | ≈6 601 三角形（24 个 section ≈275/个） |

1.23 M 三角形在 3060 上按 ≈0.45 ms 折算约 **2.7 G 三角形/秒**，与消费级 GPU 的三角形吞吐同量级——**说明几何已接近"白给"，不是瓶颈**；也就是为什么无论怎么砍几何都只能省到 0.45 ms 上限。

#### 9.32.4 剔除粒度：**column（列），不是 section**

- 代码事实：网格化以 `ClientWorld.Pack(ChunkX, ChunkZ)` 为键，**一列一个网格、一个 AABB**（`ChunkGeometryStore` 的 entry 覆盖整列高度 y −64…320，共 384 格）。
- 后果：视锥测试对"384 格高的盒子"几乎必然相交 → 只要列在视野内，**整列 24 个 section 全部绘制**，包括远高于 / 远低于相机、被前方地形完全挡住的部分。
- 原版对照（只描述资产里的行为）：`terrain.vsh` 声明了逐顶点 `ChunkPosition`（ivec3）与 `ChunkVisibility`（float），即**原版的绘制单位是 section（16³）**；`terrain.fsh` 里 `ChunkVisibility` **只用于淡入混色**（`color = mix(FogColor…, color, chunkVisibility)`），不是遮挡剔除。因此原版的遮挡规避不在着色器里，按公开行为应在 CPU 侧按 section 增量维护——**这一条属于公开知识，无法从资产直接验证，标记为假设**。
- 遮挡剔除：**本实现完全没有**（只有视锥）。被前方地形完全挡住的 section 仍在绘制。

#### 9.32.5 结论与建议（按 收益 ÷ 风险 排序）

| 优先级 | 建议 | 预期收益上限 | 视觉影响 | 风险 |
| --- | --- | --- | --- | --- |
| **P1** | **先归因剩下的 0.8–0.9 ms 固定成本**：用同一个 `MC_AB_TOGGLE` 加三个开关（跳过天空、跳过色彩/深度清屏、跳过上屏前的成像步骤），确认它是清屏带宽、上屏拷贝还是驱动每帧开销 | 若大头是固定开销，可能一次省 0.3–0.6 ms | 无（纯诊断） | 低 |
| P2 | OIT 1.32 ms（T-799 已归因；格式降级与合并目标均已被否决） | 结构改动才可能省 | 中 | 中 |
| P3 | 云 0.65 ms | 同上 | 低 | 中 |
| P4 | **section 级剔除 + 遮挡剔除**（不做） | ≤0.45 ms（等于把地形画成零） | 无（正确实现） | **高**：需要改网格化与几何存储（当前"一列一网格"的格式本身就无法 section 剔除），而收益上限比 P1 还小 |
| P5 | 减少 draw call（186 → 更少） | 接近 0（已知 binds/frame 已降到 278，实测地形成本与 draw 数不相关） | 无 | 低 |

**P1 是目前唯一"高收益、低风险、且完全没被测过"的方向**；P4 的收益被 9.32.2 的实测封顶在 0.45 ms，按当前性价比不值得动网格格式。
### 9.33 那 0.8–0.9 ms 固定成本：实测为 **1.56 ms 的 CPU/呈现底座，且与分辨率无关**（T-802，2026-11-11）

#### 9.33.1 分辨率对照（同机位、钉死白天/partial、静置 95 s、各 5 个稳定窗口取中位数）

| 配置 | 858×527 | 429×263（1/4 像素） |
| --- | --- | --- |
| **空帧**（跳过地形/实体/UI/OIT/云/天空） | **1.564 ms** | **1.565 ms** |
| 空帧 + 跳过上屏拷贝 | 1.700 ms | 1.605 ms |
| **完整帧** | **3.509 ms** | **2.978 ms** |

**结论 1：空帧耗时与像素数完全无关（1.564 vs 1.565，差 0.001 ms）⇒ 底座不是带宽、也不是填充率。**
**结论 2：跳过上屏拷贝不降低它（反而在噪声内偏大）⇒ 那次 blit 不在关键路径上。**
**结论 3：完整帧从 1/4 像素到全像素只涨 0.53 ms ⇒ 整帧的"随像素"部分只占 0.5 ms 量级，其余是逐 draw / 逐 pass / 逐帧的固定开销。**

#### 9.33.2 CPU 分阶段实测（`MC_FRAME_CPU=1`，`frame-cpu:` 行，单位 ms/帧）

| 阶段 | 空帧（全跳过） | 完整帧 |
| --- | --- | --- |
| **frame（总计）** | 2.02–2.34 | 3.00–3.32 |
| **updates**（客户端每帧更新） | **0.68–0.79** | 0.63–0.81 |
| **present**（呈现/等待交换链图像） | **0.87–1.02** | 0.66–0.80 |
| **submit**（结束编码 + 提交队列） | 0.24–0.27 | **0.72** |
| **scene**（主 pass 逐 draw 录制） | 0.11–0.13 | **0.59** |
| oit（OIT 各 pass 录制） | 0.025 | 0.29 |
| acquire / camera / text / clouds / ui | 各 ≤0.045 | 各 ≤0.03 |

**归因结论**：底座由三段构成——`present` ≈0.7–1.0 ms 的等待、`updates` ≈0.7 ms 的每帧客户端工作、`submit` ≈0.25 ms 的空提交；它们都是 **CPU 侧**，所以对分辨率不敏感。

#### 9.33.3 与原版对照（只描述资产体现的行为）

- 天空：原版 `sky.vsh` 经过 `ProjMat * ModelViewMat` 变换**几何**（天空盒/穹），`sky.fsh` 只做一次 `apply_fog(ColorModulator, …)`；本实现的渐变天空是**全屏三角 + 方向梯度计算**。实测本实现天空 **0.166 ms**。
- 清屏：清屏本身在原版是驱动调用（资产里看不到），本实现实测**色彩+深度清屏合计仅 0.051 ms**，不是问题。
- 上屏：原版有 `blit_screen.fsh`（单次全屏采样），与本实现结构相同；实测该项在本实现不进关键路径。
- **原版整帧 1.6 ms，而我们"什么都不画"的空帧就是 1.56 ms** —— 差距全部落在这层 CPU/呈现底座上，与几何、OIT、云无关。这是本项目目前为止最硬的结论：**要追平原版，必须先压掉这层底座，而不是继续优化渲染内容。**

#### 9.33.4 修法（按 收益 ÷ 风险）

| 优先级 | 措施 | 预期收益 | 视觉影响 | 风险 |
| --- | --- | --- | --- | --- |
| **P1** | **把 `updates` 与渲染帧解耦**：客户端逻辑按 tick（20 Hz）或固定 60 Hz 步进，而不是每帧全量跑 | 0.6–0.8 ms（每帧） | 若步进正确则无；实体插值需保持每帧 | 中：涉及客户端主循环时序 |
| **P2** | **查 `present` 为什么阻塞 0.7–1.0 ms**：对比 `fifo`/`mailbox`/`immediate`、减少在飞帧数、确认没有多余的 `ProcessEvents` 轮询 | 可能 0.3–0.8 ms | 无 | 低（纯呈现路径） |
| P3 | 降低 `submit`（0.25 ms 空提交）与 `scene`（186 draw ≈ 0.59 ms，**≈3.2 µs/draw** 的录制成本） | 0.2–0.4 ms | 无 | 中：需要批处理/间接绘制 |
| P4 | 天空改成原版那种低成本几何或直接并入清屏 | ≤0.17 ms | 极小（渐变细节） | 低 |

**注意 P2 与 P3 的共同上限**：即便把渲染内容全砍掉，帧时间也不会低于底座 1.56 ms；因此**渲染内容优化的边际收益已经很小**，下一步必须动主循环时序。

### 9.34 待办：四条修法**都还没做**（2026-11-11）

§9.33 的归因已完成（T-802），但**据此推出的四条修法一条都没动手**，全部停在建议阶段：

| 编号 | 修法 | 预期收益 | 风险 | 状态 |
| --- | --- | --- | --- | --- |
| **P1** | `updates` 与渲染帧解耦（按 tick / 固定 60 Hz 步进，而非每帧全量） | 0.6–0.8 ms | 中（动主循环时序） | **未开始** |
| **P2** | 查 `present` 为何阻塞 0.7–1.0 ms（present 模式、在飞帧数、`ProcessEvents` 轮询） | 可能 0.3–0.8 ms | 低（纯呈现路径） | **未开始** |
| P3 | 降 `submit` 与逐 draw 录制（186 draw ≈ 3.2 µs/draw） | 0.2–0.4 ms | 中（需批处理/间接绘制） | **未开始** |
| P4 | 天空改低成本几何或并入清屏 | ≤0.17 ms | 低 | **未开始** |

**为什么先查 P2**：`present` 等 0.87–1.02 ms 是剩余最大单项。我们用的是 `immediate`（无垂直同步），理论上不该等这么久；而此前"三种 present 模式 CPU 不变"测的是**总 CPU 占用**，不是 **`present` 段的等待时长**——那次测量不能排除这一段。P2 改动面最小、风险最低。

**共同上限提醒**：空帧 1.56 ms 已与原版整帧 1.6 ms 相当。**四条修法全部做完也才把底座降到接近 0**，而渲染内容（OIT 1.32 + 云 0.65 + 地形 0.45 + UI 0.10）仍在那之上。若最终目标是追平原版，P1 与 P2 是**必须**过的两道门，不是可选优化。
### 9.35 `present` 0.87–1.02 ms 的真相：**计时口径问题，真实交换链等待只有 0.32–0.44 ms**（T-803，2026-11-11）

#### 9.34.1 口径问题（先做了这一条）

原 `present` 阶段的计时跨度为：`surface.Present()` + `acquired.Texture.Dispose()` + **`onFrame?.Invoke(...)`（宿主每帧回调）**。也就是说它把宿主的每帧工作也算了进去。本轮把该段拆成两个独立阶段：

| 新阶段 | 覆盖范围 |
| --- | --- |
| `swap` | `surface.Present()` + 交换链图像释放 —— **真正的交换链等待** |
| `onframe` | 宿主每帧回调（输入 / 界面帧 / 标题等） |

#### 9.34.2 三档 present 模式的实测（`MC_FRAME_CPU=1`，858×527，各 3 个稳定窗口，ms/帧）

| 阶段 | immediate | fifo | mailbox |
| --- | --- | --- | --- |
| **frame 总计** | 2.92–3.59 | 5.40–5.54 | 2.70–3.19 |
| **swap（真实等待）** | **0.369–0.441** | 0.317–0.377 | 0.354–0.392 |
| **onframe（宿主回调）** | **0.375–0.462** | 0.333–0.456 | 0.314–0.410 |
| **acquire（取交换链图像）** | 0.042–0.069 | **1.42–1.71** | 0.038–0.043 |
| submit（结束编码 + 提交） | 0.79–0.87 | 1.10–1.22 | 0.71–0.82 |
| scene（186 draw 录制） | 0.59–0.67 | 0.88–1.06 | 0.57–0.66 |
| oit（8 个 pass 录制） | 0.30–0.33 | 0.51–0.61 | 0.27–0.33 |
| updates | 0.36–0.78 | 0.35–0.44 | 0.29–0.47 |
| **墙钟帧时间（cull 行）** | 3.637 | **5.588** | 2.710 |

#### 9.34.3 结论

1. **0.87–1.02 ms 里只有约一半是交换链等待**：真实 `swap` = **0.32–0.44 ms**，另外 **0.31–0.46 ms 是宿主每帧回调**（`onframe`）。原来的标签把两者混在一起了。
2. **三档模式的 `swap` 几乎相同（0.32–0.44）** ⇒ 这个等待不是"垂直同步没关"，而是 `vkQueuePresentKHR` 本身的固定代价（Windows 合成器 / 在飞帧数）。
3. **fifo 的额外等待出现在 `acquire`（1.42–1.71 ms）而不是 `swap`** ⇒ fifo 下 present 很快返回，但下一帧取图像时阻塞。这反过来验证了拆分是正确的：等待会按其物理位置出现。
4. **`swap` 与 `acquire` 之间没有可调的旋钮**：图像数量 / `desiredMaximumFrameLatency` 由 wgpu-native 默认值决定，`Brew.Wgpu` 的 `Surface.Configure` 未暴露该字段（与 T-800 的实例特性同类缺口），因此**"调大在飞帧数"本轮无法做 A/B**。

#### 9.34.4 由此得到的真正热点（比 swap 更大）

完整帧里 CPU 最大的三项现在是：**`submit` 0.71–0.87 ms**、**`scene` 0.57–0.67 ms**（186 draw ≈ 3.1–3.6 µs/draw）、**`updates` 0.29–0.78 ms**，紧随其后才是 `swap` 0.4 与 `onframe` 0.4。

- `submit` 0.8 ms 异常高：它包含 `encoder.Finish()`（12 个 render pass 的编码校验）+ `queue.Submit`。**减少 pass 数量会直接降这一项**，这也解释了为什么空帧（pass 少但仍多）里 `submit` 也有 0.25 ms。
- 因此 **P2（present）的收益上限只有 ~0.4 ms，且没有可调旋钮**；真正该动的是 **录制与提交路径**（P3 升级为 P1）。

#### 9.34.5 与原版对照（行为，不抄实现）

原版 `blit_screen.fsh` 表明它的最终呈现同样是"一次全屏采样写进交换链图像"，没有额外合成；原版整帧 1.6 ms 说明它的每帧 CPU 提交代价远低于我们（它的 pass 更少、绘制调用更多但状态切换更便宜）。**我们没有原版在飞帧数/呈现旋钮的可观测信息**，因此这一项只能以本机实测为准，不做原版推断。
### 9.36 14 个 render pass 能不能减（T-804，2026-11-11）

#### 9.36.1 pass 清单（以代码为准，实际是 **14 个**，不是 12）

| # | pass | 颜色目标 | 深度目标 | 内容 |
| --- | --- | --- | --- | --- |
| 1 | 主场景 | `Clear(frame)` | `Clear(main)` | 天空 / 不透明 / 切边 / 实体 |
| 2 | 云深度 blit | — | `Keep(cloudDepth)` | 全屏（采样主深度写入云深度） |
| 3 | OIT 深度边界 | `Clear(bounds)` = −65504 | `Keep(main)` | 半透明地形（几何） |
| 4 | OIT 不透明剔除 | `Clear(culled)` = 0 | `Keep(main)` | 全屏（读 bounds） |
| 5 | 云深度边界 | `Keep(culled)` | `Keep(cloudDepth)` | 云几何 |
| 6 | OIT 透射率 | `Clear(T0)`, `Clear(T1)` | `Keep(main)` | 半透明地形 |
| 7 | 云透射率 | `Keep(T0)`, `Keep(T1)` | `Keep(cloudDepth)` | 云几何 |
| 8 | OIT 累积 | `Clear(accum)` | `Keep(main)` | 半透明地形 |
| 9 | 云累积 | `Keep(accum)` | `Keep(cloudDepth)` | 云几何 |
| 10 | OIT 合成 | `Keep(frame)` | `Keep(main)` | 全屏（读 accum / T0 / T1 / culled） |
| 11 | 世界文本 | `Keep(frame)` | `Keep(main)` | 文本几何 |
| 12 | 云 | `Keep(frame)` | `Keep(main)` | 云几何 |
| 13 | 界面 | `Keep(frame)` | `Keep(main)` | UI 几何 |
| 14 | 上屏拷贝 | 交换链图像 | — | 全屏 blit |

#### 9.36.2 逐对可合并性

| 组合 | 可合并？ | 挡住的是什么 |
| --- | --- | --- |
| 3 + 4（边界 + 剔除） | **不行** | 剔除要**采样**边界目标的完整结果：同一 pass 内不能把正在写的纹理当采样源，且需要 barrier |
| 2 + 1（云深度 blit + 主场景） | **不行** | blit 采样主深度，而主场景正把主深度当深度附件写 —— aliasing |
| 3/6/8 与 5/7/9（地形链 vs 云链） | **不行** | **深度附件不同**（`main` vs `cloudDepth`），一个 pass 只有一个深度附件 |
| 10 + 11/12/13（合成 + 文本/云/UI） | 可行，但收益极小 | 无依赖阻挡；合成写深度、后三者读同一深度，同 pass 内按提交顺序生效 |
| 11 + 12 + 13（文本 + 云 + UI） | **可以，且已实现** | 三者颜色/深度附件完全相同、顺序不变 |
| 14（上屏拷贝） | 可去掉但会破坏截图 | 交换链图像不能作为截图源，且原版同样有 `blit_screen` |

#### 9.36.3 已实施：文本 + 云 + UI 合并为 1 个 pass（14 → 12）

- **逐像素等价证据**：同机位、钉死白天 6000 / partial 0.5、静置 100 s 的截图与合并前对比 —— **地形带（y≥350，含底部 UI/物品栏区域）0 像素差异**；差异仅在 y<350 的云带（云在动）。
- **实测收益**：`submit` 段 3 次窗口均值 **0.786 ms**（合并后）vs **0.829 ms**（合并前，同条件），**≈ −0.043 ms**；`frame` 与墙钟帧时间的差落在噪声内（±0.5 ms），无法单独测出。
- **由此得到的关键数字：每个 pass 对象的成本约 0.02 ms**，与"8 个 OIT pass ≈ 0.5 ms"的旧推断不同 —— 那 0.5 ms 主要是 **pass 内容**（半透明 630 列 × 3 阶段 ≈ 1890 次 draw，按 3 µs/draw ≈ 0.6 ms），不是 pass 对象。

#### 9.36.4 与原版对照（只描述资产行为）

原版的半透明同样分三个阶段（`terrain.fsh` 的 `OIT_DEPTH_BOUNDS` / `OIT_TRANSMITTANCE` / `OIT_ACCUMULATE` 三个变体），另有 `oit_depth_bounds_cull.fsh`、`oit_composite.fsh`、`blit_screen.fsh`、`sky.vsh/fsh`、`clouds.*`。即**阶段划分与本实现一致**；差异在于原版用 OpenGL 直接画进主帧缓冲，切换目标时没有"render pass 对象"这一层（也就没有我们的 `Finish()` 校验成本）。因此**原版的"pass 数"并不比我们少**，我们多付的是每 pass 的固定 CPU 成本（0.02 ms 量级）。

#### 9.36.5 结论：**不值得继续砍**

- 14 → 12 只省 **0.043 ms**（约 1.3 % 的帧时间）；剩余可合并的组合要么被 barrier/采样阻挡，要么要改渲染结果。
- 真正的 CPU 大头是**逐 draw 录制**（`scene` 0.59–0.67 ms / 186 draw ≈ 3 µs/draw；OIT 三阶段另算）与 `submit` 里与 draw/pass 数成正比的部分。**下一步应减少 draw 数量或降低每次 draw 的录制成本，而不是减少 pass。**