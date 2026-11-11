<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 原生适配器选择垫片
summary: 绑定缺少带 powerPreference 的适配器请求重载，故在 Framework 内直连 C 接口；记录范围、跨平台依据与删除条件
status: 已采纳
-->

# ADR-0028：用原生垫片选择 GPU 适配器

- 状态：已采纳
- 日期：2026-11-11
- 决策者：项目所有者（用户明确要求"自动选择高性能 GPU 并预留设置"）
- 关联：`docs/traceability.md` T-797；`src/Sharpcraft.Framework/Rendering/NativeAdapterSelection.cs`

## 背景

用户要求客户端**默认自动选择性能最高的 GPU**，并预留一个可指定具体 GPU 的设置。

现状：`WindowedRenderer` 与 `OffscreenRenderer` 调的是绑定的无参重载
`Instance.RequestAdapterBlocking(Surface?)`，它请求的是驱动默认适配器。在双显卡笔记本上 wgpu 自选了核显
（日志 `window: surface ready on 32.0.101.7088`，即 Intel 驱动版本 = Iris Xe），而机器上另有 RTX 3060 未被使用；
且启动日志从不打印选中的是哪块卡，这正是上一轮排查花掉大量时间的直接原因。

硬约束：

- 原则 6：不得把平台专有 API（DXGI / Metal 专用代码）写进业务代码，方案必须跨平台。
- 原则 13：诊断/选择开关由宿主经 `FrameDiagnostics` 传入，游戏不读环境变量。
- 原则 12：绕开托管绑定自写原生调用属跨模块决定，需 ADR 并写明删除条件。

## 决策

在 `Sharpcraft.Framework` 内加一个**原生垫片** `NativeAdapterSelection`（`Rendering/` 下的单文件、约 300 行、单一职责——放这里是因为架构测试要求 wgpu 类型只出现在渲染抽象层内）：

1. 用绑定已生成的 C 原型（`Native.WGPU`）调用
   `wgpuInstanceRequestAdapter`（带 `WGPURequestAdapterOptions.powerPreference`）与
   `wgpuInstanceEnumerateAdapters`。
2. 用 `[UnsafeAccessor]` 取得绑定内部的适配器构造器与 `Instance`/`Surface` 句柄，把原生适配器句柄包装回托管
   `Adapter`；`Adapter` 的归属权随之转交（枚举结果逐个 `wgpuAdapterRelease`，选中的那个交给包装对象）。
3. 适配器请求用 `WGPUCallbackMode.AllowProcessEvents` + `Instance.ProcessEvents()` 轮询（**不用**
   `wgpuInstanceWaitAny`：本机 wgpu-native 在该处 `unimplemented`，实测直接 Rust panic），5 秒超时。
4. 选择策略：`high`（默认）= `HighPerformance`；`low` = `LowPower`；`list` = 枚举并打印；其他文本 = 适配器名子串
   匹配（大小写不敏感）。任何一步失败（偏好不可用、返回空、名字匹配不到、枚举为空）都回退到绑定的无参请求，
   并打印原因与可用列表。
5. 每次启动打印一行结论：
   `gpu: selected NVIDIA GeForce RTX 3060 Laptop GPU (DiscreteGPU, Vulkan) preference=high`。
   名称优先用驱动 description；Vulkan 驱动只报版本号（如 `616.92`）时改用设备名。

宿主开关：`MC_GPU`（`high` / `low` / `list` / 名称子串），经 `GameOptions` → `FrameDiagnostics.Gpu` 传入。

## 影响面

- 代码：`src/Sharpcraft.Framework/Rendering/NativeAdapterSelection.cs`（新增）、`WindowedRenderer.cs`、`OffscreenRenderer.cs`
  （各一处改为一律走垫片）、`FrameDiagnostics.cs`（`Gpu` 选项）、`src/Sharpcraft.Client/Game/GameOptions.cs`（`MC_GPU`）。
- 测试：无新增单测（GPU 需真实设备，按既有做法用实机验收：启动日志 + 截图）；既有测试不受影响。
- 文档：本 ADR、`docs/traceability.md` T-797、`docs/STATE.md`。
- 兼容性：不影响存档、协议、模组 API；单 GPU 机器或偏好不可用时行为与改动前一致（回退到绑定默认请求）。

## 替代方案与被否原因

| 方案 | 否决原因 |
| --- | --- |
| 等绑定升级出带 `WGPURequestAdapterOptions` 的重载 | 绑定 29.0.0.5 无此重载，上游时间表未知；用户当前就要能选卡 |
| setenv / 驱动面板强制（如 `Dxvk`/`PRIME` 环境变量、NVIDIA 控制面板指定程序） | 平台专有且不跨平台；违反原则 6，且游戏内不可见、不可诊断 |
| 只用 `wgpuInstanceEnumerateAdapters` 按类型挑，不调用带偏好的请求 | 可行且更简单，但把"偏好"语义换成"按类型猜"；保留两者可让驱动偏好与确定性选择互为回退。**保留为回退路径** |
| 自己写完整 P/Invoke 声明（`DllImport("wgpu_native")`） | 绑定已生成同一套 C 原型，重复声明只增加版本漂移风险；垫片直接复用其声明 |
| 用 `wgpuInstanceWaitAny` 同步等待 | 本机 wgpu-native 该函数 `unimplemented`，实测 panic；改用 `ProcessEvents` 轮询 |

## 复查条件

- **删除条件（必须删）**：`Brew.Wgpu` 升级后若 `Instance` 提供带 `WGPURequestAdapterOptions` 或适配器列表的重载，
  立即删除 `NativeAdapterSelection.cs` 与两处调用，改回绑定 API。升级绑定任务 MUST 复查本 ADR。
- 若 `[UnsafeAccessor]` 的私有签名（适配器构造器、`get_Handle`）随绑定变化而失效，启动会立刻报
  `BadImageFormatException`（本次已实测过该形态）；届时应优先改用绑定的公开 API，而不是继续加深反射依赖。
- 若出现 wgpu-native 支持 futures 等待（`wgpuInstanceWaitAny` 可用）的版本，可换回同步等待，去掉轮询。
